using Godot;
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;
using File = Godot.File;

namespace Content.Beatmaps
{
	[Serializable, JsonObject(MemberSerialization.OptIn)]
	public partial class BeatmapSetInfo
	{
		public static int LatestFormat = 1;
		[JsonProperty("_version")]
		public int FormatVersion;
		[NonSerialized]
		public string Hash;
		[NonSerialized]
		public string Path;
		[JsonProperty("rhythiansMapId")]
		public string RhythiansMapId;
	}
	[Serializable, JsonObject(MemberSerialization.OptIn)]
	public class BeatmapSet : BeatmapSetInfo
	{
		[JsonProperty("_artist")]
		public string Artist;
		[JsonProperty("_title")]
		public string Title;
		public string Name => $"{Artist} - {Title}";
		[JsonProperty("_difficulties")]
		public List<string> _difficulties;
		public List<Beatmap> Difficulties;
		[JsonProperty("_mappers")]
		public List<string> _mappers;
		public string Mappers => string.Join(", ", _mappers.ToArray());
		[JsonProperty("_music")]
		public string Music;
		public static BeatmapSet Load(string json)
		{
			var version = JsonConvert.DeserializeObject<BeatmapSetInfo>(json);
			return JsonConvert.DeserializeObject<BeatmapSet>(json);
		}
        public static BeatmapSet LoadFromPath(string path, string hash)
        {
            var map = Load(System.IO.File.ReadAllText(System.IO.Path.Combine(path, "meta.json")));
            if (map == null || map.FormatVersion > LatestFormat || map._difficulties == null || map._difficulties.Count == 0)
                throw new InvalidDataException("Missing difficulties or unsupported map version.");
            map.Path = path;
            map.Hash = hash;
            map.Title = map.Title ?? "Untitled";
            map.Artist = map.Artist ?? "Unknown";
            map._mappers = map._mappers ?? new List<string>();
            map.Difficulties = new List<Beatmap>();
            foreach (var difficulty in map._difficulties)
            {
                try
                {
                    var diff = JsonConvert.DeserializeObject<Beatmap>(System.IO.File.ReadAllText(map.ResolveFile(difficulty)));
                    if (diff == null) throw new InvalidDataException("Empty difficulty.");
                    diff.Path = difficulty;
                    diff.Name = diff.Name ?? System.IO.Path.GetFileNameWithoutExtension(difficulty);
                    diff.Mapset = map;
                    map.Difficulties.Add(diff);
                }
                catch (Exception e) { GD.PrintErr($"Skipping difficulty {difficulty}: {e.Message}"); }
            }
            if (map.Difficulties.Count == 0) throw new InvalidDataException("No readable difficulties.");
            return map;
        }
        public string ResolveFile(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative)) throw new InvalidDataException("Missing map file path.");
            var root = System.IO.Path.GetFullPath(Path).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Map file is outside its folder.");
            return full;
        }
		[NonSerialized]
		public Texture Cover;
		public Texture LoadCover()
		{
			if (Cover != null) return Cover;
			ImageTexture texture;
			var cover = new Image();
			var coverPath = "none";
			foreach (string path in System.IO.Directory.GetFiles(Path))
			{
				if (path.GetFile().BaseName().ToLower() == "cover")
				{
					coverPath = path;
					break;
				}
			}
			if (coverPath == "none")
			{
				Cover = Global.Matt;
				return Global.Matt;
			}
			texture = new ImageTexture();
			var bytes = System.IO.File.ReadAllBytes(coverPath);
			var result = Error.FileUnrecognized;
			if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50)
				result = cover.LoadPngFromBuffer(bytes);
			else if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8)
				result = cover.LoadJpgFromBuffer(bytes);
			else if (bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP")
				result = cover.LoadWebpFromBuffer(bytes);
			if (result != Error.Ok) { Cover = Global.Matt; return Cover; }
			texture.CreateFromImage(cover);
			Cover = texture;
			return texture;
		}
		public AudioStream LoadAudio() => AudioHandler.LoadAudio(ResolveFile(Music));
	}
}
