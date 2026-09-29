using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using Content.Beatmaps;

namespace Gameplay
{
	using Mods;
	public class Game : Spatial
	{
		public static BeatmapSet LoadedMapset;
		public static Beatmap LoadedMap;
		public static BeatmapData LoadedMapData;
		public static Score Score;
		public static ModList Mods = new ModList();
		public GameCamera Camera;
		public Spatial Cursor;
		public Spatial GhostCursor;
		public NoteManager NoteManager;
		public NoteRenderer NoteRenderer;
		public SyncManager SyncManager;
		public HUDManager HUDManager;
		public bool Ended;
		public bool CanFail;
		public bool Paused { get; private set; }
		private List<double> missTimes;
		private CanvasLayer pauseLayer;

		public override void _Ready()
		{
			Camera = GetNode<GameCamera>("Camera");
			Cursor = GetNode<Spatial>("Cursor");
			GhostCursor = GetNode<Spatial>("GhostCursor");
			NoteManager = GetNode<NoteManager>("NoteManager");
			NoteRenderer = NoteManager.GetNode<NoteRenderer>("NoteRenderer");
			SyncManager = GetNode<SyncManager>("SyncManager");
			HUDManager = GetNode<HUDManager>("HUD");
			Score = new Score();
			missTimes = new List<double>();
			CanFail = false;
			Ended = false;
			Paused = false;
			Camera.Cursor = Cursor;
			Camera.GhostCursor = GhostCursor;
			BuildPauseMenu();
			ApplyAppearance();
			if (LoadedMapset == null || LoadedMapData == null)
			{
				Global.Instance.GotoScene("res://scenes/MainMenu.tscn");
				return;
			}
			RhythKitBridge.Send("MapStarted", true, LoadedMapset.RhythiansMapId);
			SyncManager.SetStream(LoadedMapset.LoadAudio());
			SyncManager.Ended += GameEnded;
			NoteManager.NoteHit += OnNoteHit;
			NoteManager.NoteMiss += OnNoteMiss;
			if (Mods.Any(m => m is IApplicableToGame)) foreach (var mod in Mods.OfType<IApplicableToGame>()) mod.ApplyToGame(this);
			if (Mods.Any(m => m is IApplicableToNoteManager)) foreach (var mod in Mods.OfType<IApplicableToNoteManager>()) mod.ApplyToNoteManager(NoteManager);
			if (Mods.Any(m => m is IApplicableToNoteRenderer)) foreach (var mod in Mods.OfType<IApplicableToNoteRenderer>()) mod.ApplyToNoteManager(NoteRenderer);
			if (Mods.Any(m => m is IApplicableToSyncManager)) foreach (var mod in Mods.OfType<IApplicableToSyncManager>()) mod.ApplyToSyncManager(SyncManager);
			if (Mods.Any(m => m is IApplicableToHUDManager)) foreach (var mod in Mods.OfType<IApplicableToHUDManager>()) mod.ApplyToHUDManager(HUDManager);
		}

		public override void _Input(InputEvent @event)
		{
			if (!(@event is InputEventKey)) return;
			var key = (InputEventKey)@event;
			if (!key.Pressed || key.Echo) return;
			if (key.Scancode == (uint)KeyList.Escape || key.Scancode == (uint)KeyList.P)
			{
				TogglePause();
				GetTree().SetInputAsHandled();
				return;
			}
			if (key.Scancode == (uint)KeyList.R)
			{
				ExitMap();
				GetTree().SetInputAsHandled();
			}
		}

		public override void _PhysicsProcess(float delta)
		{
			if (Paused || Ended) return;
			if (Input.IsActionJustPressed("skip") && SyncManager.CanSkip()) SyncManager.AttemptSkip();
			if (Score.Health <= 0)
			{
				Score.Failed = true;
				if (CanFail) GameEnded();
			}
		}

		public override void _EnterTree()
		{
			Options opt = (Options)Global.Instance.Overlays["Options"];
			opt.CanOpen = false;
			if (opt.IsActive) opt.SetActive(false);
		}

		public override void _ExitTree()
		{
			Options opt = (Options)Global.Instance.Overlays["Options"];
			opt.CanOpen = true;
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		private void BuildPauseMenu()
		{
			pauseLayer = new CanvasLayer();
			pauseLayer.Layer = 100;
			pauseLayer.Visible = false;
			AddChild(pauseLayer);

			var shade = new ColorRect();
			shade.AnchorRight = 1;
			shade.AnchorBottom = 1;
			shade.Color = new Color(0, 0, 0, 0.78f);
			pauseLayer.AddChild(shade);

			var panel = new VBoxContainer();
			panel.AnchorLeft = 0.5f;
			panel.AnchorTop = 0.5f;
			panel.AnchorRight = 0.5f;
			panel.AnchorBottom = 0.5f;
			panel.MarginLeft = -180;
			panel.MarginTop = -120;
			panel.MarginRight = 180;
			panel.MarginBottom = 120;
			panel.AddConstantOverride("separation", 12);
			shade.AddChild(panel);

			var title = new Label();
			title.Text = "PAUSED";
			title.RectMinSize = new Vector2(0, 56);
			title.Align = Label.AlignEnum.Center;
			title.Valign = Label.VAlign.Center;
			panel.AddChild(title);

			var resume = new Button();
			resume.Text = "RESUME";
			resume.RectMinSize = new Vector2(0, 48);
			resume.Connect("pressed", this, nameof(TogglePause));
			panel.AddChild(resume);

			var restart = new Button();
			restart.Text = "RESTART MAP";
			restart.RectMinSize = new Vector2(0, 48);
			restart.Connect("pressed", this, nameof(RestartMap));
			panel.AddChild(restart);

			var exit = new Button();
			exit.Text = "EXIT MAP";
			exit.RectMinSize = new Vector2(0, 48);
			exit.Connect("pressed", this, nameof(ExitMap));
			panel.AddChild(exit);

			var hint = new Label();
			hint.Text = "Esc/P: pause  •  R: exit map";
			hint.Align = Label.AlignEnum.Center;
			panel.AddChild(hint);
		}

		public void TogglePause()
		{
			if (Ended) return;
			Paused = !Paused;
			pauseLayer.Visible = Paused;
			if (SyncManager != null && SyncManager.AudioPlayer != null)
				SyncManager.AudioPlayer.StreamPaused = Paused;
			Input.MouseMode = Paused ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
		}

		public void RestartMap()
		{
			if (Ended) return;
			Ended = true;
			Paused = false;
			if (SyncManager != null && SyncManager.AudioPlayer != null)
				SyncManager.AudioPlayer.Stop();
			Score = null;
			Global.Instance.GotoScene("res://scenes/Game.tscn");
		}

		public void ExitMap()
		{
			if (Ended) return;
			Ended = true;
			Paused = false;
			if (SyncManager != null && SyncManager.AudioPlayer != null)
				SyncManager.AudioPlayer.Stop();
			var mapId = LoadedMapset == null ? null : LoadedMapset.RhythiansMapId;
			var cameraMode = Settings.CameraMode == 0 ? "spin" : "lock";
			RhythKitBridge.Send("MapEnded", true, mapId, null, null, null, null, false, cameraMode);
			Score = null;
			Global.Instance.GotoScene("res://scenes/MainMenu.tscn");
		}

		private void ApplyAppearance()
		{
			ApplyCursorAppearance(Cursor as MeshInstance, false);
			ApplyCursorAppearance(GhostCursor as MeshInstance, true);
			var scale = new Vector3(Settings.CursorScale, Settings.CursorScale, Settings.CursorScale);
			if (Cursor != null) Cursor.Scale = scale;
			if (GhostCursor != null) GhostCursor.Scale = scale;
		}

		private void ApplyCursorAppearance(MeshInstance mesh, bool ghost)
		{
			if (mesh == null) return;
			SpatialMaterial material = null;
			var active = mesh.GetActiveMaterial(0) as SpatialMaterial;
			if (active != null)
				material = active.Duplicate() as SpatialMaterial;
			if (material == null)
				material = new SpatialMaterial();
			material.FlagsTransparent = true;
			var color = new Color(Settings.CursorColor);
			color.a = ghost ? Math.Min(0.6f, Settings.CursorOpacity) : Settings.CursorOpacity;
			material.AlbedoColor = color;
			var cursorPath = Settings.ResolveCursorPath();
			if (!string.IsNullOrWhiteSpace(cursorPath))
			{
				var image = new Image();
				if (image.Load(cursorPath) == Error.Ok)
				{
					var texture = new ImageTexture();
					texture.CreateFromImage(image);
					material.AlbedoTexture = texture;
				}
			}
			mesh.MaterialOverride = material;
		}

		public void OnNoteHit(Note note)
		{
			Score.Points += 25 * Score.Multiplier;
			Score.Miniplier += 1;
			if (Score.Miniplier >= 8 && Score.Multiplier < 8)
			{
				Score.Miniplier = 0;
				Score.Multiplier = Mathf.Min(8, Score.Multiplier + 1);
			}
			Score.Combo += 1;
			if (Score.Combo > Score.HighestCombo) Score.HighestCombo = Score.Combo;
			Score.Total += 1;
			if (!Score.Failed) Score.Health = Math.Min(10, Score.Health + 10.0 / 8.0);
			HUDManager.ManualUpdate(Score);
		}

		public void OnNoteMiss(Note note)
		{
			Score.Miniplier = 0;
			Score.Multiplier = Mathf.Max(1, Score.Multiplier - 1);
			Score.Combo = 0;
			Score.Misses += 1;
			missTimes.Add(Math.Max(0, note.T * 1000.0));
			Score.Total += 1;
			if (!Score.Failed) Score.Health = Math.Max(0, Score.Health - 2);
			HUDManager.ManualUpdate(Score);
		}

		public void GameEnded()
		{
			if (Ended) return;
			Ended = true;
			Paused = false;
			SyncManager.AudioPlayer.Stop();
			var mapId = LoadedMapset == null ? null : LoadedMapset.RhythiansMapId;
			var cameraMode = Settings.CameraMode == 0 ? "spin" : "lock";
			if (!string.IsNullOrWhiteSpace(mapId) && Score.Total > 0)
			{
				var accuracy = (double)(Score.Total - Score.Misses) / Score.Total * 100.0;
				accuracy = Math.Max(0, Math.Min(100, accuracy));
				var clientScoreId = Guid.NewGuid().ToString();
				var qualified = !Score.Failed;
				RhythKitBridge.Send("MapCompleted", true, mapId, clientScoreId, accuracy, Score.Misses, SyncManager.Speed, qualified, cameraMode);
				if (qualified && RhythiansApi.IsAuthenticated)
				{
					RhythiansApi.ResetScoreResult();
					var submittedMissTimes = missTimes.ToArray();
					Task.Run(() => RhythiansApi.SubmitScore(mapId, clientScoreId, accuracy, Score.Misses, SyncManager.Speed, cameraMode, submittedMissTimes));
				}
			}
			else
			{
				RhythKitBridge.Send("MapEnded", true, mapId, null, null, null, null, null, cameraMode);
			}
			Global.Instance.GotoScene("res://scenes/MainMenu.tscn");
		}
	}
}
