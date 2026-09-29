using Godot;
using System;

using Gameplay;

public class Results : View
{
	private Label rhythiansStatus;
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

		if (Game.LoadedMapset != null && !string.IsNullOrWhiteSpace(Game.LoadedMapset.RhythiansMapId))
		{
			rhythiansStatus = new Label();
			rhythiansStatus.Name = "RhythiansStatus";
			rhythiansStatus.AnchorLeft = 0.5f;
			rhythiansStatus.AnchorRight = 0.5f;
			rhythiansStatus.AnchorTop = 0.5f;
			rhythiansStatus.AnchorBottom = 0.5f;
			rhythiansStatus.MarginLeft = -320f;
			rhythiansStatus.MarginTop = 145f;
			rhythiansStatus.MarginRight = 320f;
			rhythiansStatus.MarginBottom = 176f;
			AddChild(rhythiansStatus);
			if (Game.Score.Failed)
				rhythiansStatus.Text = "Rhythians: run failed, score not submitted.";
			else if (!RhythiansApi.IsAuthenticated)
				rhythiansStatus.Text = "Rhythians: log in to submit eligible scores.";
			else
				rhythiansStatus.Text = "Rhythians: submitting score...";
		}

		GetNode<Button>("Retry").Connect("pressed", Global.Instance, nameof(Global.GotoScene), new Godot.Collections.Array("res://scenes/Game.tscn", null));
		var menuHandler = GetParent().GetParent<MenuHandler>();
		GetNode<Button>("Return").Connect("pressed", menuHandler, nameof(MenuHandler.GoTo), new Godot.Collections.Array(1));
	}

	public override void _Process(float delta)
	{
		if (rhythiansStatus == null || Game.Score == null || Game.Score.Failed || !RhythiansApi.IsAuthenticated)
			return;
		var result = RhythiansApi.LastScoreResult;
		if (result == null)
			return;
		if (!result.Success)
		{
			rhythiansStatus.Text = "Rhythians: score submission failed — " + (result.Error ?? "unknown error");
			return;
		}
		var pointsName = result.CameraMode == "spin" ? "RPS" : "RPL";
		var total = result.CameraMode == "spin" ? result.Rps : result.Rpl;
		rhythiansStatus.Text = "Rhythians: submitted • " + result.Points + " score points • " + pointsName + " " + total;
	}
}
