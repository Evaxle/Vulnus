using Godot;
using System;

public class Approach : Control
{
	private SpinBox fadeLength;
	private CheckButton halfGhost;
	private Label windowLabel;

	public override void _Ready()
	{
		BuildExtendedSettings();
		UpdateSettings();
		GetNode<Dropdown>("Method").Connect("ValueChanged", this, nameof(OnDropdownChanged));
		GetNode<DecimalInput>("Distance").Connect("ValueChanged", this, nameof(OnValueChanged), new Godot.Collections.Array(0));
		GetNode<DecimalInput>("Time").Connect("ValueChanged", this, nameof(OnValueChanged), new Godot.Collections.Array(1));
		GetNode<DecimalInput>("Speed").Connect("ValueChanged", this, nameof(OnValueChanged), new Godot.Collections.Array(2));
	}

	private void BuildExtendedSettings()
	{
		var fadeRow = new HBoxContainer();
		fadeRow.Name = "FadeLength";
		fadeRow.RectMinSize = new Vector2(0, 32);
		AddChild(fadeRow);

		var fadeLabel = new Label();
		fadeLabel.Text = "Spawn Fade Length";
		fadeLabel.SizeFlagsHorizontal = 3;
		fadeRow.AddChild(fadeLabel);

		fadeLength = new SpinBox();
		fadeLength.MinValue = 0;
		fadeLength.MaxValue = 5;
		fadeLength.Step = 0.01;
		fadeLength.AllowGreater = true;
		fadeLength.Suffix = "s";
		fadeLength.RectMinSize = new Vector2(128, 0);
		fadeLength.Connect("value_changed", this, nameof(OnFadeChanged));
		fadeRow.AddChild(fadeLength);

		halfGhost = new CheckButton();
		halfGhost.Name = "HalfGhost";
		halfGhost.Text = "Half Ghost";
		halfGhost.RectMinSize = new Vector2(0, 32);
		halfGhost.Connect("toggled", this, nameof(OnHalfGhostChanged));
		AddChild(halfGhost);

		windowLabel = new Label();
		windowLabel.Name = "Window";
		windowLabel.RectMinSize = new Vector2(0, 34);
		AddChild(windowLabel);
	}

	public void UpdateSettings()
	{
		GetNode<Dropdown>("Method").SetValue(Settings.ApproachMode);
		GetNode<DecimalInput>("Distance").SetValue(Settings.ApproachDistance);
		GetNode<DecimalInput>("Time").SetValue(Settings.ApproachTime);
		GetNode<DecimalInput>("Speed").SetValue(Settings.ApproachRate);
		if (fadeLength != null) fadeLength.Value = Settings.FadeLength;
		if (halfGhost != null) halfGhost.Pressed = Settings.HalfGhost;
		if (windowLabel != null)
		{
			var window = Settings.UniversalWindowSeconds();
			var full = Math.Max(0, window - Settings.FadeLength);
			windowLabel.Text = "Window " + Math.Round(window * 1000) + " ms  •  fully visible " + Math.Round(full * 1000) + " ms";
		}
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

	public void OnFadeChanged(float value)
	{
		Settings.FadeLength = value;
		Settings.UpdateSettings();
		UpdateSettings();
	}

	public void OnHalfGhostChanged(bool value)
	{
		Settings.HalfGhost = value;
		Settings.UpdateSettings();
	}

	public void OnDropdownChanged(int index)
	{
		Settings.ApproachMode = index;
		Settings.UpdateSettings();
		UpdateSettings();
	}
}
