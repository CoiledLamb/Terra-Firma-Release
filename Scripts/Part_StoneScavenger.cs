using System;
using System.Collections.Generic;
using XRL.World.AI.GoalHandlers;
using XRL.World.Anatomy;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_StoneScavenger : IPart
	{
		public int Radius = 8;

		[NonSerialized]
		private int DiagTick;

		[NonSerialized]
		private string LastSlotState;
		[NonSerialized]
		private int LastPocketCount = -1;
		[NonSerialized]
		private bool LastHadMoveGoal;

		[NonSerialized]
		private Cell LastDetourTarget;
		[NonSerialized]
		private int DetourTries;
		[NonSerialized]
		private int FetchRest;

		public override bool SameAs(IPart p)
		{
			return p is Cleo_TerraFirma_StoneScavenger o && o.Radius == Radius && base.SameAs(p);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade))
				return ID == SingletonEvent<BeforeTakeActionEvent>.ID;
			return true;
		}

		public override bool HandleEvent(BeforeTakeActionEvent E)
		{
			TryScavenge(E);
			return base.HandleEvent(E);
		}

		private static bool IsLooseStone(GameObject obj)
		{
			return obj != null
				&& obj.HasTag("Cleo_TerraFirma_IsAnyStone")
				&& obj.Physics != null && obj.Physics.Takeable
				&& obj.InInventory == null && obj.Equipped == null;
		}

		private static bool CellHasLooseStone(Cell C)
		{
			foreach (GameObject obj in C.Objects)
			{
				if (IsLooseStone(obj))
					return true;
			}
			return false;
		}

		private void TryScavenge(BeforeTakeActionEvent E)
		{
			GameObject who = ParentObject;
			if (who == null || who.IsPlayerControlled() || who.Brain == null || who.CurrentCell == null)
				return;
			bool diag = ++DiagTick % 50 == 1;
			BodyPart diagSlot = who.GetFirstBodyPart("Thrown Weapon");
			string slotState = diagSlot == null ? "NO-SLOT" : (diagSlot.Equipped?.Blueprint ?? "EMPTY");
			int pocketCount = 0;
			List<GameObject> diagPack = who.Inventory?.GetObjectsDirect();
			if (diagPack != null)
				foreach (GameObject item in diagPack)
					if (item.HasTag("Cleo_TerraFirma_IsAnyStone"))
						pocketCount += item.Count;
			bool hasMoveGoal = who.Brain.HasGoal("MoveTo");
			if (slotState != LastSlotState || pocketCount != LastPocketCount || hasMoveGoal != LastHadMoveGoal)
			{
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SCAV", $"{who.Blueprint} STATE: slot {LastSlotState ?? "?"}->{slotState}, pockets {LastPocketCount}->{pocketCount}, moveGoal {LastHadMoveGoal}->{hasMoveGoal}");
				LastSlotState = slotState;
				LastPocketCount = pocketCount;
				LastHadMoveGoal = hasMoveGoal;
			}
			if (who.Brain.HasGoal("MoveTo"))
			{
				if (diag)
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SCAV", $"{who.Blueprint} gate: MoveTo goal active (scoot loop suspect) -> skipped");
				return;
			}
			BodyPart slot = who.GetFirstBodyPart("Thrown Weapon");
			if (slot == null || slot.Equipped != null)
			{
				if (diag)
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SCAV", $"{who.Blueprint} gate: {(slot == null ? "NO Thrown Weapon body part" : "slot already armed with " + slot.Equipped.Blueprint)} -> skipped");
				return;
			}
			List<GameObject> pack = who.Inventory?.GetObjectsDirect();
			if (pack != null)
			{
				foreach (GameObject item in pack)
				{
					if (item.HasTag("Cleo_TerraFirma_IsAnyStone"))
					{
						bool readied = who.FireEvent(Event.New("CommandEquipObject", "Object", item, "BodyPart", slot));
						Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SCAV", $"{who.Blueprint} pocket re-ready: {item.Blueprint} {(readied ? "readied into the thrown slot (free action)" : "REFUSED by CommandEquipObject")}");
						return;
					}
				}
			}
			if (FetchRest > 0)
			{
				FetchRest--;
				return;
			}
			Cell here = who.CurrentCell;
			Cell best = null;
			int bestDist = Radius + 1;
			if (CellHasLooseStone(here))
			{
				best = here;
				bestDist = 0;
			}
			else
			{
				foreach (Cell c in here.GetLocalAdjacentCells(Radius))
				{
					int dist = Math.Max(Math.Abs(c.X - here.X), Math.Abs(c.Y - here.Y));
					if (dist < bestDist && CellHasLooseStone(c))
					{
						best = c;
						bestDist = dist;
					}
				}
			}
			if (best == null)
			{
				if (diag)
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SCAV", $"{who.Blueprint} gate: all clear but NO loose stone within {Radius} -> nothing to fetch");
				return;
			}
			GameObject target = who.Brain.Target;
			if (GameObject.Validate(ref target) && who.DistanceTo(target) <= bestDist)
			{
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SCAV", $"{who.Blueprint}: enemy {target.Blueprint} (d={who.DistanceTo(target)}) outranks stone at d={bestDist} -> attacks instead");
				return;
			}
			if (bestDist <= 1)
			{
				LastDetourTarget = null;
				DetourTries = 0;
				foreach (GameObject obj in best.Objects)
				{
					if (IsLooseStone(obj))
					{
						if (who.TakeObject(obj, NoStack: false, Silent: false, EnergyCost: 1000))
						{
							Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SCAV", $"{who.Blueprint}: reached for {obj.Blueprint} at d={bestDist} (turn spent)");
							who.Brain.PerformReequip();
							E.PreventAction = true;
						}
						return;
					}
				}
				return;
			}
			Cell stand = null;
			int standDist = int.MaxValue;
			foreach (Cell c in best.GetLocalAdjacentCells())
			{
				if (c.IsSolid() || c.HasObjectWithPart("Combat"))
					continue;
				int d = Math.Max(Math.Abs(c.X - here.X), Math.Abs(c.Y - here.Y));
				if (d < standDist)
				{
					stand = c;
					standDist = d;
				}
			}
			if (best == LastDetourTarget)
				DetourTries++;
			else
			{
				LastDetourTarget = best;
				DetourTries = 1;
			}
			if (stand == null || DetourTries > 3)
			{
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SCAV", $"{who.Blueprint}: gave up on stone at d={bestDist} ({(stand == null ? "no standable side" : "3 failed detours")}) -> fetch rests 20 turns");
				FetchRest = 20;
				LastDetourTarget = null;
				DetourTries = 0;
				return;
			}
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SCAV", $"{who.Blueprint}: detours to stand beside loose stone at d={bestDist}");
			who.Brain.PushGoal(new MoveTo(stand, careful: false, overridesCombat: true));
		}
	}
}
