using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Godot;
using File = System.IO.File;
using Directory = System.IO.Directory;

// Portable JSON profiles. Foreign settings are data, never executable instructions.
public static class ProfileStore
{
    public static string Root => System.IO.Path.Combine(Global.UserPath, "profiles");
    public static string Active = "Default";
    public static string LastReport = "";
    private static readonly FieldInfo[] Fields = typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Static);
    private static readonly JObject Defaults = Capture();
    public static JObject Capture() => JObject.FromObject(Fields.ToDictionary(f => f.Name, f => f.GetValue(null)));
    public static string[] Names() => Directory.GetFiles(Root, "*.json").Select(System.IO.Path.GetFileNameWithoutExtension).OrderBy(n => n).ToArray();
    public static string SafeName(string name)
    {
        name = System.IO.Path.GetFileName(name.Replace('\\', '/'));
        foreach (char c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim().Trim('.');
        return string.IsNullOrWhiteSpace(name) ? "Imported" : name.Substring(0, Math.Min(80, name.Length));
    }
    public static void Initialize()
    {
        Directory.CreateDirectory(Root);
        // Bundled presets are copied only once and never overwrite user edits.
        var bundled = new Godot.Directory();
        if (bundled.Open("res://assets/profiles") == Error.Ok)
        {
            bundled.ListDirBegin(true, true);
            for (string n = bundled.GetNext(); n != ""; n = bundled.GetNext())
            {
                if (!n.EndsWith(".json") || File.Exists(System.IO.Path.Combine(Root, n))) continue;
                using (var f = new Godot.File()) { f.Open("res://assets/profiles/" + n, Godot.File.ModeFlags.Read); File.WriteAllText(System.IO.Path.Combine(Root, n), f.GetAsText()); }
            }
            bundled.ListDirEnd();
        }
        string marker = System.IO.Path.Combine(Root, "active.txt");
        if (File.Exists(marker)) Active = SafeName(File.ReadAllText(marker));
        if (!File.Exists(System.IO.Path.Combine(Root, Active + ".json"))) Save();
        try { Apply(Read(Active)); }
        catch (Exception e)
        {
            GD.PrintErr("Could not load profile " + Active + ": " + e.Message);
            Active = "Recovered " + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Apply(new JObject()); Save();
            LastReport = "A damaged profile was left untouched. A recovery profile was created.";
        }
    }
    private static JObject Read(string name) => JObject.Parse(File.ReadAllText(System.IO.Path.Combine(Root, SafeName(name) + ".json")));
    public static void Apply(JObject document)
    {
        JObject values = document["settings"] as JObject ?? document;
        foreach (var field in Fields)
        {
            var token = values[field.Name] ?? Defaults[field.Name];
            try { field.SetValue(null, token.ToObject(field.FieldType)); }
            catch { field.SetValue(null, Defaults[field.Name].ToObject(field.FieldType)); }
        }
        Settings.Validate();
    }
    public static void Save()
    {
        Directory.CreateDirectory(Root);
        var path = System.IO.Path.Combine(Root, SafeName(Active) + ".json");
        JObject document;
        try { document = File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : new JObject(); }
        catch { document = new JObject(); }
        document["format"] = "vulnus-settings";
        document["version"] = 1;
        document["settings"] = Capture();
        Write(path, document);
        File.WriteAllText(System.IO.Path.Combine(Root, "active.txt"), Active);
    }
    public static void Write(string path, JObject document)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, document.ToString(Formatting.Indented));
        if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
    }
    public static void Switch(string name)
    {
        var next = Read(name); // Validate before touching the current profile.
        Save();
        Apply(next);
        Active = SafeName(name);
        Settings.UpdateSettings();
    }
    public static void Duplicate(string name)
    {
        name = SafeName(name);
        if (File.Exists(System.IO.Path.Combine(Root, name + ".json"))) throw new IOException("That profile name already exists.");
        Save(); Active = name; Save();
    }
    public static string Import(string path)
    {
        Directory.CreateDirectory(Root);
        JObject source;
        using (var stream = File.OpenRead(path))
        {
            if (path.EndsWith(".rhs", StringComparison.OrdinalIgnoreCase))
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    var config = zip.GetEntry("config") ?? throw new InvalidDataException("The RHS archive has no config.");
                    if (config.Length > 1024 * 1024) throw new InvalidDataException("Settings are too large.");
                    using (var reader = new StreamReader(config.Open())) source = JObject.Parse(reader.ReadToEnd());
                    foreach (var entry in zip.Entries)
                    {
                        string kind = entry.FullName.StartsWith("borderSkin/") ? "borders" : entry.FullName.StartsWith("backgrounds/") ? "backgrounds" : entry.FullName.StartsWith("noteSkin/") ? "notes" : null;
                        if (kind == null || entry.Length > 16 * 1024 * 1024) continue;
                        string ext = System.IO.Path.GetExtension(entry.FullName).ToLowerInvariant();
                        if (ext != ".png" && ext != ".jpg" && ext != ".obj") continue;
                        string asset = AssetLibrary.ImportBytes(kind, System.IO.Path.GetFileName(entry.FullName), ReadEntry(entry));
                        if (kind == "borders") source["ImportedBorder"] = asset;
                        if (kind == "notes") source["ImportedNote"] = asset;
                    }
                }
            }
            else
            {
                if (stream.Length > 1024 * 1024) throw new InvalidDataException("Settings are too large.");
                using (var reader = new StreamReader(stream)) source = JObject.Parse(reader.ReadToEnd());
            }
        }
        var converted = Convert(source);
        string name = SafeName(System.IO.Path.GetFileNameWithoutExtension(path).Replace(".settings", "").Replace(".vulnus", ""));
        string unique = name;
        for (int i = 2; File.Exists(System.IO.Path.Combine(Root, unique + ".json")); i++) unique = name + " " + i;
        Write(System.IO.Path.Combine(Root, unique + ".json"), converted);
        LastReport = "Imported " + unique + ". " + ((converted["unmapped"] as JObject)?.Count ?? 0) + " source options kept in the profile as unmapped. Existing profiles are unchanged.";
        return unique;
    }
    private static byte[] ReadEntry(ZipArchiveEntry entry) { using (var s = entry.Open()) using (var m = new MemoryStream()) { s.CopyTo(m); return m.ToArray(); } }
    public static JObject Convert(JObject source)
    {
        if ((string)source["format"] == "vulnus-settings")
        {
            if ((int?)source["version"] != 1 || !(source["settings"] is JObject)) throw new InvalidDataException("Unsupported Vulnus settings version.");
            return (JObject)source.DeepClone();
        }
        var values = (JObject)Defaults.DeepClone();
        var unmapped = (JObject)source.DeepClone();
        Action<string, string, Func<JToken, JToken>> map = (key, target, transform) =>
        {
            JToken token = source[key];
            if (token == null) return;
            if (token is JObject && token["Value"] != null) token = token["Value"];
            values[target] = transform == null ? token.DeepClone() : transform(token);
            unmapped.Remove(key);
        };
        string[,] mappings = {
            {"sensitivity","MouseSensitivity"}, {"approach_rate","ApproachRate"}, {"spawn_distance","ApproachDistance"},
            {"half_ghost","HalfGhost"}, {"note_size","NoteScale"}, {"note_opacity","NoteOpacity"}, {"fade_length","FadeLength"},
            {"cursor_scale","CursorScale"}, {"cursor_spin","CursorSpin"}, {"fov","CameraFov"}, {"parallax","Parallax"},
            {"enable_drift_cursor","CursorDrift"}, {"vsync_enabled","VSync"}, {"target_fps","FPSLimit"}, {"render_scale","RenderScale"},
            {"window_fullscreen","Fullscreen"}, {"show_hp_bar","ShowHealth"}, {"show_left_panel","ShowLeftPanel"}, {"show_right_panel","ShowRightPanel"},
            {"auto_preview_song","AutoPreview"}, {"play_hit_snd","PlayHitSound"}, {"play_miss_snd","PlayMissSound"}, {"music_offset","MusicOffset"},
            {"cursor_color","CursorColor"}, {"enable_border","ShowGrid"},
            {"ApproachRate","ApproachRate"}, {"SpawnDistance","ApproachDistance"}, {"HalfGhost","HalfGhost"}, {"FadeLength","FadeLength"},
            {"NoteScale","NoteScale"}, {"NoteOpacity","NoteOpacity"}, {"CameraFov","CameraFov"}, {"Parallax","Parallax"},
            {"CursorScale","CursorScale"}, {"CursorOpacity","CursorOpacity"}, {"CursorRotation","CursorRotation"},
            {"HealthBarEnabled","ShowHealth"}, {"ImportedBorder","BorderAsset"}, {"ImportedNote","NoteAsset"}
        };
        foreach (var i in Enumerable.Range(0, mappings.GetLength(0))) map(mappings[i,0], mappings[i,1], null);
        if (unmapped.Count == source.Count && source["cam_unlock"] == null) throw new InvalidDataException("No supported Rhythia settings were found in this file.");
        map("cam_unlock", "CameraMode", t => (bool)t ? 0 : 1);
        map("SpinCamera", "CameraMode", t => (bool)t ? 0 : 1);
        var volumes = (JArray)values["Volume"];
        string[] volumeKeys = { "master_volume", "music_volume", "hit_volume" };
        for (int i = 0; i < volumeKeys.Length; i++)
        {
            if (source[volumeKeys[i]] == null) continue;
            volumes[i] = Math.Round(100 * Math.Pow(10, (double)source[volumeKeys[i]] / 20));
            unmapped.Remove(volumeKeys[i]);
        }
        if (source["CursorColorRed"] != null)
        {
            string color = "";
            foreach (string component in new[] { "Red", "Green", "Blue" })
            {
                string key = "CursorColor" + component;
                color += Math.Max(0, Math.Min(255, (int)(source[key]?["Value"] ?? 255))).ToString("x2");
                unmapped.Remove(key);
            }
            values["CursorColor"] = color;
        }
        // Both formats express approach as distance / seconds. Preserve their timing.
        values["ApproachMode"] = 1;
        values["ApproachTime"] = Math.Max(0.01, (double)values["ApproachDistance"] / Math.Max(0.01, (double)values["ApproachRate"]));
        return new JObject { ["format"] = "vulnus-settings", ["version"] = 1, ["settings"] = values, ["unmapped"] = unmapped,
            ["conversionNotes"] = "Approach timing and supported visuals are mapped. Sensitivity/parallax are engine-specific and may need adjustment. Unmapped settings are preserved, not applied." };
    }
}
