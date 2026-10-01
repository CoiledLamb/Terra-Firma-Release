using System;
using System.Collections.Generic;
using System.Threading;
using Cleo.TerraFirma.Scripts;
using ConsoleLib.Console;
using XRL.Core;
using XRL.Rules;
using XRL.UI;
using XRL.World.Effects;
using XRL.World.Parts;

namespace XRL.World.Parts.Mutation
{
	[Serializable]
	public class Cleo_TerraFirma_Fissure : BaseMutation
	{
		private const string COMMAND_ID = "Cleo_TerraFirma_CommandFissure";
		private const int RING_RADIUS = 3;
		private const string SLAM_SOUND = "Sounds/Throw/sfx_throwing_stone_large_impact";
		private const string DEBRIS_BLUEPRINT = "SmallBoulder";
		private const string RUBBLE_BLUEPRINT = "Cleo_TerraFirma_Rock_Rubble";
		private const string CRUMBLED_BLUEPRINT = "Cleo_TerraFirma_Rock_CrumbledWall";
		private const string WALL_BLUEPRINT = "Cleo_TerraFirma_Rock_Wall";
		private static readonly string[] SMOKE_COLORS = { "&K", "&y", "&w" };

		public override string GetDescription()
		{
			return "You send pulses through the earth, raising a ring of broken stone.";
		}

		public override string GetLevelText(int Level)
		{
			return "Ring radius: " + GetRingRadius(Level) + " squares\n" +
				"Damage: {{rules|" + GetPulseDice(Level, 1) + "/" + GetPulseDice(Level, 2) + "/" + GetPulseDice(Level, 3) + "}} in three spreading pulses\n" +
				"Creatures caught in its path are pushed outward and must make a Toughness save or be stunned for {{rules|" + GetStunRounds(Level) + "}} " + (GetStunRounds(Level) == 1 ? "round" : "rounds") + ".\n" +
				"Cooldown: " + GetCooldown(Level) + " rounds";
		}

		public override bool Mutate(GameObject GO, int Level = 1)
		{
			ActivatedAbilityID = AddMyActivatedAbility("Fissure", COMMAND_ID, "Physical Mutations");
			return base.Mutate(GO, Level);
		}

		public override bool Unmutate(GameObject GO)
		{
			RemoveMyActivatedAbility(ref ActivatedAbilityID);
			return base.Unmutate(GO);
		}

		public override void CollectStats(Templates.StatCollector stats, int Level)
		{
			stats.Set("RingRadius", GetRingRadius(Level));
			stats.Set("WaveDamage", GetPulseDice(Level, 1) + "/" + GetPulseDice(Level, 2) + "/" + GetPulseDice(Level, 3));
			stats.CollectCooldownTurns(MyActivatedAbility(ActivatedAbilityID), GetCooldown(Level));
		}

		public static int GetBand(int Lv) => 1 + (Lv - 1) / 3;

		public static int GetRingRadius(int Lv) => RING_RADIUS;

		public static int GetGapCount(int Lv) => Math.Max(0, 7 - GetBand(Lv));

		private static bool InDisk(int dx, int dy, int r) => dx * dx + dy * dy <= r * r + r;

		private static bool OnRing(int dx, int dy, int r)
		{
			int d2 = dx * dx + dy * dy;
			return d2 > r * r - r && d2 <= r * r + r;
		}

		public static int GetCarvePV(int Lv) => 6 + 3 * Lv;

		public const int CARVE_PCT_PER_PEN = 34;

		public const int PULSE_COUNT = 3;

		public static string GetPulseDice(int Lv, int pulse)
		{
			int steps = Lv >= pulse + 1 ? (Lv - pulse - 1) / 4 + 1 : 0;
			int count = (4 - pulse) + steps / 2;
			int die = 3 + (steps + 1) / 2;
			return count + "d" + die;
		}

		public static int GetCreaturePVFor(GameObject who) => who?.StatMod("Strength") ?? 0;

		public int GetCreaturePV() => GetCreaturePVFor(ParentObject);

		public static int GetStunRounds(int Lv) => 1 + (Lv - 1) / 4;

		public static int GetStunSaveDC(int Lv) => 16 + 2 * ((Lv - 1) / 4);

		public static int GetPushForce(int Lv) => (2 + Lv) * 1000;

		public static int GetCooldown(int Lv) => 100;

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != PooledEvent<CommandEvent>.ID && ID != PooledEvent<CheckExistenceSupportEvent>.ID)
				return ID == AIGetOffensiveAbilityListEvent.ID;
			return true;
		}

		public override bool HandleEvent(AIGetOffensiveAbilityListEvent E)
		{
			bool usable = IsMyActivatedAbilityAIUsable(ActivatedAbilityID);
			bool added = false;
			if (usable)
			{
				if (E.Distance <= 1)
					added = true;
				else if (E.Distance <= PULSE_COUNT && Stat.Random(1, 100) <= 50)
					added = true;
				if (added)
					E.Add(COMMAND_ID);
			}
			if (!ParentObject.IsPlayer())
				Helpers.VerifyLog("FIS", $"AI gate: {ParentObject.Blueprint} dist {E.Distance}, usable={usable} -> {(added ? "ADVERTISED" : "held")}");
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(CommandEvent E)
		{
			if (E.Command != COMMAND_ID)
				return base.HandleEvent(E);
			if (ParentObject.OnWorldMap())
			{
				E.Actor?.Fail("You cannot do that on the world map.");
				return false;
			}
			Cell origin = ParentObject.CurrentCell;
			Zone zone = origin?.ParentZone;
			if (zone == null)
				return false;
			int radius = GetRingRadius(Level);
			int creaturePV = GetCreaturePV();
			UseEnergy(1000, "Physical Mutation Fissure");
			CooldownMyActivatedAbility(ActivatedAbilityID, GetCooldown(Level));
			ParentObject.PlayWorldSound(SLAM_SOUND, 1f, Combat: true);
			XDidY(ParentObject, "drive", "a wave through the earth", "!", ColorAsGoodFor: ParentObject);
			Helpers.VerifyLog("FIS", $"PULSE cast by {ParentObject.Blueprint} Lv{Level}: radius={radius}, dice={GetPulseDice(Level, 1)}/{GetPulseDice(Level, 2)}/{GetPulseDice(Level, 3)}, creaturePV={creaturePV} (Str mod; engine's +4 is display-only), wallPV={GetCarvePV(Level)}, stun={GetStunRounds(Level)}r DC{GetStunSaveDC(Level)}");

			PulseWake wake = RunPulses(origin, ParentObject, ParentObject, Level, "FIS");

			HashSet<Cell> reached = Sweep(zone, origin, radius, PULSE_COUNT + 1, radius, null, wake, ParentObject, Level, "FIS");
			int carvedMass = wake.Carved.Count;
			Helpers.VerifyLog("FIS", $"front pass: {reached.Count} cell(s) reached to the rim, carvedMass={carvedMass}, ownFelled={wake.OwnFelled}");

			var rim = new List<Cell>();
			var rimAll = new List<Cell>();
			foreach (Cell c in reached)
			{
				if (OnRing(c.X - origin.X, c.Y - origin.Y, radius))
				{
					rimAll.Add(c);
					if (!c.IsSolid())
						rim.Add(c);
				}
			}
			bool sealRing = wake.OwnFelled >= GetGapCount(Level);
			var gaps = new List<Cell>();
			if (sealRing)
				Helpers.VerifyLog("FIS", $"ring SEALS: {wake.OwnFelled} own pieces recycled (quota {GetGapCount(Level)}), no gaps rolled");
			else
				for (int g = 0; g < GetGapCount(Level) && rim.Count > 0; g++)
				{
					int pick = Stat.Random(0, rim.Count - 1);
					gaps.Add(rim[pick]);
					rim.RemoveAt(pick);
				}
			int upgradeCredits = carvedMass;
			int wallShare = GetWallShare(Level);
			string dominantPaint = DominantWallPaint(zone, out string paintAtlas, out string paintExt, out char paintFg, out char paintDetail, out char paintBg);
			int deposited = 0, debris = 0;
			var deposits = new List<(Cell cell, string bp, float time)>();
			foreach (Cell c in rim)
			{
				if (StillHoldsCombatant(c) && !TryEvictRimOccupants(c, origin, GetPulseDice(Level, PULSE_COUNT)))
				{
					deposits.Add((c, DEBRIS_BLUEPRINT, 0f));
					debris++;
					continue;
				}
				string fabric = RollFabric(wallShare);
				while (upgradeCredits > 0 && fabric != WALL_BLUEPRINT)
				{
					fabric = fabric == RUBBLE_BLUEPRINT ? CRUMBLED_BLUEPRINT : WALL_BLUEPRINT;
					upgradeCredits--;
				}
				deposits.Add((c, fabric, 0f));
				deposited++;
			}
			int scattered = 0;
			foreach (Cell c in reached)
			{
				int dx = c.X - origin.X;
				int dy = c.Y - origin.Y;
				int ringIdx = RingIndexOf(dx, dy, radius);
				if (ringIdx == 0 || c.IsSolid() || StillHoldsCombatant(c))
					continue;
				int roll = Stat.Random(1, 100);
				string bp = roll <= 10 ? DEBRIS_BLUEPRINT : (roll <= 13 ? "Cleo_TerraFirma_Rock_Pebbles" : (roll <= 15 ? "MediumBoulder" : null));
				if (bp != null)
				{
					deposits.Add((c, bp, (ringIdx - 1) * Cleo_TerraFirma_StoneRiseFX.BEAT_SEC + Cleo_TerraFirma_StoneRiseFX.BOB_RISE));
					scattered++;
				}
			}

			Cleo_TerraFirma_StoneRiseFX.Schedule schedule = null;
			if (!Cleo_TerraFirma_StoneRiseFX.HarnessMute && zone.IsActive() && origin.IsVisible())
				schedule = Cleo_TerraFirma_StoneRiseFX.Play(origin, radius, reached, gaps, dominantPaint, paintAtlas, paintExt, paintFg, paintDetail, paintBg, OnRing);
			void Place((Cell cell, string bp, float time) plan)
			{
				string bp = plan.bp;
				if (bp == WALL_BLUEPRINT)
				{
					GameObject liquid = null;
					foreach (GameObject o in plan.cell.Objects)
					{
						if (o.IsOpenLiquidVolume())
						{
							liquid = o;
							break;
						}
					}
					if (liquid != null)
					{
						Cell dry = null;
						foreach (Cell adj in plan.cell.GetLocalAdjacentCells())
						{
							if (adj.IsSolid())
								continue;
							bool wet = false;
							foreach (GameObject o in adj.Objects)
							{
								if (o.IsOpenLiquidVolume())
								{
									wet = true;
									break;
								}
							}
							if (!wet)
							{
								dry = adj;
								break;
							}
						}
						if (dry != null)
						{
							liquid.RemoveFromContext();
							dry.AddObject(liquid);
							Helpers.VerifyLog("FIS", $"wall over liquid @({plan.cell.X},{plan.cell.Y}): pool displaced to ({dry.X},{dry.Y})");
						}
						else
						{
							bp = RUBBLE_BLUEPRINT;
							Helpers.VerifyLog("FIS", $"wall over liquid @({plan.cell.X},{plan.cell.Y}): no dry neighbor, downgraded to rubble");
						}
					}
				}
				if (bp == WALL_BLUEPRINT)
					SweepLooseItemsAside(plan.cell);
				GameObject piece = GameObject.Create(bp);
				if (bp == WALL_BLUEPRINT)
				{
					if (!dominantPaint.IsNullOrEmpty())
						piece.SetStringProperty("PaintedWall", dominantPaint);
					if (piece.Render != null)
					{
						piece.Render.TileColor = "&" + paintFg;
						piece.Render.DetailColor = paintDetail.ToString();
						piece.Render.ColorString = "&" + paintFg + "^" + paintBg;
					}
				}
				StampOwner(piece);
				plan.cell.AddObject(piece);
				if (bp == WALL_BLUEPRINT)
				{
					string paintedTile = piece.Render?.Tile;
					Helpers.VerifyLog("FIS", $"wall landed @({plan.cell.X},{plan.cell.Y}): post-paint Render.Tile={(paintedTile.IsNullOrEmpty() ? "NULL (paint did not take)" : paintedTile)}");
				}
				bool scatter = bp != WALL_BLUEPRINT && bp != CRUMBLED_BLUEPRINT && bp != RUBBLE_BLUEPRINT;
				if (!scatter)
					SmokePuff(plan.cell, bp == WALL_BLUEPRINT ? 2 : 1);
				else if (25.in100())
					SmokePuff(plan.cell);
			}
			if (schedule != null)
			{
				for (int i = 0; i < deposits.Count; i++)
				{
					if (deposits[i].time <= 0f && schedule.SettleTimes.TryGetValue(deposits[i].cell, out float settleAt))
						deposits[i] = (deposits[i].cell, deposits[i].bp, settleAt);
				}
				deposits.Sort((a, b) => a.time.CompareTo(b.time));
				var gapPuffs = new List<(Cell cell, float time)>();
				foreach (KeyValuePair<Cell, float> kv in schedule.GapTimes)
					gapPuffs.Add((kv.Key, kv.Value));
				gapPuffs.Sort((a, b) => a.time.CompareTo(b.time));
				ScreenBuffer buf = ScreenBuffer.GetScrapBuffer1(bLoadFromCurrent: true);
				TextConsole console = Popup._TextConsole;
				float t = 0f;
				int di = 0, gi = 0, si = 0;
				float end = schedule.Total + 0.08f;
				while (t < end)
				{
					while (si < schedule.ShakeTimes.Count && schedule.ShakeTimes[si].time <= t)
						CombatJuice.cameraShake(schedule.ShakeTimes[si++].mag);
					while (di < deposits.Count && deposits[di].time <= t)
						Place(deposits[di++]);
					while (gi < gapPuffs.Count && gapPuffs[gi].time <= t)
						SmokePuff(gapPuffs[gi++].cell);
					XRLCore.Core.RenderBaseToBuffer(buf);
					console.DrawBuffer(buf);
					Thread.Sleep(40);
					t += 0.04f;
				}
				while (di < deposits.Count)
					Place(deposits[di++]);
				while (gi < gapPuffs.Count)
					SmokePuff(gapPuffs[gi++].cell);
			}
			else
			{
				foreach ((Cell cell, string bp, float time) plan in deposits)
					Place(plan);
				foreach (Cell gap in gaps)
					SmokePuff(gap);
			}
			ZoneManager.PaintWalls(zone, origin.X - radius - 1, origin.Y - radius - 1, origin.X + radius + 1, origin.Y + radius + 1);
			Helpers.VerifyLog("FIS", $"RING complete: {deposited} fabric (wall share {wallShare}%), {gaps.Count} gap(s), {debris} debris-for-occupied, {scattered} wake scatter, carvedMass={carvedMass} (credits left {upgradeCredits}), paint={dominantPaint ?? "none/sandstone"}");
			return base.HandleEvent(E);
		}

		public static int GetWallShare(int Lv)
		{
			int baseShare, variance;
			switch (GetBand(Lv))
			{
				case 1: baseShare = 10; variance = 5; break;
				case 2: baseShare = 30; variance = 8; break;
				case 3: baseShare = 55; variance = 12; break;
				case 4: baseShare = 75; variance = 15; break;
				case 5: baseShare = 85; variance = 10; break;
				case 6: baseShare = 92; variance = 6; break;
				default: baseShare = 98; variance = 2; break;
			}
			return Math.Max(0, Math.Min(100, baseShare + Stat.Random(-variance, variance)));
		}

		private static string RollFabric(int wallShare)
		{
			int roll = Stat.Random(1, 100);
			if (roll <= wallShare)
				return WALL_BLUEPRINT;
			int rubbleThreshold = wallShare + (100 - wallShare) * 55 / 100;
			return roll <= rubbleThreshold ? RUBBLE_BLUEPRINT : CRUMBLED_BLUEPRINT;
		}

		private static string DominantWallPaint(Zone zone, out string atlas, out string ext, out char fg, out char detail, out char bg)
		{
			atlas = null;
			ext = null;
			fg = 'y';
			detail = 'K';
			bg = 'K';
			var counts = new Dictionary<string, int>();
			var samples = new Dictionary<string, GameObject>();
			foreach (GameObject obj in zone.GetObjects())
			{
				if (obj.Physics == null || !obj.Physics.Solid)
					continue;
				if (!IsCarvableStone(obj))
					continue;
				string paint = obj.GetTagOrStringProperty("PaintedWall");
				if (paint.IsNullOrEmpty())
					continue;
				if (paint.Contains(","))
					paint = paint.Split(',')[0];
				counts[paint] = counts.TryGetValue(paint, out int n) ? n + 1 : 1;
				if (!samples.ContainsKey(paint))
					samples[paint] = obj;
			}
			string best = null;
			int bestN = 0;
			foreach (KeyValuePair<string, int> kv in counts)
			{
				if (kv.Value > bestN)
				{
					best = kv.Key;
					bestN = kv.Value;
				}
			}
			if (best != null && samples.TryGetValue(best, out GameObject sample) && sample.Render != null)
			{
				atlas = sample.GetTagOrStringProperty("PaintedWallAtlas");
				ext = sample.GetTagOrStringProperty("PaintedWallExtension");
				string tc = sample.Render.TileColor;
				if (tc.IsNullOrEmpty())
					tc = sample.Render.ColorString;
				if (!tc.IsNullOrEmpty())
				{
					int amp = tc.IndexOf('&');
					if (amp >= 0 && amp + 1 < tc.Length)
						fg = tc[amp + 1];
				}
				if (!sample.Render.DetailColor.IsNullOrEmpty())
					detail = sample.Render.DetailColor[0];
				string cs = sample.Render.ColorString;
				if (!cs.IsNullOrEmpty())
				{
					int caret = cs.IndexOf('^');
					if (caret >= 0 && caret + 1 < cs.Length)
						bg = cs[caret + 1];
				}
			}
			return best;
		}

		private static int RingIndexOf(int dx, int dy, int radius)
		{
			for (int r = 1; r < radius; r++)
			{
				if (OnRing(dx, dy, r))
					return r;
			}
			return 0;
		}

		private static bool IsCarvableStone(GameObject wall)
		{
			return Cleo_TerraFirma_StoneshaperWallProperties.IsNaturalStone(wall?.GetPart<Cleo_TerraFirma_StoneshaperWallProperties>());
		}

		private enum WaveWallClass { Stone, Soft, Hard }

		private static WaveWallClass ClassifyWaveWall(GameObject wall)
		{
			var props = wall?.GetPart<Cleo_TerraFirma_StoneshaperWallProperties>();
			if (Cleo_TerraFirma_StoneshaperWallProperties.IsNaturalStone(props))
				return WaveWallClass.Stone;
			if (props?.MaterialType == "other")
				return WaveWallClass.Soft;
			return WaveWallClass.Hard;
		}

		private static bool WaveBitesWall(GameObject wall, Cell c, int level, GameObject actor, string logTag)
		{
			WaveWallClass cls = ClassifyWaveWall(wall);
			int av = wall.Stat("AV");
			int pv = GetCarvePV(level);
			int pens = Stat.RollDamagePenetrations(av, pv, pv);
			if (pens <= 0)
			{
				Helpers.VerifyLog(logTag, $"CARVE attack @({c.X},{c.Y}): {wall.Blueprint} [{cls}] AV {av} vs PV {pv} -- no penetration, the wall holds");
				SmokePuff(c, 1);
				return false;
			}
			int maxHP = wall.GetStat("Hitpoints").BaseValue;
			int dmg = Math.Max(1, maxHP * pens * CARVE_PCT_PER_PEN / 100);
			wall.TakeDamage(dmg, "from the heaving earth!", "Crushing", null, null,
				Attacker: actor, Owner: actor, Source: actor);
			bool died = !GameObject.Validate(wall) || wall.CurrentCell == null || wall.IsInGraveyard();
			Helpers.VerifyLog(logTag, $"CARVE attack @({c.X},{c.Y}): {wall.Blueprint} [{cls}] AV {av} vs PV {pv} -- {pens} pen(s), {dmg} dmg, {(died ? "FELL" : "leftover standing")}");
			SmokePuff(c);
			return died;
		}

		public class PulseWake
		{
			public readonly HashSet<Cell> Fallen = new HashSet<Cell>();
			public readonly HashSet<Cell> Carved = new HashSet<Cell>();
			public readonly HashSet<Cell> OwnFelledCells = new HashSet<Cell>();
			public int OwnFelled;
		}

		public static PulseWake RunPulses(Cell origin, GameObject actor, GameObject phaseAnchor, int level, string logTag)
		{
			var wake = new PulseWake();
			Zone zone = origin?.ParentZone;
			if (zone == null)
				return wake;
			int creaturePV = GetCreaturePVFor(actor);
			var lastMovedPulse = new Dictionary<GameObject, int>();
			var hitCounts = new Dictionary<GameObject, int>();
			for (int pulse = 1; pulse <= PULSE_COUNT; pulse++)
			{
				string dice = GetPulseDice(level, pulse);
				var struckNow = new HashSet<Cell>();
				HashSet<Cell> coverage = Sweep(zone, origin, pulse, 1, pulse, struckNow, wake, actor, level, logTag);
				coverage.Add(origin);
				var victims = new List<GameObject>();
				foreach (Cell c in coverage)
				{
					foreach (GameObject o in c.GetObjectsWithPart("Combat"))
					{
						if (o != actor && o.PhaseMatches(phaseAnchor) && !victims.Contains(o))
							victims.Add(o);
					}
				}
				victims.Sort((a, b) => ChebyshevDistance(origin, b.CurrentCell).CompareTo(ChebyshevDistance(origin, a.CurrentCell)));
				Helpers.VerifyLog(logTag, $"pulse {pulse}: coverage {coverage.Count} cell(s), {victims.Count} creature(s), dice {dice}, {struckNow.Count} standing wall(s) struck");
				foreach (GameObject victim in victims)
				{
					if (!GameObject.Validate(victim) || victim.CurrentCell == null)
						continue;
					if (victim.IsFlying)
					{
						Helpers.VerifyLog(logTag, $"pulse {pulse} spares {victim.Blueprint} (flying)");
						continue;
					}
					int av = victim.Stat("AV");
					int pens = Stat.RollDamagePenetrations(av, creaturePV, creaturePV);
					int dmg = 0;
					for (int p = 0; p < pens; p++)
						dmg += dice.RollCached();
					if (dmg > 0)
					{
						victim.TakeDamage(dmg, "from the heaving earth!", "Crushing",
							"You were battered by a wave of earth.",
							victim.It + victim.GetVerb("were", true, true) + " @@battered by a wave of earth.",
							Attacker: actor, Owner: actor, Source: actor);
					}
					hitCounts[victim] = hitCounts.TryGetValue(victim, out int hc) ? hc + 1 : 1;
					if (!GameObject.Validate(victim) || victim.CurrentCell == null)
					{
						Helpers.VerifyLog(logTag, $"pulse {pulse} hits {victim.Blueprint}: AV {av} vs PV {creaturePV}, {pens} pen(s), {dmg} dmg -- DOWN");
						continue;
					}
					Cell before = victim.CurrentCell;
					string dir = before == origin ? DIRS8[Stat.Random(0, 7)] : origin.GetDirectionFromCell(before);
					victim.Push(dir, GetPushForce(level), 1);
					bool moved = victim.CurrentCell != before;
					string note = moved ? "MOVED" : "HELD GROUND";
					if (moved)
						lastMovedPulse[victim] = pulse;
					else
					{
						Cell behind = before.GetCellFromDirection(dir);
						if (behind != null && behind.IsSolid() && GetSolidObject(behind) != null)
						{
							int slam = dice.RollCached();
							victim.TakeDamage(slam, "from the heaving earth!", "Crushing",
								"You were battered by a wave of earth.",
								victim.It + victim.GetVerb("were", true, true) + " @@battered by a wave of earth.",
								Attacker: actor, Owner: actor, Source: actor);
							note = $"PINNED (slam {slam})";
						}
					}
					Helpers.VerifyLog(logTag, $"pulse {pulse} hits {victim.Blueprint}: AV {av} vs PV {creaturePV}, {pens} pen(s), {dmg} dmg, push {dir} {note}");
				}
				var riders = new List<GameObject>();
				foreach (Cell c in struckNow)
				{
					foreach (GameObject o in c.GetObjectsWithPart("Combat"))
					{
						if (o != actor && o.PhaseMatches(phaseAnchor) && !riders.Contains(o))
							riders.Add(o);
					}
				}
				foreach (GameObject rider in riders)
				{
					if (!GameObject.Validate(rider) || rider.CurrentCell == null)
						continue;
					if (rider.IsFlying)
					{
						Helpers.VerifyLog(logTag, $"pulse {pulse} spares {rider.Blueprint} (flying, at a wall)");
						continue;
					}
					if (rider.MakeSave("Toughness", GetStunSaveDC(level), null, null, "Fissure Slam", IgnoreNaturals: false, IgnoreNatural1: false, IgnoreNatural20: false, IgnoreGodmode: false, actor))
					{
						Helpers.VerifyLog(logTag, $"pulse {pulse}: {rider.Blueprint} clings to its wall (made the slam save)");
						continue;
					}
					int slam = dice.RollCached();
					rider.TakeDamage(slam, "from the heaving earth!", "Crushing",
						"You were battered by a wave of earth.",
						rider.It + rider.GetVerb("were", true, true) + " @@battered by a wave of earth.",
						Attacker: actor, Owner: actor, Source: actor);
					Helpers.VerifyLog(logTag, $"pulse {pulse} slams {rider.Blueprint} against its wall: {slam} dmg (failed the slam save)");
				}
			}

			foreach (KeyValuePair<GameObject, int> kv in lastMovedPulse)
			{
				GameObject victim = kv.Key;
				if (!GameObject.Validate(victim) || victim.CurrentCell == null || !victim.HasPart("Combat"))
					continue;
				if (!victim.MakeSave("Toughness", GetStunSaveDC(level), null, null, "Fissure Stun", IgnoreNaturals: false, IgnoreNatural1: false, IgnoreNatural20: false, IgnoreGodmode: false, actor))
				{
					victim.ApplyEffect(new Stun(GetStunRounds(level), GetStunSaveDC(level)));
					Helpers.VerifyLog(logTag, $"churn stuns {victim.Blueprint} for {GetStunRounds(level)} round(s) (last moved on pulse {kv.Value})");
				}
			}
			foreach (KeyValuePair<GameObject, int> kv in hitCounts)
				Helpers.VerifyLog(logTag, $"churn total: {kv.Key.Blueprint} took {kv.Value} pulse hit(s)");
			return wake;
		}

		private static HashSet<Cell> Sweep(Zone zone, Cell origin, int reach, int biteFloor, int biteCeil, HashSet<Cell> struckNow, PulseWake wake, GameObject actor, int level, string logTag)
		{
			var reached = new HashSet<Cell>();
			var held = new HashSet<Cell>();
			for (int dx = -reach; dx <= reach; dx++)
			{
				for (int dy = -reach; dy <= reach; dy++)
				{
					if (dx == 0 && dy == 0)
						continue;
					if (!InDisk(dx, dy, reach))
						continue;
					Cell target = zone.GetCell(origin.X + dx, origin.Y + dy);
					if (target == null || reached.Contains(target))
						continue;
					List<Point> ray = Zone.Line(origin.X, origin.Y, target.X, target.Y);
					bool blocked = false;
					for (int i = 1; i < ray.Count; i++)
					{
						Cell step = zone.GetCell(ray[i].X, ray[i].Y);
						if (step == null) { blocked = true; break; }
						if (wake.Fallen.Contains(step) || wake.OwnFelledCells.Contains(step))
							continue;
						if (held.Contains(step)) { blocked = true; break; }
						if (!step.IsSolid())
							continue;
						GameObject wall = GetSolidObject(step);
						string maker = wall?.GetStringProperty(RING_MAKER_PROP);
						if (!maker.IsNullOrEmpty() && actor != null && actor.idmatch(maker))
						{
							if (IsCarvableStone(wall))
								wake.Carved.Add(step);
							wake.OwnFelledCells.Add(step);
							wake.OwnFelled++;
							Helpers.VerifyLog(logTag, $"own fabric yields @({step.X},{step.Y}): {wall.Blueprint} recycled");
							SmokePuff(step, 1);
							wall.Obliterate();
							continue;
						}
						int wallRing = RingOf(step.X - origin.X, step.Y - origin.Y);
						if (wall == null || wallRing < biteFloor || wallRing > biteCeil)
						{
							held.Add(step);
							struckNow?.Add(step);
							blocked = true;
							break;
						}
						if (WaveBitesWall(wall, step, level, actor, logTag))
						{
							wake.Fallen.Add(step);
							if (IsCarvableStone(wall))
								wake.Carved.Add(step);
							continue;
						}
						held.Add(step);
						struckNow?.Add(step);
						blocked = true;
						break;
					}
					if (blocked)
						continue;
					for (int i = 1; i < ray.Count; i++)
					{
						Cell step = zone.GetCell(ray[i].X, ray[i].Y);
						if (step != null)
							reached.Add(step);
					}
				}
			}
			return reached;
		}

		private static GameObject GetSolidObject(Cell C)
		{
			foreach (GameObject o in C.Objects)
			{
				if (o.Physics != null && o.Physics.Solid && !o.HasPart("Combat"))
					return o;
			}
			return null;
		}

		private const string FUGUE_DEPOSIT_PROP = "Cleo_TerraFirma_FugueRingPiece";
		private const string RING_MAKER_PROP = "Cleo_TerraFirma_RingMaker";

		private void StampOwner(GameObject rock)
		{
			rock.SetStringProperty(RING_MAKER_PROP, ParentObject.ID);
			if (ParentObject.HasStringProperty("FugueCopy"))
			{
				rock.SetIntProperty(FUGUE_DEPOSIT_PROP, 1);
				XRL.World.Parts.ExistenceSupport support = rock.RequirePart<XRL.World.Parts.ExistenceSupport>();
				support.SupportedBy = ParentObject;
				support.ValidateEveryTurn = true;
			}
		}

		public override bool HandleEvent(CheckExistenceSupportEvent E)
		{
			if (E.Object != null && E.Object.GetIntProperty(FUGUE_DEPOSIT_PROP) > 0)
				return false;
			return base.HandleEvent(E);
		}

		private static int ChebyshevDistance(Cell a, Cell b)
		{
			if (a == null || b == null)
				return int.MaxValue;
			return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
		}

		private static int RingOf(int dx, int dy)
		{
			int d2 = dx * dx + dy * dy;
			int r = 0;
			while (d2 > r * r + r)
				r++;
			return r;
		}

		private static readonly string[] DIRS8 = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

		private bool TryEvictRimOccupants(Cell c, Cell origin, string slamDice)
		{
			var occupants = new List<GameObject>();
			foreach (GameObject o in c.Objects)
			{
				if (o.HasPart("Combat") && o.IsValid() && !o.IsInGraveyard())
					occupants.Add(o);
			}
			string dir = origin.GetDirectionFromCell(c);
			Cell outCell = c.GetCellFromDirection(dir);
			Cell inCell = c.GetCellFromDirection(c.GetDirectionFromCell(origin));
			foreach (GameObject o in occupants)
			{
				if (!GameObject.Validate(o) || o.CurrentCell != c)
					continue;
				if (!o.PhaseMatches(ParentObject))
					return false;
				if (outCell != null && !outCell.IsSolid() && o.DirectMoveTo(outCell))
				{
					Helpers.VerifyLog("FIS", $"rim eviction: {o.Blueprint} bounced clear to ({outCell.X},{outCell.Y})");
				}
				else if (inCell != null && !inCell.IsSolid())
				{
					int slam = slamDice.RollCached();
					o.TakeDamage(slam, "from the heaving earth!", "Crushing",
						"You were battered by a wave of earth.",
						o.It + o.GetVerb("were", true, true) + " @@battered by a wave of earth.",
						Attacker: ParentObject, Owner: ParentObject, Source: ParentObject);
					if (GameObject.Validate(o) && o.CurrentCell == c && o.DirectMoveTo(inCell))
						Helpers.VerifyLog("FIS", $"rim eviction: {o.Blueprint} slammed for {slam} and deposited inside at ({inCell.X},{inCell.Y})");
					else
						Helpers.VerifyLog("FIS", $"rim eviction: {o.Blueprint} slammed for {slam} but the deposit was refused -- the debris downgrade stands");
				}
				else
				{
					Helpers.VerifyLog("FIS", $"rim eviction: {o.Blueprint} walled on both faces -- the debris downgrade stands");
					return false;
				}
			}
			return !StillHoldsCombatant(c);
		}

		private static void SweepLooseItemsAside(Cell C)
		{
			List<GameObject> loose = new List<GameObject>();
			foreach (GameObject o in C.Objects)
			{
				if (o.Physics != null && o.Physics.Takeable && !o.HasPart("Combat"))
					loose.Add(o);
			}
			if (loose.Count == 0)
				return;
			List<Cell> open = C.GetLocalAdjacentCells()?.FindAll(x => !x.IsSolid());
			if (open == null || open.Count == 0)
			{
				Helpers.VerifyLog("FIS", $"loot sweep: {loose.Count} loose item(s) left in place beneath the fabric (no open adjacent cell; loot rule forbids burying, so the piece shares the tile)");
				return;
			}
			Cell spill = open[Stat.Random(0, open.Count - 1)];
			Helpers.VerifyLog("FIS", $"loot sweep: {loose.Count} loose item(s) swept aside to ({spill.X},{spill.Y})");
			foreach (GameObject o in loose)
			{
				o.RemoveFromContext();
				spill.AddObject(o);
			}
		}

		private static bool StillHoldsCombatant(Cell C)
		{
			foreach (GameObject o in C.Objects)
			{
				if (o.HasPart("Combat") && o.IsValid() && !o.IsInGraveyard())
					return true;
			}
			return false;
		}

		public static void SmokePuff(Cell C, int particles = 1)
		{
			C.ParticleBlip("&y*");
			for (int i = 0; i < particles; i++)
			{
				string glyph = SMOKE_COLORS[Stat.Random(0, SMOKE_COLORS.Length - 1)] + (char)(176 + Stat.Random(0, 2));
				XRLCore.ParticleManager.AddSinusoidal(glyph, C.X, C.Y,
					1.5f * Stat.Random(1, 4), 0.1f * Stat.Random(1, 60), 0.1f + 0.025f * Stat.Random(0, 4),
					1f, 0f, 0f, -0.15f - 0.05f * Stat.Random(1, 4), 999, 0L);
			}
		}
	}
}
