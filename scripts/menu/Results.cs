using Godot;
using System;

using Gameplay;

public class Results : View
{
	private Label rhythiansStatus;
	private RhythiansScoreResult shownResult;

	public override void _Ready()
	{
		if (Game.Score == null)
			return;
		var info = GetNode<Control>("Info");
		var mapInfo = GetNode<Control>("Map");
		mapInfo.GetNode<TextureRect>("Cover").Texture = Game.LoadedMapset.LoadCover();
		mapInfo.GetNode<Label>("Title").Text = Game.LoadedMapset.Title;
		mapInfo.GetNode<Label>("Title/Artist").Text = Game.LoadedMapset.Artist;
		mapInfo.GetNode<Label>("Title/Mapper").Text = Game.LoadedMapset.Mappers;
		mapInfo.GetNode<Label>("Difficulty").Text = Game.LoadedMap.Name;
		info.GetNode<Label>("Score").Text = String.Format("{0:n0}", Game.Score.Points);
		info.GetNode<Label>("Combo").Text = String.Format("{0:n0}", Game.Score.HighestCombo);
		info.GetNode<Label>("Hits").Text = String.Format("{0:n0}", Game.Score.Total - Game.Score.Misses);
		info.GetNode<Label>("Misses").Text = String.Format("{0:n0}", Game.Score.Misses);
		double accuracy = Game.Score.Total > 0 ? (double)(Game.Score.Total - Game.Score.Misses) / (double)Game.Score.Total : 0;
		info.GetNode<Label>("Accuracy").Text = accuracy > 0 ? String.Format("{0:.##}%", accuracy * 100) : "0%";
		info.GetNode<Label>("Rank").Text = Score.GetRankForAccuracy(accuracy);

		rhythiansStatus = new Label();
		rhythiansStatus.AnchorLeft = 0.5f;
		rhythiansStatus.AnchorTop = 0.5f;
		rhythiansStatus.AnchorRight = 0.5f;
		rhythiansStatus.AnchorBottom = 0.5f;
		rhythiansStatus.MarginLeft = -288f;
		rhythiansStatus.MarginTop = 145f;
		rhythiansStatus.MarginRight = 288f;
		rhythiansStatus.MarginBottom = 175f;
		rhythiansStatus.Align = Label.AlignEnum.Center;
		rhythiansStatus.Valign = Label.VAlign.Center;
		AddChild(rhythiansStatus);
		UpdateRhythiansStatus();

		GetNode<Button>("Retry").Connect("pressed", Global.Instance, nameof(Global.GotoScene), new Godot.Collections.Array("res://scenes/Game.tscn", null));
		var menuHandler = GetParent().GetParent<MenuHandler>();
		GetNode<Button>("Return").Connect("pressed", menuHandler, nameof(MenuHandler.GoTo), new Godot.Collections.Array(1));
	}

	public override void _Process(float delta)
	{
		var current = RhythiansApi.LastScoreResult;
		if (current != shownResult)
			UpdateRhythiansStatus();
	}

	private void UpdateRhythiansStatus()
	{
		if (rhythiansStatus == null || Game.Score == null) return;
		shownResult = RhythiansApi.LastScoreResult;
		if (Game.Score.Failed)
		{
			rhythiansStatus.Text = "Failed run — not submitted to Rhythians.";
			return;
		}
		if (Game.LoadedMapset == null || string.IsNullOrWhiteSpace(Game.LoadedMapset.RhythiansMapId))
		{
			rhythiansStatus.Text = "Local map — no Rhythians score submission.";
			return;
		}
		if (!RhythiansApi.IsAuthenticated)
		{
			rhythiansStatus.Text = "Log in to Rhythians to submit eligible scores.";
			return;
		}
		if (!RhythiansApi.HasLinkedRhythia)
		{
			rhythiansStatus.Text = "Link your Rhythia profile on rhythians.com to submit scores.";
			return;
		}
		if (shownResult == null)
		{
			rhythiansStatus.Text = "Submitting score to Rhythians...";
			return;
		}
		if (!shownResult.Success)
		{
			rhythiansStatus.Text = "Rhythians submission failed: " + (shownResult.Error ?? "unknown error");
			return;
		}
		var system = shownResult.CameraMode == "spin" ? "RPS" : "RPL";
		rhythiansStatus.Text = shownResult.Ranked
			? "Rhythians accepted · +" + shownResult.Gained + " " + system + " · " + shownResult.Points + " stored"
			: "Rhythians accepted this pass · no rank points awarded.";
	}
}
