using System;
using XRL.Rules;
using XRL.World.Effects;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_GlowstoneShardOnHit : IPart
	{
		public int BleedChance = 50;

		public string BleedAmount = "1d2";
		public int BleedSaveTarget = 20;

		public override bool SameAs(IPart p)
		{
			Cleo_TerraFirma_GlowstoneShardOnHit o = p as Cleo_TerraFirma_GlowstoneShardOnHit;
			if (o.BleedChance != BleedChance || o.BleedAmount != BleedAmount || o.BleedSaveTarget != BleedSaveTarget)
			{
				return false;
			}
			return base.SameAs(p);
		}

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("WeaponHit");
			Registrar.Register("ProjectileHit");
			Registrar.Register("ThrownProjectileHit");
			base.Register(Object, Registrar);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == "WeaponHit" || E.ID == "ProjectileHit" || E.ID == "ThrownProjectileHit")
			{
				GameObject target = E.GetGameObjectParameter("Defender");
				if (GameObject.Validate(target) && target.IsCreature)
				{
					if (!target.HasEffect<Luminous>())
					{
						target.ApplyEffect(new Luminous(Stat.Random(30, 60)));
						Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHARD", $"phosphorescent applied to {target.Blueprint} via {E.ID}");
					}
					if (E.ID != "WeaponHit" && E.GetIntParameter("Penetrations") > 0 && BleedChance.in100())
					{
						GameObject attacker = E.GetGameObjectParameter("Attacker");
						if (target.ApplyEffect(new Bleeding(BleedAmount, BleedSaveTarget, attacker, Stack: false)))
						{
							Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHARD", $"bleed applied to {target.Blueprint} (thrown)");
						}
					}
				}
			}
			return base.FireEvent(E);
		}
	}
}
