using System;
using Cleo.TerraFirma.Scripts;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_Carvable : IPart
	{
		public const string COMMAND = "Cleo_TerraFirma_CarveWall";

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID;
		}

		private bool IsCarvableStone()
		{
			return Cleo_TerraFirma_StoneshaperWallProperties.IsNaturalStone(ParentObject.GetPart<Cleo_TerraFirma_StoneshaperWallProperties>());
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (E.Actor != null && E.Actor.IsPlayer() && IsCarvableStone()
				&& Cleo_TerraFirma_GaslightGauntlets.FindWorn(E.Actor) != null)
				E.AddAction("Carve", "carve", COMMAND, null, 'v', FireOnActor: false);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == COMMAND && E.Actor != null && E.Actor.IsPlayer() && AttemptCarve(E.Actor))
				E.RequestInterfaceExit();
			return base.HandleEvent(E);
		}

		private bool AttemptCarve(GameObject Actor)
		{
			if (!IsCarvableStone())
				return false;
			if (Actor.DistanceTo(ParentObject) > 1)
				return Actor.Fail("You cannot reach " + ParentObject.t() + ".");
			GameObject worn = Cleo_TerraFirma_GaslightGauntlets.FindWorn(Actor);
			var gauntlets = worn?.GetPart<Cleo_TerraFirma_GaslightGauntlets>();
			if (gauntlets == null)
				return Actor.Fail("You have nothing to carve with.");
			if (!worn.TestCharge(gauntlets.CarveCharge, LiveOnly: false, 0L))
				return Actor.Fail(worn.Does("click", int.MaxValue, null, null, "merely") + ".");

			string subject = Cleo_TerraFirma_CarvingPicker.Pick("");
			if (subject == null)
				return false;

			Cell cell = ParentObject.CurrentCell;
			var props = ParentObject.GetPart<Cleo_TerraFirma_StoneshaperWallProperties>();
			GameObject muse = GameObjectFactory.Factory.CreateSampleObject(subject);
			GameObject statue = muse == null ? null : Mutation.Cleo_TerraFirma_Stoneshaper.MakeStatueOfCreatureFromWall(muse, ParentObject, string.Empty, props);
			if (statue == null || cell == null)
			{
				string subjectName = muse?.an() ?? "that";
				muse?.Obliterate();
				statue?.Obliterate();
				return Actor.Fail("You cannot carve a statue of " + subjectName + ".");
			}
			if (!worn.UseCharge(gauntlets.CarveCharge, LiveOnly: false, 0L))
			{
				muse.Obliterate();
				statue.Obliterate();
				return Actor.Fail(worn.Does("click", int.MaxValue, null, null, "merely") + ".");
			}
			GameObject wall = ParentObject;
			Mutation.Cleo_TerraFirma_Stoneshaper.PlayCarveReveal(cell, wall, statue, () =>
			{
				wall.Obliterate();
				cell.AddObject(statue, true, Silent: true);
			});
			IComponent<GameObject>.XDidYToZ(Actor, "carve", "a statue of", muse, EndMark: "!", ColorAsGoodFor: Actor, IndefiniteObject: true, Source: Actor);
			muse.Obliterate();
			Actor.UseEnergy(1000, "Item Gaslight Gauntlets Carve");
			return true;
		}
	}
}
