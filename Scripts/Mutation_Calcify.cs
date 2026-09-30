using System;
using System.Collections.Generic;
using System.Threading;
using ConsoleLib.Console;
using UnityEngine;
using XRL.Core;
using XRL.Rules;
using XRL.UI;
using XRL.World.Capabilities;

namespace XRL.World.Parts.Mutation
{
	public class Cleo_TerraFirma_Calcify : BaseMutation
	{
		private const string COMMAND_ID = "Cleo_TerraFirma_CommandCalcify";

		private const int BASE_WALLS_CREATED = 4;

		private const int COOLDOWN_MAX = 100;
		private const int COOLDOWN_STEP = 5;
		private const int COOLDOWN_FLOOR = 50;

		public override string GetDescription()
		{
			return "You raise walls of solid stone.";
		}

		public override string GetLevelText(int Level)
		{
			return "Creates a contiguous line of walls in empty tiles starting adjacent to you.\n" +
				"Number created: {{rules|" + GetWallsCreated(Level) + "}}\n" +
				"Cooldown: {{rules|" + GetCooldown(Level) + "}} rounds";
		}

		public override bool Mutate(GameObject GO, int Level = 1)
		{
			ActivatedAbilityID = AddMyActivatedAbility("Calcify", COMMAND_ID, "Physical Mutations");
			return base.Mutate(GO, Level);
		}

		public override bool Unmutate(GameObject GO)
		{
			RemoveMyActivatedAbility(ref ActivatedAbilityID);
			return base.Unmutate(GO);
		}

		public override void CollectStats(Templates.StatCollector stats, int Level)
		{
			stats.Set("WallNum", GetWallsCreated(Level));
			stats.CollectCooldownTurns(MyActivatedAbility(ActivatedAbilityID), GetCooldown(Level));
		}

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != AIGetDefensiveAbilityListEvent.ID && ID != AIGetRetreatAbilityListEvent.ID && ID != PooledEvent<CheckExistenceSupportEvent>.ID)
				return ID == PooledEvent<CommandEvent>.ID;
			return true;
		}

		public override bool HandleEvent(CheckExistenceSupportEvent E)
		{
			if (E.Object?.HasPart<Cleo_TerraFirma_CalcifyWallPart>() == true)
				return false;
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(AIGetDefensiveAbilityListEvent E)
		{
			GameObject threat;
			if (IsMyActivatedAbilityAIUsable(ActivatedAbilityID) && (threat = FindWallThreat(E.Target)) != null)
				E.Add(COMMAND_ID, 1, null, Inv: false, Self: false, threat);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(AIGetRetreatAbilityListEvent E)
		{
			GameObject threat;
			if (IsMyActivatedAbilityAIUsable(ActivatedAbilityID) && (threat = FindWallThreat(E.Target)) != null)
				E.Add(COMMAND_ID, 1, null, Inv: false, Self: false, threat);
			return base.HandleEvent(E);
		}

		private GameObject FindWallThreat(GameObject given)
		{
			if (GameObject.Validate(ref given))
				return given;
			GameObject nearest = null;
			int nearestDistance = int.MaxValue;
			ParentObject.CurrentZone?.ForeachObject(delegate(GameObject obj)
			{
				if (obj.IsCreature && obj != ParentObject && ParentObject.IsHostileTowards(obj)
					&& ParentObject.HasLOSTo(obj, IncludeSolid: true, BlackoutStops: false, UseTargetability: true))
				{
					int d = ParentObject.DistanceTo(obj);
					if (d < nearestDistance)
					{
						nearest = obj;
						nearestDistance = d;
					}
				}
			});
			return nearest;
		}

		private List<Cell> PickNpcWallLine(GameObject threat)
		{
			Cell here = ParentObject.CurrentCell;
			Cell there = threat?.CurrentCell;
			if (here == null || there == null || here.ParentZone != there.ParentZone)
				return null;
			string dir = here.GetDirectionFromCell(there);
			Cell anchor = here.GetCellFromDirection(dir);
			if (anchor == null || anchor.ParentZone != here.ParentZone)
				return null;
			int walls = GetWallsCreated(Level);
			List<Cell> line = new List<Cell>(walls) { anchor };
			string[] flanks = XRL.Rules.Directions.GetOrthogonalDirections(dir);
			Cell left = anchor, right = anchor;
			while (line.Count < walls && (left != null || right != null))
			{
				left = left?.GetCellFromDirection(flanks[0]);
				if (left != null && left.ParentZone != here.ParentZone)
					left = null;
				if (left != null && line.Count < walls)
					line.Add(left);
				right = right?.GetCellFromDirection(flanks[1]);
				if (right != null && right.ParentZone != here.ParentZone)
					right = null;
				if (right != null && line.Count < walls)
					line.Add(right);
			}
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CALC", $"NPC cast: {ParentObject.Blueprint} walls off {threat.Blueprint} ({dir}ward), {line.Count} candidate cells");
			return line;
		}

		public override bool HandleEvent(CommandEvent E)
		{
			if (E.Command != COMMAND_ID)
				return base.HandleEvent(E);
			List<Cell> targeted = null;
			if (ParentObject.IsPlayer())
				targeted = PickFieldAdjacent(GetWallsCreated(Level), ParentObject);
			else
				targeted = PickNpcWallLine(E.Target ?? ParentObject.Target);
			if (targeted.IsNullOrEmpty())
				return false;
			string bp = ParentObject.CurrentZone.GetDefaultWall();
			string rawDefault = bp;
			if (bp != null)
			{
				GameObjectBlueprint defaultWall = GameObjectFactory.Factory.GetBlueprintIfExists(bp);
				if (defaultWall == null
					|| defaultWall.GetPartParameter<string>("Cleo_TerraFirma_StoneshaperWallProperties", "MaterialType") != "stone"
					|| !defaultWall.GetPartParameter("Cleo_TerraFirma_StoneshaperWallProperties", "Natural", false))
					bp = null;
			}
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CALC", $"stone call: zone default wall={rawDefault ?? "none"}, vet {(bp == null ? "REFUSED (not natural stone) -- fallback/borrow decides" : "passed")}");
			List<Cell> planCells = new List<Cell>();
			List<string> planBps = new List<string>();
			foreach (Cell cell in targeted)
			{
				if (!cell.IsEmpty())
					continue;
				foreach (Cell c in cell.GetCardinalAdjacentCellsWhere(x => x.HasWall()))
				{
					var wallProps = c.GetFirstWall().GetPart<Cleo_TerraFirma_StoneshaperWallProperties>();
					if (Cleo_TerraFirma_StoneshaperWallProperties.IsNaturalStone(wallProps))
					{
						bp = wallProps.ParentObject.Blueprint;
						break;
					}
				}
				planCells.Add(cell);
				planBps.Add(bp ?? "Cleo_TerraFirma_WallCalcify");
			}
			if (planCells.Count == 0)
				return ParentObject.Fail("You need empty spaces to calcify.");
			UseEnergy(1000, $"Physical Mutation {Name}");
			CooldownMyActivatedAbility(ActivatedAbilityID, GetCooldown(Level));
			PlayWorldSound("Sounds/Throw/sfx_throwing_stone_large_impact", 1f, Combat: true);
			if (ParentObject.IsVisible())
				CombatJuice.cameraShake(0.15f);
			void PlaceWall(int i)
			{
				GameObject wall = GameObject.Create(planBps[i]);
				wall.AddPart(new Cleo_TerraFirma_CalcifyWallPart());
				Phase.carryOver(ParentObject, wall);
				if (ParentObject.HasStringProperty("FugueCopy"))
				{
					XRL.World.Parts.ExistenceSupport support = wall.RequirePart<XRL.World.Parts.ExistenceSupport>();
					support.SupportedBy = ParentObject;
					support.ValidateEveryTurn = true;
				}
				planCells[i].AddObject(wall);
				if (wall.TryGetPart(out XRL.World.Parts.ShaleVines vines))
					vines.GrowVines();
				string paintedTile = wall.Render?.Tile;
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CALC", $"wall landed @({planCells[i].X},{planCells[i].Y}): bp={planBps[i]}, post-paint Render.Tile={(paintedTile.IsNullOrEmpty() ? "NULL (paint did not take)" : paintedTile)}");
			}
			Cleo.TerraFirma.Scripts.Cleo_TerraFirma_StoneRiseFX.Schedule schedule = null;
			Zone zone = ParentObject.CurrentZone;
			if (!Cleo.TerraFirma.Scripts.Cleo_TerraFirma_StoneRiseFX.HarnessMute && zone != null && zone.IsActive() && ParentObject.CurrentCell?.IsVisible() == true)
				schedule = Cleo.TerraFirma.Scripts.Cleo_TerraFirma_StoneRiseFX.PlayWallRise(planCells, planBps);
			if (schedule != null)
			{
				ScreenBuffer buf = ScreenBuffer.GetScrapBuffer1(bLoadFromCurrent: true);
				TextConsole console = Popup._TextConsole;
				float t = 0f;
				float end = schedule.Total + 0.08f;
				int placed = 0;
				while (t < end)
				{
					while (placed < planCells.Count
						&& (!schedule.SettleTimes.TryGetValue(planCells[placed], out float settleAt) || settleAt <= t))
						PlaceWall(placed++);
					XRLCore.Core.RenderBaseToBuffer(buf);
					console.DrawBuffer(buf);
					Thread.Sleep(40);
					t += 0.04f;
				}
				while (placed < planCells.Count)
					PlaceWall(placed++);
			}
			else
			{
				for (int i = 0; i < planCells.Count; i++)
					PlaceWall(i);
			}
			if (planCells.Count > 0)
			{
				int puffs = Stat.Random(1, 4);
				for (int i = 0; i < puffs; i++)
					Cleo_TerraFirma_Fissure.SmokePuff(planCells[Stat.Random(0, planCells.Count - 1)]);
			}
			if (planCells.Count > 0)
			{
				int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
				foreach (Cell c in planCells)
				{
					minX = Math.Min(minX, c.X); maxX = Math.Max(maxX, c.X);
					minY = Math.Min(minY, c.Y); maxY = Math.Max(maxY, c.Y);
				}
				ZoneManager.PaintWalls(zone, minX - 1, minY - 1, maxX + 1, maxY + 1);
			}
			DidX("calcify", "loose matter", ColorAsGoodFor: ParentObject, Source: ParentObject);
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CALC", $"cast Lv{Level}: {planCells.Count} wall(s) raised of {targeted.Count} targeted (formula {GetWallsCreated(Level)}), true-copy stats (rebalance), blueprint={bp ?? "FALLBACK Cleo_TerraFirma_WallCalcify"}, animated={(schedule != null ? schedule.Total.ToString("0.00") + "s pick-order ripple" : "no")}{(ParentObject.HasStringProperty("FugueCopy") ? ", fugue-tethered (walls die with the clone)" : "")}");
			return base.HandleEvent(E);
		}

		public static int GetWallsCreated(int Lv) => (int)(BASE_WALLS_CREATED + Mathf.Floor(Lv / 2f));

		public static int GetCooldown(int Lv) => Math.Max(COOLDOWN_MAX - COOLDOWN_STEP * ((Lv + 1) / 2), COOLDOWN_FLOOR);
	}
}
