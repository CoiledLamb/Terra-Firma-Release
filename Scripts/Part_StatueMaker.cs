using Cleo.TerraFirma.Scripts;
using System;
using System.Collections.Generic;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_StatueMakerPart : IPart
	{
		private const string COMMAND_ID = "Cleo_TerraFirma_CommandActivateStatueMaker";
		private const string DEBUG_CONTEXT = "STM";

		public override bool WantEvent(int ID, int Cascade)
		{
			if (!base.WantEvent(ID, Cascade) &&
				ID != GetInventoryActionsEvent.ID)
				return ID == InventoryActionEvent.ID;
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Activate", "activate", COMMAND_ID, null, Key: 'a', Default: 10);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			Helpers.DebugLog($"taking inventory action: {E.Command}", DEBUG_CONTEXT);
			if (E.Command != COMMAND_ID || E.Actor == null || !E.Actor.IsPlayer())
			{
				Helpers.DebugLog($"-> invalid ({E.Command != COMMAND_ID} || {E.Actor == null} || {!E.Actor.IsPlayer()})", DEBUG_CONTEXT);
				return base.HandleEvent(E);
			}
			if (E.Actor.OnWorldMap())
			{
				Helpers.DebugLog($"-> on world map", DEBUG_CONTEXT);
				E.Actor.ShowFailure("You attempt to sculpt a statue of Qud…");
				E.Actor.ShowFailure("But alas. You are no geographer.");
				return base.HandleEvent(E);
			}
			ParentObject.SplitFromStack();
		pickCell:
			Helpers.DebugLog($"picking cell…", DEBUG_CONTEXT);
			Cell cell = E.Actor.Physics.PickDestinationCell(1, RequireCombat: false, Label: "Choose any adjacent wall.");
			if (cell == null)
			{
				Helpers.DebugLog($"-> is null", DEBUG_CONTEXT);
				ParentObject.CheckStack();
				return false;
			}
			if (!cell.HasWall() || !cell.IsAdjacentTo(E.Actor.CurrentCell))
			{
				Helpers.DebugLog($"-> no wall or not adjacent ({!cell.HasWall()}, {!cell.IsAdjacentTo(E.Actor.CurrentCell)}", DEBUG_CONTEXT);
				E.Actor.ShowFailure("You must choose an adjacent wall.");
				goto pickCell;
			}
		targetSelection:
			Helpers.DebugLog($"picking creature…", DEBUG_CONTEXT);
			Cell targetCreatureCell = E.Actor.Physics.PickDestinationCell(Snap: true, Label: "Choose a creature to sculpt a statue of.");
			if (targetCreatureCell == null)
			{
				Helpers.DebugLog($"-> is null", DEBUG_CONTEXT);
				ParentObject.CheckStack();
				return false;
			}
			GameObject creature = targetCreatureCell.GetCombatTarget(E.Actor, true);
			if (creature == null || !creature.IsCreature)
			{
				Helpers.DebugLog($"-> no creature", DEBUG_CONTEXT);
				E.Actor.Fail("You must target a cell containing a creature.");
				goto targetSelection;
			}
			Helpers.DebugLog($"sculpting", DEBUG_CONTEXT);
			GameObject wall = cell.GetFirstWall();
			GameObject statue = Mutation.Cleo_TerraFirma_Stoneshaper.MakeStatueOfCreatureFromWall(creature, wall, string.Empty, wall.GetPart<Cleo_TerraFirma_StoneshaperWallProperties>() ?? null);
			wall.Obliterate();
			cell.AddObject(statue);
			XDidYToZ(E.Actor, "create a sculpture of", creature, EndMark: "!", ColorAsGoodFor: E.Actor, Source: E.Actor);
			PlayWorldSound("Sounds/Throw/sfx_throwing_stone_small_impact", 1f, SourceCell: cell);
			ParentObject.Destroy();
			E.Actor.UseEnergy(1000, Name);
			E.RequestInterfaceExit();
			return base.HandleEvent(E);
		}
	}
}
