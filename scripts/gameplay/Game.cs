using Godot;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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

		public override void _Ready()
		{
			Camera = GetNode<GameCamera>("Camera");
			Cursor = GetNode<Spatial>("Cursor");
			GhostCursor = GetNode<Spatial>("GhostCursor");
			NoteManager = GetNode<NoteManager>("NoteManager");
			NoteRenderer = NoteManager.GetNode<NoteRenderer>("NoteRenderer");
			SyncManager = GetNode<SyncManager>("SyncManager");
			HUDManager = GetNode<HUDManager>("HUD");
			ApplyAppearance();
			Score = new Score();
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
        private AudioStreamPlayer hitSound, missSound;
        private AudioStreamPlayer Sound(string kind, string name)
        {
            var path = AssetLibrary.Resolve(kind, name);
            if (path == null) return null;
            var player = new AudioStreamPlayer { Bus = "SFX", Stream = AudioHandler.LoadAudio(path) }; AddChild(player); return player;
        }
        public void ApplyAppearance()
        {
            Camera.Fov = Settings.CameraFov;
            foreach (MeshInstance cursor in new[] { (MeshInstance)Cursor, (MeshInstance)GhostCursor })
            {
                cursor.Scale = Vector3.One * Settings.CursorScale;
                cursor.RotateObjectLocal(Vector3.Up, Mathf.Deg2Rad(Settings.CursorRotation));
                cursor.MaterialOverride = new SpatialMaterial { FlagsTransparent = true, FlagsUnshaded = true, RenderPriority = 3,
                    AlbedoTexture = AssetLibrary.Texture("cursors", Settings.CursorAsset, "res://assets/skin/cursor.png"),
                    AlbedoColor = new Color(new Color(Settings.CursorColor), Settings.CursorOpacity) };
            }
            GhostCursor.Visible = false;
            var grid = GetNode<MeshInstance>("Grid"); grid.Visible = Settings.ShowGrid;
            grid.MaterialOverride = new SpatialMaterial { FlagsTransparent = true, FlagsUnshaded = true,
                AlbedoTexture = AssetLibrary.Texture("borders", Settings.BorderAsset, "res://assets/skin/grid_minimal.png") };
            GetNode<Spatial>("HUD/LeftPanel").Visible = Settings.ShowLeftPanel;
            GetNode<Spatial>("HUD/RightPanel").Visible = Settings.ShowRightPanel;
            GetNode<Spatial>("HUD/HealthPanel").Visible = Settings.ShowHealth;
            var background = AssetLibrary.Texture("backgrounds", Settings.BackgroundAsset);
            if (background != null)
            {
                var plane = new MeshInstance { Mesh = new QuadMesh { Size = new Vector2(220, 140) }, Translation = new Vector3(0, 0, -150),
                    MaterialOverride = new SpatialMaterial { FlagsUnshaded = true, AlbedoTexture = background } }; AddChild(plane);
            }
            var meshPath = AssetLibrary.Resolve("notes", Settings.NoteAsset);
            if (meshPath != null) try { NoteRenderer.Multimesh.Mesh = AssetLibrary.ParseObj(System.IO.File.ReadAllText(meshPath)); } catch(Exception e) { GD.PrintErr(e.Message); }
            hitSound = Sound("hitsounds", Settings.HitSoundAsset); missSound = Sound("misssounds", Settings.MissSoundAsset);
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
			if (Settings.PlayHitSound) hitSound?.Play();
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
			if (Settings.PlayMissSound) missSound?.Play();
			Score.Misses += 1;
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
			if (!string.IsNullOrWhiteSpace(mapId) && Score.Total > 0)
			{
				var accuracy = (double)(Score.Total - Score.Misses) / Score.Total * 100.0;
				accuracy = Math.Max(0, Math.Min(100, accuracy));
				var scoreKey = $"vulnus:{mapId}:{Score.Points}:{Score.Total}:{Score.Misses}:{Score.HighestCombo}";
				using (var sha = SHA256.Create())
				{
					var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(scoreKey));
					var clientScoreId = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
					RhythKitBridge.Send("MapCompleted", true, mapId, clientScoreId, accuracy, Score.Misses, SyncManager.Speed, !Score.Failed);
				}
			}
			else
			{
				RhythKitBridge.Send("MapEnded", true, mapId);
			}
			Global.Instance.GotoScene("res://scenes/MainMenu.tscn");
		}
	}
}
