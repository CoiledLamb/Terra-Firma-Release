using Cleo.TerraFirma.Scripts;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XRL.World.Effects;

namespace XRL.World.Parts.Mutation
{
	public class Cleo_TerraFirma_Geomancy : BaseMutation
	{
		private const string DEBUG_CONTEXT = "GMC";

		private const float POWER_PER_TILE = 90f;
		private const float POWER_PER_TURN = 35f;

		private const float MIN_RADIUS_TILES = 3f;
		private const float MIN_DURATION_TURNS = 7f;

		[NonSerialized]
		public List<Cleo_TerraFirma_GeomancyCellData> Cells = new();

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) &&
				ID != PooledEvent<GetThrowProfileEvent>.ID &&
				ID != SingletonEvent<EndTurnEvent>.ID)
				return ID == BeforeRenderEvent.ID;
			return true;
		}

		public override bool HandleEvent(BeforeRenderEvent E)
		{
			long now = Cleo_TerraFirma_GeomancyPulseFX.NowMS;
			foreach (Cleo_TerraFirma_GeomancyCellData cd in Cells)
			{
				if (cd.RevealAtMS > now)
					continue;
				if (!cd.MapMarked)
				{
					cd.Cell.SetExplored();
					cd.MapMarked = true;
				}
				cd.Cell.ParentZone.AddLight(cd.Cell.X, cd.Cell.Y, 0, LightLevel.Omniscient);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			foreach (Cleo_TerraFirma_GeomancyCellData cd in Cells.ToArray())
			{
				cd.Duration--;
				if (cd.Duration <= 0)
					Cells.Remove(cd);
				if (cd.DoScan)
				{
					foreach (GameObject go in cd.Cell.GetObjects(x => VibesWith(x)))
					{
						if (go == ParentObject)
							continue;
						if (go.TryGetPart<Cleo_TerraFirma_PartGeomancyScan>(out var vibes))
							vibes.Duration = 2;
						else
							go.AddPart<Cleo_TerraFirma_PartGeomancyScan>();
					}
				}
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetThrowProfileEvent E)
		{
			E.Range += 2;
			return base.HandleEvent(E);
		}

		public void OnStoneThrown(GameObject Weapon)
		{
			Helpers.DebugLog($"handling landed stone", DEBUG_CONTEXT);
			Helpers.DebugLog($"-> weapon: {Weapon?.DisplayName}", DEBUG_CONTEXT);
			if (Weapon == null || (!Weapon.HasTag("Cleo_TerraFirma_IsAnyStone") && !Weapon.HasTag("Cleo_TerraFirma_IsGeomancyStone")))
				return;
			Cell landingCell = Weapon.CurrentCell;
			if (landingCell == null || landingCell.OnWorldMap())
				return;
			if (!ParentObject.IsRealityDistortionUsable())
			{
				RealityStabilized.ShowGenericInterdictMessage(ParentObject);
				return;
			}
			Event e = Event.New("InitiateRealityDistortionTransit", "Object", ParentObject, "Mutation", this, "Cell", landingCell);
			if (!ParentObject.FireEvent(e) || !landingCell.FireEvent(e))
				return;
			float powerScaler = Mathf.Max(Weapon.WeightEach, (float)Weapon.ValueEach);
			int radiusRevealed = (int)Mathf.Max(MIN_RADIUS_TILES, Mathf.Ceil(powerScaler / POWER_PER_TILE));
			int durationTurns = (int)Mathf.Max(MIN_DURATION_TURNS, Mathf.Ceil(powerScaler / POWER_PER_TURN));
			bool doScan = Weapon.HasTag("Cleo_TerraFirma_IsGemstone");
			var pickedRadius = landingCell.GetLocalAdjacentCellsCircular(radiusRevealed, true).Where(x =>
				!x.OnWorldMap() &&
				CheckRealityDistortionAccessibility(Cell: x, Actor: ParentObject, Mutation: this)
			).ToList();
			if (ParentObject.IsPlayer())
				DidX("listen", "for vibrations", ColorAsGoodFor: ParentObject);
			Helpers.DebugLog($"---> radius, duration, do scan: {radiusRevealed}, {durationTurns}, {doScan}", DEBUG_CONTEXT);
			Helpers.VerifyLog("GEO", $"reveal: {Weapon.Blueprint} landed, radius {radiusRevealed}, {durationTurns} turns, scan={doScan}, {pickedRadius.Count} cells pass the distortion mask");
			bool animate = !Cleo_TerraFirma_StoneRiseFX.HarnessMute && landingCell.ParentZone != null && landingCell.ParentZone.IsActive();
			long now = animate ? Cleo_TerraFirma_GeomancyPulseFX.NowMS : 0L;
			List<(Cell cell, float dist)> pulseCells = animate ? new List<(Cell cell, float dist)>(pickedRadius.Count) : null;
			float maxDist = 0f;
			foreach (Cell c in pickedRadius)
			{
				var cd = new Cleo_TerraFirma_GeomancyCellData(c, durationTurns, doScan);
				if (animate)
				{
					int dx = c.X - landingCell.X, dy = c.Y - landingCell.Y;
					float dist = Mathf.Sqrt(dx * dx + dy * dy);
					cd.RevealAtMS = now + (long)(dist * Cleo_TerraFirma_GeomancyPulseFX.RING_MS);
					pulseCells.Add((c, dist));
					if (dist > maxDist)
						maxDist = dist;
				}
				else
				{
					c.SetExplored();
					cd.MapMarked = true;
				}
				Cells.Add(cd);
				if (doScan)
				{
					foreach (GameObject go in c.GetObjects(x => x != ParentObject && VibesWith(x) && !x.HasPart<Cleo_TerraFirma_PartGeomancyScan>()))
						go.AddPart<Cleo_TerraFirma_PartGeomancyScan>();
				}
			}
			if (animate)
			{
				int dots = Cleo_TerraFirma_GeomancyPulseFX.Play(landingCell, maxDist, pulseCells);
				Helpers.VerifyLog("GEO", $"pulse: {pickedRadius.Count} cells unveiling over {(long)(maxDist * Cleo_TerraFirma_GeomancyPulseFX.RING_MS)}ms, sparks={dots}, classic={Cleo_TerraFirma_GeomancyPulseFX.CLASSIC_FRONT}");
			}
			else
				Helpers.VerifyLog("GEO", "pulse: skipped (muted or inactive zone), instant reveal");
		}

		public override bool CanLevel() => false;

		public override string GetDescription()
		{
			return "You sense minute details through earthly vibrations.\n\n" +
				"Stones you throw reveal the area around their point of impact.\n" +
				"Power equals the stone's sell value or weight, whichever is higher.\n" +
				"Vision radius: 1 tile per " + (int)POWER_PER_TILE + " power (minimum " + (int)MIN_RADIUS_TILES + ")\n" +
				"Vision duration: 1 round per " + (int)POWER_PER_TURN + " power (minimum " + (int)MIN_DURATION_TURNS + ")\n" +
				"Gemstones also apply full scanning to creatures and walls in the revealed area.\n" +
				"+2 thrown weapon range";
		}

		public override string GetLevelText(int Level)
		{
			return "";
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.Write(Cells.Count);
			foreach (Cleo_TerraFirma_GeomancyCellData cd in Cells)
			{
				Writer.Write(cd.Cell.ParentZone.ZoneID);
				Writer.Write(cd.Cell.X);
				Writer.Write(cd.Cell.Y);
				Writer.Write(cd.Duration);
				Writer.Write(cd.DoScan);
			}
			base.Write(Basis, Writer);
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			int numOfCells = Reader.ReadInt32();
			Cells.Clear();
			for (int i = 0; i < numOfCells; i++)
			{
				string zoneId = Reader.ReadString();
				int x = Reader.ReadInt32();
				int y = Reader.ReadInt32();
				int duration = Reader.ReadInt32();
				bool doScan = Reader.ReadBoolean();
				Cell cell = The.ZoneManager.GetZone(zoneId).GetCell(x, y);
				Cells.Add(new Cleo_TerraFirma_GeomancyCellData(cell, duration, doScan));
			}
			base.Read(Basis, Reader);
		}

		public static bool VibesWith(GameObject Object) => (Object.IsCreature || Object.IsWall());

		public class Cleo_TerraFirma_GeomancyCellData
		{
			public Cell Cell;
			public int Duration;
			public bool DoScan;

			public long RevealAtMS;
			public bool MapMarked;

			public Cleo_TerraFirma_GeomancyCellData(Cell cell, int duration, bool doScan)
			{
				Cell = cell;
				Duration = duration;
				DoScan = doScan;
			}
		}
	}
}
