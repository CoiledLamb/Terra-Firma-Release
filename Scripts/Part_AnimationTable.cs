using System;
using System.Collections.Generic;
using XRL.UI;
using XRL.World.Parts.Mutation;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_AnimationTablePart : IPart
	{
		public const string ANIMATE_COMMAND = "Cleo_TerraFirma_AnimateFigurine";

		public string WalkerBlueprint = "Cleo_TerraFirma_AnimateFigurine";

		public int AnimationCost = 1000;

		public int StoneshaperLevel = 1;

		public bool CreationSpawnDone;

		public override bool SameAs(IPart p)
		{
			return false;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| (ID == EnteredCellEvent.ID && !CreationSpawnDone)
				|| ID == GetInventoryActionsEvent.ID
				|| ID == CanSmartUseEvent.ID
				|| ID == CommandSmartUseEvent.ID
				|| ID == InventoryActionEvent.ID;
		}

		public override bool HandleEvent(EnteredCellEvent E)
		{
			if (!CreationSpawnDone)
			{
				CreationSpawnDone = true;
				Cell spot = ParentObject.CurrentCell?.GetFirstEmptyAdjacentCell(1, 2);
				if (spot != null)
				{
					spot.AddObject(GameObject.Create(WalkerBlueprint)).MakeActive();
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("FIGTABLE", "creation spawn: ambient figurine placed beside the table");
				}
				else
				{
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("FIGTABLE", "creation spawn: no open adjacent tile, skipped");
				}
			}
			return base.HandleEvent(E);
		}

		private bool CanAnimate(GameObject actor)
		{
			return actor != null && actor != ParentObject && ParentObject.Understood()
				&& actor.PhaseMatches(ParentObject) && actor.FlightCanReach(ParentObject)
				&& FindCarriedFigurines(actor).Count > 0;
		}

		public override bool HandleEvent(CanSmartUseEvent E)
		{
			if (CanAnimate(E.Actor)) return false;
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(CommandSmartUseEvent E)
		{
			if (CanAnimate(E.Actor))
				InventoryActionEvent.Check(ParentObject, E.Actor, ParentObject, ANIMATE_COMMAND);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (FindCarriedFigurines(E.Actor).Count > 0)
			{
				E.AddAction("Animate", "animate a figurine", ANIMATE_COMMAND, null, 'a', FireOnActor: false, Default: 10);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == ANIMATE_COMMAND)
			{
				List<GameObject> figurines = FindCarriedFigurines(E.Actor);
				if (figurines.Count == 0)
				{
					if (E.Actor.IsPlayer())
						Popup.ShowFail("You carry no figurine to animate.");
					return false;
				}
				if (ParentObject.QueryCharge() < AnimationCost)
				{
					if (E.Actor.IsPlayer())
						Popup.ShowFail(ParentObject.Does("click", int.MaxValue, null, null, "merely") + ".");
					return false;
				}
				GameObject pick;
				if (E.Actor.IsPlayer())
				{
					List<string> options = new List<string>();
					foreach (GameObject fig in figurines)
						options.Add(fig.DisplayName);
					int choice = Popup.PickOption(
						Intro: "Animate which figurine?",
						Options: options.ToArray(),
						AllowEscape: true);
					if (choice < 0)
						return false;
					pick = figurines[choice];
				}
				else
				{
					pick = figurines[0];
				}
				pick = pick.SplitFromStack() ?? pick;
				Cell spot = ParentObject.CurrentCell?.GetFirstEmptyAdjacentCell(1, 2);
				if (spot == null)
				{
					if (E.Actor.IsPlayer())
						Popup.ShowFail("There is no open ground beside " + ParentObject.t() + ".");
					return false;
				}
				if (!ParentObject.UseCharge(AnimationCost))
					return false;

				GameObject walker = BuildWalker(pick, E.Actor);
				spot.AddObject(walker).MakeActive();
				if (E.Actor.IsPlayer())
				{
					IComponent<GameObject>.AddPlayerMessage("You imbue " + pick.t() + " with life.");
				}
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("FIGTABLE",
					$"animated {pick.Blueprint} -> {walker.DisplayNameOnlyStripped} (charge left {ParentObject.QueryCharge()})");
				pick.Destroy();
				E.Actor.UseEnergy(1000, "Item AnimateFigurine");
				E.RequestInterfaceExit();
			}
			return base.HandleEvent(E);
		}

		private static List<GameObject> FindCarriedFigurines(GameObject actor)
		{
			List<GameObject> found = new List<GameObject>();
			if (actor?.Inventory != null)
			{
				foreach (GameObject obj in actor.Inventory.GetObjectsDirect())
				{
					if (obj.HasPart<RandomFigurine>())
						found.Add(obj);
				}
			}
			return found;
		}

		private GameObject BuildWalker(GameObject figurine, GameObject animator)
		{
			GameObject walker = GameObject.Create(WalkerBlueprint);

			walker.GetStat("Hitpoints").BaseValue = Cleo_TerraFirma_Stoneshaper.GetHP(StoneshaperLevel);
			walker.GetStat("AV").BaseValue = Cleo_TerraFirma_Stoneshaper.GetAV(StoneshaperLevel);

			walker.Render.DisplayName = "animate " + figurine.DisplayNameOnlyStripped;

			Description figDesc = figurine.GetPart<Description>();
			Description walkerDesc = walker.GetPart<Description>();
			if (figDesc != null && walkerDesc != null)
				walkerDesc._Short = figDesc._Short;

			RandomFigurine figPart = figurine.GetPart<RandomFigurine>();
			if (figPart?.Creature != null && walker.Brain != null)
			{
				GameObject sample = GameObjectFactory.Factory.CreateSampleObject(figPart.Creature);
				if (sample?.Brain != null)
				{
					foreach (KeyValuePair<string, int> kv in sample.Brain.Allegiance)
					{
						if (!walker.Brain.Allegiance.ContainsKey(kv.Key))
							walker.Brain.Allegiance.Add(kv.Key, kv.Value);
					}
				}
			}

			walker.AddPart(new Cleo_TerraFirma_BondedFigurinePart(animator));
			return walker;
		}
	}
}
