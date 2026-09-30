using System;
using XRL.World.Effects;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_FlintSpikePauldrons : IPart
	{
		public string Damage = "1d3";

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != TookDamageEvent.ID)
				return ID == GetShortDescriptionEvent.ID;
			return true;
		}

		public int GetSaveTarget()
		{
			return 20 + 2 * ParentObject.GetTier();
		}

		public override bool HandleEvent(TookDamageEvent E)
		{
			GameObject wearer = ParentObject.Equipped;
			GameObject attacker = E.Actor;
			if (wearer != null && E.Object == wearer && ParentObject.IsEquippedProperly()
				&& attacker != null && attacker != wearer && GameObject.Validate(attacker)
				&& !E.Indirect && E.Projectile == null
				&& E.Weapon != null
				&& E.Damage != null && E.Damage.Amount > 0
				&& !E.Damage.HasAttribute("Thrown") && !E.Damage.HasAttribute("reflected")
				&& attacker.CurrentCell != null && wearer.CurrentCell != null
				&& attacker.CurrentCell.IsAdjacentTo(wearer.CurrentCell))
			{
				attacker.ApplyEffect(new Bleeding(Damage, GetSaveTarget(), wearer));
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.AppendRules("Creatures who damage you with melee attacks begin bleeding (Toughness save, difficulty " + GetSaveTarget() + ").");
			return base.HandleEvent(E);
		}
	}
}
