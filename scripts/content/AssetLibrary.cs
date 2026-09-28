using Path = System.IO.Path;
using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using File = System.IO.File;
using Directory = System.IO.Directory;

public static class AssetLibrary
{
    public static readonly string[] Kinds = { "cursors", "colorsets", "borders", "backgrounds", "notes", "hitsounds", "misssounds" };
    public static string Root => Path.Combine(Global.UserPath, "assets");
    public static void Initialize()
    {
        foreach (var kind in Kinds)
        {
            Directory.CreateDirectory(Path.Combine(Root, kind));
            using (var dir = new Godot.Directory())
            {
                string source = "res://assets/library/" + kind;
                if (dir.Open(source) != Error.Ok) continue;
                dir.ListDirBegin(true, true);
                for (string name=dir.GetNext(); name!=""; name=dir.GetNext())
                {
                    string target=Path.Combine(Root,kind,name);
                    if (dir.CurrentIsDir() || name.EndsWith(".import") || File.Exists(target)) continue;
                    using(var file=new Godot.File()) { if(file.Open(source+"/"+name,Godot.File.ModeFlags.Read)!=Error.Ok)continue; File.WriteAllBytes(target,file.GetBuffer((long)file.GetLen())); }
                }
                dir.ListDirEnd();
            }
        }
    }
    public static string[] Names(string kind) => Directory.GetFiles(Path.Combine(Root, kind)).Select(Path.GetFileName).OrderBy(n => n).ToArray();
    public static string Resolve(string kind, string name)
    {
        if (!Kinds.Contains(kind) || string.IsNullOrEmpty(name) || Path.GetFileName(name) != name || name.Contains("\\") || name.Contains("/")) return null;
        var path = Path.Combine(Root, kind, name);
        return File.Exists(path) ? path : null;
    }
    public static string Import(string kind, string path)
    {
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("Assets must be smaller than 32 MB.");
        return ImportBytes(kind, Path.GetFileName(path), File.ReadAllBytes(path));
    }
    public static string ImportBytes(string kind, string name, byte[] bytes)
    {
        if (!Kinds.Contains(kind)) throw new InvalidDataException("Unknown asset type.");
        Initialize();
        string ext = Path.GetExtension(name).ToLowerInvariant();
        if (kind == "colorsets") ParseColors(System.Text.Encoding.UTF8.GetString(bytes));
        else if (kind == "notes")
        {
            if (ext != ".obj") throw new InvalidDataException("Note meshes use OBJ files.");
            ParseObj(System.Text.Encoding.UTF8.GetString(bytes));
        }
        else if (kind.EndsWith("sounds"))
        {
            if (ext != ".ogg" && ext != ".mp3" && ext != ".wav") throw new InvalidDataException("Choose an OGG, MP3 or PCM WAV sound.");
        }
        else
        {
            var img = new Image();
            Error err = ext == ".png" ? img.LoadPngFromBuffer(bytes) : ext == ".jpg" || ext == ".jpeg" ? img.LoadJpgFromBuffer(bytes) : Error.FileUnrecognized;
            if (err != Error.Ok || img.GetWidth() > 4096 || img.GetHeight() > 4096) throw new InvalidDataException("Choose a valid PNG or JPEG up to 4096 × 4096.");
        }
        var basename = ProfileStore.SafeName(Path.GetFileNameWithoutExtension(name));
        string unique = basename + ext;
        for (int i = 2; File.Exists(Path.Combine(Root, kind, unique)); i++) unique = basename + " " + i + ext;
        File.WriteAllBytes(Path.Combine(Root, kind, unique), bytes);
        if (kind.EndsWith("sounds"))
        {
            var sound = AudioHandler.LoadAudio(Path.Combine(Root,kind,unique));
            if (sound == null || sound.GetLength() <= 0) { File.Delete(Path.Combine(Root,kind,unique)); throw new InvalidDataException("Unsupported or damaged audio. WAV sounds must be 8/16-bit PCM."); }
        }
        return unique;
    }
    public static string[] ParseColors(string text)
    {
        var colors = text.Trim('\uFEFF').Split(new[] { ',', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim().TrimStart('#')).ToArray();
        if (colors.Length < 1 || colors.Length > 256 || colors.Any(c => c.Length != 6 || !c.All(Uri.IsHexDigit))) throw new InvalidDataException("Use 1–256 comma-separated six-digit hex colors, such as ff0000,00ffff.");
        return colors;
    }
    public static Color[] Colors()
    {
        try
        {
            var path = Resolve("colorsets", Settings.ColorsetAsset);
            if (path != null) return ParseColors(File.ReadAllText(path)).Select(c => new Color(c)).ToArray();
            if (Settings.ColorsetAsset == "Rainbow Freeze")
            {
                using (var file = new Godot.File()) { file.Open("res://assets/colorsets/Rainbow Freeze.txt", Godot.File.ModeFlags.Read); return ParseColors(file.GetAsText()).Select(c => new Color(c)).ToArray(); }
            }
        }
        catch (Exception e) { GD.PrintErr(e.Message); }
        return new[] { new Color("ff0000"), new Color("00ffff") };
    }
    public static Texture Texture(string kind, string name, string fallback = null)
    {
        var path = Resolve(kind, name);
        if (path != null)
        {
            var image = new Image();
            if (image.Load(path) == Error.Ok) { var texture = new ImageTexture(); texture.CreateFromImage(image); return texture; }
        }
        return fallback == null ? null : GD.Load<Texture>(fallback);
    }
    public static ArrayMesh ParseObj(string text)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<Vector3>();
        foreach (string line in text.Split('\n'))
        {
            var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            if (parts[0] == "v" && parts.Length >= 4)
            {
                var p = parts.Skip(1).Take(3).Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                if (p.Any(x => float.IsNaN(x) || float.IsInfinity(x) || Math.Abs(x) > 10000)) throw new InvalidDataException("Invalid OBJ vertex.");
                vertices.Add(new Vector3(p[0], p[1], p[2]));
            }
            if (parts[0] == "f" && parts.Length >= 4)
            {
                var face = parts.Skip(1).Select(s => int.Parse(s.Split('/')[0], CultureInfo.InvariantCulture)).Select(i => i < 0 ? vertices.Count + i : i - 1).ToArray();
                if (face.Any(i => i < 0 || i >= vertices.Count)) throw new InvalidDataException("Invalid OBJ face.");
                for (int i = 1; i < face.Length - 1; i++) { triangles.Add(vertices[face[0]]); triangles.Add(vertices[face[i]]); triangles.Add(vertices[face[i+1]]); }
            }
            if (vertices.Count > 100000 || triangles.Count > 300000) throw new InvalidDataException("Note mesh is too detailed.");
        }
        if (triangles.Count == 0) throw new InvalidDataException("OBJ has no faces.");
        var arrays = new Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max); arrays[(int)Mesh.ArrayType.Vertex] = triangles.ToArray();
        var mesh = new ArrayMesh(); mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays); return mesh;
    }
}
