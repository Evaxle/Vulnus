using Godot;
using System;

public class CameraSettings : Control
{
	private HBoxContainer parallaxRow;
	private SpinBox parallax;
	private SpinBox fov;

	public override void _Ready()
	{
		BuildExtendedSettings();
		UpdateSettings();
		GetNode<Dropdown>("Mode").Connect("ValueChanged", this, nameof(OnDropdownChanged));
		GetNode<DecimalInput>("Sensitivity").Connect("ValueChanged", this, nameof(OnSensitivityChanged));
		GetNode<CheckButton>("Drift").Connect("pressed", this, nameof(OnButtonPressed));
	}

	private void BuildExtendedSettings()
	{
		parallaxRow = new HBoxContainer();
		parallaxRow.Name = "ParallaxAmount";
		parallaxRow.RectMinSize = new Vector2(0, 32);
		AddChild(parallaxRow);

		var parallaxLabel = new Label();
		parallaxLabel.Text = "Parallax Amount";
		parallaxLabel.SizeFlagsHorizontal = 3;
		parallaxRow.AddChild(parallaxLabel);

		parallax = new SpinBox();
		parallax.MinValue = 0;
		parallax.MaxValue = 1;
		parallax.Step = 0.01;
		parallax.RectMinSize = new Vector2(128, 0);
		parallax.Connect("value_changed", this, nameof(OnParallaxChanged));
		parallaxRow.AddChild(parallax);

		var fovRow = new HBoxContainer();
		fovRow.Name = "Fov";
		fovRow.RectMinSize = new Vector2(0, 32);
		AddChild(fovRow);

		var fovLabel = new Label();
		fovLabel.Text = "Field of View";
		fovLabel.SizeFlagsHorizontal = 3;
		fovRow.AddChild(fovLabel);

		fov = new SpinBox();
		fov.MinValue = 40;
		fov.MaxValue = 120;
		fov.Step = 1;
		fov.Suffix = "°";
		fov.RectMinSize = new Vector2(128, 0);
		fov.Connect("value_changed", this, nameof(OnFovChanged));
		fovRow.AddChild(fov);
	}

	public void UpdateSettings()
	{
		GetNode<Dropdown>("Mode").SetValue(Settings.CameraMode);
		GetNode<DecimalInput>("Sensitivity").SetValue(Settings.MouseSensitivity);
		GetNode<CheckButton>("Drift").Pressed = Settings.CursorDrift;
		if (parallax != null) parallax.Value = Settings.Parallax;
		if (fov != null) fov.Value = Settings.CameraFov;
		if (parallaxRow != null) parallaxRow.Visible = Settings.CameraMode == 2;
	}

	public void OnButtonPressed()
	{
		Settings.CursorDrift = GetNode<CheckButton>("Drift").Pressed;
		Settings.UpdateSettings();
	}

	public void OnSensitivityChanged(float value)
	{
		Settings.MouseSensitivity = value;
		Settings.UpdateSettings();
	}

	public void OnParallaxChanged(float value)
	{
		Settings.Parallax = value;
		Settings.UpdateSettings();
	}

	public void OnFovChanged(float value)
	{
		Settings.CameraFov = value;
		Settings.UpdateSettings();
	}

	public void OnDropdownChanged(int index)
	{
		Settings.CameraMode = index;
		Settings.UpdateSettings();
		UpdateSettings();
	}
}
