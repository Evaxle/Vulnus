using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Content.Beatmaps;
using File = System.IO.File;
using Path = System.IO.Path;

public class ImportCoordinator : CanvasLayer
{
    public static ImportCoordinator Instance;
    public event Action MapsChanged;
    private readonly Queue<string> pending = new Queue<string>();
    private bool busy;
    private Label status;
    private FileDialog dialog;
    private float remaining;
    public override void _Ready()
    {
        Instance = this; Layer = 20;
        GetTree().Connect("files_dropped", this, nameof(Dropped));
        status = new Label { AnchorLeft = .12f, AnchorRight = .88f, MarginTop = 8, MarginBottom = 64, Autowrap = true, Align = Label.AlignEnum.Center, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        AddChild(status);
        dialog = new FileDialog { Access = FileDialog.AccessEnum.Filesystem, Mode = FileDialog.ModeEnum.OpenFiles };
        dialog.AddFilter("*.sspm ; Sound Space Plus map"); dialog.AddFilter("*.vul ; Vulnus map");
        AddChild(dialog); dialog.Connect("files_selected", this, nameof(Selected));
    }
    public void Open() { dialog.PopupCenteredRatio(.8f); }
    public void Selected(string[] files) { Dropped(files, 0); }
    public void Dropped(string[] files, int screen)
    {
        foreach (var path in files) if (File.Exists(path)) pending.Enqueue(path);
        Notice(Global.Instance.CurrentScene is Gameplay.Game ? "Files queued. Import will begin when you return to the menu." : "Preparing imports…");
    }
    private void Notice(string text) { status.Text = text; status.Visible = true; remaining = 12; }
    public override void _Process(float delta)
    {
        if (Global.Instance.CurrentScene is Gameplay.Game && !busy) status.Visible = false;
        if (!busy && pending.Count > 0 && !(Global.Instance.CurrentScene is Gameplay.Game) && Global.Instance.Overlay != null) ProcessNext();
        if (!busy && pending.Count == 0) { remaining -= delta; if (remaining <= 0) status.Visible = false; }
    }
    private async void ProcessNext()
    {
        busy = true; var path = pending.Dequeue();
        Notice("Importing " + Path.GetFileName(path) + "…");
        try
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".sspm" || ext == ".vul")
            {
                string mapPath = path;
                if (ext == ".sspm")
                {
                    mapPath = await Task.Run(() => Compatibility.SSP.SspmImporter.Import(path));
                    if (mapPath == null) throw new InvalidDataException(Compatibility.SSP.SspmImporter.LastError);
                }
                var map = BeatmapLoader.ImportFile(mapPath);
                MapsChanged?.Invoke();
                Notice("Imported “" + map.Title + "” · " + TimeSpan.FromSeconds(map.Length).ToString(@"m\:ss") + ". Find it in Play.");
            }
            else if (ext == ".rhs" || ext == ".json") { ProfileStore.Import(path); Notice(ProfileStore.LastReport + " Choose it in Settings → Profiles."); }
            else
            {
                string kind = ext == ".txt" ? "colorsets" : ext == ".obj" ? "notes" : ext == ".png" || ext == ".jpg" || ext == ".jpeg" ? "cursors" : null;
                if (kind == null) throw new InvalidDataException("Drop SSPM/VUL maps, RHS/JSON profiles, PNG cursors, TXT color sets or OBJ notes. Import other assets from Settings.");
                AssetLibrary.Import(kind, path); Notice("Imported " + Path.GetFileName(path) + ". Select it in Settings → Appearance.");
            }
        }
        catch (Exception e) { Notice("Could not import " + Path.GetFileName(path) + ": " + e.Message); GD.PrintErr(e); }
        finally { busy = false; }
    }
}
