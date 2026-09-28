using Godot;
using System;
using System.IO;
using System.Reflection;
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
	public static int[] Volume = new int[] { 25, 25, 25 };
	public static float RenderScale = 1f;
	public static float UIScale = 1f;
	public static bool CursorDrift = false;
	public static int Bloom = 0;
	public static int FPSLimit = 0;
	public static bool VSync = false;
	public static bool Debanding = true;
	public static bool HalfGhost = false;
	public static float FadeLength = 0.25f;
	public static float NoteScale = 1f;
	public static float NoteOpacity = 1f;
	public static float CursorScale = 1f;
	public static float CursorOpacity = 1f;
	public static float CursorRotation = 0f;
	public static float CursorSpin = 0f;
	public static string CursorColor = "ffffff";
	public static float CameraFov = 70f;
	public static float Parallax = 1f;
	public static bool Fullscreen = false;
	public static bool ShowGrid = true;
	public static bool ShowHealth = true;
	public static bool ShowLeftPanel = true;
	public static bool ShowRightPanel = true;
	public static bool AutoPreview = true;
	public static bool PlayHitSound = true;
	public static bool PlayMissSound = true;
	public static float MusicOffset = 0f;
	public static string CursorAsset = "";
	public static string ColorsetAsset = "";
	public static string BorderAsset = "";
	public static string BackgroundAsset = "";
	public static string NoteAsset = "";
	public static string HitSoundAsset = "";
	public static string MissSoundAsset = "";
	public static void Validate()
	{
		foreach (var field in typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Static))
		{
			if (field.FieldType == typeof(float)) { float v = (float)field.GetValue(null); if (float.IsNaN(v) || float.IsInfinity(v)) field.SetValue(null, 1f); }
			if (field.FieldType == typeof(string) && field.GetValue(null) == null) field.SetValue(null, "");
		}
		ApproachMode = Mathf.Clamp(ApproachMode, 0, 2);
		CameraMode = Mathf.Clamp(CameraMode, 0, 1);
		ApproachDistance = Mathf.Clamp(ApproachDistance, 1, 200);
		ApproachRate = Mathf.Clamp(ApproachRate, 1, 200);
		ApproachTime = Mathf.Clamp(ApproachTime, 0.01f, 10);
		MouseSensitivity = Mathf.Clamp(MouseSensitivity, 0.01f, 10);
		RenderScale = Mathf.Clamp(RenderScale, 0.25f, 2);
		UIScale = Mathf.Clamp(UIScale, 0.5f, 2);
		FadeLength = Mathf.Clamp(FadeLength, 0, 1);
		NoteOpacity = Mathf.Clamp(NoteOpacity, 0.05f, 1);
		NoteScale = Mathf.Clamp(NoteScale, 0.1f, 3);
		CursorScale = Mathf.Clamp(CursorScale, 0.1f, 4);
		CursorOpacity = Mathf.Clamp(CursorOpacity, 0.05f, 1);
		CursorRotation = Mathf.Clamp(CursorRotation, -360, 360);
		CursorSpin = Mathf.Clamp(CursorSpin, -1080, 1080);
		CameraFov = Mathf.Clamp(CameraFov, 30, 120);
		Parallax = Mathf.Clamp(Parallax, 0, 10);
		MusicOffset = Mathf.Clamp(MusicOffset, -1000, 1000);
		FPSLimit = Mathf.Clamp(FPSLimit, 0, 1000);
		Bloom = Mathf.Clamp(Bloom, 0, 2);
		if (Volume == null || Volume.Length != 3) Volume = new[] { 25, 25, 25 };
		for (int i = 0; i < 3; i++) Volume[i] = Mathf.Clamp(Volume[i], 0, 100);
		if (!System.Text.RegularExpressions.Regex.IsMatch(CursorColor, "^#?[0-9a-fA-F]{6}$")) CursorColor = "ffffff";
	}
	public static void UpdateSettings(bool loading = false)
	{
		if (loading)
			LoadSettings();
		Validate();
		Global.Instance.ViewportChanged();
		OS.VsyncEnabled = VSync;
		Engine.TargetFps = FPSLimit;
		OS.WindowFullscreen = Fullscreen;
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
		SaveSettings();
		AudioServer.SetBusVolumeDb(masterBus, GD.Linear2Db(Volume[0] / 100f));
		var musicBus = AudioServer.GetBusIndex("Music");
		AudioServer.SetBusVolumeDb(musicBus, GD.Linear2Db(Volume[1] / 100f));
		var sfxBus = AudioServer.GetBusIndex("SFX");
		AudioServer.SetBusVolumeDb(sfxBus, GD.Linear2Db(Volume[2] / 100f));
	}
	public static void LoadSettings()
	{
		AssetLibrary.Initialize();
		var path = Global.UserPath.PlusFile("settings.bin");
		var file = new Godot.File();
		var settings = new SerializedSettings();
		settings.SetDefaults();
		if (!System.IO.Directory.Exists(ProfileStore.Root) && file.FileExists(path))
		{
			try
			{
				file.Open(path, Godot.File.ModeFlags.Read);
				if (file.GetLen() > 1024 * 1024) throw new InvalidDataException("Legacy settings are too large.");
				var deserializer = new BinaryFormatter { Binder = new LegacySettingsBinder() };
				using (var stream = new MemoryStream(file.GetBuffer((long)file.GetLen()))) settings = (SerializedSettings)deserializer.Deserialize(stream);
			}
			catch (Exception e) { GD.PrintErr("Legacy settings could not be migrated: " + e.Message); }
			finally { file.Close(); }
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
		ProfileStore.Initialize();
	}
	public static void SaveSettings()
	{
		ProfileStore.Save();
	}
	private sealed class LegacySettingsBinder : SerializationBinder
	{
		public override Type BindToType(string assemblyName, string typeName)
		{
			if (typeName == typeof(SerializedSettings).FullName) return typeof(SerializedSettings);
			if (typeName == "System.Int32[]") return typeof(int[]);
			throw new SerializationException("Unexpected legacy settings type.");
		}
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
		}
	}
}
