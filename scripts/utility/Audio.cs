using Godot;
using System;
using System.IO;
using System.Text;

public static class AudioHandler
{
    public static AudioStream LoadAudio(string path)
    {
        try
        {
            var bytes = System.IO.File.ReadAllBytes(path);
            if (bytes.Length < 4) return null;
            AudioStream stream;
            if (Encoding.ASCII.GetString(bytes, 0, 4) == "OggS")
                stream = new AudioStreamOGGVorbis { Data = bytes };
            else if (Encoding.ASCII.GetString(bytes, 0, 3) == "ID3" || (bytes[0] == 0xff && (bytes[1] & 0xe0) == 0xe0))
                stream = new AudioStreamMP3 { Data = bytes };
            else if (Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF")
                stream = LoadWave(bytes);
            else
            {
                // Some existing maps have padding before their first MPEG frame.
                var offset = FindMp3Frame(bytes);
                if (offset < 0) return null;
                var audio = new byte[bytes.Length - offset];
                Array.Copy(bytes, offset, audio, 0, audio.Length);
                stream = new AudioStreamMP3 { Data = audio };
            }
            return stream != null && stream.GetLength() > 0 ? stream : null;
        }
        catch (Exception e) { GD.PrintErr($"Could not load audio {path}: {e.Message}"); return null; }
    }
    private static int FindMp3Frame(byte[] bytes)
    {
        for (int i = 0; i < Math.Min(bytes.Length - 3, 4096); i++)
            if (bytes[i] == 0xff && (bytes[i + 1] & 0xe0) == 0xe0 &&
                (bytes[i + 1] & 0x18) != 0x08 && (bytes[i + 1] & 0x06) != 0 &&
                (bytes[i + 2] & 0xf0) != 0 && (bytes[i + 2] & 0xf0) != 0xf0 &&
                (bytes[i + 2] & 0x0c) != 0x0c) return i;
        return -1;
    }
    private static AudioStream LoadWave(byte[] bytes)
    {
        using (var input = new MemoryStream(bytes))
        using (var reader = new BinaryReader(input))
        {
            input.Position = 8;
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") return null;
            int format = 0, channels = 0, rate = 0, bits = 0;
            byte[] pcm = null;
            while (input.Position + 8 <= input.Length)
            {
                var id = Encoding.ASCII.GetString(reader.ReadBytes(4));
                var size = reader.ReadUInt32();
                var end = input.Position + size;
                if (end > input.Length || size > int.MaxValue) return null;
                if (id == "fmt " && size >= 16)
                {
                    format = reader.ReadUInt16(); channels = reader.ReadUInt16(); rate = reader.ReadInt32();
                    reader.ReadUInt32(); reader.ReadUInt16(); bits = reader.ReadUInt16();
                }
                else if (id == "data") pcm = reader.ReadBytes((int)size);
                input.Position = end + (size % 2);
            }
            if (format != 1 || (channels != 1 && channels != 2) || rate <= 0 || (bits != 8 && bits != 16) || pcm == null) return null;
            // WAV 8-bit PCM is unsigned; Godot's sample data is signed.
            if (bits == 8) for (int i = 0; i < pcm.Length; i++) pcm[i] ^= 0x80;
            return new AudioStreamSample { Data = pcm, MixRate = rate, Stereo = channels == 2,
                Format = bits == 8 ? AudioStreamSample.FormatEnum.Format8Bits : AudioStreamSample.FormatEnum.Format16Bits };
        }
    }
}
