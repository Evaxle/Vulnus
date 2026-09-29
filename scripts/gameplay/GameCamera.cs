using Godot;
using System;
using System.IO;

namespace Gameplay
{
	public class GameCamera : Godot.Camera
	{
		public float Yaw = 0f;
		public float Pitch = 0f;
		public Vector2 CursorPosition = new Vector2();
		public Vector2 ClampedCursorPosition = new Vector2();
		public Spatial Cursor = null;
		public Spatial GhostCursor = null;
		private static float clamp = (6f - 0.525f) / 2f;

		public override void _EnterTree()
		{
			Yaw = 0f;
			Pitch = 0f;
			CursorPosition = new Vector2();
			ClampedCursorPosition = new Vector2();
			Input.MouseMode = Input.MouseModeEnum.Captured;
			Fov = Settings.FieldOfView;
			UpdateCameraTransform();
		}

		public override void _ExitTree()
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		public void ApplyVisualSettings()
		{
			Fov = Settings.FieldOfView;
			if (Cursor != null)
			{
				Cursor.Scale = Vector3.One * Settings.CursorScale;
				var mesh = Cursor as MeshInstance;
				if (mesh != null)
				{
					var material = mesh.GetSurfaceMaterial(0) as SpatialMaterial;
					if (material != null)
					{
						material.AlbedoColor = Settings.ParseColor(Settings.CursorColor, Colors.White);
						if (!string.IsNullOrWhiteSpace(Settings.CursorPath) && File.Exists(Settings.CursorPath))
						{
							var image = new Image();
							if (image.Load(Settings.CursorPath) == Error.Ok)
							{
								var texture = new ImageTexture();
								texture.CreateFromImage(image);
								material.AlbedoTexture = texture;
							}
						}
					}
				}
			}
			if (GhostCursor != null)
				GhostCursor.Scale = Vector3.One * Settings.CursorScale;
			UpdateCameraTransform();
		}

		public override void _Input(InputEvent @event)
		{
			if (!(@event is InputEventMouseMotion))
				return;

			var input = (InputEventMouseMotion)@event;

			if (Settings.CameraMode == 0)
			{
				var spinRelative = input.Relative * Settings.MouseSensitivity * 0.2f;
				Yaw = Mathf.Wrap(Yaw - spinRelative.x, -180f, 180f);
				Pitch = Mathf.Clamp(Pitch - spinRelative.y, -90f, 90f);
				Rotation = new Vector3(Mathf.Deg2Rad(Pitch), Mathf.Deg2Rad(Yaw), 0);
				UpdateCameraTransform();
				var position = new Vector2(Translation.x, Translation.y);
				var look = Transform.basis.z;
				if (Mathf.Abs(look.z) > 0.0001f)
					CursorPosition = position + new Vector2(look.x, look.y) * -Mathf.Abs(Translation.z) / look.z;
			}
			else
			{
				Yaw = 0f;
				Pitch = 0f;
				Rotation = Vector3.Zero;
				CursorPosition += new Vector2(input.Relative.x, -input.Relative.y) * (0.036f * Settings.MouseSensitivity);
			}

			ClampedCursorPosition = new Vector2(
				Mathf.Clamp(CursorPosition.x, -clamp, clamp),
				Mathf.Clamp(CursorPosition.y, -clamp, clamp)
			);

			if (Settings.CursorDrift)
				CursorPosition = ClampedCursorPosition;

			UpdateCameraTransform();

			if (Cursor != null)
				Cursor.Translation = new Vector3(ClampedCursorPosition.x, ClampedCursorPosition.y, 0);

			if (GhostCursor != null)
			{
				GhostCursor.Visible = CursorPosition != ClampedCursorPosition;
				if (GhostCursor.Visible)
				{
					GhostCursor.Translation = new Vector3(CursorPosition.x, CursorPosition.y, 0);
					var distance = Mathf.Min(1f, ClampedCursorPosition.DistanceSquaredTo(CursorPosition));
					((MeshInstance)GhostCursor).MaterialOverride.Set("albedo_color", new Color(1f, 1f, 1f, distance));
				}
			}
		}

		private void UpdateCameraTransform()
		{
			var basePosition = new Vector3(0, 0, 7) + Transform.basis.z / 2f;
			if (Settings.CameraMode == 2)
			{
				var factor = Settings.ParallaxAmount / 40f;
				basePosition += new Vector3(ClampedCursorPosition.x, ClampedCursorPosition.y, 0) * factor;
			}
			Translation = basePosition;
		}
	}
}
