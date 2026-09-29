using Godot;
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

public static class Settings
{
	public static bool AnyPause = false;
	public static int CameraMode = 0;
	public static float MouseSensitivity = 1f;
	public static int ApproachMode = 0;
	public static float ApproachDistance = 50f;
	public static float ApproachTime = 1f;
	public static float ApproachRate = 50f;
	public static int[] Volume = new int[3];
	public static float RenderScale = 1f;
	public static float UIScale = 1f;
	public static bool CursorDrift = false;
	public static int Bloom = 0;
	public static int FPSLimit = 0;
	public static bool VSync = false;
	public static bool Debanding = true;
	public static float FadeLength = 0.25f;
	public static bool HalfGhost = false;
	public static float Parallax = 0.1f;
	public static float CameraFov = 70f;
	public static float NoteOpacity = 1f;
	public static float NoteSize = 0.875f;
	public static string[] NoteColors = new string[] { "#ff0000", "#00ffff" };
	public static string CursorColor = "#ffffff";
	public static float CursorScale = 1f;
	public static float CursorOpacity = 1f;
	public static string SelectedColorPreset = "Default";
	public static string SelectedCursor = "Default";
	public static void UpdateSettings(bool loading = false)
	{
		if (loading)
			LoadSettings();
		Sanitize();
		if (!loading)
			SaveSettings();
		if (Global.Instance != null)
			Global.Instance.ViewportChanged();
		OS.VsyncEnabled = VSync;
		Engine.TargetFps = FPSLimit;
		switch (ApproachMode)
		{
			case 0:
				ApproachRate = ApproachDistance / ApproachTime;
				break;
			case 2:
				ApproachDistance = ApproachRate * ApproachTime;
				break;
			case 1:
				ApproachTime = ApproachDistance / ApproachRate;
				break;
			default:
				break;
		}
		var masterBus = AudioServer.GetBusIndex("Master");
		AudioServer.SetBusVolumeDb(masterBus, GD.Linear2Db(Volume[0] / 100f));
		var musicBus = AudioServer.GetBusIndex("Music");
		AudioServer.SetBusVolumeDb(musicBus, GD.Linear2Db(Volume[1] / 100f));
		var sfxBus = AudioServer.GetBusIndex("SFX");
		AudioServer.SetBusVolumeDb(sfxBus, GD.Linear2Db(Volume[2] / 100f));
	}
	private static void Sanitize()
	{
		CameraMode = Math.Max(0, Math.Min(2, CameraMode));
		MouseSensitivity = Mathf.Clamp(MouseSensitivity, 0.01f, 10f);
		ApproachMode = Math.Max(0, Math.Min(2, ApproachMode));
		ApproachDistance = Mathf.Max(0.1f, ApproachDistance);
		ApproachTime = Mathf.Max(0.01f, ApproachTime);
		ApproachRate = Mathf.Max(0.01f, ApproachRate);
		if (Volume == null || Volume.Length != 3)
			Volume = new int[3] { 25, 25, 25 };
		for (var i = 0; i < Volume.Length; i++)
			Volume[i] = Math.Max(0, Math.Min(100, Volume[i]));
		RenderScale = Mathf.Clamp(RenderScale <= 0f ? 1f : RenderScale, 0.05f, 2f);
		UIScale = Mathf.Clamp(UIScale <= 0f ? 1f : UIScale, 0.5f, 2f);
		Bloom = Math.Max(0, Math.Min(2, Bloom));
		FPSLimit = Math.Max(0, Math.Min(1000, FPSLimit));
		FadeLength = Mathf.Clamp(FadeLength, 0f, Math.Max(0.01f, ApproachTime));
		Parallax = Mathf.Clamp(Parallax, 0f, 20f);
		CameraFov = Mathf.Clamp(CameraFov, 40f, 120f);
		NoteOpacity = Mathf.Clamp(NoteOpacity, 0f, 1f);
		NoteSize = Mathf.Clamp(NoteSize, 0.1f, 2f);
		CursorScale = Mathf.Clamp(CursorScale, 0.1f, 4f);
		CursorOpacity = Mathf.Clamp(CursorOpacity, 0f, 1f);
		if (NoteColors == null || NoteColors.Length == 0)
			NoteColors = new string[] { "#ff0000", "#00ffff" };
		for (var i = 0; i < NoteColors.Length; i++)
			if (!IsHexColor(NoteColors[i])) NoteColors[i] = i % 2 == 0 ? "#ff0000" : "#00ffff";
		if (!IsHexColor(CursorColor)) CursorColor = "#ffffff";
	}

	public static float UniversalWindowSeconds()
	{
		return ApproachRate <= 0 ? 0 : ApproachDistance / ApproachRate;
	}

	public static float FullyVisibleWindowSeconds()
	{
		return Math.Max(0f, UniversalWindowSeconds() - FadeLength);
	}

	public static string ProfilesPath => OS.GetUserDataDir().PlusFile("profiles");
	public static string ColorSetsPath => OS.GetUserDataDir().PlusFile("colorsets");
	public static string CursorsPath => OS.GetUserDataDir().PlusFile("cursors");

	public static string[] GetProfileNames()
	{
		EnsurePresetDirectories();
		return GetNames(ProfilesPath, "*.json");
	}

	public static string[] GetColorPresetNames()
	{
		EnsurePresetDirectories();
		var names = new List<string> { "Default", "Cotton Candy", "White / Red" };
		names.AddRange(GetNames(ColorSetsPath, "*.txt"));
		return names.ToArray();
	}

	public static string[] GetCursorNames()
	{
		EnsurePresetDirectories();
		var names = new List<string> { "Default" };
		names.AddRange(GetNames(CursorsPath, "*.png"));
		names.AddRange(GetNames(CursorsPath, "*.jpg"));
		return names.ToArray();
	}

	public static bool SaveProfile(string name)
	{
		EnsurePresetDirectories();
		name = SafeName(name);
		if (string.IsNullOrWhiteSpace(name)) return false;
		var json = new JObject
		{
			["cameraMode"] = CameraMode,
			["sensitivity"] = MouseSensitivity,
			["approachDistance"] = ApproachDistance,
			["approachRate"] = ApproachRate,
			["approachTime"] = ApproachTime,
			["fadeLength"] = FadeLength,
			["halfGhost"] = HalfGhost,
			["parallax"] = Parallax,
			["fov"] = CameraFov,
			["noteOpacity"] = NoteOpacity,
			["noteSize"] = NoteSize,
			["noteColors"] = new JArray(NoteColors),
			["cursorColor"] = CursorColor,
			["cursorScale"] = CursorScale,
			["cursorOpacity"] = CursorOpacity,
			["colorPreset"] = SelectedColorPreset,
			["cursor"] = SelectedCursor,
			["cursorDrift"] = CursorDrift
		};
		System.IO.File.WriteAllText(System.IO.Path.Combine(ProfilesPath, name + ".json"), json.ToString());
		return true;
	}

	public static bool LoadProfile(string name)
	{
		EnsurePresetDirectories();
		var path = System.IO.Path.Combine(ProfilesPath, SafeName(name) + ".json");
		if (!System.IO.File.Exists(path)) return false;
		try
		{
			var json = JObject.Parse(System.IO.File.ReadAllText(path));
			CameraMode = json.Value<int?>("cameraMode") ?? CameraMode;
			MouseSensitivity = json.Value<float?>("sensitivity") ?? MouseSensitivity;
			ApproachDistance = json.Value<float?>("approachDistance") ?? ApproachDistance;
			ApproachRate = json.Value<float?>("approachRate") ?? ApproachRate;
			ApproachTime = json.Value<float?>("approachTime") ?? ApproachTime;
			FadeLength = json.Value<float?>("fadeLength") ?? FadeLength;
			HalfGhost = json.Value<bool?>("halfGhost") ?? HalfGhost;
			Parallax = json.Value<float?>("parallax") ?? Parallax;
			CameraFov = json.Value<float?>("fov") ?? CameraFov;
			NoteOpacity = json.Value<float?>("noteOpacity") ?? NoteOpacity;
			NoteSize = json.Value<float?>("noteSize") ?? NoteSize;
			CursorColor = json.Value<string>("cursorColor") ?? CursorColor;
			CursorScale = json.Value<float?>("cursorScale") ?? CursorScale;
			CursorOpacity = json.Value<float?>("cursorOpacity") ?? CursorOpacity;
			SelectedColorPreset = json.Value<string>("colorPreset") ?? SelectedColorPreset;
			SelectedCursor = json.Value<string>("cursor") ?? SelectedCursor;
			CursorDrift = json.Value<bool?>("cursorDrift") ?? CursorDrift;
			var colors = json["noteColors"] as JArray;
			if (colors != null && colors.Count > 0)
				NoteColors = colors.Values<string>().ToArray();
			UpdateSettings();
			return true;
		}
		catch (Exception e)
		{
			GD.PrintErr("Profile load failed: " + e.Message);
			return false;
		}
	}

	public static bool ApplyColorPreset(string name)
	{
		EnsurePresetDirectories();
		string[] colors = null;
		if (name == "Default") colors = new[] { "#ff0000", "#00ffff" };
		else if (name == "Cotton Candy") colors = new[] { "#ff5dc8", "#70d7ff", "#fff1fb" };
		else if (name == "White / Red") colors = new[] { "#ffffff", "#ff174d" };
		else
		{
			var path = System.IO.Path.Combine(ColorSetsPath, name + ".txt");
			if (System.IO.File.Exists(path))
				colors = ParseColors(System.IO.File.ReadAllText(path)).ToArray();
		}
		if (colors == null || colors.Length == 0) return false;
		NoteColors = colors;
		SelectedColorPreset = name;
		UpdateSettings();
		return true;
	}

	public static string ResolveCursorPath()
	{
		if (string.IsNullOrWhiteSpace(SelectedCursor) || SelectedCursor == "Default") return null;
		foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
		{
			var path = System.IO.Path.Combine(CursorsPath, SelectedCursor + ext);
			if (System.IO.File.Exists(path)) return path;
		}
		return null;
	}

	public static void EnsurePresetDirectories()
	{
		System.IO.Directory.CreateDirectory(ProfilesPath);
		System.IO.Directory.CreateDirectory(ColorSetsPath);
		System.IO.Directory.CreateDirectory(CursorsPath);
	}

	private static string[] GetNames(string folder, string pattern)
	{
		var result = new List<string>();
		foreach (var file in System.IO.Directory.GetFiles(folder, pattern))
			result.Add(System.IO.Path.GetFileNameWithoutExtension(file));
		result.Sort(StringComparer.OrdinalIgnoreCase);
		return result.ToArray();
	}

	private static List<string> ParseColors(string text)
	{
		var result = new List<string>();
		foreach (var raw in text.Replace(",", " ").Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
		{
			var value = raw.Trim();
			if (!value.StartsWith("#")) value = "#" + value;
			if (IsHexColor(value) && !result.Contains(value))
				result.Add(value);
		}
		return result;
	}

	private static bool IsHexColor(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#') return false;
		for (var i = 1; i < value.Length; i++)
			if (!Uri.IsHexDigit(value[i])) return false;
		return true;
	}

	private static string SafeName(string value)
	{
		if (string.IsNullOrWhiteSpace(value)) return "";
		foreach (var invalid in System.IO.Path.GetInvalidFileNameChars())
			value = value.Replace(invalid, '_');
		return value.Trim();
	}

	public static void LoadSettings()
	{
		var path = OS.GetUserDataDir().PlusFile("settings.bin");
		var file = new Godot.File();
		var settings = new SerializedSettings();
		settings.SetDefaults();
		if (file.FileExists(path))
		{
			file.Open(path, Godot.File.ModeFlags.Read);
			var deserializer = new BinaryFormatter();
			var buffer = file.GetBuffer((long)file.GetLen());
			var stream = new MemoryStream(buffer);
			settings = (SerializedSettings)deserializer.Deserialize(stream);
			file.Close();
		}
		foreach (FieldInfo field in typeof(SerializedSettings).GetFields())
		{
			try
			{
				var value = field.GetValue(settings);
				typeof(Settings).GetField(field.Name).SetValue(null, value);
			}
			catch (Exception e)
			{
				GD.Print($"Failed loading {field.Name}: {e.Message}");
			}
		}
	}
	public static void SaveSettings()
	{
		var writer = new FileStream(OS.GetUserDataDir().PlusFile("settings.bin"), FileMode.Create);
		var serializer = new BinaryFormatter();
		var settings = new SerializedSettings();
		foreach (FieldInfo field in typeof(SerializedSettings).GetFields())
		{
			field.SetValue(settings, typeof(Settings).GetField(field.Name).GetValue(null));
		}
		serializer.Serialize(writer, settings);
		writer.Flush();
		writer.Dispose();
	}
	[Serializable]
	private class SerializedSettings
	{
		public bool AnyPause;
		public int CameraMode;
		public float MouseSensitivity;
		public int ApproachMode;
		public float ApproachDistance;
		public float ApproachTime;
		public float ApproachRate;
		public int[] Volume;
		[OptionalField(VersionAdded = 2)]
		public float RenderScale;
		[OptionalField(VersionAdded = 2)]
		public float UIScale;
		[OptionalField(VersionAdded = 2)]
		public bool CursorDrift;
		[OptionalField(VersionAdded = 2)]
		public int Bloom;
		[OptionalField(VersionAdded = 2)]
		public int FPSLimit;
		[OptionalField(VersionAdded = 2)]
		public bool VSync;
		[OptionalField(VersionAdded = 2)]
		public bool Debanding;
		[OptionalField(VersionAdded = 3)]
		public float FadeLength;
		[OptionalField(VersionAdded = 3)]
		public bool HalfGhost;
		[OptionalField(VersionAdded = 3)]
		public float Parallax;
		[OptionalField(VersionAdded = 3)]
		public float CameraFov;
		[OptionalField(VersionAdded = 3)]
		public float NoteOpacity;
		[OptionalField(VersionAdded = 3)]
		public float NoteSize;
		[OptionalField(VersionAdded = 3)]
		public string[] NoteColors;
		[OptionalField(VersionAdded = 3)]
		public string CursorColor;
		[OptionalField(VersionAdded = 3)]
		public float CursorScale;
		[OptionalField(VersionAdded = 3)]
		public float CursorOpacity;
		[OptionalField(VersionAdded = 3)]
		public string SelectedColorPreset;
		[OptionalField(VersionAdded = 3)]
		public string SelectedCursor;
		[OnDeserializing()]
		internal void OnDeserializing(StreamingContext ctx)
		{
			SetDefaults();
		}
		public void SetDefaults()
		{
			AnyPause = false;
			CameraMode = 0;
			MouseSensitivity = 1f;
			ApproachMode = 0;
			ApproachDistance = 50f;
			ApproachTime = 1f;
			ApproachRate = 50f;
			Volume = new int[3] { 25, 25, 25 };
			RenderScale = 1f;
			UIScale = 1f;
			CursorDrift = false;
			Bloom = 0;
			FPSLimit = 0;
			VSync = false;
			Debanding = true;
			FadeLength = 0.25f;
			HalfGhost = false;
			Parallax = 0.1f;
			CameraFov = 70f;
			NoteOpacity = 1f;
			NoteSize = 0.875f;
			NoteColors = new string[] { "#ff0000", "#00ffff" };
			CursorColor = "#ffffff";
			CursorScale = 1f;
			CursorOpacity = 1f;
			SelectedColorPreset = "Default";
			SelectedCursor = "Default";
		}
	}
}
