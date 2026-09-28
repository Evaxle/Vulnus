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
	public static int[] Volume = new int[3];
	public static float RenderScale = 1f;
	public static float UIScale = 1f;
	public static bool CursorDrift = false;
	public static int Bloom = 0;
	public static int FPSLimit = 0;
	public static bool VSync = false;
	public static bool Debanding = true;
	public static void UpdateSettings(bool loading = false)
	{
		if (loading)
			LoadSettings();
		Normalize();
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
		if (!loading) SaveSettings();
		var masterBus = AudioServer.GetBusIndex("Master");
		AudioServer.SetBusVolumeDb(masterBus, GD.Linear2Db(Volume[0] / 100f));
		var musicBus = AudioServer.GetBusIndex("Music");
		AudioServer.SetBusVolumeDb(musicBus, GD.Linear2Db(Volume[1] / 100f));
		var sfxBus = AudioServer.GetBusIndex("SFX");
		AudioServer.SetBusVolumeDb(sfxBus, GD.Linear2Db(Volume[2] / 100f));
	}
	public static void LoadSettings()
	{
		var path = OS.GetUserDataDir().PlusFile("settings.bin");
		var file = new Godot.File();
		var settings = new SerializedSettings();
		settings.SetDefaults();
        try
        {
            if (file.FileExists(path))
            {
                if (file.Open(path, Godot.File.ModeFlags.Read) != Error.Ok) throw new IOException("Cannot open settings.");
                using (var stream = new MemoryStream(file.GetBuffer((long)file.GetLen())))
                    settings = (SerializedSettings)new BinaryFormatter().Deserialize(stream);
                if (settings == null) throw new SerializationException("Empty settings.");
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("Using default settings: " + e.Message);
            settings = new SerializedSettings();
            settings.SetDefaults();
        }
        finally { file.Close(); }
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
    public static void Normalize()
    {
        CameraMode = Math.Max(0, Math.Min(1, CameraMode));
        ApproachMode = Math.Max(0, Math.Min(2, ApproachMode));
        Bloom = Math.Max(0, Math.Min(2, Bloom));
        FPSLimit = Math.Max(0, FPSLimit);
        MouseSensitivity = Positive(MouseSensitivity, 1);
        ApproachDistance = Positive(ApproachDistance, 50);
        ApproachTime = Positive(ApproachTime, 1);
        ApproachRate = Positive(ApproachRate, 50);
        RenderScale = Mathf.Clamp(Positive(RenderScale, 1), 0.25f, 2);
        UIScale = Positive(UIScale, 1);
        if (Volume == null || Volume.Length != 3) Volume = new[] { 25, 25, 25 };
        for (int i = 0; i < Volume.Length; i++) Volume[i] = Math.Max(0, Math.Min(100, Volume[i]));
    }
    private static float Positive(float value, float fallback)
        => float.IsNaN(value) || float.IsInfinity(value) || value <= 0 ? fallback : value;
    public static void SaveSettings()
    {
        var path = OS.GetUserDataDir().PlusFile("settings.bin");
        try
        {
            var settings = new SerializedSettings();
            foreach (FieldInfo field in typeof(SerializedSettings).GetFields())
                field.SetValue(settings, typeof(Settings).GetField(field.Name).GetValue(null));
            using (var writer = new FileStream(path + ".tmp", FileMode.Create))
                new BinaryFormatter().Serialize(writer, settings);
            if (System.IO.File.Exists(path)) System.IO.File.Replace(path + ".tmp", path, null);
            else System.IO.File.Move(path + ".tmp", path);
        }
        catch (Exception e) { GD.PrintErr("Could not save settings: " + e.Message); }
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
