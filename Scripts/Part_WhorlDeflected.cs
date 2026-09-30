using System;
using Cleo.TerraFirma.Scripts;
using UnityEngine;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_WhorlDeflectedPart : IPart
	{
		[NonSerialized]
		public Cell Intended;

		[NonSerialized]
		public Cell Bounce;

		[NonSerialized]
		public bool Ricochet;

		public override bool WantEvent(int ID, int cascade)
		{
			return ID == AfterThrownEvent.ID
				|| ID == PooledEvent<ConfigureMissileVisualEffectEvent>.ID
				|| base.WantEvent(ID, cascade);
		}

		public override bool HandleEvent(ConfigureMissileVisualEffectEvent E)
		{
			if (E.Projectile == ParentObject && E.Path != null && Bounce != null)
			{
				E.Path.SetParameter("ParticleRate", "0");
				if (E.Path.first != null && GameManager.Instance != null)
				{
					float speed = 350f;
					if (E.Path.TryGetValue("Speed", out string sp))
						float.TryParse(sp, out speed);
					if (speed <= 0f)
						speed = 350f;
					Vector3 a = GameManager.Instance.getTileCenter(E.Path.first.X, E.Path.first.Y);
					Vector3 b = GameManager.Instance.getTileCenter(Bounce.X, Bounce.Y);
					float leg1Sec = Vector3.Distance(a, b) / speed;
					E.Path.last = Bounce.Location;
					if (Ricochet && Intended != null && Intended != Bounce)
						Cleo_TerraFirma_GravelWhorlFX.PlayRicochetFlight(ParentObject, Bounce, Intended, leg1Sec);
				}
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(AfterThrownEvent E)
		{
			Restore();
			return base.HandleEvent(E);
		}

		public void Restore()
		{
			GameObject o = ParentObject;
			if (o == null)
				return;
			if (Intended != null && GameObject.Validate(o) && o.CurrentCell != null && o.CurrentCell != Intended)
			{
				Helpers.VerifyLog("GVW", $"deflect mark: engine re-placed {o.Blueprint} at ({o.CurrentCell.X},{o.CurrentCell.Y}), restoring whorl placement ({Intended.X},{Intended.Y})");
				o.DirectMoveTo(Intended, 0, Forced: true, IgnoreCombat: true);
			}
			Intended = null;
			o.RemovePart(this);
		}
	}
}
