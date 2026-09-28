using Godot;
using System;

public class Options : View
{
	public bool CanOpen = true;
	public override void _Ready()
	{
		base._Ready();
		var topbar = GetNode<Control>("Topbar");
		var closeBtn = topbar.GetNode<Button>("Close");
		closeBtn.Connect("pressed", this, nameof(SetActive), new Godot.Collections.Array(false));
	}
	public override void _PhysicsProcess(float delta)
	{
		if (CanOpen && Input.IsActionJustPressed("options"))
		{
			SetActive(!IsActive);
		}
		if (!IsActive)
			return;
		bool rankable = true;
		if (Settings.ApproachRate > 100f)
			rankable = false;
		if (Settings.ApproachDistance > 100f)
			rankable = false;
		if (Settings.ApproachTime > 2f)
			rankable = false;
		GetNode<Label>("RankStatus").Text = rankable ? "These settings are rankable." : "These settings are not rankable.";
	}
    public override void OnShow()
    {
        if (!CanOpen) return;
        IsActive = true;
        Visible = true;
        ViewTween.RemoveAll();
        ViewTween.InterpolateProperty(this, "modulate:a", Modulate.a, 1, 0.15f);
        ViewTween.Start();
    }
    public override void OnHide()
    {
        IsActive = false;
        ViewTween.RemoveAll();
        Visible = false;
    }
}
