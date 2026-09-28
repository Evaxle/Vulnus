using Godot;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Content.Beatmaps
{
	[Serializable, JsonObject(MemberSerialization.OptIn)]
	public partial class BeatmapInfo
	{
		public static int LatestFormat = 1;
		[JsonProperty("_version")]
		public int FormatVersion;
		public string Path;
		[NonSerialized]
		public bool Playable;
	}
	[Serializable, JsonObject(MemberSerialization.OptIn)]
	public class Beatmap : BeatmapInfo
	{
		[NonSerialized]
		public BeatmapSet Mapset;
		[JsonProperty("_name")]
		public string Name;
		[NonSerialized]
		public BeatmapData Data;
        public Error Load()
        {
            if (Data != null && Playable) return Error.Ok;
            Playable = false;
            try
            {
                var json = System.IO.File.ReadAllText(Mapset.ResolveFile(Path));
                var version = JsonConvert.DeserializeObject<BeatmapInfo>(json);
                if (version == null || version.FormatVersion > LatestFormat) return Error.FileUnrecognized;
                var data = JsonConvert.DeserializeObject<BeatmapData>(json);
                if (data?.Notes == null || data.Notes.Count == 0) return Error.FileCorrupt;
                foreach (var note in data.Notes)
                    if (note == null || !Finite(note.X) || !Finite(note.Y) || !Finite(note.T) || note.T < 0)
                        return Error.FileCorrupt;
                FormatVersion = version.FormatVersion;
                Data = data;
                Playable = true;
                return Error.Ok;
            }
            catch (Exception e)
            {
                GD.PrintErr($"Could not load difficulty {Path}: {e.Message}");
                return Error.FileCorrupt;
            }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

	}
	public class BeatmapData
	{
		[JsonProperty("_notes")]
		public List<NoteData> Notes;
	}
	public class NoteData
	{
		[JsonProperty("_x")]
		public float X;
		[JsonProperty("_y")]
		public float Y;
		[JsonProperty("_time")]
		public float T;
	}
}