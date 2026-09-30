using System;
using XRL.Rules;
using XRL.World.Effects;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_DazeOnThrowPensPart : IPart
	{
		public float DazeChance;

		public Cleo_TerraFirma_DazeOnThrowPensPart() { }
		public Cleo_TerraFirma_DazeOnThrowPensPart(float Chance)
		{
			DazeChance = Chance;
		}

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("ProjectileHit");
			Registrar.Register("ThrownProjectileHit");
			base.Register(Object, Registrar);
		}

		public override bool FireEvent(Event E)
		{
			if ((E.ID == "ProjectileHit" || E.ID == "ThrownProjectileHit"))
			{
				GameObject target = E.GetGameObjectParameter("Defender");
				if (GameObject.Validate(target) && target.IsCreature && DazeChance.in100() && E.GetIntParameter("Penetrations") > 0)
					target.ApplyEffect(new Dazed(Stat.Random(2, 3)));
			}
			return base.FireEvent(E);
		}
	}
}
