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
using Compatibility.SSP;

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
	public string ImageUrl;
	public double? Rating;
	public int LengthSeconds;
	public int NoteCount;
	public bool IsRanked;
	public bool IsLegacy;
	public bool ScoreEligible;
	public string SourceStatus;
	public bool HasScore;
	public bool Passed;
	public int Rpl;
	public int Rps;
	public int Rpvr;

	public string StatusLabel => IsLegacy ? "LEGACY" : IsRanked ? "RANKED" : "UNRANKED";
	public bool Completed => HasScore || Passed;
	public bool Installed => RhythiansApi.IsMapInstalled(Id);
}

public sealed class RhythiansCatalogPage
{
	public List<RhythiansMapInfo> Maps = new List<RhythiansMapInfo>();
	public int Offset;
	public int Limit;
	public int Total;
	public bool HasMore;
	public int NextOffset;
}

public sealed class RhythiansScoreResult
{
	public bool Success;
	public string Error;
	public int Points;
	public int Gained;
	public int Rhp;
	public int Rpl;
	public int Rps;
	public int Rpvr;
	public bool Ranked;
	public string CameraMode;
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
	public static RhythiansScoreResult LastScoreResult { get; private set; }
	public static bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token);
	public static Action AccountChanged = () => { };
	public static Action CatalogChanged = () => { };

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
			ClearLocalAuth();
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
				ClearLocalAuth();
				return false;
			}
			Username = result.Value<string>("username") ?? Username;
			InstallationId = result.Value<string>("installationId") ?? InstallationId;
			SaveAuth();
			AccountChanged();
			return true;
		}
		catch (Exception e)
		{
			LastError = e.Message;
			if (e.Message.IndexOf("Unauthorized", StringComparison.OrdinalIgnoreCase) >= 0)
				ClearLocalAuth();
			return false;
		}
	}

	public static RhythiansDeviceLogin StartDeviceLogin()
	{
		try
		{
			LastError = null;
			var result = PostJson("device/start", new JObject { ["client"] = "vulnus" }, false);
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
			LastError = null;
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
			AccountChanged();
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
		if (IsAuthenticated)
		{
			try
			{
				PostJson("logout", new JObject());
			}
			catch
			{
			}
		}
		ClearLocalAuth();
	}

	private static void ClearLocalAuth()
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
		AccountChanged();
		CatalogChanged();
	}

	public static RhythiansCatalogPage FetchCatalogPage(string query, int offset = 0, int limit = 40)
	{
		if (!IsAuthenticated)
		{
			LastError = "Log in to Rhythians to browse the map catalog.";
			return null;
		}
		try
		{
			LastError = null;
			offset = Math.Max(0, offset);
			limit = Math.Max(1, Math.Min(100, limit));
			var path = "maps?limit=" + limit + "&offset=" + offset;
			if (!string.IsNullOrWhiteSpace(query))
				path += "&q=" + Uri.EscapeDataString(query.Trim());
			var result = GetJson(path);
			if (result == null || result.Value<bool?>("ok") != true)
				throw new Exception(result?.Value<string>("error") ?? "Rhythians catalog request failed.");
			var page = new RhythiansCatalogPage
			{
				Offset = result.Value<int?>("offset") ?? offset,
				Limit = result.Value<int?>("limit") ?? limit,
				Total = result.Value<int?>("total") ?? 0,
				HasMore = result.Value<bool?>("hasMore") == true,
				NextOffset = result["nextOffset"] == null || result["nextOffset"].Type == JTokenType.Null
					? -1
					: result.Value<int>("nextOffset")
			};
			var maps = result["maps"] as JArray;
			if (maps != null)
			{
				foreach (var token in maps)
				{
					var map = ParseMap(token as JObject);
					if (map == null)
						continue;
					page.Maps.Add(map);
					lock (CatalogLock)
						Catalog[map.Id] = map;
				}
			}
			CatalogChanged();
			return page;
		}
		catch (Exception e)
		{
			LastError = e.Message;
			GD.PrintErr("Rhythians catalog error: " + e.Message);
			return null;
		}
	}

	public static bool SyncMaps(bool downloadMissing)
	{
		if (!IsAuthenticated)
			return false;
		try
		{
			var offset = 0;
			var fresh = new Dictionary<string, RhythiansMapInfo>(StringComparer.OrdinalIgnoreCase);
			while (offset >= 0)
			{
				var page = FetchCatalogPage("", offset, 100);
				if (page == null)
					return false;
				foreach (var map in page.Maps)
				{
					fresh[map.Id] = map;
					if (downloadMissing && !map.Installed)
						DownloadMap(map.Id);
				}
				offset = page.HasMore ? page.NextOffset : -1;
			}
			lock (CatalogLock)
			{
				Catalog.Clear();
				foreach (var pair in fresh)
					Catalog[pair.Key] = pair.Value;
			}
			CatalogChanged();
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

	public static bool IsMapInstalled(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
			return false;
		var target = Global.MapPath.PlusFile("rhythians_" + Sanitize(id) + ".vul");
		if (File.Exists(target))
			return true;
		try
		{
			foreach (var map in BeatmapLoader.LoadedMaps)
				if (string.Equals(map.RhythiansMapId, id, StringComparison.OrdinalIgnoreCase))
					return true;
		}
		catch
		{
		}
		return false;
	}

	public static bool DownloadMap(string id)
	{
		if (!IsAuthenticated || string.IsNullOrWhiteSpace(id))
			return false;
		if (IsMapInstalled(id))
			return true;
		var tempPath = Global.MapPath.PlusFile(".rhythians_" + Sanitize(id) + ".download.sspm");
		try
		{
			LastError = null;
			using (var request = CreateRequest(HttpMethod.Get, "maps/" + Uri.EscapeDataString(id) + "/download"))
			using (var response = Client.SendAsync(request).GetAwaiter().GetResult())
			{
				if (!response.IsSuccessStatusCode)
				throw new Exception("Map download failed: HTTP " + (int)response.StatusCode + ".");
				var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
				if (bytes == null || bytes.Length < 6)
					throw new Exception("Rhythians returned an empty map file.");
				var magic = BitConverter.ToUInt32(bytes, 0);
				if (magic != 0x6D2B5353)
					throw new Exception("Rhythians returned an invalid SSPM file.");
				File.WriteAllBytes(tempPath, bytes);
			}
			var converted = SspmImporter.Import(tempPath, id);
			if (string.IsNullOrWhiteSpace(converted) || !File.Exists(converted))
				throw new Exception("Vulnus could not convert the downloaded SSPM map.");
			CatalogChanged();
			return true;
		}
		catch (Exception e)
		{
			LastError = e.Message;
			GD.PrintErr("Rhythians map download error for " + id + ": " + e.Message);
			return false;
		}
		finally
		{
			try
			{
				if (File.Exists(tempPath))
					File.Delete(tempPath);
			}
			catch
			{
			}
		}
	}

	public static RhythiansScoreResult SubmitScore(string mapId, string clientScoreId, double accuracy, int misses, double speed, string cameraMode, IList<double> missTimes = null)
	{
		var scoreResult = new RhythiansScoreResult { Success = false, CameraMode = cameraMode == "spin" ? "spin" : "lock" };
		if (!IsAuthenticated || string.IsNullOrWhiteSpace(mapId))
		{
			scoreResult.Error = "Log in to Rhythians before submitting scores.";
			LastScoreResult = scoreResult;
			return scoreResult;
		}
		try
		{
			LastError = null;
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
				["integrationVersion"] = "rhythians-v2",
				["cameraMode"] = scoreResult.CameraMode,
				["modifiers"] = "Vulnus"
			};
			if (missTimes != null && missTimes.Count == misses)
				payload["missTimes"] = new JArray(missTimes);
			var result = PostJson("scores", payload);
			if (result == null || result.Value<bool?>("ok") != true)
				throw new Exception(result?.Value<string>("error") ?? "Score submission failed.");
			scoreResult.Success = true;
			scoreResult.Points = result.Value<int?>("points") ?? 0;
			scoreResult.Gained = result.Value<int?>("gained") ?? 0;
			scoreResult.Rhp = result.Value<int?>("rhp") ?? 0;
			scoreResult.Rpl = result.Value<int?>("rpl") ?? 0;
			scoreResult.Rps = result.Value<int?>("rps") ?? 0;
			scoreResult.Rpvr = result.Value<int?>("rpvr") ?? 0;
			scoreResult.Ranked = result.Value<bool?>("ranked") == true;
			lock (CatalogLock)
			{
				RhythiansMapInfo map;
				if (Catalog.TryGetValue(mapId, out map))
				{
					map.HasScore = true;
					map.Passed = true;
				}
			}
			LastScoreResult = scoreResult;
			CatalogChanged();
			return scoreResult;
		}
		catch (Exception e)
		{
			LastError = e.Message;
			scoreResult.Error = e.Message;
			LastScoreResult = scoreResult;
			GD.PrintErr("Rhythians score submission error: " + e.Message);
			return scoreResult;
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
			ImageUrl = json.Value<string>("imageUrl"),
			Rating = json["rating"] == null || json["rating"].Type == JTokenType.Null ? (double?)null : json.Value<double?>("rating"),
			LengthSeconds = json.Value<int?>("length") ?? 0,
			NoteCount = json.Value<int?>("noteCount") ?? json.Value<int?>("notes") ?? 0,
			IsRanked = json.Value<bool?>("isRanked") == true,
			IsLegacy = json.Value<bool?>("isLegacy") == true,
			ScoreEligible = json.Value<bool?>("scoreEligible") == true,
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
		request.Headers.UserAgent.ParseAdd("Vulnus/1.1 Rhythians/2.0");
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
