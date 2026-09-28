using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;
using Godot;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Compatibility.SSP
{
    public static class SspmImporter
    {
        public static string LastError { get; private set; }
        public static string Import(string path)
        {
            LastError = "Unsupported or malformed SSPM file.";
            try
            {
                if (new FileInfo(path).Length > 512L * 1024 * 1024) throw new InvalidDataException("Map exceeds 512 MB.");
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
                {
                    if (reader.ReadUInt32() != 0x6D2B5353) return null;
                    var version = reader.ReadUInt16();
                    stream.Position = 0;
                    if (version == 1) return ImportV1(reader);
                    if (version == 2)
                    {
                        return ImportV2(reader);
                    }
                    return null;
                }
            }
            catch (Exception e)
            {
                LastError = e.Message;
                GD.PrintErr($"SSPM import failed: {e.Message}");
                return null;
            }
        }

        public static void ImportDirectory(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var path in Directory.GetFiles(directory, "*.sspm", SearchOption.TopDirectoryOnly)) Import(path);
        }

        public static string ReadMapId(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
                {
                    if (reader.ReadUInt32() != 0x6D2B5353) return null;
                    var version = reader.ReadUInt16();
                    if (version == 1)
                    {
                        reader.ReadUInt16();
                        return ReadLine(reader);
                    }
                    if (version != 2) return null;
                    reader.ReadUInt32();
                    reader.ReadBytes(20);
                    reader.ReadUInt32();
                    reader.ReadUInt32();
                    reader.ReadUInt32();
                    reader.ReadByte();
                    reader.ReadUInt16();
                    reader.ReadByte();
                    reader.ReadByte();
                    reader.ReadByte();
                    for (var i = 0; i < 10; i++) reader.ReadUInt64();
                    return ReadString(reader);
                }
            }
            catch { return null; }
        }

        private static string ImportV1(BinaryReader reader)
        {
            reader.ReadUInt32();
            reader.ReadUInt16();
            reader.ReadUInt16();
            var id = ReadLine(reader) ?? string.Empty;
            var mapName = ReadLine(reader) ?? id;
            var mapper = ReadLine(reader) ?? "Unknown";
            var duration = reader.ReadUInt32() / 1000.0;
            var count = reader.ReadUInt32();
            if (count > 2000000) throw new InvalidDataException("Too many notes.");
            var difficulty = DifficultyName(reader.ReadByte());
            var coverType = reader.ReadByte();
            byte[] cover;
            if (coverType == 1)
            {
                int height = reader.ReadUInt16(), width = reader.ReadUInt16();
                bool mipmaps = reader.ReadByte() != 0; var format = (Image.Format)reader.ReadByte();
                if (width < 1 || height < 1 || width > 4096 || height > 4096) throw new InvalidDataException("Invalid cover size.");
                var image = new Image(); image.CreateFromData(width, height, mipmaps, format, ReadSizedBlock(reader));
                cover = image.SavePngToBuffer();
            }
            else if (coverType == 2) cover = ReadSizedBlock(reader);
            else if (coverType == 0) cover = Array.Empty<byte>();
            else throw new InvalidDataException("Unknown cover encoding.");
            var audioType = reader.ReadByte();
            if (audioType != 1) throw new InvalidDataException("This map has no embedded audio.");
            var audio = ReadSizedBlock(reader);
            var notes = new List<JObject>();
            for (var i = 0; i < count; i++)
            {
                var time = reader.ReadUInt32();
                var storage = reader.ReadByte();
                float x;
                float y;
                if (storage == 0)
                {
                    x = -(reader.ReadByte() - 1);
                    y = -(reader.ReadByte() - 1);
                }
                else if (storage == 1)
                {
                    x = -(reader.ReadSingle() - 1f);
                    y = -(reader.ReadSingle() - 1f);
                }
                else return null;
                notes.Add(Note(time, x, y, id));
            }
            var separator = mapName.IndexOf(" - ", StringComparison.Ordinal);
            var artist = separator > 0 ? mapName.Substring(0, separator) : string.Empty;
            var title = separator > 0 ? mapName.Substring(separator + 3) : mapName;
            return WriteVulnus(id, artist, title, mapper, difficulty, notes, audio, cover, duration);
        }

        private static string ImportV2(BinaryReader reader)
        {
            reader.ReadUInt32();
            reader.ReadUInt16();
            reader.ReadUInt32();
            reader.ReadBytes(20);
            var duration = reader.ReadUInt32() / 1000.0;
            var noteCount = reader.ReadUInt32();
            var markerCount = reader.ReadUInt32();
            if (noteCount > 2000000 || markerCount > 4000000) throw new InvalidDataException("Too many markers.");
            var difficulty = DifficultyName(reader.ReadByte());
            reader.ReadUInt16();
            var hasAudio = reader.ReadByte() != 0;
            if (!hasAudio) throw new InvalidDataException("This map has no embedded audio.");
            var hasCover = reader.ReadByte() != 0;
            if (reader.ReadByte() != 0) throw new InvalidDataException("This map requires unsupported mods.");
            reader.ReadUInt64();
            reader.ReadUInt64();
            var audioOffset = reader.ReadUInt64();
            var audioLength = reader.ReadUInt64();
            var coverOffset = reader.ReadUInt64();
            var coverLength = reader.ReadUInt64();
            var definitionsOffset = reader.ReadUInt64();
            var definitionsLength = reader.ReadUInt64();
            var markersOffset = reader.ReadUInt64();
            var markersLength = reader.ReadUInt64();
            ValidateRange(reader, definitionsOffset, definitionsLength);
            ValidateRange(reader, markersOffset, markersLength);
            var id = ReadString(reader);
            var mapName = ReadString(reader);
            var songName = ReadString(reader);
            var mapperCount = reader.ReadUInt16();
            var mappers = new List<string>();
            for (var i = 0; i < mapperCount; i++) mappers.Add(ReadString(reader));
            if (definitionsOffset == 0) return null;
            reader.BaseStream.Position = (long)definitionsOffset;
            var definitionCount = reader.ReadByte();
            var definitions = new List<List<byte>>();
            var noteDefinition = -1;
            for (var i = 0; i < definitionCount; i++)
            {
                var name = ReadString(reader);
                var valueCount = reader.ReadByte();
                var types = new List<byte>();
                for (var j = 0; j < valueCount; j++) types.Add(reader.ReadByte());
                reader.ReadByte();
                definitions.Add(types);
                if (name == "ssp_note") noteDefinition = i;
            }
            if ((ulong)reader.BaseStream.Position > definitionsOffset + definitionsLength) throw new InvalidDataException("Marker definitions exceed their block.");
            if (noteDefinition < 0) return null;
            var audio = hasAudio ? ReadBlock(reader, audioOffset, audioLength) : Array.Empty<byte>();
            var cover = hasCover ? ReadBlock(reader, coverOffset, coverLength) : Array.Empty<byte>();
            reader.BaseStream.Position = (long)markersOffset;
            var notes = new List<JObject>();
            for (var i = 0; i < markerCount; i++)
            {
                var time = reader.ReadUInt32();
                var markerType = reader.ReadByte();
                if (markerType >= definitions.Count) return null;
                var types = definitions[markerType];
                if (markerType == noteDefinition && types.Count == 1 && types[0] == 0x07)
                {
                    var storage = reader.ReadByte();
                    float x;
                    float y;
                    if (storage == 0)
                    {
                        x = -(reader.ReadByte() - 1);
                        y = -(reader.ReadByte() - 1);
                    }
                    else if (storage == 1)
                    {
                        x = -(reader.ReadSingle() - 1f);
                        y = -(reader.ReadSingle() - 1f);
                    }
                    else return null;
                    notes.Add(Note(time, x, y, id));
                }
                else
                {
                    foreach (var type in types) SkipValue(reader, type);
                }
                if ((ulong)reader.BaseStream.Position > markersOffset + markersLength) throw new InvalidDataException("Markers exceed their block.");
            }
            if (notes.Count != noteCount || (ulong)reader.BaseStream.Position != markersOffset + markersLength) return null;
            var separator = mapName.IndexOf(" - ", StringComparison.Ordinal);
            var artist = separator > 0 ? mapName.Substring(0, separator) : string.Join(" & ", mappers);
            var title = separator > 0 ? mapName.Substring(separator + 3) : string.IsNullOrWhiteSpace(songName) ? mapName : songName;
            return WriteVulnus(id, artist, title, string.Join(" & ", mappers), difficulty, notes, audio, cover, duration);
        }

        private static JObject Note(uint time, float x, float y, string id)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) throw new InvalidDataException("Invalid note coordinates.");
            var note = new JObject();
            note["_time"] = time / 1000f;
            note["_x"] = x;
            note["_y"] = y;
            note["rhythiansMapId"] = id;
            return note;
        }

        private static string WriteVulnus(string id, string artist, string title, string mapper, string difficulty, List<JObject> notes, byte[] audio, byte[] cover, double duration)
        {
            if (string.IsNullOrWhiteSpace(id) || notes.Count == 0) return null;
            Directory.CreateDirectory(Global.MapPath);
            if (audio.Length == 0) throw new InvalidDataException("Missing audio.");
            duration = Math.Max(duration, notes.Max(n => (double)n["_time"]));
            notes = notes.OrderBy(n => (double)n["_time"]).ToList();
            string fingerprint;
            using (var sha = SHA256.Create()) fingerprint = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(id))).Replace("-", "").ToLowerInvariant();
            var output = Global.MapPath.PlusFile("sspm_" + fingerprint + ".vul");
            var temp = output + ".tmp";
            if (File.Exists(temp)) File.Delete(temp);
            using (var stream = File.Create(temp))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteJson(zip, "meta.json", new JObject
                {
                    ["_version"] = 1,
                    ["_artist"] = artist,
                    ["_title"] = title,
                    ["_mappers"] = new JArray(mapper),
                    ["_difficulties"] = new JArray("converted.json"),
                    ["_music"] = "music.bin",
                    ["_cover"] = cover.Length > 0 ? "cover.png" : "",
                    ["_length"] = duration,
                    ["rhythiansMapId"] = id
                });
                WriteJson(zip, "converted.json", new JObject
                {
                    ["_version"] = 1,
                    ["_name"] = difficulty,
                    ["_notes"] = new JArray(notes),
                    ["rhythiansMapId"] = id
                });
                if (audio.Length > 0) WriteBytes(zip, "music.bin", audio);
                if (cover.Length > 0) WriteBytes(zip, "cover.png", cover);
            }
            if (File.Exists(output)) File.Replace(temp, output, null); else File.Move(temp, output);
            LastError = null;
            return output;
        }

        private static void SkipValue(BinaryReader reader, byte type, int depth = 0)
        {
            if (depth > 16) throw new InvalidDataException("SSPM value nesting is too deep.");
            switch (type)
            {
                case 0x01: reader.ReadByte(); break;
                case 0x02: reader.ReadUInt16(); break;
                case 0x03: reader.ReadUInt32(); break;
                case 0x04: reader.ReadUInt64(); break;
                case 0x05: reader.ReadSingle(); break;
                case 0x06: reader.ReadDouble(); break;
                case 0x07:
                    var storage = reader.ReadByte();
                    if (storage == 0) { reader.ReadByte(); reader.ReadByte(); }
                    else if (storage == 1) { reader.ReadSingle(); reader.ReadSingle(); }
                    else throw new InvalidDataException("Invalid SSPM position storage type.");
                    break;
                case 0x08:
                case 0x09:
                    reader.ReadBytes(reader.ReadUInt16());
                    break;
                case 0x0A:
                case 0x0B:
                    var length = reader.ReadUInt32();
                    if (length > int.MaxValue) throw new InvalidDataException("SSPM value is too large.");
                    reader.ReadBytes((int)length);
                    break;
                case 0x0C:
                    var arrayType = reader.ReadByte();
                    var arrayLength = reader.ReadUInt16();
                    for (var i = 0; i < arrayLength; i++) SkipValue(reader, arrayType, depth + 1);
                    break;
                default: throw new InvalidDataException($"Unsupported SSPM data type: {type:X2}");
            }
        }

        private static byte[] ReadSizedBlock(BinaryReader reader)
        {
            var length = reader.ReadUInt64();
            if (length > 256 * 1024 * 1024 || length > (ulong)(reader.BaseStream.Length - reader.BaseStream.Position)) throw new InvalidDataException("Invalid block length.");
            return reader.ReadBytes((int)length);
        }

        private static byte[] ReadBlock(BinaryReader reader, ulong offset, ulong length)
        {
            ValidateRange(reader, offset, length);
            reader.BaseStream.Position = (long)offset;
            return reader.ReadBytes((int)length);
        }

        private static void ValidateRange(BinaryReader reader, ulong offset, ulong length)
        {
            if (length > 256 * 1024 * 1024 || offset > (ulong)reader.BaseStream.Length || length > (ulong)reader.BaseStream.Length - offset) throw new InvalidDataException("Invalid block range.");
        }

        private static string ReadString(BinaryReader reader)
        {
            var length = reader.ReadUInt16();
            if (length > reader.BaseStream.Length - reader.BaseStream.Position) throw new EndOfStreamException();
            return System.Text.Encoding.UTF8.GetString(reader.ReadBytes(length));
        }

        private static string ReadLine(BinaryReader reader)
        {
            var bytes = new List<byte>();
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var value = reader.ReadByte();
                if (value == 10) break;
                if (value != 13) bytes.Add(value);
            }
            return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
        }

        private static string DifficultyName(byte value)
        {
            switch (value)
            {
                case 1: return "Easy";
                case 2: return "Medium";
                case 3: return "Hard";
                case 4: return "Logic";
                case 5: return "Tasukete";
                default: return "Unknown";
            }
        }

        private static string Sanitize(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value;
        }

        private static void WriteJson(ZipArchive zip, string name, JObject json)
        {
            var entry = zip.CreateEntry(name);
            entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using (var writer = new StreamWriter(entry.Open(), new System.Text.UTF8Encoding(false))) writer.Write(json.ToString(Formatting.None));
        }

        private static void WriteBytes(ZipArchive zip, string name, byte[] data)
        {
            var entry = zip.CreateEntry(name);
            entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using (var stream = entry.Open()) stream.Write(data, 0, data.Length);
        }
    }
}
