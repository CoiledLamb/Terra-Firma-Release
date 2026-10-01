using System;
using XRL.Liquids;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_VesselTint : IPart
	{
		public string EmptyColor = "B";

		public override bool SameAs(IPart p) => false;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == SingletonEvent<EndTurnEvent>.ID
				|| ID == ObjectCreatedEvent.ID;
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			Retint();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ObjectCreatedEvent E)
		{
			Retint();
			return base.HandleEvent(E);
		}

		private static string OverrideFor(string id)
		{
			switch (id)
			{
				case "cider": return "r";
				default: return null;
			}
		}

		public void Retint()
		{
			Render render = ParentObject?.Render;
			if (render == null)
				return;
			string color = EmptyColor;
			BaseLiquid primary = ParentObject.LiquidVolume?.GetPrimaryLiquid();
			if (primary != null)
				color = OverrideFor(primary.ID) ?? primary.GetColor();
			if (!color.IsNullOrEmpty() && render.DetailColor != color)
				render.DetailColor = color;
		}
	}
}
