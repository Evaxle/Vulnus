using Godot;
using System;
using System.Collections.Generic;
using Content.Beatmaps;
using Compatibility.SSP;

public class Global : Node
{
	public static string MapPath = OS.GetUserDataDir().PlusFile("maps");

	public static Global Instance;
	public static Texture Matt;
	public Node CurrentScene { get; private set; }
	public Control Overlay { get; private set; }
	public Dictionary<string, Control> Overlays { get; private set; }
	public Global() : base()
	{
		if (!System.IO.Directory.Exists(MapPath))
			System.IO.Directory.CreateDirectory(MapPath);
		Instance = this;
	}
	public override void _Ready()
	{
		Input.UseAccumulatedInput = false;
		if (!GetTree().IsConnected("files_dropped", this, nameof(OnFilesDropped)))
			GetTree().Connect("files_dropped", this, nameof(OnFilesDropped));
		RhythKitBridge.Send("VulnusReady", true);
		var mattPath = ProjectSettings.GlobalizePath("user://").PlusFile("matt.jpg");
		if (System.IO.File.Exists(mattPath))
		{
			var image = new Image();
			image.Load(mattPath);
			var texture = new ImageTexture();
			texture.CreateFromImage(image);
			Matt = texture;
		}
		else
			Matt = (StreamTexture)GD.Load("res://assets/images/matt.jpg");
		Viewport root = GetTree().Root;
		CurrentScene = root.GetChild(root.GetChildCount() - 1);
	}
	public void OnFilesDropped(string[] files, int screen)
	{
		if (files == null || files.Length == 0)
			return;
		var imported = 0;
		foreach (var path in files)
		{
			if (string.IsNullOrWhiteSpace(path) || !string.Equals(System.IO.Path.GetExtension(path), ".sspm", StringComparison.OrdinalIgnoreCase))
				continue;
			var converted = SspmImporter.Import(path);
			if (string.IsNullOrEmpty(converted))
			{
				GD.PrintErr("Failed to import dropped SSPM: " + path);
				continue;
			}
			imported++;
			GD.Print("Imported SSPM: " + path + " -> " + converted);
		}
		if (imported == 0)
			return;
		BeatmapLoader.LoadMapsFromDirectory(MapPath, true);
		CallDeferred(nameof(RefreshMapLists));
	}

	private void RefreshMapLists()
	{
		GetTree().CallGroup("map_lists", nameof(MapList.ReloadMaps));
	}

	public override void _PhysicsProcess(float delta)
	{
		if (Input.IsActionJustPressed("fullscreen"))
			OS.WindowFullscreen = !OS.WindowFullscreen;
	}
	public void AddOverlay()
	{
		CallDeferred(nameof(DeferredAddOverlay));
	}
	private void DeferredAddOverlay()
	{
		var overlayScene = (PackedScene)GD.Load("res://scenes/Overlay.tscn");
		Overlay = (Control)overlayScene.Instance();
		Overlays = new Dictionary<string, Control>();
		GetTree().Root.AddChild(Overlay);
		GetTree().Root.MoveChild(Overlay, 2);
		foreach (Control overlay in Overlay.GetChildren())
			Overlays.Add(overlay.Name, overlay);
	}
	public void GotoScene(string path, Action<Node> callback = null)
	{
		CallDeferred(nameof(DeferredGotoScene), path, callback);
	}
	private void DeferredGotoScene(string path, Action<Node> callback = null)
	{
		CurrentScene.Free();
		var nextScene = (PackedScene)GD.Load(path);
		CurrentScene = nextScene.Instance();
		GetTree().Root.AddChild(CurrentScene);
		GetTree().Root.MoveChild(CurrentScene, 1);
		callback?.Invoke(CurrentScene);
	}
	public void FinishedLoading()
	{
		ViewportChanged();
		GetViewport().Connect("size_changed", this, nameof(ViewportChanged));
	}
	public void ViewportChanged()
	{
		GetViewport().Size = OS.WindowSize * Settings.RenderScale;
		GetViewport().Debanding = Settings.Debanding;
	}
}
