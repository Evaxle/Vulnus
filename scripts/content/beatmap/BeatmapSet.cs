using Godot;
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;
using System.Linq;
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
		[JsonProperty("_length")]
        public double Length;
        [JsonProperty("_cover")]
        public string CoverFile;
		[JsonProperty("_music")]
		public string Music;
		public static BeatmapSet Load(string json)
		{
			var version = JsonConvert.DeserializeObject<BeatmapSetInfo>(json);
			return JsonConvert.DeserializeObject<BeatmapSet>(json);
		}
		public static BeatmapSet LoadFromPath(string path, string hash)
		{
			path = path.Replace("user://", OS.GetUserDataDir());
            string SafeFile(string name)
            {
                if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Missing map filename.");
                var target = System.IO.Path.GetFullPath(System.IO.Path.Combine(path, name));
                if (!target.StartsWith(System.IO.Path.GetFullPath(path) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe metadata path.");
                return target;
            }
            var map = Load(System.IO.File.ReadAllText(SafeFile("meta.json")));
            if (map.FormatVersion != 1 || string.IsNullOrWhiteSpace(map.Title) || map._difficulties == null || map._difficulties.Count == 0 || map._difficulties.Count > 128) throw new InvalidDataException("Invalid map metadata.");
            map._mappers = map._mappers ?? new List<string>(); map.Artist = map.Artist ?? "";
            if (!System.IO.File.Exists(SafeFile(map.Music))) throw new InvalidDataException("Map audio is missing.");
            if (!string.IsNullOrEmpty(map.CoverFile)) SafeFile(map.CoverFile);
            map.Path = path; map.Hash = hash; map.Difficulties = new List<Beatmap>();
            double lastNote = 0;
            foreach (string difficulty in map._difficulties)
            {
                string json = System.IO.File.ReadAllText(SafeFile(difficulty));
                var diff = JsonConvert.DeserializeObject<Beatmap>(json);
                var data = JsonConvert.DeserializeObject<BeatmapData>(json);
                if (diff.FormatVersion != 1 || data?.Notes == null || data.Notes.Count == 0 || data.Notes.Count > 2000000 || data.Notes.Any(n => n == null || float.IsNaN(n.T) || float.IsInfinity(n.T) || n.T < 0 || float.IsNaN(n.X) || float.IsNaN(n.Y) || float.IsInfinity(n.X) || float.IsInfinity(n.Y))) throw new InvalidDataException("Invalid difficulty notes.");
                lastNote = Math.Max(lastNote, data.Notes.Max(n => n.T));
                diff.Path = difficulty; diff.Mapset = map; diff.Data = data; diff.Playable = true; map.Difficulties.Add(diff);
            }
            if (double.IsNaN(map.Length) || double.IsInfinity(map.Length) || map.Length < 0) map.Length = 0;
            map.Length = Math.Max(map.Length, lastNote);
            return map;
        }
		[NonSerialized]
		public Texture Cover;
		public Texture LoadCover()
		{
			if (Cover != null) return Cover;
			ImageTexture texture;
			var cover = new Image();
			var coverPath = !string.IsNullOrEmpty(CoverFile) && System.IO.File.Exists(Path.PlusFile(CoverFile)) ? Path.PlusFile(CoverFile) : "none";
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
			if (cover.Load(coverPath) != Error.Ok) { Cover = Global.Matt; return Cover; }
			texture.CreateFromImage(cover);
			Cover = texture;
			return texture;
		}
		public AudioStream LoadAudio() => AudioHandler.LoadAudio(Path.PlusFile(Music));
	}
}
