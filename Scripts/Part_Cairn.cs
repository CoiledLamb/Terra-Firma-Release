using System;
using Cleo.TerraFirma.Scripts;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_CairnTopple : IPart
	{
		public string ToppledBlueprint = "Cleo_TerraFirma_CairnToppled";

		public string ToppleSound = "Sounds/Throw/sfx_throwing_stone_medium_impact";

		public int AvoidWeight = 7;

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) &&
				ID != ObjectEnteredCellEvent.ID &&
				ID != GetNavigationWeightEvent.ID)
				return ID == PooledEvent<InterruptAutowalkEvent>.ID;
			return true;
		}

		public override bool HandleEvent(GetNavigationWeightEvent E)
		{
			if (E.Smart && E.PhaseMatches(ParentObject))
				E.MinWeight(AvoidWeight);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InterruptAutowalkEvent E)
		{
			E.IndicateObject = ParentObject;
			return false;
		}

		public override bool HandleEvent(ObjectEnteredCellEvent E)
		{
			GameObject stepper = E.Object;
			if (GameObject.Validate(ref stepper) && stepper != ParentObject &&
				stepper.IsCombatObject() && stepper.PhaseAndFlightMatches(ParentObject))
			{
				string owner = ParentObject.Physics?.Owner;
				if (!string.IsNullOrEmpty(owner) && stepper.GetPrimaryFaction() == owner)
					return base.HandleEvent(E);
				Cell here = ParentObject.CurrentCell;
				if (here == null)
					return base.HandleEvent(E);
				GameObject toppled = GameObject.Create(ToppledBlueprint);
				if (!string.IsNullOrEmpty(owner) && toppled.Physics != null)
					toppled.Physics.Owner = owner;
				if (Visible())
					XDidY(stepper, "topple", ParentObject.the + ParentObject.ShortDisplayName, "!", ColorAsBadFor: stepper);
				ParentObject.PlayWorldSound(ToppleSound, 1f);
				if (!string.IsNullOrEmpty(owner))
					ParentObject.Physics?.BroadcastForHelp(stepper);
				Helpers.VerifyLog("CAIRN", $"toppled by {stepper.Blueprint}{(string.IsNullOrEmpty(owner) ? "" : $" (owner {owner}, help broadcast)")}");
				here.AddObject(toppled);
				ParentObject.Destroy(null, Silent: true);
			}
			return base.HandleEvent(E);
		}
	}

	[Serializable]
	public class Cleo_TerraFirma_CairnRestack : IPart
	{
		public const string COMMAND = "Cleo_TerraFirma_RestackCairn";

		public string StandingBlueprint = "Cleo_TerraFirma_Cairn";

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) &&
				ID != GetInventoryActionsEvent.ID &&
				ID != CanSmartUseEvent.ID &&
				ID != CommandSmartUseEvent.ID)
				return ID == InventoryActionEvent.ID;
			return true;
		}

		public override bool HandleEvent(CanSmartUseEvent E) => false;

		public override bool HandleEvent(CommandSmartUseEvent E)
		{
			InventoryActionEvent.Check(ParentObject, E.Actor, ParentObject, COMMAND);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Restack", "restack", COMMAND, null, 'r', FireOnActor: false, Default: 10);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == COMMAND)
			{
				Cell here = ParentObject.CurrentCell;
				if (here == null)
					return false;
				GameObject blocker = here.GetFirstObject(o => o.IsCombatObject());
				if (blocker != null)
					return E.Actor.ShowFailure(ParentObject.T() + " cannot be restacked with " + (blocker.IsPlayer() ? "you" : blocker.t()) + " in the way.");
				GameObject standing = GameObject.Create(StandingBlueprint);
				string owner = ParentObject.Physics?.Owner;
				if (!string.IsNullOrEmpty(owner) && standing.Physics != null)
					standing.Physics.Owner = owner;
				if (E.Actor.IsPlayer())
					IComponent<GameObject>.AddPlayerMessage("You stack the stones back into a cairn.");
				Helpers.VerifyLog("CAIRN", $"restacked by {E.Actor.Blueprint}{(string.IsNullOrEmpty(owner) ? "" : $" (owner {owner} kept)")}");
				here.AddObject(standing);
				ParentObject.Destroy(null, Silent: true);
				E.Actor.UseEnergy(1000, "Item RestackCairn");
				E.RequestInterfaceExit();
			}
			return base.HandleEvent(E);
		}
	}
}
