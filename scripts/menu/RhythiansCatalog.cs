using Godot;
using System;
using System.Threading.Tasks;

public class RhythiansCatalog : View
{
	private const int PageSize = 40;
	private MenuHandler menu;
	private LineEdit search;
	private VBoxContainer rows;
	private Label status;
	private Button previous;
	private Button next;
	private Task<RhythiansCatalogPage> loadTask;
	private Task<bool> downloadTask;
	private RhythiansCatalogPage page;
	private int offset;
	private string downloadingMapId;

	public override void _Ready()
	{
		menu = GetParent<Control>().GetParent<MenuHandler>();
		BuildUi();
	}

	private void BuildUi()
	{
		var root = new VBoxContainer();
		root.Name = "Layout";
		root.AnchorRight = 1f;
		root.AnchorBottom = 1f;
		root.MarginLeft = 48f;
		root.MarginTop = 32f;
		root.MarginRight = -48f;
		root.MarginBottom = -32f;
		root.AddConstantOverride("separation", 12);
		AddChild(root);

		var title = new Label();
		title.Text = "Rhythians Map Catalog";
		title.RectMinSize = new Vector2(0, 42);
		root.AddChild(title);

		var tools = new HBoxContainer();
		tools.RectMinSize = new Vector2(0, 40);
		tools.AddConstantOverride("separation", 8);
		root.AddChild(tools);

		search = new LineEdit();
		search.PlaceholderText = "Search title, artist, or mapper";
		search.SizeFlagsHorizontal = 3;
		search.Connect("text_entered", this, nameof(OnSearchSubmitted));
		tools.AddChild(search);

		var searchButton = new Button();
		searchButton.Text = "SEARCH";
		searchButton.RectMinSize = new Vector2(110, 0);
		searchButton.Connect("pressed", this, nameof(OnSearchPressed));
		tools.AddChild(searchButton);

		var refreshButton = new Button();
		refreshButton.Text = "REFRESH";
		refreshButton.RectMinSize = new Vector2(110, 0);
		refreshButton.Connect("pressed", this, nameof(OnRefreshPressed));
		tools.AddChild(refreshButton);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = 3;
		scroll.SizeFlagsHorizontal = 3;
		root.AddChild(scroll);

		rows = new VBoxContainer();
		rows.SizeFlagsHorizontal = 3;
		rows.AddConstantOverride("separation", 6);
		scroll.AddChild(rows);

		var footer = new HBoxContainer();
		footer.RectMinSize = new Vector2(0, 48);
		footer.AddConstantOverride("separation", 8);
		root.AddChild(footer);

		var back = new Button();
		back.Text = "BACK";
		back.RectMinSize = new Vector2(120, 0);
		back.Connect("pressed", this, nameof(OnBackPressed));
		footer.AddChild(back);

		previous = new Button();
		previous.Text = "PREVIOUS";
		previous.RectMinSize = new Vector2(120, 0);
		previous.Connect("pressed", this, nameof(OnPreviousPressed));
		footer.AddChild(previous);

		next = new Button();
		next.Text = "NEXT";
		next.RectMinSize = new Vector2(120, 0);
		next.Connect("pressed", this, nameof(OnNextPressed));
		footer.AddChild(next);

		status = new Label();
		status.SizeFlagsHorizontal = 3;
		footer.AddChild(status);
	}

	public override void OnShow()
	{
		base.OnShow();
		if (!RhythiansApi.IsAuthenticated)
		{
			ClearRows();
			status.Text = "Log in with Rhythians Account first.";
			previous.Disabled = true;
			next.Disabled = true;
			return;
		}
		StartLoad(true);
	}

	public override void _Process(float delta)
	{
		if (loadTask != null && loadTask.IsCompleted)
		{
			var task = loadTask;
			loadTask = null;
			if (task.IsFaulted || task.Result == null)
			{
				status.Text = RhythiansApi.LastError ?? "Could not load the Rhythians catalog.";
			}
			else
			{
				page = task.Result;
				offset = page.Offset;
				RenderPage();
			}
		}

		if (downloadTask != null && downloadTask.IsCompleted)
		{
			var task = downloadTask;
			downloadTask = null;
			var id = downloadingMapId;
			downloadingMapId = null;
			if (!task.IsFaulted && task.Result)
			{
				Content.Beatmaps.BeatmapLoader.LoadMapsFromDirectory(Global.MapPath, true);
				GetTree().CallGroup("map_lists", nameof(MapList.ReloadMaps));
				status.Text = "Map installed. It is now available in Singleplayer.";
				RenderPage();
			}
			else
			{
				status.Text = RhythiansApi.LastError ?? "Map download failed.";
				RenderPage();
			}
		}
	}

	private void StartLoad(bool reset)
	{
		if (!RhythiansApi.IsAuthenticated || loadTask != null || downloadTask != null)
			return;
		if (reset)
			offset = 0;
		status.Text = "Loading Rhythians catalog...";
		previous.Disabled = true;
		next.Disabled = true;
		var query = search.Text;
		var requestedOffset = offset;
		loadTask = Task.Run(() => RhythiansApi.FetchCatalogPage(query, requestedOffset, PageSize));
	}

	private void RenderPage()
	{
		ClearRows();
		if (page == null)
			return;

		foreach (var map in page.Maps)
		{
			var row = new HBoxContainer();
			row.RectMinSize = new Vector2(0, 70);
			row.AddConstantOverride("separation", 10);
			rows.AddChild(row);

			var info = new VBoxContainer();
			info.SizeFlagsHorizontal = 3;
			row.AddChild(info);

			var title = new Label();
			title.Text = map.Title + " — " + map.Artist;
			title.ClipText = true;
			info.AddChild(title);

			var meta = new Label();
			var rating = map.Rating.HasValue ? map.Rating.Value.ToString("0.00") + "★" : "Unrated";
			meta.Text = map.Mapper + "  •  " + rating + "  •  " + map.StatusLabel + "  •  " + map.NoteCount + " notes" + (map.Completed ? "  •  COMPLETED" : "");
			meta.ClipText = true;
			info.AddChild(meta);

			var rewards = new Label();
			rewards.Text = map.IsRanked
				? "RPL " + map.Rpl + "  •  RPS " + map.Rps
				: map.IsLegacy ? "Legacy map" : "Unranked map";
			info.AddChild(rewards);

			var button = new Button();
			button.RectMinSize = new Vector2(150, 50);
			var installed = map.Installed;
			var downloading = string.Equals(downloadingMapId, map.Id, StringComparison.OrdinalIgnoreCase);
			button.Text = installed ? "INSTALLED" : downloading ? "DOWNLOADING..." : "DOWNLOAD";
			button.Disabled = installed || downloading || downloadTask != null;
			button.Connect("pressed", this, nameof(OnDownloadPressed), new Godot.Collections.Array(map.Id));
			row.AddChild(button);
		}

		previous.Disabled = page.Offset <= 0 || loadTask != null || downloadTask != null;
		next.Disabled = !page.HasMore || loadTask != null || downloadTask != null;
		var first = page.Total == 0 ? 0 : page.Offset + 1;
		var last = Math.Min(page.Total, page.Offset + page.Maps.Count);
		status.Text = page.Total == 0 ? "No maps found." : "Showing " + first + "–" + last + " of " + page.Total + " maps";
	}

	private void ClearRows()
	{
		if (rows == null)
			return;
		foreach (Node child in rows.GetChildren())
		{
			rows.RemoveChild(child);
			child.QueueFree();
		}
	}

	public void OnSearchSubmitted(string value)
	{
		StartLoad(true);
	}

	public void OnSearchPressed()
	{
		StartLoad(true);
	}

	public void OnRefreshPressed()
	{
		StartLoad(false);
	}

	public void OnPreviousPressed()
	{
		if (page == null)
			return;
		offset = Math.Max(0, page.Offset - PageSize);
		StartLoad(false);
	}

	public void OnNextPressed()
	{
		if (page == null || !page.HasMore)
			return;
		offset = page.NextOffset;
		StartLoad(false);
	}

	public void OnDownloadPressed(string mapId)
	{
		if (downloadTask != null || string.IsNullOrWhiteSpace(mapId))
			return;
		downloadingMapId = mapId;
		status.Text = "Downloading and converting map...";
		RenderPage();
		downloadTask = Task.Run(() => RhythiansApi.DownloadMap(mapId));
	}

	public void OnBackPressed()
	{
		menu.GoTo(0);
	}
}
