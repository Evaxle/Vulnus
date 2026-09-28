using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using IOPath = System.IO.Path;
using File = System.IO.File;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Content.Beatmaps;

public sealed class RhythiansDeviceLogin
{
	public string DeviceCode;
	public string UserCode;
	public string VerificationUrl;
	public int ExpiresIn;
}

public sealed class RhythiansMapInfo
{
	public string Id;
	public string Title;
	public string Artist;
	public string Mapper;
	public double? Rating;
	public bool IsRanked;
	public bool IsLegacy;
	public string SourceStatus;
	public bool HasScore;
	public bool Passed;
	public int Rpl;
	public int Rps;
	public int Rpvr;

	public string StatusLabel => IsLegacy ? "LEGACY" : IsRanked ? "RANKED" : "UNRANKED";
	public bool Completed => HasScore || Passed;
}

public static class RhythiansApi
{
	private const string BaseUrl = "https://www.rhythians.com/api/rhythkit/";
	private static readonly string AuthPath = IOPath.Combine(OS.GetUserDataDir(), "rhythians-auth.json");
	private static readonly HttpClient Client = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(60) };
	private static readonly object CatalogLock = new object();
	private static readonly Dictionary<string, RhythiansMapInfo> Catalog = new Dictionary<string, RhythiansMapInfo>(StringComparer.OrdinalIgnoreCase);

	public static string Token { get; private set; }
	public static string Username { get; private set; }
	public static string InstallationId { get; private set; }
	public static string LastError { get; private set; }
	public static bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token);

	public static void Initialize()
	{
		if (!File.Exists(AuthPath))
			return;
		try
		{
			var json = JObject.Parse(File.ReadAllText(AuthPath));
			Token = (string)json["token"];
			Username = (string)json["username"];
			InstallationId = (string)json["installationId"];
		}
		catch
		{
			Logout();
		}
	}

	public static bool ValidateSession()
	{
		if (!IsAuthenticated)
			return false;
		try
		{
			var result = GetJson("status");
			if (result == null || result.Value<bool?>("ok") != true)
			{
				Logout();
				return false;
			}
			Username = result.Value<string>("username") ?? Username;
			InstallationId = result.Value<string>("installationId") ?? InstallationId;
			SaveAuth();
			return true;
		}
		catch (Exception e)
		{
			LastError = e.Message;
			return false;
		}
	}

	public static RhythiansDeviceLogin StartDeviceLogin()
	{
		try
		{
			var result = PostJson("device/start", new JObject());
			if (result == null || result.Value<bool?>("ok") != true)
			{
				LastError = result?.Value<string>("error") ?? "Unable to start Rhythians login.";
				return null;
			}
			return new RhythiansDeviceLogin
			{
				DeviceCode = result.Value<string>("deviceCode"),
				UserCode = result.Value<string>("userCode"),
				VerificationUrl = result.Value<string>("verificationUrl"),
				ExpiresIn = result.Value<int?>("expiresIn") ?? 300
			};
		}
		catch (Exception e)
		{
			LastError = e.Message;
			return null;
		}
	}

	public static bool PollDeviceLogin(string deviceCode, out bool pending)
	{
		pending = false;
		try
		{
			var result = PostJson("device/poll", new JObject { ["deviceCode"] = deviceCode }, false);
			if (result == null)
				return false;
			if (result.Value<bool?>("pending") == true)
			{
				pending = true;
				return false;
			}
			if (result.Value<bool?>("authorized") != true)
			{
				LastError = result.Value<string>("error") ?? "Rhythians login was not authorized.";
				return false;
			}
			Token = result.Value<string>("token");
			Username = result.Value<string>("username");
			InstallationId = result.Value<string>("installationId");
			if (string.IsNullOrWhiteSpace(Token))
			{
				LastError = "Rhythians did not return an access token.";
				return false;
			}
			SaveAuth();
			return true;
		}
		catch (Exception e)
		{
			LastError = e.Message;
			return false;
		}
	}

	public static void Logout()
	{
		Token = null;
		Username = null;
		InstallationId = null;
		lock (CatalogLock) Catalog.Clear();
		try
		{
			if (File.Exists(AuthPath))
				File.Delete(AuthPath);
		}
		catch
		{
		}
	}

	public static bool SyncMaps(bool downloadMissing)
	{
		if (!IsAuthenticated)
			return false;
		try
		{
			var next = 0;
			var downloaded = false;
			var fresh = new Dictionary<string, RhythiansMapInfo>(StringComparer.OrdinalIgnoreCase);
			while (next >= 0)
			{
				var result = GetJson("maps?limit=100&offset=" + next);
				if (result == null || result.Value<bool?>("ok") != true)
				throw new Exception(result?.Value<string>("error") ?? "Rhythians map sync failed.");
				var maps = result["maps"] as JArray;
				if (maps == null)
					break;
				foreach (var token in maps)
				{
					var map = ParseMap(token as JObject);
					if (map == null)
						continue;
					fresh[map.Id] = map;
					if (downloadMissing)
					{
						try
						{
							if (EnsureMapDownloaded(map.Id))
								downloaded = true;
						}
						catch (Exception e)
						{
							GD.PrintErr("Rhythians map download skipped for " + map.Id + ": " + e.Message);
						}
					}
				}
				var nextToken = result["nextOffset"];
				next = nextToken == null || nextToken.Type == JTokenType.Null ? -1 : nextToken.Value<int>();
			}
			lock (CatalogLock)
			{
				Catalog.Clear();
				foreach (var pair in fresh)
					Catalog[pair.Key] = pair.Value;
			}
			if (downloadMissing || downloaded)
				BeatmapLoader.LoadMapsFromDirectory(Global.MapPath, true);
			return true;
		}
		catch (Exception e)
		{
			LastError = e.Message;
			GD.PrintErr("Rhythians map sync error: " + e.Message);
			return false;
		}
	}

	public static RhythiansMapInfo GetMap(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
			return null;
		lock (CatalogLock)
		{
			RhythiansMapInfo map;
			return Catalog.TryGetValue(id, out map) ? map : null;
		}
	}

	public static bool SubmitScore(string mapId, string clientScoreId, double accuracy, int misses, double speed, string cameraMode)
	{
		if (!IsAuthenticated || string.IsNullOrWhiteSpace(mapId))
			return false;
		try
		{
			var payload = new JObject
			{
				["challengeMapId"] = mapId,
				["clientScoreId"] = string.IsNullOrWhiteSpace(clientScoreId) ? Guid.NewGuid().ToString() : clientScoreId,
				["accuracy"] = Math.Max(0, Math.Min(100, accuracy)),
				["misses"] = Math.Max(0, misses),
				["speed"] = speed > 0 ? speed : 1,
				["resultQualified"] = true,
				["completedAt"] = DateTimeOffset.UtcNow.ToString("o"),
				["gameVersion"] = "Vulnus",
				["integrationVersion"] = "rhythians-v1",
				["cameraMode"] = cameraMode == "spin" ? "spin" : "lock",
				["modifiers"] = "Vulnus"
			};
			var result = PostJson("scores", payload);
			if (result == null || result.Value<bool?>("ok") != true)
			{
				LastError = result?.Value<string>("error") ?? "Score submission failed.";
				return false;
			}
			lock (CatalogLock)
			{
				RhythiansMapInfo map;
				if (Catalog.TryGetValue(mapId, out map))
				{
					map.HasScore = true;
					map.Passed = true;
				}
			}
			return true;
		}
		catch (Exception e)
		{
			LastError = e.Message;
			GD.PrintErr("Rhythians score submission error: " + e.Message);
			return false;
		}
	}

	private static bool EnsureMapDownloaded(string id)
	{
		var safe = Sanitize(id);
		var sspmPath = Global.MapIOPath.PlusFile("rhythians_" + safe + ".sspm");
		var vulPath = Global.MapIOPath.PlusFile("rhythians_" + safe + ".vul");
		if (File.Exists(sspmPath) || File.Exists(vulPath))
			return false;
		using (var request = CreateRequest(HttpMethod.Get, "maps/" + Uri.EscapeDataString(id) + "/download"))
		using (var response = Client.SendAsync(request).GetAwaiter().GetResult())
		{
			if (!response.IsSuccessStatusCode)
				throw new Exception("Map download failed for " + id + ": HTTP " + (int)response.StatusCode);
			var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
			if (bytes == null || bytes.Length == 0)
				throw new Exception("Map download returned an empty file for " + id + ".");
			File.WriteAllBytes(sspmPath, bytes);
			return true;
		}
	}

	private static RhythiansMapInfo ParseMap(JObject json)
	{
		var id = json?.Value<string>("id");
		if (string.IsNullOrWhiteSpace(id))
			return null;
		var completion = json["completion"] as JObject;
		var rewards = json["maxRewards"] as JObject;
		return new RhythiansMapInfo
		{
			Id = id,
			Title = json.Value<string>("title") ?? "Unknown",
			Artist = json.Value<string>("artist") ?? "Unknown Artist",
			Mapper = json.Value<string>("mapper") ?? "Unknown",
			Rating = json["rating"] == null || json["rating"].Type == JTokenType.Null ? (double?)null : json.Value<double?>("rating"),
			IsRanked = json.Value<bool?>("isRanked") == true,
			IsLegacy = json.Value<bool?>("isLegacy") == true,
			SourceStatus = json.Value<string>("sourceStatus") ?? "unranked",
			HasScore = json.Value<bool?>("hasScore") == true,
			Passed = completion?.Value<bool?>("passed") == true,
			Rpl = rewards?.Value<int?>("lock") ?? 0,
			Rps = rewards?.Value<int?>("spin") ?? 0,
			Rpvr = rewards?.Value<int?>("vr") ?? 0
		};
	}

	private static JObject GetJson(string path)
	{
		using (var request = CreateRequest(HttpMethod.Get, path))
		using (var response = Client.SendAsync(request).GetAwaiter().GetResult())
			return ReadJson(response);
	}

	private static JObject PostJson(string path, JObject body, bool authenticated = true)
	{
		using (var request = CreateRequest(HttpMethod.Post, path, authenticated))
		{
			request.Content = new StringContent(body.ToString(Formatting.None), System.Text.Encoding.UTF8, "application/json");
			using (var response = Client.SendAsync(request).GetAwaiter().GetResult())
				return ReadJson(response);
		}
	}

	private static HttpRequestMessage CreateRequest(HttpMethod method, string path, bool authenticated = true)
	{
		var request = new HttpRequestMessage(method, path);
		request.Headers.UserAgent.ParseAdd("Vulnus/1.0 Rhythians/1.0");
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		if (authenticated && IsAuthenticated)
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
		return request;
	}

	private static JObject ReadJson(HttpResponseMessage response)
	{
		var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		JObject json = null;
		try
		{
			if (!string.IsNullOrWhiteSpace(text))
				json = JObject.Parse(text);
		}
		catch
		{
		}
		if (!response.IsSuccessStatusCode)
		{
			var error = json?.Value<string>("error");
			throw new Exception(string.IsNullOrWhiteSpace(error) ? "Rhythians API returned HTTP " + (int)response.StatusCode + "." : error);
		}
		return json;
	}

	private static void SaveAuth()
	{
		try
		{
			File.WriteAllText(AuthPath, new JObject
			{
				["token"] = Token,
				["username"] = Username,
				["installationId"] = InstallationId
			}.ToString(Formatting.None));
		}
		catch (Exception e)
		{
			GD.PrintErr("Unable to save Rhythians login: " + e.Message);
		}
	}

	private static string Sanitize(string value)
	{
		foreach (var invalid in IOPath.GetInvalidFileNameChars())
			value = value.Replace(invalid, '_');
		return value;
	}
}
