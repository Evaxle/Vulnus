using Godot;
using System;
using System.IO;
using Path = System.IO.Path;
using System.IO.Compression;
using System.Collections.Generic;
using System.Security.Cryptography;
using File = System.IO.File;
using Directory = System.IO.Directory;
using Compatibility.SSP;

namespace Content.Beatmaps
{
    public static class BeatmapLoader
    {
        public static List<BeatmapSet> LoadedMaps = new List<BeatmapSet>();
        public static bool LoadMapsFromDirectory(string directory, bool reset = false)
        {
            if (reset) LoadedMaps.Clear();
            if (!Directory.Exists(directory)) return false;
            try
            {
                SspmImporter.ImportDirectory(directory);
                foreach (var folder in Directory.GetDirectories(directory))
                    if (File.Exists(Path.Combine(folder, "meta.json"))) TryLoad(folder, Path.GetFullPath(folder));
                foreach (var file in Directory.GetFiles(directory))
                {
                    if (!string.Equals(Path.GetExtension(file), ".vul", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        string hash;
                        using (var md5 = MD5.Create())
                        using (var input = File.OpenRead(file))
                            hash = BitConverter.ToString(md5.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                        if (LoadedMaps.Exists(map => map.Hash == hash)) continue;
                        var cache = Path.Combine(Global.MapPath, ".cache", hash);
                        // A completion marker prevents a failed extraction being mistaken for a valid cache.
                        if (!File.Exists(Path.Combine(cache, ".complete")))
                        {
                            if (Directory.Exists(cache)) Directory.Delete(cache, true);
                            ZipFile.ExtractToDirectory(file, cache);
                            File.WriteAllText(Path.Combine(cache, ".complete"), "");
                        }
                        TryLoad(cache, hash);
                    }
                    catch (Exception e) { GD.PrintErr($"Skipping map {file}: {e.Message}"); }
                }
                return true;
            }
            catch (Exception e)
            {
                GD.PrintErr($"Could not scan maps in {directory}: {e.Message}");
                return false;
            }
        }
        private static void TryLoad(string path, string hash)
        {
            if (LoadedMaps.Exists(map => map.Hash == hash)) return;
            try { LoadedMaps.Add(BeatmapSet.LoadFromPath(path, hash)); }
            catch (Exception e) { GD.PrintErr($"Skipping map {path}: {e.Message}"); }
        }
    }
}
