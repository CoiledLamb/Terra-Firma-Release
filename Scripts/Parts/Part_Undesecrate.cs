using System;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_UndesecratePart : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == PooledEvent<IsRepairableEvent>.ID
				|| ID == PooledEvent<RepairedEvent>.ID;
		}

		public override bool HandleEvent(IsRepairableEvent E)
		{
			if (ParentObject.HasPart<ModDesecrated>())
				return false;
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(RepairedEvent E)
		{
			ParentObject.RemovePart<ModDesecrated>();
			Statistic hp = ParentObject.GetStat("Hitpoints");
			if (hp != null)
				hp.Penalty = 0;
			return base.HandleEvent(E);
		}
	}
}
