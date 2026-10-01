using System;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_SpallSandals : IPart
	{
		public int MS = 10;

		public bool StoneOnly = true;

		public bool ShowInShortDescription = true;

		public int CurrentBonus;

		public override bool SameAs(IPart p)
		{
			Cleo_TerraFirma_SpallSandals other = p as Cleo_TerraFirma_SpallSandals;
			if (other.MS != MS || other.StoneOnly != StoneOnly || other.ShowInShortDescription != ShowInShortDescription)
				return false;
			return base.SameAs(p);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == EquippedEvent.ID
				|| ID == UnequippedEvent.ID
				|| ID == GetShortDescriptionEvent.ID;
		}

		public override bool HandleEvent(EquippedEvent E)
		{
			E.Actor.RegisterPartEvent(this, "EndTurn");
			E.Actor.RegisterPartEvent(this, "EnteredCell");
			CurrentBonus = 0;
			CheckWalls();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(UnequippedEvent E)
		{
			if (CurrentBonus > 0)
			{
				if (E.Actor != null)
					StatShifter.RemoveStatShift(E.Actor, "MoveSpeed");
				CurrentBonus = 0;
			}
			E.Actor.UnregisterPartEvent(this, "EndTurn");
			E.Actor.UnregisterPartEvent(this, "EnteredCell");
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			if (ShowInShortDescription && MS != 0)
			{
				E.Postfix.Append("\n&C");
				if (MS > 0)
					E.Postfix.Append('+');
				E.Postfix.Append(MS).Append(" move speed while adjacent to ")
					.Append(StoneOnly ? "a stone wall" : "a wall");
			}
			return base.HandleEvent(E);
		}

		private bool HasQualifyingWallAdjacent(Cell cell)
		{
			foreach (Cell adjacent in cell.GetAdjacentCells())
			{
				for (int i = 0, count = adjacent.Objects.Count; i < count; i++)
				{
					GameObject obj = adjacent.Objects[i];
					if (!obj.IsWall())
						continue;
					if (!StoneOnly || obj.GetPart<Cleo_TerraFirma_StoneshaperWallProperties>()?.MaterialType == "stone")
						return true;
				}
			}
			return false;
		}

		private void CheckWalls()
		{
			GameObject equipped = ParentObject.Equipped;
			Cell cell = equipped?.CurrentCell;
			if (cell == null)
				return;
			if (HasQualifyingWallAdjacent(cell))
			{
				if (CurrentBonus == 0)
				{
					CurrentBonus = MS;
					StatShifter.SetStatShift(equipped, "MoveSpeed", -MS);
				}
			}
			else if (CurrentBonus > 0)
			{
				StatShifter.RemoveStatShift(equipped, "MoveSpeed");
				CurrentBonus = 0;
			}
		}

		public override bool AllowStaticRegistration()
		{
			return false;
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == "EnteredCell" || E.ID == "EndTurn")
				CheckWalls();
			return base.FireEvent(E);
		}
	}
}
