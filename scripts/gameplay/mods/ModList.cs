using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Gameplay.Mods
{
	public class ModList : List<Mod>
	{
		public new void Add(Mod mod)
		{
			RemoveAll(other => other.GetType() == mod.GetType() || !mod.CompatibleWith(other));
			base.Add(mod);
		}
		public new string ToString()
		{
			return Count == 0 ? "None" : string.Join(", ", this);
		}
	}
}