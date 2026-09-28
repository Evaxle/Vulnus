using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class Options : View
{
    public bool CanOpen = true;
    private TabContainer tabs;
    private Label status;
    private FileDialog picker;
    private string importKind;
    private LineEdit profileName;
    private readonly List<Action> refresh = new List<Action>();
    public override void _Ready()
    {
        tabs = new TabContainer { AnchorRight = 1, AnchorBottom = 1, MarginLeft = 18, MarginTop = 56, MarginRight = -18, MarginBottom = -66 };
        AddChild(tabs);
        var title = new Label { Text = "VULNUS  /  Settings", MarginLeft = 20, MarginTop = 16 }; AddChild(title);
        var close = new Button { Text = "Close", AnchorLeft = 1, AnchorRight = 1, MarginLeft = -100, MarginRight = -18, MarginTop = 12, MarginBottom = 44 };
        AddChild(close); close.Connect("pressed", this, nameof(Close));
        status = new Label { AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1, MarginLeft = 20, MarginTop = -56, MarginRight = -20, MarginBottom = -8, Autowrap = true };
        AddChild(status);
        picker = new FileDialog { Access = FileDialog.AccessEnum.Filesystem, Mode = FileDialog.ModeEnum.OpenFiles };
        AddChild(picker); picker.Connect("files_selected", this, nameof(ImportFiles));
        Build();
        Visible = false;
    }
    private VBoxContainer Page(string name)
    {
        var scroll = new ScrollContainer { Name = name, SizeFlagsHorizontal = (int)SizeFlags.ExpandFill, SizeFlagsVertical = (int)SizeFlags.ExpandFill };
        tabs.AddChild(scroll);
        var margin = new MarginContainer { SizeFlagsHorizontal = (int)SizeFlags.ExpandFill };
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddConstantOverride("margin_" + side, 16);
        scroll.AddChild(margin);
        var box = new VBoxContainer { SizeFlagsHorizontal = (int)SizeFlags.ExpandFill }; box.AddConstantOverride("separation", 12); margin.AddChild(box); return box;
    }
    private HBoxContainer Row(VBoxContainer page, string label)
    {
        var row = new HBoxContainer(); page.AddChild(row);
        row.AddChild(new Label { Text = label, RectMinSize = new Vector2(245, 38), SizeFlagsHorizontal = (int)SizeFlags.ExpandFill }); return row;
    }
    private void Text(VBoxContainer page, string text) { page.AddChild(new Label { Text = text, Autowrap = true, RectMinSize = new Vector2(0, 58) }); }
    private Button Button(Node parent, string text, string method, params object[] args)
    {
        var button = new Button { Text = text, RectMinSize = new Vector2(110, 36) }; parent.AddChild(button);
        var binds = new Godot.Collections.Array(); foreach (var arg in args) binds.Add(arg);
        button.Connect("pressed", this, method, binds); return button;
    }
    private void Number(VBoxContainer page, string label, string field, double min, double max, double step)
    {
        var input = new SpinBox { MinValue = min, MaxValue = max, Step = step, RectMinSize = new Vector2(180, 38) };
        var f = typeof(Settings).GetField(field); input.Value = Convert.ToDouble(f.GetValue(null));
        Row(page, label).AddChild(input); input.Connect("value_changed", this, nameof(NumberChanged), new Godot.Collections.Array(field));
        refresh.Add(() => input.Value = Convert.ToDouble(f.GetValue(null)));
    }
    private void Toggle(VBoxContainer page, string label, string field)
    {
        var f = typeof(Settings).GetField(field); var input = new CheckButton { Pressed = (bool)f.GetValue(null) };
        Row(page, label).AddChild(input); input.Connect("toggled", this, nameof(ToggleChanged), new Godot.Collections.Array(field));
        refresh.Add(() => input.Pressed = (bool)f.GetValue(null));
    }
    private void Choice(VBoxContainer page, string label, string field, string[] choices)
    {
        var input = new OptionButton { RectMinSize = new Vector2(220, 38) }; foreach (var choice in choices) input.AddItem(choice);
        var f = typeof(Settings).GetField(field); input.Selected = (int)f.GetValue(null);
        Row(page, label).AddChild(input); input.Connect("item_selected", this, nameof(ChoiceChanged), new Godot.Collections.Array(field));
        refresh.Add(() => input.Selected = (int)f.GetValue(null));
    }
    private void Asset(VBoxContainer page, string label, string field, string kind)
    {
        var row = Row(page, label); var input = new OptionButton { RectMinSize = new Vector2(210, 38), SizeFlagsHorizontal = (int)SizeFlags.ExpandFill, ClipText = true };
        row.AddChild(input);
        Action update = () => {
            input.Clear(); input.AddItem(kind == "colorsets" ? "Default · Red / Cyan" : "Default"); input.SetItemMetadata(0, "");
            if (kind == "colorsets") { input.AddItem("Rainbow Freeze"); input.SetItemMetadata(1, "Rainbow Freeze"); }
            foreach (var name in AssetLibrary.Names(kind)) { input.AddItem(name); input.SetItemMetadata(input.GetItemCount()-1, name); }
            string selected = (string)typeof(Settings).GetField(field).GetValue(null);
            for (int i = 0; i < input.GetItemCount(); i++) if ((string)input.GetItemMetadata(i) == selected) input.Selected = i;
        };
        update(); refresh.Add(update);
        input.Connect("item_selected", this, nameof(AssetChanged), new Godot.Collections.Array(field, input));
        Button(row, "Import…", nameof(Pick), kind);
    }
    private void Build()
    {
        var profiles = Page("Profiles");
        Text(profiles, "Each profile remembers gameplay, appearance and selected assets. Changes save automatically. Import Vulnus JSON, Rhythia settings JSON or an RHS archive.");
        var profile = new OptionButton { RectMinSize = new Vector2(300, 38) }; Row(profiles, "Active profile").AddChild(profile);
        Action updateProfiles = () => { profile.Clear(); foreach (var n in ProfileStore.Names()) profile.AddItem(n); for (int i=0;i<profile.GetItemCount();i++) if(profile.GetItemText(i)==ProfileStore.Active) profile.Selected=i; };
        updateProfiles(); refresh.Add(updateProfiles); profile.Connect("item_selected", this, nameof(SwitchProfile), new Godot.Collections.Array(profile));
        profileName = new LineEdit { PlaceholderText = "New profile name", SizeFlagsHorizontal = (int)SizeFlags.ExpandFill };
        var row = Row(profiles, "Save a copy"); row.AddChild(profileName); Button(row, "Create", nameof(Duplicate));
        Button(profiles, "Import settings…", nameof(Pick), "profiles");
        Button(profiles, "Open profiles folder / export JSON", nameof(OpenProfiles));
        Text(profiles, "Imports keep unsupported source options under ‘unmapped’ in the JSON. Foreign skin paths and game-specific features are never silently treated as working Vulnus settings.");
        var gameplay = Page("Gameplay");
        Choice(gameplay, "Camera", "CameraMode", new[] { "Free camera", "Locked camera" });
        Number(gameplay, "Mouse sensitivity", "MouseSensitivity", .01, 10, .01);
        Choice(gameplay, "Approach calculation", "ApproachMode", new[] { "Distance + time", "Distance + rate", "Rate + time" });
        Number(gameplay, "Spawn distance", "ApproachDistance", 1, 200, .01);
        Number(gameplay, "Approach rate", "ApproachRate", 1, 200, .01);
        Number(gameplay, "Approach time (seconds)", "ApproachTime", .01, 10, .01);
        Number(gameplay, "Field of view", "CameraFov", 30, 120, 1);
        Number(gameplay, "Camera parallax", "Parallax", 0, 10, .1);
        Toggle(gameplay, "Clamp cursor drift", "CursorDrift");
        Number(gameplay, "Music offset (milliseconds)", "MusicOffset", -1000, 1000, 1);
        var appearance = Page("Appearance");
        Asset(appearance, "Color set", "ColorsetAsset", "colorsets");
        Asset(appearance, "Cursor", "CursorAsset", "cursors");
        Asset(appearance, "Playfield border", "BorderAsset", "borders");
        Asset(appearance, "Background image", "BackgroundAsset", "backgrounds");
        Asset(appearance, "Note mesh", "NoteAsset", "notes");
        var preview = new HBoxContainer(); appearance.AddChild(preview);
        var cursorPreview = new TextureRect { RectMinSize = new Vector2(48, 48), Expand = true, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        preview.AddChild(cursorPreview);
        var swatches = new HBoxContainer(); preview.AddChild(swatches);
        Action previewAssets = () => {
            cursorPreview.Texture = AssetLibrary.Texture("cursors", Settings.CursorAsset, "res://assets/skin/cursor.png");
            cursorPreview.Modulate = new Color(new Color(Settings.CursorColor), Settings.CursorOpacity);
            foreach (Node child in swatches.GetChildren()) { swatches.RemoveChild(child); child.QueueFree(); }
            foreach (var c in AssetLibrary.Colors().Take(24)) swatches.AddChild(new ColorRect { Color = c, RectMinSize = new Vector2(16, 24), SizeFlagsVertical = (int)SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
        };
        previewAssets(); refresh.Add(previewAssets);
        Text(appearance, "Color sets use comma-separated hex colors. Images use PNG/JPEG; note meshes use OBJ geometry. Imported assets stay available when switching profiles.");
        Number(appearance, "Cursor size", "CursorScale", .1, 4, .01);
        Number(appearance, "Cursor opacity", "CursorOpacity", .05, 1, .01);
        Number(appearance, "Cursor rotation (degrees)", "CursorRotation", -360, 360, 1);
        Number(appearance, "Cursor spin (degrees / second)", "CursorSpin", -1080, 1080, 1);
        var color = new ColorPickerButton { Color = new Color(Settings.CursorColor), RectMinSize = new Vector2(180, 38), EditAlpha = false };
        Row(appearance, "Cursor tint").AddChild(color); color.Connect("color_changed", this, nameof(ColorChanged)); refresh.Add(() => color.Color = new Color(Settings.CursorColor));
        Number(appearance, "Note size (visual only)", "NoteScale", .1, 3, .01);
        Number(appearance, "Note opacity", "NoteOpacity", .05, 1, .01);
        Number(appearance, "Spawn fade length", "FadeLength", 0, 1, .01);
        Toggle(appearance, "Half ghost", "HalfGhost");
        Text(appearance, "Half ghost fades notes from 100% to 20% opacity between 240 ms and 60 ms before arrival. It does not change the hit window.");
        Toggle(appearance, "Show playfield border", "ShowGrid");
        Toggle(appearance, "Show health bar", "ShowHealth");
        Toggle(appearance, "Show left panel", "ShowLeftPanel");
        Toggle(appearance, "Show right panel", "ShowRightPanel");
        var audio = Page("Audio");
        string[] buses = { "Master", "Music", "Sound effects" };
        for (int i=0; i<3; i++) { var v = new SpinBox { MinValue=0,MaxValue=100,Step=1,Value=Settings.Volume[i],RectMinSize=new Vector2(180,38) }; Row(audio,buses[i]+" volume (%)").AddChild(v); v.Connect("value_changed",this,nameof(VolumeChanged),new Godot.Collections.Array(i)); int index=i; refresh.Add(()=>v.Value=Settings.Volume[index]); }
        Toggle(audio, "Preview selected song", "AutoPreview");
        Toggle(audio, "Play imported hit sound", "PlayHitSound");
        Toggle(audio, "Play imported miss sound", "PlayMissSound");
        Asset(audio, "Hit sound", "HitSoundAsset", "hitsounds");
        Asset(audio, "Miss sound", "MissSoundAsset", "misssounds");
        var display = Page("Display");
        Toggle(display, "Fullscreen", "Fullscreen"); Toggle(display, "VSync", "VSync"); Toggle(display, "Debanding", "Debanding");
        Number(display, "Frame limit (0 = unlimited)", "FPSLimit", 0, 1000, 1);
        Number(display, "Render scale", "RenderScale", .25, 2, .01);
        Choice(display, "Bloom", "Bloom", new[] { "High", "Low", "Off" });
    }
    private bool refreshing;
    private void Refresh() { refreshing=true; foreach(var action in refresh) action(); refreshing=false; }
    public void NumberChanged(double value,string field) { if(refreshing)return; var f=typeof(Settings).GetField(field); f.SetValue(null,Convert.ChangeType(value,f.FieldType)); Commit(); }
    public void ToggleChanged(bool value,string field) { if(refreshing)return; typeof(Settings).GetField(field).SetValue(null,value); Commit(); }
    public void ChoiceChanged(int value,string field) { typeof(Settings).GetField(field).SetValue(null,value); Commit(); }
    public void AssetChanged(int index,string field,OptionButton input) { typeof(Settings).GetField(field).SetValue(null,(string)input.GetItemMetadata(index)); Commit(); }
    public void ColorChanged(Color color) { if(refreshing)return; Settings.CursorColor=color.ToHtml(false); Commit(); }
    public void VolumeChanged(double value,int index) { if(refreshing)return; Settings.Volume[index]=(int)value; Commit(); }
    private void Commit() { try { Settings.UpdateSettings(); Refresh(); status.Text="Saved to “"+ProfileStore.Active+"”."; } catch(Exception e) { status.Text=e.Message; } }
    public void SwitchProfile(int index,OptionButton input) { try { ProfileStore.Switch(input.GetItemText(index)); Refresh(); status.Text="Active profile: "+ProfileStore.Active; } catch(Exception e) { status.Text=e.Message; } }
    public void Duplicate() { try { if(string.IsNullOrWhiteSpace(profileName.Text))throw new Exception("Enter a profile name."); ProfileStore.Duplicate(profileName.Text); profileName.Text=""; Refresh(); } catch(Exception e) { status.Text=e.Message; } }
    public void OpenProfiles() { OS.ShellOpen(ProfileStore.Root); }
    public void Pick(string kind)
    {
        importKind=kind; picker.ClearFilters();
        if(kind=="profiles") { picker.AddFilter("*.json ; Settings JSON"); picker.AddFilter("*.rhs ; Rhythia settings archive"); }
        else if(kind=="colorsets") picker.AddFilter("*.txt ; Color set");
        else if(kind=="notes") picker.AddFilter("*.obj ; Note mesh");
        else if(kind.EndsWith("sounds")) { picker.AddFilter("*.ogg ; OGG"); picker.AddFilter("*.mp3 ; MP3"); picker.AddFilter("*.wav ; WAV"); }
        else { picker.AddFilter("*.png ; PNG image"); picker.AddFilter("*.jpg,*.jpeg ; JPEG image"); }
        picker.PopupCenteredRatio(.8f);
    }
    public void ImportFiles(string[] paths)
    {
        foreach(var path in paths) try { if(importKind=="profiles") { ProfileStore.Import(path); status.Text=ProfileStore.LastReport; } else { AssetLibrary.Import(importKind,path); status.Text="Imported "+System.IO.Path.GetFileName(path)+". Select it from the list."; } } catch(Exception e) { status.Text=e.Message; }
        Refresh();
    }
    public override void _PhysicsProcess(float delta) { if(CanOpen && Input.IsActionJustPressed("options")) SetActive(!IsActive); if(IsActive && Input.IsActionJustPressed("ui_cancel") && !picker.Visible) Close(); }
    public void Close() { SetActive(false); }
    public override void OnShow() { Refresh(); IsActive=true; Visible=true; status.Text="Active profile: "+ProfileStore.Active; }
    public override void OnHide() { IsActive=false; Visible=false; }
}
