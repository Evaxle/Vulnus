using Godot;
using System;

public static class AudioHandler
{
	public static AudioStream LoadAudio(string path)
	{
		var file = new File();
		if (file.Open(path, File.ModeFlags.Read) != Error.Ok) return null;
		var buffer = file.GetBuffer((long)file.GetLen());
		file.Close();
		AudioStream stream;
		switch (GetFileFormat(buffer))
		{
			case "mp3":
				stream = new AudioStreamMP3();
				((AudioStreamMP3)stream).Data = buffer;
				break;
			case "ogg":
				stream = new AudioStreamOGGVorbis();
				((AudioStreamOGGVorbis)stream).Data = buffer;
				break;
			case "wav":
				return LoadWav(buffer);
			default:
				return null;
		}
		return stream;
	}
	private static AudioStream LoadWav(byte[] bytes)
	{
		try
		{
			using (var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(bytes)))
			{
				reader.BaseStream.Position = 12;
				int channels = 0, rate = 0, bits = 0; byte[] data = null;
				while (reader.BaseStream.Position + 8 <= bytes.Length)
				{
					string chunk = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
					uint size = reader.ReadUInt32(); long next = reader.BaseStream.Position + size + (size % 2);
					if (next > bytes.Length) return null;
					if (chunk == "fmt ")
					{
						if (size < 16 || reader.ReadUInt16() != 1) return null;
						channels = reader.ReadUInt16(); rate = reader.ReadInt32(); reader.ReadUInt32(); reader.ReadUInt16(); bits = reader.ReadUInt16();
					}
					else if (chunk == "data") data = reader.ReadBytes((int)size);
					reader.BaseStream.Position = next;
				}
				if (data == null || (channels != 1 && channels != 2) || (bits != 8 && bits != 16) || rate < 8000 || rate > 192000) return null;
				if (bits == 8) for (int i = 0; i < data.Length; i++) data[i] = (byte)(data[i] - 128);
				return new AudioStreamSample { Data = data, MixRate = rate, Stereo = channels == 2, Format = bits == 16 ? AudioStreamSample.FormatEnum.Format16Bits : AudioStreamSample.FormatEnum.Format8Bits };
			}
		}
		catch { return null; }
	}
	private static string GetFileFormat(byte[] bytes)
	{
		if (bytes.Length < 10) return "unknown";
		if (bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46) return "wav";
		if ((bytes[0] == 0xFF && (bytes[1] == 0xFB || (bytes[1] == 0xFA && bytes[2] == 0x90))) || (bytes[0] == 0x49 && bytes[1] == 0x44 && bytes[2] == 0x33)) return "mp3";
		if (bytes[0] == 0x4F && bytes[1] == 0x67 && bytes[2] == 0x67 && bytes[3] == 0x53) return "ogg";
		return "unknown";
	}
}
