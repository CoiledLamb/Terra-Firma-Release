using System;
using System.Collections.Generic;
using XRL.World.AI.GoalHandlers;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_RockSweeper : IPart
	{
		public int MaxWeight = 100;

		[NonSerialized] public int SeekRadius = 8;

		[NonSerialized]
		private Cell LastSeekTarget;
		[NonSerialized]
		private int SeekTries;
		[NonSerialized]
		private int SeekRest;

		public override bool SameAs(IPart p)
		{
			return p is Cleo_TerraFirma_RockSweeper o && o.MaxWeight == MaxWeight && o.SeekRadius == SeekRadius && base.SameAs(p);
		}

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("EnteredCell");
			base.Register(Object, Registrar);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade))
				return ID == SingletonEvent<BeforeTakeActionEvent>.ID;
			return true;
		}

		public override bool HandleEvent(BeforeTakeActionEvent E)
		{
			TrySeek();
			return base.HandleEvent(E);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == "EnteredCell")
				TrySweep();
			return base.FireEvent(E);
		}

		private bool IsEdibleStone(GameObject obj)
		{
			return obj != null
				&& obj.HasTag("Cleo_TerraFirma_IsAnyStone")
				&& obj.Physics != null && obj.Physics.Takeable
				&& obj.InInventory == null && obj.Equipped == null
				&& !obj.IsCombat && obj.Weight < MaxWeight;
		}

		private bool CellHasEdibleStone(Cell C)
		{
			foreach (GameObject obj in C.Objects)
			{
				if (IsEdibleStone(obj))
					return true;
			}
			return false;
		}

		private void TrySeek()
		{
			GameObject who = ParentObject;
			if (who == null || who.CurrentCell == null || who.Brain == null || who.IsPlayerControlled())
				return;
			if (SeekRest > 0)
			{
				SeekRest--;
				return;
			}
			if (who.Brain.HasGoal("MoveTo"))
				return;
			Cell here = who.CurrentCell;
			Cell best = null;
			int bestDist = SeekRadius + 1;
			foreach (Cell c in here.GetLocalAdjacentCells(SeekRadius))
			{
				if (c.IsSolid())
					continue;
				int dist = Math.Max(Math.Abs(c.X - here.X), Math.Abs(c.Y - here.Y));
				if (dist < bestDist && CellHasEdibleStone(c))
				{
					best = c;
					bestDist = dist;
				}
			}
			if (best == null)
				return;
			GameObject target = who.Brain.Target;
			if (GameObject.Validate(ref target) && who.DistanceTo(target) <= bestDist)
				return;
			if (best == LastSeekTarget)
				SeekTries++;
			else
			{
				LastSeekTarget = best;
				SeekTries = 1;
			}
			if (SeekTries > 3)
			{
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("HOUND", $"{who.Blueprint} gave up on stone at d={bestDist} (3 failed seeks) -> nose rests 20 turns");
				SeekRest = 20;
				LastSeekTarget = null;
				SeekTries = 0;
				return;
			}
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("HOUND", $"{who.Blueprint} beelines to loose stone at d={bestDist}");
			who.Brain.PushGoal(new MoveTo(best, careful: false, overridesCombat: true));
		}

		private void TrySweep()
		{
			GameObject who = ParentObject;
			if (who == null || who.CurrentCell == null || who.IsPlayerControlled())
				return;
			foreach (GameObject item in who.CurrentCell.GetObjectsWithPart("Physics"))
			{
				if (item == who || item.IsCombat)
					continue;
				if (!item.HasTag("Cleo_TerraFirma_IsAnyStone"))
					continue;
				if (!item.IsTakeable() || item.Weight >= MaxWeight)
					continue;
				if (who.FireEvent(Event.New("CommandTakeObject", "Object", item).SetSilent(Silent: true)))
				{
					item.SetIntProperty("NoAIEquip", 1);
					DidXToY("crunch", item);
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("HOUND", $"{who.Blueprint} swallowed {item.Blueprint} (gut now {GutCount(who)})");
				}
			}
		}

		private static int GutCount(GameObject who)
		{
			int n = 0;
			List<GameObject> pack = who.Inventory?.GetObjectsDirect();
			if (pack != null)
				foreach (GameObject item in pack)
					if (item.HasTag("Cleo_TerraFirma_IsAnyStone"))
						n += item.Count;
			return n;
		}
	}
}
