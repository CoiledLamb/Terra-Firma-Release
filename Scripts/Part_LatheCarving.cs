using System;
using System.Collections.Generic;
using Cleo.TerraFirma.Scripts;
using XRL.UI;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_LatheCarving : IPart
	{
		public const string COMMAND = "Cleo_TerraFirma_LatheCarve";
		public const string INPUT_TAG = "Cleo_TerraFirma_LatheFigurine";

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID
				|| ID == CanSmartUseEvent.ID
				|| ID == CommandSmartUseEvent.ID;
		}

		private static List<GameObject> GetInputs(GameObject Actor)
		{
			return Actor.GetInventory(item => item.HasTag(INPUT_TAG));
		}

		private bool CanCarve(GameObject actor)
		{
			return actor != null && actor.IsPlayer() && actor != ParentObject && ParentObject.Understood()
				&& actor.PhaseMatches(ParentObject) && actor.FlightCanReach(ParentObject)
				&& GetInputs(actor).Count > 0;
		}

		public override bool HandleEvent(CanSmartUseEvent E)
		{
			if (CanCarve(E.Actor))
				return false;
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(CommandSmartUseEvent E)
		{
			if (CanCarve(E.Actor))
				AttemptCarve(E.Actor);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (E.Actor != null && E.Actor.IsPlayer() && GetInputs(E.Actor).Count > 0)
				E.AddAction("Carve", "carve", COMMAND, null, 'v', FireOnActor: false, Default: 5);
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
			if (Actor.DistanceTo(ParentObject) > 1)
				return Actor.Fail("You cannot reach " + ParentObject.t() + ".");
			ChargeSink sink = ParentObject.GetPart<ChargeSink>();
			if (sink != null && !sink.IsReady())
				return Actor.Fail(ParentObject.Does("click", int.MaxValue, null, null, "merely") + ".");

			List<GameObject> inputs = GetInputs(Actor);
			if (inputs.Count == 0)
				return Actor.Fail("You have nothing to carve.");
			GameObject material = Popup.PickGameObject("Choose something to carve.", inputs, AllowEscape: true);
			if (material == null)
				return false;
			string figurineBlueprint = material.GetTag(INPUT_TAG);
			if (GameObjectFactory.Factory.GetBlueprintIfExists(figurineBlueprint) == null)
				return Actor.Fail("You cannot carve " + material.t() + ".");

			string subject = Cleo_TerraFirma_CarvingPicker.Pick("");
			if (subject == null)
				return false;

			GameObject figurine = GameObject.Create(figurineBlueprint, BeforeObjectCreated: o =>
			{
				RandomFigurine rf = o.GetPart<RandomFigurine>();
				if (rf != null)
					rf.Creature = subject;
			});
			if (figurine == null)
				return Actor.Fail("You cannot carve " + material.t() + ".");

			material.RemoveOne().Obliterate();
			Actor.ReceiveObject(figurine);
			IComponent<GameObject>.XDidYToZ(Actor, "lathe", figurine, EndMark: ".", IndefiniteObject: true, Source: Actor);
			Actor.UseEnergy(1000, "Item Lathe Carve");
			return true;
		}
	}
}
