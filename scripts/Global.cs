using Godot;
using System;
using System.Collections.Generic;

public class Global : Node
{
	public static string UserPath = ResolveUserPath();
	public static string MapPath = UserPath.PlusFile("maps");
	private static string ResolveUserPath()
	{
		var args = OS.GetCmdlineArgs();
		for (int i = 0; i < args.Length - 1; i++)
			if (args[i] == "--vulnus-user-dir") return System.IO.Path.GetFullPath(args[i + 1]);
		return OS.GetUserDataDir();
	}

	public static Global Instance;
	public static Texture Matt;
	public Node CurrentScene { get; private set; }
	public void RegisterScene(Node scene) { CurrentScene = scene; }
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
		AddChild(new ImportCoordinator());
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
			{ Settings.Fullscreen = !OS.WindowFullscreen; Settings.UpdateSettings(); }
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
		nextPath = path; nextCallback = callback;
        CallDeferred(nameof(DeferredGotoScene));
	}
	private string nextPath;
    private Action<Node> nextCallback;
    private void DeferredGotoScene()
	{
        var path = nextPath; var callback = nextCallback;
        nextPath = null; nextCallback = null;
        if (path == null) return;
        CurrentScene?.QueueFree();
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
