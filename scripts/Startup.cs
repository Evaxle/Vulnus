using Godot;
using System;
using System.Threading.Tasks;

public class Startup : Node
{
	private Label label;
	private string stage = "Loading Vulnus";
	private float counter;
	private bool failed;
	public override async void _Ready()
	{
		label = GetNode<Label>("Label");
		try
		{
			if (TryHandleRhythKitConversion()) return;
			if (OS.HasFeature("Android")) OS.RequestPermissions();
			SetStage("Loading settings");
			Settings.UpdateSettings(true);
			SetStage("Adding overlays");
			Global.Instance.AddOverlay();
            await ToSignal(GetTree(), "idle_frame");
			SetStage("Loading maps");
			var bundledMaps = OS.HasFeature("standalone")
				? OS.GetExecutablePath().GetBaseDir().PlusFile("maps")
				: ProjectSettings.GlobalizePath("res://maps");
			// Only map file parsing runs on the worker; scene APIs stay on the main thread.
			await Task.Run(() => {
				Content.Beatmaps.BeatmapLoader.LoadMapsFromDirectory(Global.MapPath, true);
				if (!string.Equals(bundledMaps, Global.MapPath, StringComparison.OrdinalIgnoreCase))
					Content.Beatmaps.BeatmapLoader.LoadMapsFromDirectory(bundledMaps);
			});
			Global.Instance.FinishedLoading();
			Global.Instance.GotoScene("res://scenes/MainMenu.tscn");
		}
		catch (Exception e)
		{
			failed = true;
			label.Text = "Could not load Vulnus: " + e.Message + "\nSee the game log for details.";
			GD.PrintErr(e);
		}
	}
	private void SetStage(string value) { stage = value; counter = 0; }
	private bool TryHandleRhythKitConversion()
	{
		var args = OS.GetCmdlineArgs();
		for (var i = 0; i < args.Length - 1; i++)
		{
			if (!string.Equals(args[i], "--rhythkit-convert-sspm", StringComparison.OrdinalIgnoreCase)) continue;
			var result = Compatibility.SSP.SspmImporter.Import(args[i + 1]);
			GD.Print(result == null ? "RhythKit SSPM conversion failed" : $"RhythKit SSPM conversion complete: {result}");
			GetTree().Quit(result == null ? 1 : 0);
			return true;
		}
		return false;
	}
	public override void _Process(float delta)
	{
		if (failed) return;
		counter += delta;
		label.Text = stage + new string('.', (int)(counter * 3) % 4);
	}
}
