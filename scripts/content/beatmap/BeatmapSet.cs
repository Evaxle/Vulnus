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
			path = path.Replace("user://", OS.GetUserDataDir());
			var file = new File();
			if (file.Open(path.PlusFile("cache.bin"), File.ModeFlags.Read) == Error.Ok)
			{
				var deserializer = new BinaryFormatter();
				var buffer = file.GetBuffer((long)file.GetLen());
				var stream = new MemoryStream(buffer);
				var cachedMap = (BeatmapSet)deserializer.Deserialize(stream);
				cachedMap.Path = path;
				cachedMap.Hash = hash;
				foreach (Beatmap difficulty in cachedMap.Difficulties) difficulty.Mapset = cachedMap;
				return cachedMap;
			}
			file.Open(path.PlusFile("meta.json"), File.ModeFlags.Read);
			var map = BeatmapSet.Load(file.GetAsText());
			map.Path = path;
			map.Difficulties = new List<Beatmap>();
			foreach (string difficulty in map._difficulties)
			{
				var diffFile = new File();
				diffFile.Open(path.PlusFile(difficulty), File.ModeFlags.Read);
				var diff = JsonConvert.DeserializeObject<Beatmap>(diffFile.GetAsText());
				diff.Path = difficulty;
				diff.Mapset = map;
				map.Difficulties.Add(diff);
				diffFile.Close();
			}
			file.Close();
			var writer = new FileStream(path.PlusFile("cache.bin"), FileMode.Create);
			map.SerializeToFile(ref writer);
			writer.Dispose();
			map.Hash = hash;
			return map;
		}
		public void SerializeToFile(ref FileStream stream)
		{
			var serializer = new BinaryFormatter();
			serializer.Serialize(stream, this);
			stream.Flush();
		}
		[NonSerialized]
		public Texture Cover;
		public Texture LoadCover()
		{
			if (Cover != null) return Cover;
			var coverPath = "";
			foreach (string path in System.IO.Directory.GetFiles(Path))
			{
				if (path.GetFile().BaseName().ToLower() == "cover")
				{
					coverPath = path;
					break;
				}
			}
			if (string.IsNullOrEmpty(coverPath))
				return LoadFallbackCover();
			var cover = new Image();
			var error = cover.Load(coverPath);
			if (error != Error.Ok)
			{
				var file = new File();
				if (file.Open(coverPath, File.ModeFlags.Read) != Error.Ok)
					return LoadFallbackCover();
				var buffer = file.GetBuffer((long)file.GetLen());
				file.Close();
				if (buffer.Length >= 8 &&
					buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
					error = cover.LoadPngFromBuffer(buffer);
				else if (buffer.Length >= 3 && buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
					error = cover.LoadJpgFromBuffer(buffer);
				else if (buffer.Length >= 12 &&
					buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46 &&
					buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50)
					error = cover.LoadWebpFromBuffer(buffer);
			}
			if (error != Error.Ok)
				return LoadFallbackCover();
			var texture = new ImageTexture();
			texture.CreateFromImage(cover);
			Cover = texture;
			return Cover;
		}
		private Texture LoadFallbackCover()
		{
			Cover = (Texture)GD.Load("res://assets/images/vulnus.png");
			return Cover;
		}
		public AudioStream LoadAudio() => AudioHandler.LoadAudio(Path.PlusFile(Music));
	}
}
