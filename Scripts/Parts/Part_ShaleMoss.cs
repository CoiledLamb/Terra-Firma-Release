using System;
using XRL.Rules;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_ShaleMoss : IPart
	{
		public const string VINE_PAINT = "vineshale1";

		public string StartRipeChance = "1:20";

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == ZoneBuiltEvent.ID;
		}

		public override bool HandleEvent(ZoneBuiltEvent E)
		{
			ParentObject.GetPart<ShaleVines>()?.GrowVines();
			if (ParentObject.Render != null
				&& ParentObject.GetStringProperty("PaintedWall") == VINE_PAINT
				&& !ParentObject.HasPart<Harvestable>()
				&& Stat.Chance(StartRipeChance))
				Grow();
			return base.HandleEvent(E);
		}

		private void Grow()
		{
			var h = ParentObject.RequirePart<Harvestable>();
			h.DestroyOnHarvest = false;
			h.OnSuccess = "Cleo_TerraFirma_MossClump";
			h.OnSuccessAmount = "1";
			h.RipeColor = "&K"; h.RipeTileColor = "&K"; h.RipeDetailColor = "g";
			h.UnripeColor = ParentObject.Render.ColorString ?? "";
			h.UnripeTileColor = ParentObject.Render.TileColor ?? "";
			h.UnripeDetailColor = ParentObject.Render.DetailColor ?? "";
			ParentObject.RequirePart<Cleo_TerraFirma_MossyWall>();
			h.UpdateRipeStatus(newRipeStatus: true);
		}
	}
}
