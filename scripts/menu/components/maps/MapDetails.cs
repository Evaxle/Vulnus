using Godot;
using System;
using System.Threading.Tasks;
using System.Linq;
using Content.Beatmaps;
using Gameplay;

public class MapDetails : View
{
    private BeatmapSet currentMap;
    private Beatmap currentDifficulty;
    private MapList mapList;
    private Control mapDetails;
    private Control details;
    private Control loading;
    private AudioStreamPlayer musicPreview;
    private Button play;
    private Label error;
    private Control modPanel;
    private CheckButton noFail;
    private int selection;
    private bool busy;
    private bool starting;
    public override void _Ready()
    {
        mapList = GetParent().GetNode<MapList>("MapList");
        mapList.MapSelected += MapSelected;
        modPanel = GetParent().GetNode<Control>("ModSelect");
        modPanel.Visible = false;
        noFail = new CheckButton { Name = "NoFail", Text = "No Fail", Pressed = Game.Mods.Any(m => m is Gameplay.Mods.ModNoFail) };
        modPanel.GetNode<Control>("ModPanel/VBoxContainer/Misc/HBoxContainer").AddChild(noFail);
        noFail.Connect("toggled", this, nameof(SetNoFail));
        modPanel.GetNode<Button>("ModPanel/Button").Connect("pressed", this, nameof(HideMods));
        modPanel.GetNode<Control>("ModPanel/ConfigPanel").Visible = false;
        details = GetNode<Control>("Details");
        mapDetails = details.GetNode<Control>("AspectRatioContainer/Map");
        loading = GetNode<Control>("Loading");
        musicPreview = GetNode<AudioStreamPlayer>("MusicPreview");
        play = details.GetNode<Button>("Play");
        play.Connect("pressed", this, nameof(PlayMap));
        details.GetNode<Button>("Mods").Connect("pressed", this, nameof(ShowMods));
        error = new Label { Autowrap = true, MouseFilter = MouseFilterEnum.Ignore };
        details.AddChild(error);
        error.AnchorTop = 1; error.AnchorBottom = 1; error.AnchorRight = 1;
        error.MarginTop = -100; error.MarginBottom = -50;
        Visible = false;
    }
    public void ShowMods()
    {
        modPanel.Visible = true;
        UpdateModsLabel();
    }
    public void HideMods() => modPanel.Visible = false;
    public void SetNoFail(bool enabled)
    {
        if (enabled) Game.Mods.Add(new Gameplay.Mods.ModNoFail());
        else Game.Mods.RemoveAll(m => m is Gameplay.Mods.ModNoFail);
        UpdateModsLabel();
    }
    private void UpdateModsLabel()
        => modPanel.GetNode<Label>("ModPanel/VBoxContainer/SelectedMods").Text = "Selected mods: " + Game.Mods;
    public void PlayMap()
    {
        if (busy || starting || currentDifficulty?.Playable != true || musicPreview.Stream == null) return;
        starting = true;
        play.Disabled = true;
        musicPreview.Stop();
        Game.LoadedMapset = currentMap;
        Game.LoadedMap = currentDifficulty;
        Game.LoadedMapData = currentDifficulty.Data;
        Global.Instance.GotoScene("res://scenes/Game.tscn");
    }
    public async void MapSelected(Beatmap map)
    {
        var request = ++selection;
        musicPreview.Stop();
        musicPreview.Stream = null;
        currentDifficulty = map;
        if (map == null) { SetActive(false); return; }
        currentMap = map.Mapset;
        SetActive(true);
        busy = true;
        play.Disabled = true;
        loading.Visible = true;
        details.Visible = false;
        error.Text = "";
        try
        {
            var result = await Task.Run(() => map.Load());
            if (!IsInsideTree() || request != selection) return;
            mapDetails.GetNode<TextureRect>("Cover").Texture = currentMap.LoadCover();
            mapDetails.GetNode<Label>("Title").Text = currentMap.Title;
            mapDetails.GetNode<Label>("Title/Artist").Text = currentMap.Artist;
            mapDetails.GetNode<Label>("Title/Mapper").Text = currentMap.Mappers;
            mapDetails.GetNode<Label>("Difficulty").Text = map.Name;
            if (result != Error.Ok || !map.Playable) throw new Exception("This difficulty is empty, damaged, or uses an unsupported format.");
            musicPreview.Stream = currentMap.LoadAudio();
            if (musicPreview.Stream == null || musicPreview.Stream.GetLength() <= 0) throw new Exception("The map's audio is missing or unsupported.");
            if (IsVisibleInTree()) musicPreview.Play(musicPreview.Stream.GetLength() / 3f);
            play.Disabled = false;
        }
        catch (Exception e)
        {
            if (!IsInsideTree() || request != selection) return;
            error.Text = e.Message;
            GD.PrintErr(e.Message);
        }
        finally
        {
            if (IsInsideTree() && request == selection)
            {
                busy = false;
                loading.Visible = false;
                details.Visible = true;
            }
        }
    }
    public override void _Process(float delta)
    {
        if (busy) loading.GetNode<Control>("Circle").RectRotation += delta * 90;
        if (!IsVisibleInTree() && musicPreview.Playing) musicPreview.Stop();
    }
    public override void _ExitTree()
    {
        ++selection;
        mapList.MapSelected -= MapSelected;
    }
    public override void OnShow() { IsActive = true; Visible = true; }
    public override void OnHide() { IsActive = false; Visible = false; musicPreview.Stop(); }
}
