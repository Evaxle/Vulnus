using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using Content.Beatmaps;
using Gameplay;

// Run with a dedicated custom_user_dir_name containing "VulnusSmokeTest".
public class SmokeTest : Node
{
    public override void _Ready() { Global.Instance.CallDeferred("add_child", new SmokeRunner()); }
}

public class SmokeRunner : Node
{
    private int assertions;
    private async Task Capture(string name)
    {
        await ToSignal(GetTree().CreateTimer(0.6f), "timeout");
        var image = GetViewport().GetTexture().GetData();
        image.FlipY();
        image.SavePng(OS.GetUserDataDir().PlusFile(name + ".png"));
    }
    private void Check(bool pass, string message)
    {
        if (!pass) throw new Exception(message);
        assertions++;
        GD.Print("PASS: " + message);
    }
    private async Task Until(Func<bool> condition, string name)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new Exception("Timed out: " + name);
            await ToSignal(GetTree(), "idle_frame");
        }
        await ToSignal(GetTree(), "idle_frame");
    }
    public override async void _Ready()
    {
        try
        {
            Check(OS.GetUserDataDir().Contains("VulnusSmokeTest"), "Isolated test data directory");
            System.IO.File.WriteAllText(OS.GetUserDataDir().PlusFile("settings.bin"), "damaged settings fixture");
            System.IO.File.WriteAllText(Global.MapPath.PlusFile("broken.vul"), "damaged archive fixture");
            Global.Instance.GotoScene("res://scenes/Startup.tscn");
            await Until(() => Global.Instance.CurrentScene is MenuHandler, "startup to main menu");
            Check(BeatmapLoader.LoadedMaps.Count > 0, "Startup loads folder maps despite corrupt settings/archive");
            Check(Global.Instance.Overlays.ContainsKey("Options"), "Settings overlay exists before menu");
            int difficulties = 0;
            foreach (var set in BeatmapLoader.LoadedMaps)
            {
                Check(set.LoadAudio() != null, "Audio: " + set.Name);
                foreach (var map in set.Difficulties)
                {
                    Check(map.Load() == Error.Ok && map.Playable, "Difficulty: " + set.Name + " / " + map.Name);
                    difficulties++;
                }
            }
            var menu = (MenuHandler)Global.Instance.CurrentScene;
            menu.GoTo(1);
            var single = menu.GetNode<Singleplayer>("ViewContainer/Singleplayer");
            var list = single.GetNode<MapList>("MapList");
            var detail = single.GetNode<MapDetails>("MapDetails");
            var search = list.GetNode<LineEdit>("Filters/Search");
            search.Text = "no-such-map-zzzzzzzz"; list.UpdateDisplayed(true);
            Check(list.DisplayedMaps.Count == 0, "Search with no results");
            search.Text = ""; list.UpdateDisplayed(true);
            Check(list.DisplayedMaps.Count == BeatmapLoader.LoadedMaps.Count, "Clearing search restores maps");
            list.Scroll(10000); list.Scroll(-10000);
            foreach (var index in new[] { 0, 1, 2, 3 })
            {
                list.GetNode<OptionButton>("Filters/Sort").Selected = index; list.UpdateDisplayed(true);
                Check(list.DisplayedMaps.Count > 0, "Sort mode " + index);
            }
            var options = (Options)Global.Instance.Overlays["Options"];
            options.SetActive(true); options.SetActive(false); options.SetActive(true);
            Check(options.IsActive && options.Visible, "Rapid settings toggle");
            var approach = options.GetNode<Approach>("Content/Gameplay/Grid/Approach/VBoxContainer");
            approach.GetNode<SpinBox>("Time/SpinBox").Value = 0.5;
            Check(Settings.ApproachTime == 0.5f && Settings.ApproachRate == 100, "Approach controls update derived values without recursion");
            var camera = options.GetNode<CameraSettings>("Content/Gameplay/Grid/Camera/VBoxContainer");
            camera.GetNode<SpinBox>("Sensitivity/SpinBox").Value = 1.5;
            Check(Settings.MouseSensitivity == 1.5f, "Sensitivity control updates settings");
            var render = options.GetNode<RenderSettings>("Content/Visual/Grid/Render/VBoxContainer");
            render.GetNode<SpinBox>("FPS/SpinBox").Value = 120;
            Check(Engine.TargetFps == 120, "FPS control applies limit");
            var volume = options.GetNode<Volume>("Content/Audio/Volume/VBoxContainer");
            volume.GetNode<Godot.Slider>("Music/Slider").Value = 40;
            Check(Settings.Volume[1] == 40, "Music volume control applies setting");
            Settings.ApproachMode = 0; Settings.ApproachTime = 0; Settings.RenderScale = 0; Settings.Volume = null;
            Settings.UpdateSettings();
            Check(Settings.ApproachTime > 0 && Settings.RenderScale > 0 && Settings.Volume.Length == 3, "Invalid settings recover safely");
            Settings.MouseSensitivity = 1.75f; Settings.UpdateSettings(); Settings.MouseSensitivity = 1; Settings.LoadSettings();
            Check(Settings.MouseSensitivity == 1.75f, "Settings save and reload");
            await Capture("settings");
            options.SetActive(false);
            var first = list.DisplayedMaps[0].Difficulties[0];
            var last = list.DisplayedMaps.Last().Difficulties[0];
            detail.MapSelected(first); detail.MapSelected(last);
            var play = detail.GetNode<Button>("Details/Play");
            await Until(() => !play.Disabled, "rapid map selection");
            Check(detail.GetNode<Label>("Details/AspectRatioContainer/Map/Title").Text == last.Mapset.Title, "Latest selection wins");
            detail.GetNode<Button>("Details/Mods").EmitSignal("pressed");
            var modsPanel = single.GetNode<Control>("ModSelect");
            Check(modsPanel.Visible, "Mods button opens panel");
            var noFail = modsPanel.GetNode<CheckButton>("ModPanel/VBoxContainer/Misc/HBoxContainer/NoFail");
            noFail.Pressed = true;
            Check(Game.Mods.Any(m => m is Gameplay.Mods.ModNoFail), "No Fail can be selected");
            noFail.Pressed = false;
            Check(Game.Mods.Count == 0, "No Fail can be removed");
            modsPanel.GetNode<Button>("ModPanel/Button").EmitSignal("pressed");
            Check(!modsPanel.Visible, "Mods panel closes");
            await Capture("selection");
            play.EmitSignal("pressed");
            await Until(() => Global.Instance.CurrentScene is Game, "Play button");
            var game = (Game)Global.Instance.CurrentScene;
            Check(Game.LoadedMap == last && game.NoteManager.Notes.Count == last.Data.Notes.Count, "Selected map enters gameplay");
            Check(!options.CanOpen, "Settings disabled during gameplay");
            await Until(() => game.SyncManager.AudioPlayer.Playing, "audio starts");
            Check(game.SyncManager.SongTime >= 0, "Audio clock advances");
            await Capture("gameplay");
            game.NoteManager.SetPhysicsProcess(false);
            game.NoteManager.SetProcess(false);
            game.SyncManager.SetProcess(false);
            game.NoteManager.NextNote = new Note(0, 0, 30, 0);
            game.NoteManager.LastNote = null;
            game.SyncManager.NoteTime = game.SyncManager.SongTime = 1;
            Check(game.SyncManager.CanSkip(), "Long intro can be skipped");
            game.SyncManager.AttemptSkip();
            Check(game.SyncManager.SongTime == 29, "Skip lands one second before next note");
            var late = new Note(0, 0, 0, 0);
            game.NoteManager.Notes = new System.Collections.Generic.List<Note> { late };
            game.NoteManager.OrderedNotes = game.NoteManager.Notes;
            game.NoteManager.NextNote = late;
            game.Camera.ClampedCursorPosition = new Vector2(0, 0);
            game.SyncManager.NoteTime = 1;
            var misses = Game.Score.Misses;
            game.NoteManager._PhysicsProcess(0);
            Check(Game.Score.Misses == misses + 1, "Expired note cannot count as a hit");
            var hit = new Note(0, 0, 2, 0);
            game.NoteManager.Notes[0] = hit;
            game.SyncManager.NoteTime = 2;
            var points = Game.Score.Points;
            game.NoteManager._PhysicsProcess(0);
            Check(Game.Score.Points > points, "On-time cursor contact scores a hit");
            Game.Score.Health = 0;
            game._PhysicsProcess(0);
            await Until(() => Global.Instance.CurrentScene is MenuHandler, "results");
            menu = (MenuHandler)Global.Instance.CurrentScene;
            Check(menu.GetNode<Results>("ViewContainer/Results").IsActive, "Results display after ending");
            Check(options.CanOpen, "Settings restored after gameplay");
            Check(Game.Score.Failed, "Zero health ends normal gameplay as failed");
            menu.GetNode<Button>("ViewContainer/Results/Retry").EmitSignal("pressed");
            await Until(() => Global.Instance.CurrentScene is Game, "retry");
            Check(Game.Score.Total == 0, "Retry resets score");
            ((Game)Global.Instance.CurrentScene).SyncManager.AudioPlayer.EmitSignal("finished");
            await Until(() => Global.Instance.CurrentScene is MenuHandler, "second results");
            menu = (MenuHandler)Global.Instance.CurrentScene;
            menu.GetNode<Button>("ViewContainer/Results/Return").EmitSignal("pressed");
            Check(menu.GetNode<Singleplayer>("ViewContainer/Singleplayer").IsActive, "Return button restores map selection");
            GD.Print($"SMOKE PASSED: {assertions} assertions; {BeatmapLoader.LoadedMaps.Count} map sets; {difficulties} difficulties.");
            GetTree().Quit(0);
        }
        catch (Exception e) { GD.PrintErr("SMOKE FAILED: " + e); GetTree().Quit(1); }
    }
}
