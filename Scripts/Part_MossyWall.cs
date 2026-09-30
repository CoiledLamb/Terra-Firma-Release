using System;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_MossyWall : IPart
	{
		public string Adjective = "{{green|mossy}}";

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == PooledEvent<GetDisplayNameEvent>.ID
				|| ID == EndTurnEvent.ID;
		}

		private bool CanReach(GameObject actor)
		{
			return actor?.CurrentCell != null && ParentObject.CurrentCell != null
				&& actor.CurrentCell.ParentZone == ParentObject.CurrentCell.ParentZone
				&& actor.DistanceTo(ParentObject) <= 1;
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			GameObject actor = The.Player;
			if (CanReach(actor) && actor.CurrentCell != ParentObject.CurrentCell)
				ParentObject.GetPart<Harvestable>()?.AttemptHarvest(actor, Automatic: true);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetDisplayNameEvent E)
		{
			if (ParentObject.GetPart<Harvestable>()?.Ripe == true)
				E.AddAdjective(Adjective);
			return base.HandleEvent(E);
		}
	}
}
