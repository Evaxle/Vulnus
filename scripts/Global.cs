using Godot;
using System;
using System.Collections.Generic;

public class Global : Node
{
	public static string MapPath = OS.GetUserDataDir().PlusFile("maps");

	public static Global Instance;
	public static Texture Matt;
	public Node CurrentScene => GetTree().CurrentScene;
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
		if (Overlay != null) return;
		var overlayScene = (PackedScene)GD.Load("res://scenes/Overlay.tscn");
		Overlay = (Control)overlayScene.Instance();
		Overlays = new Dictionary<string, Control>();
		GetTree().Root.AddChild(Overlay);
		GetTree().Root.MoveChild(Overlay, 2);
		foreach (Control overlay in Overlay.GetChildren())
			Overlays.Add(overlay.Name, overlay);
	}
    private string pendingScene;
    public void GotoScene(string path)
    {
        if (pendingScene != null) return;
        pendingScene = path;
        CallDeferred(nameof(DeferredGotoScene));
    }
    private void DeferredGotoScene()
    {
        var path = pendingScene;
        pendingScene = null;
        // Resolve the destination before releasing the current scene.
        var nextScene = GD.Load<PackedScene>(path);
        if (nextScene == null) { GD.PrintErr("Could not load scene: " + path); return; }
        var next = nextScene.Instance();
        CurrentScene?.Free();
        GetTree().Root.AddChild(next);
        GetTree().CurrentScene = next;
        GetTree().Root.MoveChild(next, 1);
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
