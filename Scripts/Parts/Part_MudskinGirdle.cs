using System;
using XRL.World.Effects;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_MudskinGirdle : IPart
	{
		public int Cap = 2;

		public int FlakeInterval = 20;

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != TookDamageEvent.ID && ID != UnequippedEvent.ID)
				return ID == GetShortDescriptionEvent.ID;
			return true;
		}

		public override bool HandleEvent(TookDamageEvent E)
		{
			GameObject wearer = ParentObject.Equipped;
			if (wearer != null && E.Object == wearer && ParentObject.IsEquippedProperly()
				&& E.Damage != null && E.Damage.Amount > 0 && E.Damage.HasAttribute("Bleeding"))
			{
				if (!wearer.TryGetEffect<Cleo_TerraFirma_Scabbed>(out var scab))
				{
					scab = new Cleo_TerraFirma_Scabbed { Cap = Cap, FlakeInterval = FlakeInterval };
					if (!wearer.ApplyEffect(scab))
						return base.HandleEvent(E);
				}
				scab.Harden();
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(UnequippedEvent E)
		{
			E.Actor?.RemoveEffect<Cleo_TerraFirma_Scabbed>();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.AppendRules("+1 AV each turn you take bleeding damage, up to +" + Cap + ". Loses 1 AV for every " + FlakeInterval + " turns you go without bleeding.");
			return base.HandleEvent(E);
		}
	}
}
