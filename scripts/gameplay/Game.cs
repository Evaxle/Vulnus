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
		private bool paused;
		private CanvasLayer pauseLayer;
		private List<double> missTimes;

		public override void _Ready()
		{
			PauseMode = PauseModeEnum.Process;
			Camera = GetNode<GameCamera>("Camera");
			Cursor = GetNode<Spatial>("Cursor");
			GhostCursor = GetNode<Spatial>("GhostCursor");
			NoteManager = GetNode<NoteManager>("NoteManager");
			NoteRenderer = NoteManager.GetNode<NoteRenderer>("NoteRenderer");
			SyncManager = GetNode<SyncManager>("SyncManager");
			HUDManager = GetNode<HUDManager>("HUD");
			Score = new Score();
			missTimes = new List<double>();
			CanFail = true;
			Ended = false;
			paused = false;
			Camera.Cursor = Cursor;
			Camera.GhostCursor = GhostCursor;
			Camera.ApplyVisualSettings();
			BuildPauseMenu();
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

		public override void _PhysicsProcess(float delta)
		{
			if (Ended) return;
			if (Input.IsActionJustPressed("force_end"))
			{
				ExitMap();
				return;
			}
			if (Input.IsActionJustPressed("pause"))
			{
				TogglePause();
				return;
			}
			if (paused) return;
			if (Input.IsActionJustPressed("skip") && SyncManager.CanSkip()) SyncManager.AttemptSkip();
			if (Score.Health <= 0 && !Score.Failed)
			{
				Score.Failed = true;
				if (CanFail)
					FailMap();
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
			if (GetTree().Paused) GetTree().Paused = false;
			Options opt = (Options)Global.Instance.Overlays["Options"];
			opt.CanOpen = true;
		}

		private void BuildPauseMenu()
		{
			pauseLayer = new CanvasLayer();
			pauseLayer.Name = "PauseMenu";
			pauseLayer.PauseMode = PauseModeEnum.Process;
			pauseLayer.Visible = false;
			AddChild(pauseLayer);

			var shade = new ColorRect();
			shade.AnchorRight = 1f;
			shade.AnchorBottom = 1f;
			shade.Color = new Color(0, 0, 0, 0.78f);
			shade.PauseMode = PauseModeEnum.Process;
			pauseLayer.AddChild(shade);

			var panel = new Panel();
			panel.AnchorLeft = 0.5f;
			panel.AnchorTop = 0.5f;
			panel.AnchorRight = 0.5f;
			panel.AnchorBottom = 0.5f;
			panel.MarginLeft = -190;
			panel.MarginTop = -150;
			panel.MarginRight = 190;
			panel.MarginBottom = 150;
			panel.PauseMode = PauseModeEnum.Process;
			shade.AddChild(panel);

			var layout = new VBoxContainer();
			layout.AnchorRight = 1f;
			layout.AnchorBottom = 1f;
			layout.MarginLeft = 24;
			layout.MarginTop = 24;
			layout.MarginRight = -24;
			layout.MarginBottom = -24;
			layout.AddConstantOverride("separation", 12);
			layout.PauseMode = PauseModeEnum.Process;
			panel.AddChild(layout);

			var title = new Label();
			title.Text = "Paused";
			title.Align = Label.AlignEnum.Center;
			title.RectMinSize = new Vector2(0, 52);
			layout.AddChild(title);

			AddPauseButton(layout, "RESUME", nameof(ResumeMap));
			AddPauseButton(layout, "RESTART MAP", nameof(RestartMap));
			AddPauseButton(layout, "EXIT MAP", nameof(ExitMap));
		}

		private void AddPauseButton(VBoxContainer parent, string text, string method)
		{
			var button = new Button();
			button.Text = text;
			button.RectMinSize = new Vector2(0, 52);
			button.PauseMode = PauseModeEnum.Process;
			button.Connect("pressed", this, method);
			parent.AddChild(button);
		}

		private void TogglePause()
		{
			if (paused) ResumeMap();
			else PauseMap();
		}

		public void PauseMap()
		{
			if (Ended || paused) return;
			paused = true;
			Settings.AnyPause = true;
			pauseLayer.Visible = true;
			Input.MouseMode = Input.MouseModeEnum.Visible;
			GetTree().Paused = true;
		}

		public void ResumeMap()
		{
			if (!paused) return;
			GetTree().Paused = false;
			paused = false;
			pauseLayer.Visible = false;
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}

		public void RestartMap()
		{
			GetTree().Paused = false;
			paused = false;
			Settings.AnyPause = true;
			Global.Instance.GotoScene("res://scenes/Game.tscn");
		}

		public void ExitMap()
		{
			GetTree().Paused = false;
			paused = false;
			Settings.AnyPause = true;
			Ended = true;
			SyncManager.AudioPlayer.Stop();
			RhythKitBridge.Send("MapEnded", true, LoadedMapset == null ? null : LoadedMapset.RhythiansMapId, null, null, null, false, null, Settings.CameraMode == 0 ? "spin" : "lock");
			Score = null;
			Global.Instance.GotoScene("res://scenes/MainMenu.tscn");
		}

		private void FailMap()
		{
			if (Ended) return;
			Ended = true;
			SyncManager.AudioPlayer.Stop();
			Input.MouseMode = Input.MouseModeEnum.Visible;
			RhythKitBridge.Send("MapCompleted", true, LoadedMapset == null ? null : LoadedMapset.RhythiansMapId, null, 0, Score.Misses, SyncManager.Speed, false, Settings.CameraMode == 0 ? "spin" : "lock");
			Global.Instance.GotoScene("res://scenes/MainMenu.tscn");
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
