using Godot;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using File = System.IO.File;
using Directory = System.IO.Directory;
using Path = System.IO.Path;

namespace Content.Beatmaps
{
    public static class BeatmapLoader
    {
        public static List<BeatmapSet> LoadedMaps = new List<BeatmapSet>();
        public static bool LoadMapsFromDirectory(string directory, bool reset = false)
        {
            if (reset) LoadedMaps.Clear();
            Directory.CreateDirectory(directory);
            Compatibility.SSP.SspmImporter.ImportDirectory(directory);
            foreach (string path in Directory.GetFiles(directory).Where(p => p.EndsWith(".vul", StringComparison.OrdinalIgnoreCase)))
                try { ImportFile(path); } catch (Exception e) { GD.PrintErr(Path.GetFileName(path) + ": " + e.Message); }
            return true;
        }
        public static BeatmapSet ImportFile(string path)
        {
            if (new FileInfo(path).Length > 512L * 1024 * 1024) throw new InvalidDataException("Map exceeds 512 MB.");
            string hash;
            using (var sha = SHA256.Create()) using (var file = File.OpenRead(path)) hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
            var existing = LoadedMaps.Find(m => m.Hash == hash); if (existing != null) return existing;
            string cache = Path.GetFullPath(Path.Combine(Global.MapPath, ".cache", hash));
            string staging = cache + "." + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(staging);
            try
            {
                using (var zip = ZipFile.OpenRead(path))
                {
                    long total = 0;
                    if (zip.Entries.Count > 1024) throw new InvalidDataException("Too many archive entries.");
                    foreach (var entry in zip.Entries)
                    {
                        total += entry.Length;
                        if (total > 1024L * 1024 * 1024) throw new InvalidDataException("Unpacked map exceeds 1 GB.");
                        string target = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                        if (!target.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe archive path.");
                        if (Path.GetFileName(target).Equals("cache.bin", StringComparison.OrdinalIgnoreCase)) continue;
                        if (entry.Name.Length == 0) { Directory.CreateDirectory(target); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(target)); entry.ExtractToFile(target);
                    }
                }
                var map = BeatmapSet.LoadFromPath(staging, hash);
                var audio = map.LoadAudio();
                if (audio == null || audio.GetLength() <= 0) throw new InvalidDataException("Unsupported or damaged map audio. Use OGG, MP3 or PCM WAV.");
                if (Directory.Exists(cache)) Directory.Delete(cache, true);
                Directory.Move(staging, cache); map.Path = cache;
                string destination = Path.Combine(Global.MapPath, hash + ".vul");
                if (!Path.GetFullPath(path).StartsWith(Path.GetFullPath(Global.MapPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !File.Exists(destination)) File.Copy(path, destination);
                LoadedMaps.RemoveAll(m => !string.IsNullOrEmpty(map.RhythiansMapId) && m.RhythiansMapId == map.RhythiansMapId);
                LoadedMaps.Add(map); return map;
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }
    }
}
