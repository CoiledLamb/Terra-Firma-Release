using System;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_CobblefurCloak : IPart
	{
		public int Bonus = 2;

		public override bool SameAs(IPart p)
		{
			return false;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != EquippedEvent.ID && ID != UnequippedEvent.ID)
				return ID == GetShortDescriptionEvent.ID;
			return true;
		}

		public override bool HandleEvent(EquippedEvent E)
		{
			if (E.Item.IsEquippedProperly())
			{
				E.Actor.RegisterEvent(this, EnteredCellEvent.ID, 0, Serialize: true);
				E.Actor.RegisterEvent(this, EndTurnEvent.ID, 0, Serialize: true);
				CheckRubble();
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(UnequippedEvent E)
		{
			E.Actor.UnregisterEvent(this, EnteredCellEvent.ID);
			E.Actor.UnregisterEvent(this, EndTurnEvent.ID);
			base.StatShifter.RemoveStatShifts(E.Actor);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EnteredCellEvent E)
		{
			CheckRubble();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			CheckRubble();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.AppendRules(Bonus.Signed() + " DV while amid loose rubble");
			return base.HandleEvent(E);
		}

		private static bool CellHasRubble(Cell C)
		{
			if (C == null)
				return false;
			foreach (GameObject obj in C.Objects)
			{
				if (obj.HasTag("Cleo_TerraFirma_IsAnyStone") || obj.HasTag("Cleo_TerraFirma_IsRubbleCover"))
					return true;
			}
			return false;
		}

		private void CheckRubble()
		{
			GameObject wearer = ParentObject.Equipped;
			Cell cell = wearer?.CurrentCell;
			if (cell == null)
				return;
			bool amidRubble = CellHasRubble(cell);
			if (!amidRubble)
			{
				foreach (Cell c in cell.GetLocalAdjacentCells())
				{
					if (CellHasRubble(c))
					{
						amidRubble = true;
						break;
					}
				}
			}
			if (amidRubble)
			{
				base.StatShifter.DefaultDisplayName = "rubble cover";
				base.StatShifter.SetStatShift(wearer, "DV", Bonus);
			}
			else
			{
				base.StatShifter.RemoveStatShifts(wearer);
			}
		}
	}
}
