using Godot;
using System;
using System.IO;
using File = System.IO.File;
using Directory = System.IO.Directory;
using Path = System.IO.Path;
using Environment = System.Environment;
using Newtonsoft.Json;

public static class RhythKitBridge
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CapoRhythia", "Rhythians", "bridge");
    private static readonly string EventPath = Path.Combine(DirectoryPath, "events-vulnus.jsonl");

    public static void Send(string eventName, bool running, string mapId = null, string clientScoreId = null, double? accuracy = null, int? misses = null, double? speed = null, bool? qualified = null)
    {
        if (Array.IndexOf(OS.GetCmdlineArgs(), "--smoke-test") >= 0) return;
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var payload = new
            {
                Event = eventName,
                Running = running,
                IntegrationConnected = true,
                MapCaptureReady = !string.IsNullOrWhiteSpace(mapId),
                Game = "Vulnus",
                GameVersion = "Vulnus",
                MapId = mapId,
                ClientScoreId = clientScoreId,
                Accuracy = accuracy,
                Misses = misses,
                Speed = speed,
                CompletedAt = DateTimeOffset.UtcNow,
                ResultQualified = qualified
            };
            File.AppendAllText(EventPath, JsonConvert.SerializeObject(payload) + Environment.NewLine);
        }
        catch (Exception e)
        {
            GD.PrintErr($"RhythKit bridge error: {e.Message}");
        }
    }
}
