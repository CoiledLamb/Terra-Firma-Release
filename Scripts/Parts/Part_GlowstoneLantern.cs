using System;
using XRL.UI;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_GlowstoneLantern : IPart
	{
		public const string LOAD_COMMAND = "Cleo_TerraFirma_LoadGlowstoneLantern";
		public const string UNLOAD_COMMAND = "Cleo_TerraFirma_UnloadGlowstoneLantern";
		public string ShardBlueprint = "Cleo_TerraFirma_GlowstoneShard";
		public int BurnInterval = 2;
		public string LitTile = "lantern_lit.png";
		public string UnlitTile = "lantern_unlit.png";

		public string LitDescription = "A wrought metal cage and panes of crystal enclose a phosphorescent shard, amplifying its flickering glow.";
		public string UnlitDescription = "A wrought metal cage and panes of crystal enclose an inert wire socket.";

		public GameObject Shard;

		public int BurnPhase;

		public bool StartLoaded;
		public string StartFuel = "2000-4500";

		public override bool SameAs(IPart p)
		{
			if (!(p is Cleo_TerraFirma_GlowstoneLantern o) || o.Shard != null || Shard != null)
			{
				return false;
			}
			return o.ShardBlueprint == ShardBlueprint && o.BurnInterval == BurnInterval
				&& o.BurnPhase == BurnPhase && o.LitTile == LitTile && o.UnlitTile == UnlitTile
				&& o.LitDescription == LitDescription && o.UnlitDescription == UnlitDescription
				&& o.StartLoaded == StartLoaded && o.StartFuel == StartFuel && base.SameAs(p);
		}

		public override IPart DeepCopy(GameObject Parent, Func<GameObject, GameObject> MapInv)
		{
			Cleo_TerraFirma_GlowstoneLantern copy = (Cleo_TerraFirma_GlowstoneLantern)base.DeepCopy(Parent, MapInv);
			if (GameObject.Validate(ref Shard))
			{
				copy.Shard = MapInv?.Invoke(Shard) ?? Shard.DeepCopy(CopyEffects: false, CopyID: false, MapInv);
			}
			else
			{
				copy.Shard = null;
			}
			return copy;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == SingletonEvent<EndTurnEvent>.ID
				|| ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID
				|| ID == PooledEvent<GetDisplayNameEvent>.ID
				|| ID == ObjectCreatedEvent.ID;
		}

		public override bool HandleEvent(ObjectCreatedEvent E)
		{
			if (StartLoaded && Shard == null)
			{
				GameObject shard = GameObject.Create(ShardBlueprint);
				MoteProperties mote = shard?.GetPart<MoteProperties>();
				if (mote != null)
				{
					int fuel = XRL.Rules.Stat.Roll(StartFuel);
					mote.Fuel = fuel < 1 ? 1 : fuel;
				}
				Shard = shard;
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("LANTERN", $"spawn-loaded shard fuel={GetFuel()}");
			}
			SyncState();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (Shard == null)
			{
				E.AddAction("Load", "load fragment", LOAD_COMMAND, null, 'o', FireOnActor: false);
			}
			else
			{
				E.AddAction("Unload", "remove fragment", UNLOAD_COMMAND, null, 'o', FireOnActor: false);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == LOAD_COMMAND && E.Actor != null)
			{
				GameObject pick = FindBestShard(E.Actor);
				if (pick == null)
				{
					if (E.Actor.IsPlayer())
					{
						Popup.ShowFail("You have no glowsphere fragment to load.");
					}
					return false;
				}
				pick = pick.SplitFromStack() ?? pick;
				pick.RemoveFromContext();
				Shard = pick;
				SyncState();
				if (E.Actor.IsPlayer())
				{
					IComponent<GameObject>.AddPlayerMessage("You seat " + pick.t() + " inside " + ParentObject.t() + ".");
				}
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("LANTERN", $"loaded shard fuel={GetFuel()}");
				E.Actor.UseEnergy(1000, "Item LoadGlowstoneLantern");
				E.RequestInterfaceExit();
			}
			else if (E.Command == UNLOAD_COMMAND && E.Actor != null && Shard != null)
			{
				GameObject outShard = Shard;
				Shard = null;
				E.Actor.ReceiveObject(outShard);
				SyncState();
				if (E.Actor.IsPlayer())
				{
					IComponent<GameObject>.AddPlayerMessage("You take " + outShard.t() + " out of " + ParentObject.t() + ".");
				}
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("LANTERN", "shard removed");
				E.Actor.UseEnergy(1000, "Item UnloadGlowstoneLantern");
				E.RequestInterfaceExit();
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			if (GameObject.Validate(ref Shard))
			{
				MoteProperties mote = Shard.GetPart<MoteProperties>();
				if (mote != null)
				{
					BurnPhase = (BurnPhase + 1) % Math.Max(1, BurnInterval);
					if (BurnPhase == 0 && mote.Fuel > 0)
					{
						mote.Fuel--;
						if (mote.Fuel <= 0)
						{
							if (ParentObject.Equipped != null && ParentObject.Equipped.IsPlayer())
							{
								IComponent<GameObject>.AddPlayerMessage("The fragment inside " + The.Player.Poss(ParentObject) + " goes dark.");
							}
							Cleo.TerraFirma.Scripts.Helpers.VerifyLog("LANTERN", "socketed shard burned out");
							Shard.Obliterate();
							Shard = null;
						}
					}
				}
				SyncState();
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetDisplayNameEvent E)
		{
			int fuel = GetFuel();
			if (fuel > 0 && ParentObject.IsReal)
			{
				E.AddTag("(" + FuelDescription(fuel) + ")");
			}
			return base.HandleEvent(E);
		}

		private GameObject FindBestShard(GameObject actor)
		{
			GameObject best = null;
			int bestFuel = -1;
			foreach (GameObject item in actor.GetInventoryAndEquipmentReadonly())
			{
				if (item.Blueprint == ShardBlueprint)
				{
					int fuel = item.GetPart<MoteProperties>()?.Fuel ?? 0;
					if (fuel > bestFuel)
					{
						bestFuel = fuel;
						best = item;
					}
				}
			}
			return best;
		}

		private int GetFuel()
		{
			if (!GameObject.Validate(ref Shard))
			{
				return 0;
			}
			return Shard.GetPart<MoteProperties>()?.Fuel ?? 0;
		}

		[NonSerialized]
		private int FrameOffset;

		public override bool Render(RenderEvent E)
		{
			if (GetFuel() > 0)
			{
				int num = (XRL.Core.XRLCore.CurrentFrame + FrameOffset) % 60;
				if (!XRL.UI.Options.DisableTextAnimationEffects)
				{
					FrameOffset += XRL.Rules.Stat.Random(1, 5);
				}
				string color = num < 15 ? "Y" : num < 30 ? "W" : num >= 45 ? "W" : "C";
				E.ApplyDetailColor(color, 50);
			}
			return base.Render(E);
		}

		private static string FuelDescription(int fuel)
		{
			if (fuel > 3500)
			{
				return "{{Y|blazing}}";
			}
			if (fuel > 2000)
			{
				return "{{W|bright}}";
			}
			if (fuel > 500)
			{
				return "dim";
			}
			return "{{K|faint}}";
		}

		private void SyncState()
		{
			bool lit = GetFuel() > 0;
			LightSource light = ParentObject.GetPart<LightSource>();
			if (light != null)
			{
				light.Lit = lit;
			}
			Render render = ParentObject.GetPart<Render>();
			if (render != null)
			{
				render.Tile = lit ? LitTile : UnlitTile;
			}
			AnimatedMaterialGeneric anim = ParentObject.GetPart<AnimatedMaterialGeneric>();
			if (anim != null)
			{
				ParentObject.RemovePart(anim);
			}
			Description desc = ParentObject.GetPart<Description>();
			if (desc != null)
			{
				desc.Short = lit ? LitDescription : UnlitDescription;
			}
		}
	}
}
