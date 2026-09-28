using Godot;
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Content.Beatmaps;
using File = System.IO.File;
using Path = System.IO.Path;
using Directory = System.IO.Directory;

// Runs inside the real engine with an isolated --vulnus-user-dir. No mocked Godot API.
public class IntegrationChecks : Node
{
    public string Fixtures;
    private int checks;
    private void Check(bool result, string message) { if (!result) throw new Exception(message); GD.Print("PASS " + message); checks++; }
    public override void _Ready() { CallDeferred(nameof(Run)); }
    public async void Run()
    {
        try
        {
            Settings.UpdateSettings(true);
            Check(Settings.Volume.Length == 3 && Settings.ApproachTime > 0, "Settings initialize");
            var original = ProfileStore.Capture();
            string legacy = ProfileStore.Import(Path.Combine(Fixtures, "CAMLOCKKKKKK.settings.json"));
            string initialLegacy = File.ReadAllText(Path.Combine(ProfileStore.Root, legacy + ".json"));
            ProfileStore.Switch(legacy);
            Check(Settings.HalfGhost && Math.Abs(Settings.ApproachRate - 29.1f) < .001 && Settings.CameraMode == 1, "Legacy profile maps half ghost, rate and locked camera");
            Check(Settings.Volume.SequenceEqual(new[] { 50, 35, 10 }), "Decibel volumes convert to linear percentages");
            string rhs = ProfileStore.Import(Path.Combine(Fixtures, "main.rhs"));
            string initialRhs = File.ReadAllText(Path.Combine(ProfileStore.Root, rhs + ".json"));
            ProfileStore.Switch(rhs);
            Check(Settings.HalfGhost && Math.Abs(Settings.NoteOpacity - .65f) < .001 && Math.Abs(Settings.CursorScale - 1.1f) < .001, "RHS wrapped values convert");
            Check(AssetLibrary.Resolve("notes", Settings.NoteAsset) != null && AssetLibrary.Resolve("borders", Settings.BorderAsset) != null, "RHS embedded assets import");
            Check(AssetLibrary.ParseObj(File.ReadAllText(AssetLibrary.Resolve("notes", Settings.NoteAsset))).GetSurfaceCount() == 1, "Embedded OBJ loads in Godot");
            ProfileStore.Switch(legacy); Check(Math.Abs(Settings.NoteOpacity - 1) < .001, "Switching profiles resets absent settings");
            string color = AssetLibrary.Import("colorsets", Path.Combine(Fixtures, "Rainbow Freeze.txt"));
            Settings.ColorsetAsset = color; Check(AssetLibrary.Colors().Length == 24, "Rainbow Freeze has 24 colors");
            string cursor = AssetLibrary.Import("cursors", Path.Combine(Fixtures, "cursor.png"));
            Settings.CursorAsset = cursor; Check(AssetLibrary.Texture("cursors", cursor).GetWidth() == 512, "PNG cursor loads");
            ProfileStore.Save(); ProfileStore.Switch(rhs); ProfileStore.Switch(legacy);
            Check(Settings.CursorAsset == cursor && Settings.ColorsetAsset == color, "Profile retains asset selections");
            Settings.LoadSettings();
            Check(Settings.CursorAsset == cursor && Settings.ColorsetAsset == color && ProfileStore.Active == legacy, "Active profile and assets survive reload");
            string native = ProfileStore.Import(Path.Combine(ProfileStore.Root, legacy + ".json"));
            ProfileStore.Switch(native); Check(Settings.CursorAsset == cursor, "Native Vulnus profile imports");
            ProfileStore.Switch(legacy);
            Check(AssetLibrary.Resolve("cursors", "../escape.png") == null, "Asset paths stay inside library");
            bool rejected = false; try { AssetLibrary.ParseColors("ff0000,not-a-color"); } catch { rejected=true; }
            Check(rejected, "Invalid colorset rejected");
            Settings.HalfGhost = true; Settings.NoteOpacity=1; Settings.FadeLength=0;
            Check(Math.Abs(Gameplay.NoteRenderer.Opacity(.1f,.06f)-.2f)<.001 && Math.Abs(Gameplay.NoteRenderer.Opacity(.5f,.24f)-1)<.001, "Half ghost matches 20% / 100% fade endpoints");
            BeatmapSet playable = null;
            foreach (string name in new[] { "v1.sspm", "v2.sspm", "real.sspm" })
            {
                string fixture=Path.Combine(Fixtures,name); if(!File.Exists(fixture)) continue;
                string converted=Compatibility.SSP.SspmImporter.Import(fixture);
                Check(converted != null, name+" converts: "+Compatibility.SSP.SspmImporter.LastError);
                var map=BeatmapLoader.ImportFile(converted);
                Check(map.Length>0 && map.LoadCover()!=null && map.LoadAudio()!=null, name+" has duration, cover and audio");
                if(name != "real.sspm")
                {
                    var notes=map.Difficulties[0].Data.Notes;
                    Check(notes.Count==2 && notes[0].X==notes[1].X && notes[0].Y==notes[1].Y && notes[0].X==1 && notes[0].Y==-1, name+" integer and fractional coordinates agree");
                    Check(Math.Abs(map.Length-(name=="v1.sspm"?2.5:2.7))<.001, name+" preserves metadata duration");
                    Check(Math.Abs(map.LoadAudio().GetLength()-3)<.001, "PCM WAV duration decodes correctly");
                }
                int count=BeatmapLoader.LoadedMaps.Count; BeatmapLoader.ImportFile(converted);
                Check(BeatmapLoader.LoadedMaps.Count==count,"Repeated map import does not duplicate"); playable=map;
            }
            Check(Compatibility.SSP.SspmImporter.Import(Path.Combine(Fixtures,"broken.sspm"))==null,"Truncated SSPM rejected");
            rejected=false; try { BeatmapLoader.ImportFile(Path.Combine(Fixtures,"unsafe.vul")); } catch { rejected=true; }
            Check(rejected && !File.Exists(Path.Combine(Global.MapPath,".cache","escape.txt")),"Archive traversal rejected");
            // Export the actual converter results for the two user-supplied presets.
            string exports=Path.Combine(Fixtures,"converted"); Directory.CreateDirectory(exports);
            File.WriteAllText(Path.Combine(exports,"CAMLOCKKKKKK.vulnus.json"),initialLegacy);
            File.WriteAllText(Path.Combine(exports,"main.vulnus.json"),initialRhs);
            ProfileStore.Switch(rhs);
            // Keep the checks alive while the real scene transition frees Startup.
            GetParent().RemoveChild(this); GetTreeRoot().AddChild(this);
            Global.Instance.AddOverlay();
            await ToSignal(GetTree(),"idle_frame"); await ToSignal(GetTree(),"idle_frame");
            var options=(Options)Global.Instance.Overlays["Options"]; options.SetActive(true);
            await ToSignal(GetTree(),"idle_frame");
            Check(options.IsActive && options.Visible,"Settings menu opens");
            var tabs=options.GetChildren().OfType<TabContainer>().Single();
            Check(tabs.GetTabCount()==5,"All five settings tabs exist");
            tabs.CurrentTab=2;
            await ToSignal(GetTree(),"idle_frame"); await ToSignal(GetTree(),"idle_frame");
            var screenshot=GetViewport().GetTexture().GetData(); screenshot.FlipY(); screenshot.SavePng(Path.Combine(Fixtures,"settings.png"));
            options.SetActive(false);
            Global.Instance.GotoScene("res://scenes/MainMenu.tscn");
            await ToSignal(GetTree(),"idle_frame"); await ToSignal(GetTree(),"idle_frame");
            Check(Global.Instance.CurrentScene.Name == "MainMenu", "Startup transitions to main menu");
            var menu = (MenuHandler)Global.Instance.CurrentScene;
            menu.GoTo(1);
            await ToSignal(GetTree().CreateTimer(.4f), "timeout");
            var mapList = menu.GetNode<MapList>("ViewContainer/Singleplayer/MapList");
            Check(mapList.DisplayedMaps.Count == BeatmapLoader.LoadedMaps.Count, "Song list displays imported maps");
            bool imported=false; ImportCoordinator.Instance.MapsChanged += () => imported=true;
            GetTree().EmitSignal("files_dropped", new string[] { Path.Combine(Fixtures,"v2.sspm") }, 0);
            for(int i=0; i<300 && !imported; i++) await ToSignal(GetTree(),"idle_frame");
            Check(imported, "Window file-drop signal converts and refreshes maps");
            Gameplay.Game.LoadedMapset=playable; Gameplay.Game.LoadedMap=playable.Difficulties[0]; Gameplay.Game.LoadedMapData=playable.Difficulties[0].Data;
            Global.Instance.GotoScene("res://scenes/Game.tscn");
            await ToSignal(GetTree(),"idle_frame"); await ToSignal(GetTree(),"idle_frame");
            var game=(Gameplay.Game)Global.Instance.CurrentScene;
            Check(game.Camera.Fov==Settings.CameraFov && game.NoteRenderer.Multimesh.Mesh!=null,"Gameplay loads profile camera and note mesh");
            Check(((SpatialMaterial)((MeshInstance)game.Cursor).MaterialOverride).AlbedoTexture!=null,"Gameplay applies cursor texture");
            imported=false;
            GetTree().EmitSignal("files_dropped", new string[] { Path.Combine(Fixtures,"v1.sspm") }, 0);
            await ToSignal(GetTree(),"idle_frame"); await ToSignal(GetTree(),"idle_frame");
            Check(!imported,"Map drops wait during gameplay");
            await ToSignal(GetTree().CreateTimer(1.5f), "timeout");
            var image=GetViewport().GetTexture().GetData(); image.FlipY(); image.SavePng(Path.Combine(Fixtures,"gameplay.png"));
            game.GameEnded();
            await ToSignal(GetTree(),"idle_frame"); await ToSignal(GetTree(),"idle_frame");
            Check(!(Global.Instance.CurrentScene is Gameplay.Game), "Gameplay returns to menu");
            for(int i=0; i<300 && !imported; i++) await ToSignal(GetTree(),"idle_frame");
            Check(imported,"Queued map imports after gameplay");
            GD.Print("VULNUS CHECKS PASSED: "+checks); GetTree().Quit(0);
        }
        catch(Exception e) { GD.PrintErr("VULNUS CHECK FAILED: "+e); GetTree().Quit(1); }
    }
    private Viewport GetTreeRoot() => Global.Instance.GetTree().Root;
}
