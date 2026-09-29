using Godot;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class Settings
{
	public static bool AnyPause = false;
	public static int CameraMode = 0;
	public static float MouseSensitivity = 1f;
	public static float ParallaxAmount = 2f;
	public static float FieldOfView = 70f;
	public static int ApproachMode = 0;
	public static float ApproachDistance = 50f;
	public static float ApproachTime = 1f;
	public static float ApproachRate = 50f;
	public static float FadeLength = 25f;
	public static bool HalfGhost = false;
	public static float NoteScale = 1f;
	public static float NoteOpacity = 1f;
	public static string NoteColorA = "#ff0000";
	public static string NoteColorB = "#00ffff";
	public static float CursorScale = 1f;
	public static string CursorColor = "#ffffff";
	public static string CursorPath = "";
	public static int[] Volume = new int[3];
	public static float RenderScale = 1f;
	public static float UIScale = 1f;
	public static bool CursorDrift = false;
	public static int Bloom = 0;
	public static int FPSLimit = 0;
	public static bool VSync = false;
	public static bool Debanding = true;

	public static float UniversalWindowSeconds => ApproachRate <= 0 ? 0 : ApproachDistance / ApproachRate;
	public static float FadeSeconds => UniversalWindowSeconds * FadeLength / 100f;

	private static string PresetDirectory => OS.GetUserDataDir().PlusFile("presets");
	private static string CursorDirectory => OS.GetUserDataDir().PlusFile("cursors");
	private static string ColorDirectory => OS.GetUserDataDir().PlusFile("colors");

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
		ParallaxAmount = Mathf.Clamp(ParallaxAmount, 0f, 20f);
		FieldOfView = Mathf.Clamp(FieldOfView, 30f, 120f);
		ApproachMode = Math.Max(0, Math.Min(2, ApproachMode));
		ApproachDistance = Mathf.Clamp(ApproachDistance, 0.1f, 200f);
		ApproachTime = Mathf.Clamp(ApproachTime, 0.01f, 20f);
		ApproachRate = Mathf.Clamp(ApproachRate, 0.01f, 200f);
		FadeLength = Mathf.Clamp(FadeLength, 0f, 100f);
		NoteScale = Mathf.Clamp(NoteScale, 0.25f, 3f);
		NoteOpacity = Mathf.Clamp(NoteOpacity, 0f, 1f);
		CursorScale = Mathf.Clamp(CursorScale, 0.1f, 5f);
		if (!IsHexColor(NoteColorA)) NoteColorA = "#ff0000";
		if (!IsHexColor(NoteColorB)) NoteColorB = "#00ffff";
		if (!IsHexColor(CursorColor)) CursorColor = "#ffffff";
		if (Volume == null || Volume.Length != 3)
			Volume = new int[3] { 25, 25, 25 };
		for (var i = 0; i < Volume.Length; i++)
			Volume[i] = Math.Max(0, Math.Min(100, Volume[i]));
		RenderScale = Mathf.Clamp(RenderScale <= 0f ? 1f : RenderScale, 0.05f, 2f);
		UIScale = Mathf.Clamp(UIScale <= 0f ? 1f : UIScale, 0.5f, 2f);
		Bloom = Math.Max(0, Math.Min(2, Bloom));
		FPSLimit = Math.Max(0, Math.Min(1000, FPSLimit));
	}

	private static bool IsHexColor(string value)
	{
		if (string.IsNullOrWhiteSpace(value)) return false;
		var text = value.Trim().TrimStart('#');
		return (text.Length == 6 || text.Length == 8) && text.All(c => Uri.IsHexDigit(c));
	}

	public static Color ParseColor(string value, Color fallback)
	{
		try { return new Color(value); }
		catch { return fallback; }
	}

	public static void LoadSettings()
	{
		var path = OS.GetUserDataDir().PlusFile("settings.bin");
		var file = new Godot.File();
		var settings = new SerializedSettings();
		settings.SetDefaults();
		if (file.FileExists(path))
		{
			try
			{
				file.Open(path, Godot.File.ModeFlags.Read);
				var deserializer = new BinaryFormatter();
				var buffer = file.GetBuffer((long)file.GetLen());
				var stream = new MemoryStream(buffer);
				settings = (SerializedSettings)deserializer.Deserialize(stream);
				file.Close();
			}
			catch (Exception e)
			{
				GD.PrintErr("Settings load failed, using defaults: " + e.Message);
				settings.SetDefaults();
			}
		}
		foreach (FieldInfo field in typeof(SerializedSettings).GetFields())
		{
			try
			{
				var target = typeof(Settings).GetField(field.Name);
				if (target != null) target.SetValue(null, field.GetValue(settings));
			}
			catch (Exception e)
			{
				GD.Print("Failed loading " + field.Name + ": " + e.Message);
			}
		}
		EnsurePresetDirectories();
	}

	public static void SaveSettings()
	{
		var writer = new FileStream(OS.GetUserDataDir().PlusFile("settings.bin"), FileMode.Create);
		var serializer = new BinaryFormatter();
		var settings = new SerializedSettings();
		foreach (FieldInfo field in typeof(SerializedSettings).GetFields())
		{
			var source = typeof(Settings).GetField(field.Name);
			if (source != null) field.SetValue(settings, source.GetValue(null));
		}
		serializer.Serialize(writer, settings);
		writer.Flush();
		writer.Dispose();
	}

	public static string[] GetPresetNames()
	{
		EnsurePresetDirectories();
		return Directory.GetFiles(PresetDirectory, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x).ToArray();
	}

	public static string[] GetCursorFiles()
	{
		EnsurePresetDirectories();
		return Directory.GetFiles(CursorDirectory).Where(path =>
		{
			var ext = Path.GetExtension(path).ToLowerInvariant();
			return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp";
		}).OrderBy(path => Path.GetFileName(path)).ToArray();
	}

	public static string[] GetColorPresetFiles()
	{
		EnsurePresetDirectories();
		return Directory.GetFiles(ColorDirectory).Where(path =>
		{
			var ext = Path.GetExtension(path).ToLowerInvariant();
			return ext == ".txt" || ext == ".json";
		}).OrderBy(path => Path.GetFileName(path)).ToArray();
	}

	public static bool SavePreset(string name)
	{
		EnsurePresetDirectories();
		name = SafeName(name);
		if (string.IsNullOrWhiteSpace(name)) return false;
		try
		{
			File.WriteAllText(Path.Combine(PresetDirectory, name + ".json"), BuildPresetJson().ToString(Formatting.Indented));
			return true;
		}
		catch (Exception e)
		{
			GD.PrintErr("Could not save settings preset: " + e.Message);
			return false;
		}
	}

	public static bool LoadPreset(string name)
	{
		EnsurePresetDirectories();
		var path = Path.Combine(PresetDirectory, SafeName(name) + ".json");
		if (!File.Exists(path)) return false;
		try
		{
			ApplyPresetJson(JObject.Parse(File.ReadAllText(path)));
			UpdateSettings();
			return true;
		}
		catch (Exception e)
		{
			GD.PrintErr("Could not load settings preset: " + e.Message);
			return false;
		}
	}

	public static bool ApplyColorPreset(string path)
	{
		try
		{
			var text = File.ReadAllText(path);
			var matches = System.Text.RegularExpressions.Regex.Matches(text, "#?[0-9a-fA-F]{6,8}");
			if (matches.Count < 2) return false;
			NoteColorA = "#" + matches[0].Value.TrimStart('#').Substring(0, 6);
			NoteColorB = "#" + matches[1].Value.TrimStart('#').Substring(0, 6);
			UpdateSettings();
			return true;
		}
		catch { return false; }
	}

	private static JObject BuildPresetJson()
	{
		return new JObject
		{
			["cameraMode"] = CameraMode,
			["sensitivity"] = MouseSensitivity,
			["parallax"] = ParallaxAmount,
			["fov"] = FieldOfView,
			["approachDistance"] = ApproachDistance,
			["approachRate"] = ApproachRate,
			["fadeLength"] = FadeLength,
			["halfGhost"] = HalfGhost,
			["noteScale"] = NoteScale,
			["noteOpacity"] = NoteOpacity,
			["noteColorA"] = NoteColorA,
			["noteColorB"] = NoteColorB,
			["cursorScale"] = CursorScale,
			["cursorColor"] = CursorColor,
			["cursorPath"] = CursorPath
		};
	}

	private static void ApplyPresetJson(JObject json)
	{
		CameraMode = json.Value<int?>("cameraMode") ?? CameraMode;
		MouseSensitivity = json.Value<float?>("sensitivity") ?? MouseSensitivity;
		ParallaxAmount = json.Value<float?>("parallax") ?? ParallaxAmount;
		FieldOfView = json.Value<float?>("fov") ?? FieldOfView;
		ApproachDistance = json.Value<float?>("approachDistance") ?? ApproachDistance;
		ApproachRate = json.Value<float?>("approachRate") ?? ApproachRate;
		ApproachMode = 1;
		FadeLength = json.Value<float?>("fadeLength") ?? FadeLength;
		HalfGhost = json.Value<bool?>("halfGhost") ?? HalfGhost;
		NoteScale = json.Value<float?>("noteScale") ?? NoteScale;
		NoteOpacity = json.Value<float?>("noteOpacity") ?? NoteOpacity;
		NoteColorA = json.Value<string>("noteColorA") ?? NoteColorA;
		NoteColorB = json.Value<string>("noteColorB") ?? NoteColorB;
		CursorScale = json.Value<float?>("cursorScale") ?? CursorScale;
		CursorColor = json.Value<string>("cursorColor") ?? CursorColor;
		CursorPath = json.Value<string>("cursorPath") ?? CursorPath;
	}

	private static void EnsurePresetDirectories()
	{
		Directory.CreateDirectory(PresetDirectory);
		Directory.CreateDirectory(CursorDirectory);
		Directory.CreateDirectory(ColorDirectory);
	}

	private static string SafeName(string value)
	{
		if (string.IsNullOrWhiteSpace(value)) return "";
		foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
		return value.Trim();
	}

	[Serializable]
	private class SerializedSettings
	{
		public bool AnyPause;
		public int CameraMode;
		public float MouseSensitivity;
		[OptionalField(VersionAdded = 3)] public float ParallaxAmount;
		[OptionalField(VersionAdded = 3)] public float FieldOfView;
		public int ApproachMode;
		public float ApproachDistance;
		public float ApproachTime;
		public float ApproachRate;
		[OptionalField(VersionAdded = 3)] public float FadeLength;
		[OptionalField(VersionAdded = 3)] public bool HalfGhost;
		[OptionalField(VersionAdded = 3)] public float NoteScale;
		[OptionalField(VersionAdded = 3)] public float NoteOpacity;
		[OptionalField(VersionAdded = 3)] public string NoteColorA;
		[OptionalField(VersionAdded = 3)] public string NoteColorB;
		[OptionalField(VersionAdded = 3)] public float CursorScale;
		[OptionalField(VersionAdded = 3)] public string CursorColor;
		[OptionalField(VersionAdded = 3)] public string CursorPath;
		public int[] Volume;
		[OptionalField(VersionAdded = 2)] public float RenderScale;
		[OptionalField(VersionAdded = 2)] public float UIScale;
		[OptionalField(VersionAdded = 2)] public bool CursorDrift;
		[OptionalField(VersionAdded = 2)] public int Bloom;
		[OptionalField(VersionAdded = 2)] public int FPSLimit;
		[OptionalField(VersionAdded = 2)] public bool VSync;
		[OptionalField(VersionAdded = 2)] public bool Debanding;

		[OnDeserializing()]
		internal void OnDeserializing(StreamingContext ctx) { SetDefaults(); }

		public void SetDefaults()
		{
			AnyPause = false;
			CameraMode = 0;
			MouseSensitivity = 1f;
			ParallaxAmount = 2f;
			FieldOfView = 70f;
			ApproachMode = 0;
			ApproachDistance = 50f;
			ApproachTime = 1f;
			ApproachRate = 50f;
			FadeLength = 25f;
			HalfGhost = false;
			NoteScale = 1f;
			NoteOpacity = 1f;
			NoteColorA = "#ff0000";
			NoteColorB = "#00ffff";
			CursorScale = 1f;
			CursorColor = "#ffffff";
			CursorPath = "";
			Volume = new int[3] { 25, 25, 25 };
			RenderScale = 1f;
			UIScale = 1f;
			CursorDrift = false;
			Bloom = 0;
			FPSLimit = 0;
			VSync = false;
			Debanding = true;
		}
	}
}
