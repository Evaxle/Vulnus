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
		private List<double> missTimes;

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
			Camera.Cursor = Cursor;
			Camera.GhostCursor = GhostCursor;
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
			if (Input.IsActionJustPressed("skip") && SyncManager.CanSkip()) SyncManager.AttemptSkip();
			if (Input.IsActionJustPressed("force_end"))
			{
				Score.Failed = true;
				GameEnded();
			}
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
					var submittedMissTimes = missTimes.ToArray();
					Task.Run(() => RhythiansApi.SubmitScore(mapId, clientScoreId, accuracy, Score.Misses, SyncManager.Speed, cameraMode, submittedMissTimes));
				}
			}
			else
			{
				RhythKitBridge.Send("MapEnded", true, mapId, null, null, null, null, null, cameraMode);
			}
			Global.Instance.GotoScene("res://scenes/MainMenu.tscn");
		}	}
}
