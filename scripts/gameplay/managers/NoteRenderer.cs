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
			var fadeFraction = Mathf.Clamp(Settings.FadeLength / 100f, 0f, 1f);
			for (int i = 0; i < Notes.Length; i++)
			{
				var note = Notes[i];
				var noteTime = note.CalculateTime(NoteManager.SyncManager.NoteTime, NoteManager.ApproachTime);
				var noteDistance = noteTime * Settings.ApproachDistance;
				var scale = Settings.NoteScale;
				var basis = Basis.Identity.Scaled(new Vector3(scale, scale, scale));
				Multimesh.SetInstanceTransform(i, new Transform(basis, new Vector3(note.X, note.Y, (float)-noteDistance)));

				var progress = Mathf.Clamp(1f - (float)noteTime, 0f, 1f);
				var fadeOpacity = fadeFraction <= 0.0001f ? 1f : Mathf.Clamp(progress / fadeFraction, 0f, 1f);
				var ghostOpacity = 1f;
				if (Settings.HalfGhost)
				{
					if (noteTime > 0.5)
						ghostOpacity = 0.35f;
					else
						ghostOpacity = Mathf.Lerp(1f, 0.35f, Mathf.Clamp(((float)noteTime - 0.25f) / 0.25f, 0f, 1f));
				}
				var opacity = Mathf.Clamp(fadeOpacity * ghostOpacity * Settings.NoteOpacity, 0f, 1f);
				Multimesh.SetInstanceColor(i, new Color(note.Color, opacity));
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
