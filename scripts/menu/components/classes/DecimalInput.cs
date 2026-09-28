using Godot;
using System;

public class DecimalInput : Control
{
	public float Value = 0;
	public override void _Ready()
	{
		GetNode<SpinBox>("SpinBox").Connect("value_changed", this, nameof(OnValueChanged));
	}
	public void SetValue(float value)
	{
		Value = value;
		var input = GetNode<SpinBox>("SpinBox");
		input.SetBlockSignals(true);
		input.Value = value;
		input.SetBlockSignals(false);
	}
	public void OnValueChanged(float value)
	{
		Value = value;
		EmitSignal(nameof(ValueChanged), value);
	}
	[Signal]
	public delegate void ValueChanged(float value);
}
