using Godot;
using System;

public class CameraSettings : Control
{
	private SpinBox parallax;
	private SpinBox fov;

	public override void _Ready()
	{
		BuildExtraSettings();
		GetNode<OptionButton>("Mode/OptionButton").SetItemText(1, "Full Lock");
		UpdateSettings();
		GetNode<Dropdown>("Mode").Connect("ValueChanged", this, nameof(OnDropdownChanged));
		GetNode<DecimalInput>("Sensitivity").Connect("ValueChanged", this, nameof(OnValueChanged));
		GetNode<CheckButton>("Drift").Connect("pressed", this, nameof(OnButtonPressed));
	}

	private void BuildExtraSettings()
	{
		var parallaxRow = new HBoxContainer();
		parallaxRow.Name = "ParallaxAmount";
		parallaxRow.RectMinSize = new Vector2(0, 32);
		AddChild(parallaxRow);
		var label = new Label();
		label.Text = "Parallax Amount";
		label.SizeFlagsHorizontal = 3;
		label.Valign = Label.VAlign.Center;
		parallaxRow.AddChild(label);
		parallax = new SpinBox();
		parallax.MinValue = 0;
		parallax.MaxValue = 20;
		parallax.Step = 0.01;
		parallax.RectMinSize = new Vector2(128, 32);
		parallax.Connect("value_changed", this, nameof(OnParallaxChanged));
		parallaxRow.AddChild(parallax);

		var fovRow = new HBoxContainer();
		fovRow.Name = "FOV";
		fovRow.RectMinSize = new Vector2(0, 32);
		AddChild(fovRow);
		var fovLabel = new Label();
		fovLabel.Text = "Field of View";
		fovLabel.SizeFlagsHorizontal = 3;
		fovLabel.Valign = Label.VAlign.Center;
		fovRow.AddChild(fovLabel);
		fov = new SpinBox();
		fov.MinValue = 30;
		fov.MaxValue = 120;
		fov.Step = 1;
		fov.Suffix = "°";
		fov.RectMinSize = new Vector2(128, 32);
		fov.Connect("value_changed", this, nameof(OnFovChanged));
		fovRow.AddChild(fov);
	}

	public void UpdateSettings()
	{
		GetNode<Dropdown>("Mode").SetValue(Settings.CameraMode);
		GetNode<DecimalInput>("Sensitivity").SetValue(Settings.MouseSensitivity);
		GetNode<CheckButton>("Drift").Pressed = Settings.CursorDrift;
		if (parallax != null)
		{
			parallax.Value = Settings.ParallaxAmount;
			parallax.Editable = Settings.CameraMode == 2;
		}
		if (fov != null) fov.Value = Settings.FieldOfView;
	}

	public void OnButtonPressed()
	{
		Settings.CursorDrift = GetNode<CheckButton>("Drift").Pressed;
		Settings.UpdateSettings();
	}

	public void OnValueChanged(float value)
	{
		Settings.MouseSensitivity = value;
		Settings.UpdateSettings();
	}

	public void OnDropdownChanged(int index)
	{
		Settings.CameraMode = index;
		Settings.UpdateSettings();
		UpdateSettings();
	}

	public void OnParallaxChanged(float value)
	{
		Settings.ParallaxAmount = value;
		Settings.UpdateSettings();
	}

	public void OnFovChanged(float value)
	{
		Settings.FieldOfView = value;
		Settings.UpdateSettings();
	}
}
