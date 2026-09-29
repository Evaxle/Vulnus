using Godot;
using System;

namespace Gameplay
{
	public class NoteRenderer : MultiMeshInstance
	{
		public NoteManager NoteManager;
		public Note[] Notes = new Note[0];

		public override void _Ready()
		{
			NoteManager = GetParent<NoteManager>();
			Multimesh.InstanceCount = 0;
			Multimesh.VisibleInstanceCount = 0;
			Multimesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3d;
			Multimesh.ColorFormat = MultiMesh.ColorFormatEnum.Color8bit;
			Multimesh.CustomDataFormat = MultiMesh.CustomDataFormatEnum.None;
		}

		public override void _Process(float delta)
		{
			if (NoteManager.Game.Paused)
				return;
			for (int i = 0; i < Notes.Length; i++)
			{
				var note = Notes[i];
				var normalized = note.CalculateTime(NoteManager.SyncManager.NoteTime, NoteManager.ApproachTime);
				var noteDistance = normalized * Settings.ApproachDistance;
				var basis = Basis.Identity.Scaled(Vector3.One * Settings.NoteSize);
				Multimesh.SetInstanceTransform(i, new Transform(basis, new Vector3(note.X, note.Y, (float)-noteDistance)));

				var aheadSeconds = Math.Max(0.0, normalized * NoteManager.ApproachTime);
				var elapsedSinceSpawn = Math.Max(0.0, NoteManager.ApproachTime - aheadSeconds);
				var fadeSeconds = Settings.FadeLength * NoteManager.SyncManager.Speed;
				var fadeIn = fadeSeconds <= 0.0001
					? 1f
					: Mathf.Pow(Mathf.Clamp((float)(elapsedSinceSpawn / fadeSeconds), 0f, 1f), 1.3f);
				var fadeOut = 1f;
				if (Settings.HalfGhost)
				{
					var far = 0.24 * NoteManager.SyncManager.Speed;
					var near = 0.06 * NoteManager.SyncManager.Speed;
					var t = far <= near ? 1f : Mathf.Clamp((float)((aheadSeconds - near) / (far - near)), 0f, 1f);
					fadeOut = 0.2f + 0.8f * Mathf.Pow(t, 1.3f);
				}
				var alpha = Mathf.Min(fadeIn, fadeOut) * Settings.NoteOpacity;
				var color = note.Color;
				color.a *= alpha;
				Multimesh.SetInstanceColor(i, color);
			}
		}

		public void ManualUpdate()
		{
			if (Notes.Length > Multimesh.InstanceCount)
				Multimesh.InstanceCount = Notes.Length;
			Multimesh.VisibleInstanceCount = Notes.Length;
		}

		public void SetNotes(Note[] notes)
		{
			Notes = notes;
			ManualUpdate();
		}
	}
}