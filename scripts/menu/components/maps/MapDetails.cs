using Godot;
using System;
using System.Threading.Tasks;

using Content.Beatmaps;
using Gameplay;

public class MapDetails : View
{
	private BeatmapSet currentMap;
	private Beatmap currentDifficulty;
	private Task loadingMap;
	private MapList mapList;
	private Control mapDetails;
	private Control details;
	private Control loading;
	private Control modPanel;
	private AudioStreamPlayer musicPreview;
	private Label extraInfo;
	public override void _Ready()
	{
		mapList = GetParent().GetNode<MapList>("MapList");
		mapList.MapSelected += MapSelected;
		modPanel = GetParent().GetNode<Control>("ModSelect");
		modPanel.Visible = false;
		details = GetNode<Control>("Details");
		mapDetails = details.GetNode<Control>("AspectRatioContainer/Map");
		loading = GetNode<Control>("Loading");
		musicPreview = GetNode<AudioStreamPlayer>("MusicPreview");
		extraInfo = new Label();
		extraInfo.Name = "ExtraInfo";
		extraInfo.AnchorRight = 1f;
		extraInfo.MarginLeft = 32f;
		extraInfo.MarginTop = 270f;
		extraInfo.MarginRight = -32f;
		extraInfo.MarginBottom = 390f;
		extraInfo.Autowrap = true;
		details.AddChild(extraInfo);

		details.GetNode<Button>("Play").Connect("pressed", this, nameof(PlayMap));

		SetActive(false);
	}
	public void ShowMods()
	{
		modPanel.Visible = true;
		var tween = modPanel.GetNode<Tween>("Tween");
		tween.InterpolateProperty(modPanel, "modulate:a", 0, 1, 0.3f);
		tween.Start();
	}
	public void PlayMap()
	{
		if (!currentDifficulty.Playable)
			return;
		Game.LoadedMapset = currentMap;
		Game.LoadedMap = currentDifficulty;
		Game.LoadedMapData = currentDifficulty.Data;
		Global.Instance.GotoScene("res://scenes/Game.tscn", (Node scn) =>
		{
			var menuHandler = GetParent().GetParent<MenuHandler>();
			menuHandler.GoTo(2);
		});
	}
	public void MapSelected(Beatmap map)
	{
		currentMap = map.Mapset;
		currentDifficulty = map;
		if (!this.IsActive)
			SetActive(true);
		mapDetails.GetNode<TextureRect>("Cover").Texture = currentMap.LoadCover();
		mapDetails.GetNode<Label>("Title").Text = currentMap.Title;
		mapDetails.GetNode<Label>("Title/Artist").Text = currentMap.Artist;
		mapDetails.GetNode<Label>("Title/Mapper").Text = currentMap.Mappers;
		var difficultyText = map.Name;
		var mapInfo = RhythiansApi.GetMap(currentMap.RhythiansMapId);
		if (mapInfo != null)
		{
			var rating = mapInfo.Rating.HasValue ? mapInfo.Rating.Value.ToString("0.00") + "★" : "UNRATED";
			difficultyText += " · " + rating + " · " + mapInfo.StatusLabel;
			if (mapInfo.Completed)
				difficultyText += " · COMPLETED";
		}
		mapDetails.GetNode<Label>("Difficulty").Text = difficultyText;
		UpdateExtraInfo(mapInfo);
		musicPreview.Stream = currentMap.LoadAudio();
		musicPreview.Play(musicPreview.Stream.GetLength() / 3f);
		loadingMap = Task.Run(loadMap);
	}
	private async void loadMap()
	{
		var map = currentDifficulty;
		Error loaded = map.Load();
		CallDeferred(nameof(RefreshExtraInfo));
		await Task.Delay(TimeSpan.FromSeconds(1));
		if (!map.Playable || loaded != Error.Ok)
			SetActive(false);
	}
	private void RefreshExtraInfo()
	{
		UpdateExtraInfo(RhythiansApi.GetMap(currentMap == null ? null : currentMap.RhythiansMapId));
	}

	private void UpdateExtraInfo(RhythiansMapInfo mapInfo)
	{
		if (extraInfo == null || currentMap == null || currentDifficulty == null)
			return;
		var notes = mapInfo != null && mapInfo.NoteCount > 0
			? mapInfo.NoteCount
			: currentDifficulty.Data != null && currentDifficulty.Data.Notes != null ? currentDifficulty.Data.Notes.Count : 0;
		var length = mapInfo != null && mapInfo.LengthSeconds > 0 ? mapInfo.LengthSeconds + "s" : "local audio";
		var mods = Gameplay.Game.Mods == null ? "None" : Gameplay.Game.Mods.ToString();
		var lines = "Notes: " + notes + "  •  Length: " + length + "  •  Mods: " + mods;
		if (mapInfo != null)
		{
			lines += "\nRhythians: " + mapInfo.StatusLabel;
			if (mapInfo.Rating.HasValue) lines += "  •  Rating " + mapInfo.Rating.Value.ToString("0.00");
			if (mapInfo.Completed) lines += "  •  Completed";
			if (mapInfo.IsRanked)
			{
				lines += "\nRPL " + (mapInfo.LockScore > 0 ? mapInfo.LockScore + " earned" : mapInfo.Rpl + " available");
				lines += "  •  RPS " + (mapInfo.SpinScore > 0 ? mapInfo.SpinScore + " earned" : mapInfo.Rps + " available");
				lines += "  •  RPVR " + (mapInfo.VrScore > 0 ? mapInfo.VrScore + " earned" : mapInfo.Rpvr + " available");
			}
		}
		extraInfo.Text = lines;
	}

	private float circleSpin = 0f;
	public override void _Process(float delta)
	{
		if (currentDifficulty == null || loadingMap == null)
			return;
		if (!loadingMap.IsCompleted)
		{
			circleSpin += delta;
			loading.GetNode<Control>("Circle").RectRotation = Mathf.Wrap(circleSpin * 90f, 0, 360);
			loading.Visible = true;
			details.Visible = false;
			return;
		}
		loading.Visible = false;
		details.Visible = true;
	}
	public override async void OnShow()
	{
		this.Visible = true;
		ViewTween.RemoveAll();
		ViewTween.InterpolateProperty(this, "modulate:a", 0, 1, 0.15f, Tween.TransitionType.Sine, Tween.EaseType.Out);
		ViewTween.Start();
		await ToSignal(ViewTween, "tween_all_completed");
		this.IsActive = true;
	}
	public override async void OnHide()
	{
		this.IsActive = false;
		ViewTween.RemoveAll();
		ViewTween.InterpolateProperty(this, "modulate:a", 1, 0, 0.15f, Tween.TransitionType.Sine, Tween.EaseType.Out);
		ViewTween.Start();
		await ToSignal(ViewTween, "tween_all_completed");
		this.Visible = false;
	}
}
