using System;
using System.Collections.Generic;
using XRL.UI;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_GrinderCell : IPart
	{
		public const string FEED_COMMAND = "Cleo_TerraFirma_FeedGrinderCell";
		public const string STOP_COMMAND = "Cleo_TerraFirma_StopGrinderCell";
		public const string STONE_TAG = "Cleo_TerraFirma_IsAnyStone";
		public int ChargePerPound = 50;
		public int TricklePerTurn = 10;

		public int StoneChargeLeft;

		public string StoneName;

		public override bool SameAs(IPart p)
		{
			Cleo_TerraFirma_GrinderCell o = p as Cleo_TerraFirma_GrinderCell;
			if (o.StoneChargeLeft != StoneChargeLeft || o.StoneName != StoneName
				|| o.ChargePerPound != ChargePerPound || o.TricklePerTurn != TricklePerTurn)
			{
				return false;
			}
			return base.SameAs(p);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == SingletonEvent<EndTurnEvent>.ID
				|| ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID
				|| ID == PooledEvent<GetDisplayNameEvent>.ID
				|| ID == GetExtrinsicWeightEvent.ID;
		}

		public int RemainingStoneWeight()
		{
			return (StoneChargeLeft + ChargePerPound - 1) / ChargePerPound;
		}

		public override bool HandleEvent(GetExtrinsicWeightEvent E)
		{
			E.Weight += RemainingStoneWeight();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (StoneChargeLeft <= 0)
			{
				E.AddAction("Feed", "feed a stone", FEED_COMMAND, null, 'f', FireOnActor: false);
			}
			else
			{
				E.AddAction("Stop", "stop grinding", STOP_COMMAND, null, 's', FireOnActor: false);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == STOP_COMMAND && E.Actor != null && StoneChargeLeft > 0)
			{
				int dumped = RemainingStoneWeight();
				StoneChargeLeft = 0;
				StoneName = null;
				SyncGrindAnimation(false);
				ParentObject.FlushWeightCaches();
				if (E.Actor.IsPlayer())
				{
					IComponent<GameObject>.AddPlayerMessage("The spiral stills and dumps its unground grist.");
				}
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("GRINDER", $"grind stopped, {dumped} lb of grist dumped");
				E.Actor.UseEnergy(1000, "Item StopGrinderCell");
				E.RequestInterfaceExit();
				return base.HandleEvent(E);
			}
			if (E.Command == FEED_COMMAND && E.Actor != null)
			{
				List<GameObject> stones = E.Actor.GetInventoryDirect((GameObject go) =>
					go.HasTagOrProperty(STONE_TAG) && go != ParentObject && !go.HasPart<Temporary>());
				int carried = stones.Count;
				Cell origin = E.Actor.CurrentCell;
				if (origin != null)
				{
					List<Cell> reach = new List<Cell>(origin.GetLocalAdjacentCells());
					reach.Insert(0, origin);
					foreach (Cell c in reach)
					{
						foreach (GameObject go in c.Objects)
						{
							bool tagged = go.HasTagOrProperty(STONE_TAG);
							if (tagged && go != ParentObject && !go.HasPart<Temporary>()
								&& !go.IsCreature
								&& (go.GetPart<Physics>()?.Owner).IsNullOrEmpty())
							{
								stones.Add(go);
							}
							else if (tagged || (go.Blueprint != null && go.Blueprint.IndexOf("oulder", StringComparison.Ordinal) >= 0))
							{
								Cleo.TerraFirma.Scripts.Helpers.VerifyLog("GRINDER",
									$"feed scan refused {go.Blueprint}: tagged={tagged}, creature={go.IsCreature}, wall={go.IsWall()}, owner={go.GetPart<Physics>()?.Owner ?? "none"}, temp={go.HasPart<Temporary>()}");
							}
						}
					}
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("GRINDER",
						$"feed scan: reach={reach.Count} cells, carried={carried}, nearby={stones.Count - carried}");
				}
				if (stones.Count == 0)
				{
					if (E.Actor.IsPlayer())
					{
						Popup.ShowFail("There is no stone in reach to feed it.");
					}
					return false;
				}
				GameObject pick;
				if (E.Actor.IsPlayer())
				{
					List<string> options = new List<string>();
					for (int i = 0; i < stones.Count; i++)
					{
						int w = stones[i].GetPart<Physics>()?.Weight ?? 0;
						options.Add(stones[i].DisplayName + " {{K|(" + w + "#)}}"
							+ (i >= carried ? " {{K|(nearby)}}" : ""));
					}
					int choice = Popup.PickOption(
						Intro: "Feed which stone?",
						Options: options.ToArray(),
						AllowEscape: true);
					if (choice < 0)
					{
						return false;
					}
					pick = stones[choice];
				}
				else
				{
					pick = stones[0];
					foreach (GameObject stone in stones)
					{
						if ((stone.GetPart<Physics>()?.Weight ?? 0) > (pick.GetPart<Physics>()?.Weight ?? 0))
						{
							pick = stone;
						}
					}
				}
				pick = pick.SplitFromStack() ?? pick;
				int weight = pick.GetPart<Physics>()?.Weight ?? 0;
				StoneChargeLeft = Math.Max(weight, 1) * ChargePerPound;
				StoneName = pick.DisplayName;
				SyncGrindAnimation(true);
				ParentObject.FlushWeightCaches();
				if (E.Actor.IsPlayer())
				{
					IComponent<GameObject>.AddPlayerMessage("You feed " + pick.t() + " into " + ParentObject.t() + ", and the spiral begins to churn.");
				}
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("GRINDER", $"fed {pick.Blueprint} weight={weight} pool={StoneChargeLeft}");
				pick.Destroy();
				E.Actor.UseEnergy(1000, "Item FeedGrinderCell");
				E.RequestInterfaceExit();
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			if (StoneChargeLeft > 0)
			{
				EnergyCell cell = ParentObject.GetPart<EnergyCell>();
				if (cell != null && cell.Charge < cell.MaxCharge)
				{
					int tick = Math.Min(TricklePerTurn, Math.Min(StoneChargeLeft, cell.MaxCharge - cell.Charge));
					int weightBefore = RemainingStoneWeight();
					cell.AddCharge(tick);
					StoneChargeLeft -= tick;
					if (RemainingStoneWeight() != weightBefore)
					{
						ParentObject.FlushWeightCaches();
					}
					if (StoneChargeLeft <= 0)
					{
						StoneName = null;
						SyncGrindAnimation(false);
						GameObject holder = ParentObject.InInventory ?? ParentObject.Equipped;
						if (holder != null && holder.IsPlayer())
						{
							IComponent<GameObject>.AddPlayerMessage("The spiral grinds its stone down to nothing and stills.");
						}
						Cleo.TerraFirma.Scripts.Helpers.VerifyLog("GRINDER", $"stone ground out, cell at {cell.Charge}/{cell.MaxCharge}");
					}
				}
			}
			return base.HandleEvent(E);
		}

		private void SyncGrindAnimation(bool grinding)
		{
			AnimatedMaterialGeneric anim = ParentObject.GetPart<AnimatedMaterialGeneric>();
			if (grinding && anim == null)
			{
				anim = ParentObject.AddPart<AnimatedMaterialGeneric>();
				anim.AnimationLength = 60;
				anim.TileAnimationFrames = "0=grinder_cell.png,30=grinder_cell2.png";
			}
			else if (!grinding && anim != null)
			{
				ParentObject.RemovePart(anim);
				Render render = ParentObject.GetPart<Render>();
				if (render != null)
				{
					render.Tile = "grinder_cell.png";
				}
			}
		}

		public override bool HandleEvent(GetDisplayNameEvent E)
		{
			if (StoneChargeLeft > 0 && ParentObject.IsReal)
			{
				E.AddTag("{{y|(grinding)}}");
			}
			return base.HandleEvent(E);
		}
	}
}
