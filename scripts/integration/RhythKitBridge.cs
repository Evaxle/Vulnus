using Godot;
using System;
using System.IO;
using IOFile = System.IO.File;
using IODirectory = System.IO.Directory;
using IOPath = System.IO.Path;
using SysEnvironment = System.Environment;
using Newtonsoft.Json;

public static class RhythKitBridge
{
    private static readonly string DirectoryPath = IOPath.Combine(SysEnvironment.GetFolderPath(SysEnvironment.SpecialFolder.ApplicationData), "CapoRhythia", "Rhythians", "bridge");
    private static readonly string EventPath = IOPath.Combine(DirectoryPath, "events-vulnus.jsonl");

    public static void Send(string eventName, bool running, string mapId = null, string clientScoreId = null, double? accuracy = null, int? misses = null, double? speed = null, bool? qualified = null, string cameraMode = null)
    {
        try
        {
            IODirectory.CreateDirectory(DirectoryPath);
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
                ResultQualified = qualified,
                CameraMode = cameraMode == "spin" ? "spin" : cameraMode == null ? null : "lock",
                PointSystem = cameraMode == "spin" ? "RPS" : cameraMode == null ? null : "RPL"
            };
            IOFile.AppendAllText(EventPath, JsonConvert.SerializeObject(payload) + SysEnvironment.NewLine);
        }
        catch (Exception e)
        {
            GD.PrintErr($"RhythKit bridge error: {e.Message}");
        }
    }
}
