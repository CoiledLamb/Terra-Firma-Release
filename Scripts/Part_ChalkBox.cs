using System;
using XRL.Rules;
using XRL.UI;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_ChalkBox : IPart
	{
		public const string DRAW_COMMAND = "Cleo_TerraFirma_CommandDrawChalk";
		public const string LINE_BLUEPRINT = "Cleo_TerraFirma_ChalkLine";

		public int Uses = -1;

		public int UsesMin = 8;
		public int UsesMax = 12;

		public Guid AbilityID = Guid.Empty;

		public override bool SameAs(IPart p)
		{
			return false;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == ObjectCreatedEvent.ID
				|| ID == TakenEvent.ID
				|| ID == AddedToInventoryEvent.ID
				|| ID == EquippedEvent.ID
				|| ID == DroppedEvent.ID
				|| ID == BeforeDestroyObjectEvent.ID
				|| ID == PooledEvent<GetDisplayNameEvent>.ID;
		}

		public override bool HandleEvent(ObjectCreatedEvent E)
		{
			if (Uses < 0)
			{
				Uses = Stat.Random(UsesMin, UsesMax);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetDisplayNameEvent E)
		{
			if (Uses >= 0 && ParentObject.IsReal && ParentObject.IsTakeable())
			{
				if (Uses == 1)
				{
					E.AddTag("{{y|[{{C|1}} stick]}}");
				}
				else
				{
					E.AddTag("{{y|[{{C|" + Uses + "}} sticks]}}");
				}
				GameObject who = ParentObject.InInventory ?? ParentObject.Equipped;
				if (who != null)
				{
					SyncAbilityName(who);
				}
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(TakenEvent E)
		{
			EnsureGrant(E.Actor);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(AddedToInventoryEvent E)
		{
			EnsureGrant(ParentObject.InInventory);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EquippedEvent E)
		{
			EnsureGrant(E.Actor);
			return base.HandleEvent(E);
		}

		private static System.Collections.Generic.List<GameObject> CarriedBoxes(GameObject who, GameObject skip = null)
		{
			var found = new System.Collections.Generic.List<GameObject>();
			if (who == null)
			{
				return found;
			}
			foreach (GameObject item in who.GetInventoryDirectAndEquipment())
			{
				if (item != skip && item.HasPart<Cleo_TerraFirma_ChalkBox>())
				{
					found.Add(item);
				}
			}
			return found;
		}

		private void EnsureGrant(GameObject who)
		{
			if (who != null && who.IsCreature)
			{
				HealLegacyStack();
				if (MergeIntoCarried(who))
				{
					return;
				}
			}
			if (who != null && who.IsCreature && who.ActivatedAbilities != null)
			{
				ActivatedAbilityEntry existing = who.ActivatedAbilities.GetAbilityByCommand(DRAW_COMMAND);
				if (existing == null)
				{
					AbilityID = who.ActivatedAbilities.AddAbility("Draw with Chalk", DRAW_COMMAND, "Items",
						Description: "Draw a chalk line on an adjacent square.",
						Silent: true,
						UITileDefault: new ConsoleLib.Console.Renderable(ParentObject.Render));
					who.RegisterPartEvent(this, DRAW_COMMAND);
				}
				else if (AbilityID == Guid.Empty && !AnyOtherBoxClaimsGrant(who))
				{
					AbilityID = existing.ID;
					who.RegisterPartEvent(this, DRAW_COMMAND);
				}
				SyncAbilityName(who);
			}
		}

		private bool MergeIntoCarried(GameObject who)
		{
			if (Uses <= 0)
			{
				return false;
			}
			var others = CarriedBoxes(who, skip: ParentObject);
			if (others.Count == 0)
			{
				return false;
			}
			Cleo_TerraFirma_ChalkBox survivor = null;
			foreach (GameObject other in others)
			{
				Cleo_TerraFirma_ChalkBox part = other.GetPart<Cleo_TerraFirma_ChalkBox>();
				if (part.AbilityID != Guid.Empty)
				{
					survivor = part;
					break;
				}
			}
			if (survivor == null)
			{
				survivor = others[0].GetPart<Cleo_TerraFirma_ChalkBox>();
			}
			survivor.HealLegacyStack();
			int poured = Uses;
			survivor.Uses += poured;
			Uses = 0;
			if (who.IsPlayer())
			{
				IComponent<GameObject>.AddPlayerMessage("You tip the new chalk into your box.");
			}
			GameObject husk = ParentObject;
			bool destroyed = husk.Destroy();
			bool gone = !GameObject.Validate(husk) || husk.IsInGraveyard();
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CHALK",
				$"box merge: +{poured} -> carried box now {survivor.Uses}, husk Destroy()={destroyed}, gone={gone}");
			if (!gone && husk.InInventory != null)
			{
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CHALK", "merge BELT: husk survived Destroy in an inventory; obliterating");
				husk.Obliterate();
			}
			SyncAbilityName(who);
			return true;
		}

		private void HealLegacyStack()
		{
			Stacker stacker = ParentObject.Stacker;
			if (stacker != null && stacker.StackCount > 1)
			{
				int k = stacker.StackCount;
				Uses *= k;
				stacker.StackCount = 1;
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CHALK", $"legacy stack healed: x{k} -> one box, {Uses} uses");
			}
		}

		public static void SyncAbilityName(GameObject who)
		{
			ActivatedAbilityEntry ent = who?.ActivatedAbilities?.GetAbilityByCommand(DRAW_COMMAND);
			if (ent == null)
			{
				return;
			}
			int total = 0;
			foreach (GameObject item in CarriedBoxes(who))
			{
				int uses = item.GetPart<Cleo_TerraFirma_ChalkBox>().Uses;
				if (uses > 0)
				{
					total += uses;
				}
			}
			ent.DisplayName = "Draw with Chalk (" + total + ")";
		}

		public override bool HandleEvent(DroppedEvent E)
		{
			ReleaseGrant(E.Actor);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(BeforeDestroyObjectEvent E)
		{
			ReleaseGrant(ParentObject.InInventory ?? ParentObject.Equipped);
			return base.HandleEvent(E);
		}

		private void ReleaseGrant(GameObject who)
		{
			if (who == null || AbilityID == Guid.Empty)
			{
				return;
			}
			var others = CarriedBoxes(who, skip: ParentObject);
			who.UnregisterPartEvent(this, DRAW_COMMAND);
			if (others.Count > 0)
			{
				Cleo_TerraFirma_ChalkBox heir = others[0].GetPart<Cleo_TerraFirma_ChalkBox>();
				heir.AbilityID = AbilityID;
				who.RegisterPartEvent(heir, DRAW_COMMAND);
			}
			else
			{
				who.ActivatedAbilities?.RemoveAbility(AbilityID);
			}
			AbilityID = Guid.Empty;
			SyncAbilityName(who);
		}

		private bool AnyOtherBoxClaimsGrant(GameObject who)
		{
			foreach (GameObject item in CarriedBoxes(who, skip: ParentObject))
			{
				if (item.GetPart<Cleo_TerraFirma_ChalkBox>().AbilityID != Guid.Empty)
				{
					return true;
				}
			}
			return false;
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == DRAW_COMMAND)
			{
				GameObject actor = E.GetGameObjectParameter("Actor");
				if (actor != null)
				{
					if ((ParentObject.InInventory == actor || ParentObject.Equipped == actor) && Uses > 0)
					{
						AttemptDraw(actor);
					}
					else
					{
						Cleo_TerraFirma_ChalkBox live = FindCarriedBox(actor);
						Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CHALK",
							$"draw re-route: registered box uses={Uses}, inInv={(ParentObject.InInventory == actor)}, live={(live != null)}");
						if (live != null && live != this)
						{
							live.AttemptDraw(actor);
						}
						else if (actor.IsPlayer())
						{
							Popup.ShowFail("You have no chalk.");
						}
					}
				}
			}
			return base.FireEvent(E);
		}

		private void AttemptDraw(GameObject actor)
		{
			if (Uses <= 0)
			{
				if (actor.IsPlayer())
				{
					Popup.ShowFail(ParentObject.Does("are") + " empty.");
				}
				return;
			}
			Cell target = PickDirection("Draw where?", actor);
			if (target == null || target == actor.CurrentCell)
			{
				return;
			}
			string dir = actor.CurrentCell.GetDirectionFromCell(target);
			if (target.HasWall() || target.HasObjectWithPart(nameof(Cleo_TerraFirma_ChalkLine)))
			{
				if (actor.IsPlayer())
				{
					Popup.ShowFail("There is no clear floor there to chalk.");
				}
				return;
			}
			GameObject line = GameObject.Create(LINE_BLUEPRINT);
			Cleo_TerraFirma_ChalkLine linePart = line.GetPart<Cleo_TerraFirma_ChalkLine>();
			if (linePart != null)
			{
				linePart.StrokeDir = dir;
			}
			float settle = Cleo.TerraFirma.Scripts.Cleo_TerraFirma_ChalkRevealFX.Play(target);
			if (settle > 0f)
			{
				Cleo.TerraFirma.Scripts.Cleo_TerraFirma_ChalkRevealFX.Hold(settle);
			}
			target.AddObject(line);
			linePart?.Retile();
			Cleo_TerraFirma_ChalkLine.RetileNeighbors(target);
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CHALK", $"line drawn {dir}, reveal {(settle > 0f ? "played" : "skipped")}, {Uses - 1} uses left");
			actor.UseEnergy(1000, "Item DrawChalk");
			ConsumeUse(actor);
		}

		public void ConsumeUse(GameObject actor)
		{
			HealLegacyStack();
			Uses--;
			GameObject box = ParentObject;
			GameObject holder = box.InInventory ?? actor;
			if (Uses <= 0)
			{
				if (actor != null && actor.IsPlayer())
				{
					IComponent<GameObject>.AddPlayerMessage("The last stick of chalk crumbles to dust.");
				}
				bool destroyed = box.Destroy();
				bool gone = !GameObject.Validate(box) || box.IsInGraveyard();
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CHALK",
					$"crumble: Destroy()={destroyed}, gone={gone}, context={(box?.InInventory != null ? "inventory" : box?.CurrentCell != null ? "cell" : "none")}");
				if (!gone && box.InInventory != null)
				{
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CHALK", "crumble BELT: spent box survived Destroy in an inventory; obliterating");
					box.Obliterate();
				}
			}
			SyncAbilityName(holder);
		}

		public static Cleo_TerraFirma_ChalkBox FindCarriedBox(GameObject actor)
		{
			Cleo_TerraFirma_ChalkBox best = null;
			foreach (GameObject item in CarriedBoxes(actor))
			{
				Cleo_TerraFirma_ChalkBox box = item.GetPart<Cleo_TerraFirma_ChalkBox>();
				if (box.Uses > 0 && (best == null || box.Uses > best.Uses))
				{
					best = box;
				}
			}
			return best;
		}
	}
}
