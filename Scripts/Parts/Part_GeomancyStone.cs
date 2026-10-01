using System;
using XRL.World.Parts.Mutation;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_GeomancyStonePart : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade))
				return ID == AfterThrownEvent.ID;
			return true;
		}

		public override bool HandleEvent(AfterThrownEvent E)
		{
			if (E.Actor != null && E.Actor.TryGetPart(out Cleo_TerraFirma_Geomancy geomancy))
				geomancy.OnStoneThrown(E.Item);
			return base.HandleEvent(E);
		}
	}
}
