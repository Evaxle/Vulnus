using Godot;
using System;

public class Approach : Control
{
	private SpinBox fadeLength;
	private CheckButton halfGhost;

	public override void _Ready()
	{
		BuildExtraSettings();
		UpdateSettings();
		GetNode<Dropdown>("Method").Connect("ValueChanged", this, nameof(OnDropdownChanged));
		GetNode<DecimalInput>("Distance").Connect("ValueChanged", this, nameof(OnValueChanged), new Godot.Collections.Array(0));
		GetNode<DecimalInput>("Time").Connect("ValueChanged", this, nameof(OnValueChanged), new Godot.Collections.Array(1));
		GetNode<DecimalInput>("Speed").Connect("ValueChanged", this, nameof(OnValueChanged), new Godot.Collections.Array(2));
	}

	private void BuildExtraSettings()
	{
		var row = new HBoxContainer();
		row.Name = "FadeLength";
		row.RectMinSize = new Vector2(0, 32);
		AddChild(row);
		var label = new Label();
		label.Text = "Fade Length";
		label.SizeFlagsHorizontal = 3;
		label.Valign = Label.VAlign.Center;
		row.AddChild(label);
		fadeLength = new SpinBox();
		fadeLength.MinValue = 0;
		fadeLength.MaxValue = 100;
		fadeLength.Step = 1;
		fadeLength.Suffix = "%";
		fadeLength.RectMinSize = new Vector2(128, 32);
		fadeLength.Connect("value_changed", this, nameof(OnFadeChanged));
		row.AddChild(fadeLength);

		halfGhost = new CheckButton();
		halfGhost.Name = "HalfGhost";
		halfGhost.Text = "Half Ghost";
		halfGhost.RectMinSize = new Vector2(0, 32);
		halfGhost.Connect("toggled", this, nameof(OnHalfGhostChanged));
		AddChild(halfGhost);
	}

	public void UpdateSettings()
	{
		GetNode<Dropdown>("Method").SetValue(Settings.ApproachMode);
		GetNode<DecimalInput>("Distance").SetValue(Settings.ApproachDistance);
		GetNode<DecimalInput>("Time").SetValue(Settings.ApproachTime);
		GetNode<DecimalInput>("Speed").SetValue(Settings.ApproachRate);
		if (fadeLength != null) fadeLength.Value = Settings.FadeLength;
		if (halfGhost != null) halfGhost.Pressed = Settings.HalfGhost;
		switch (Settings.ApproachMode)
		{
			case 0:
				GetNode<SpinBox>("Distance/SpinBox").Editable = true;
				GetNode<SpinBox>("Time/SpinBox").Editable = true;
				GetNode<SpinBox>("Speed/SpinBox").Editable = false;
				break;
			case 1:
				GetNode<SpinBox>("Distance/SpinBox").Editable = true;
				GetNode<SpinBox>("Time/SpinBox").Editable = false;
				GetNode<SpinBox>("Speed/SpinBox").Editable = true;
				break;
			case 2:
				GetNode<SpinBox>("Distance/SpinBox").Editable = false;
				GetNode<SpinBox>("Time/SpinBox").Editable = true;
				GetNode<SpinBox>("Speed/SpinBox").Editable = true;
				break;
		}
	}

	public void OnValueChanged(float value, int spinbox)
	{
		switch (spinbox)
		{
			case 0: Settings.ApproachDistance = value; break;
			case 1: Settings.ApproachTime = value; break;
			case 2: Settings.ApproachRate = value; break;
		}
		Settings.UpdateSettings();
		UpdateSettings();
	}

	public void OnDropdownChanged(int index)
	{
		Settings.ApproachMode = index;
		Settings.UpdateSettings();
		UpdateSettings();
	}

	public void OnFadeChanged(float value)
	{
		Settings.FadeLength = value;
		Settings.UpdateSettings();
	}

	public void OnHalfGhostChanged(bool enabled)
	{
		Settings.HalfGhost = enabled;
		Settings.UpdateSettings();
	}
}
