using Godot;
using System;

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
			Fov = Settings.CameraFov;
			UpdateCameraTransform();
		}

		public override void _ExitTree()
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		public override void _Process(float delta)
		{
			Fov = Settings.CameraFov;
			UpdateCameraTransform();
		}

		public override void _Input(InputEvent @event)
		{
			var game = GetParent<Game>();
			if (game != null && game.Paused)
				return;
			if (!(@event is InputEventMouseMotion))
				return;

			var input = (InputEventMouseMotion)@event;
			if (Settings.CameraMode == 0)
			{
				Yaw = Mathf.Wrap(Yaw - input.Relative.x * Settings.MouseSensitivity * 0.2f, -180f, 180f);
				Pitch = Mathf.Clamp(Pitch - input.Relative.y * Settings.MouseSensitivity * 0.2f, -89f, 89f);
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
				CursorPosition += new Vector2(input.Relative.x, -input.Relative.y) * (0.018f * Settings.MouseSensitivity);
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
					var material = ((MeshInstance)GhostCursor).MaterialOverride as SpatialMaterial;
					if (material != null)
						material.AlbedoColor = new Color(Settings.CursorColor) { a = distance * Settings.CursorOpacity };
				}
			}
		}

		private void UpdateCameraTransform()
		{
			var basePosition = new Vector3(0, 0, 7) + Transform.basis.z / 2f;
			if (Settings.CameraMode == 2)
				basePosition += new Vector3(ClampedCursorPosition.x * Settings.Parallax, ClampedCursorPosition.y * Settings.Parallax, 0);
			Translation = basePosition;
		}
	}
}