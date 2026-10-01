using System;
using System.Collections.Generic;
using System.Linq;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Tinkering;

namespace Cleo.TerraFirma.Scripts
{
	public static partial class WarrenCarver
	{
		private static Row[] _wiringRows, _wireDamageRows, _machineCountRows, _machineRows;
		private const string MillBlueprint = "Cleo_TerraFirma_PiezoelectricMill";
		private const string TableBlueprint = "Cleo_TerraFirma_AnimationTable";

		private static Row[] MachineRows()
		{
			if (_machineRows != null) return _machineRows;
			var rows = new List<Row>();
			foreach (var po in ReadPopRows("Cleo_TerraFirma_WarrenMachines"))
				if (!IsNothingRow(po.Blueprint)) rows.Add(new Row((int)Math.Min(po.Weight, int.MaxValue), po.Blueprint));
			if (rows.Count == 0)
			{
				MetricsManager.LogError("TerraFirma: machine table 'Cleo_TerraFirma_WarrenMachines' missing or empty; tumbler only");
				rows.Add(new Row(1, "Rock Tumbler"));
			}
			return _machineRows = rows.ToArray();
		}

		private static void LayPowerGrid(Grid g, List<PocketRec> pockets, RolesRec roles, int si, string occupancy)
		{
			if (si != 2) return;
			bool live = occupancy == "in use";
			bool wired = RollLabelTable(ref _wiringRows, "Cleo_TerraFirma_WarrenWiring", new[] { "wired", "unwired" }) == "wired";
			bool Free(int i) => g.C[i] == FLOOR && g.Host[i] == 0 && g.Item[i] == null
				&& g.Water[i] == 0 && g.Tree[i] == 0 && g.Art[i] == 0 && i != g.MouthIdx;
			int Walls(int i)
			{
				int count = 0;
				foreach (var d in N4)
				{
					int x = i % g.W + d[0], y = i / g.W + d[1];
					if (g.Inb(x, y) && g.C[g.Idx(x, y)] != FLOOR) count++;
				}
				return count;
			}
			bool NearRoad(int i)
			{
				if (g.Art[i] != 0) return true;
				foreach (var d in N4)
				{
					int x = i % g.W + d[0], y = i / g.W + d[1];
					if (g.Inb(x, y) && g.Art[g.Idx(x, y)] != 0) return true;
				}
				return false;
			}
			List<int> Cells(Box b, int pocket = -1)
			{
				var cells = new List<int>();
				if (b != null)
					for (int y = b.Y0; y <= b.Y1; y++)
						for (int x = b.X0; x <= b.X1; x++)
							if (g.Inb(x, y) && (pocket < 0 || g.Pocket[g.Idx(x, y)] == pocket)) cells.Add(g.Idx(x, y));
				return cells;
			}
			int Seat(List<int> cells, bool mill = false)
			{
				var free = cells.Where(i => Free(i) && (!mill || (Walls(i) > 0 && !NearRoad(i)))).ToList();
				if (free.Count == 0) return -1;
				var wall = free.Where(i => Walls(i) > 0).ToList();
				if (wall.Count > 0) free = wall;
				if (mill)
				{
					int best = free.Max(Walls);
					free = free.Where(i => Walls(i) == best).ToList();
				}
				return Pick(free);
			}
			int SeatRoles(string[] wanted, bool mill = false)
			{
				foreach (string role in wanted)
					foreach (var p in pockets)
						if (p.Role == role)
						{
							int seat = Seat(Cells(p.In, p.Id), mill);
							if (seat >= 0) return seat;
						}
				return -1;
			}
			int millI = -1;
			if (wired)
			{
				millI = Seat(Cells(roles.YardRect), true);
				if (millI < 0) millI = SeatRoles(new[] { "face" }, true);
				if (millI < 0)
					foreach (var p in pockets)
					{
						if (p.Role == "commons") continue;
						millI = Seat(Cells(p.In, p.Id), true);
						if (millI >= 0) break;
					}
				if (millI >= 0) g.Item[millI] = MillBlueprint + (live ? "" : "Dormant");
			}
			int tableI = SeatRoles(new[] { "commons", "dwelling", "plain", "face" });
			if (tableI >= 0) g.Item[tableI] = TableBlueprint + (live ? "" : "Dormant");
			if (!wired || millI < 0)
			{
				Helpers.VerifyLog("WARREN", $"power: {(wired ? "unplaced mill" : "unwired")}, table={tableI}");
				return;
			}
			var consumers = new List<int>();
			if (tableI >= 0) consumers.Add(tableI);
			var pool = MachineRows().ToList();
			int count = Math.Min(pool.Count, int.Parse(RollLabelTable(ref _machineCountRows, "Cleo_TerraFirma_WarrenMachineCount", new[] { "1", "2" })));
			var machines = new List<string>();
			for (int k = 0; k < count; k++)
			{
				int t = pool.Sum(r => r.Wt);
				double x = Rng() * t;
				int j = pool.Count - 1;
				for (int q = 0; q < pool.Count; q++) { x -= pool[q].Wt; if (x < 0) { j = q; break; } }
				string machine = pool[j].Id;
				pool.RemoveAt(j);
				int seat = SeatRoles(new[] { "face", "plain", "dwelling" });
				if (seat < 0) continue;
				g.Item[seat] = machine;
				consumers.Add(seat);
				machines.Add(machine);
			}
			for (int i = 0; i < g.Item.Length; i++) if (g.Item[i] == "Kiln") consumers.Add(i);
			var endpoints = new HashSet<int>(consumers) { millI };
			g.PowerWires = new Dictionary<int, bool>();
			double Cost(int i)
			{
				if (g.PowerWires.ContainsKey(i)) return .2;
				if (endpoints.Contains(i)) return .3;
				if (g.Water[i] != 0 || g.Tree[i] != 0 || i == g.MouthIdx) return double.PositiveInfinity;
				if (g.C[i] != FLOOR)
				{
					foreach (var d in N8)
					{
						int x = i % g.W + d[0], y = i / g.W + d[1];
						if (g.Inb(x, y) && g.C[g.Idx(x, y)] == FLOOR && g.Host[g.Idx(x, y)] == 0) return 1;
					}
					return double.PositiveInfinity;
				}
				if (g.Host[i] != 0 || g.Art[i] != 0) return double.PositiveInfinity;
				return g.Item[i] != null ? 12 : 3;
			}
			int routed = 0;
			foreach (int consumer in consumers)
			{
				int n = g.W * g.H;
				var distances = Enumerable.Repeat(double.PositiveInfinity, n).ToArray();
				var previous = Enumerable.Repeat(-1, n).ToArray();
				var queue = new SortedSet<(double Cost, int Index)>();
				distances[consumer] = 0;
				queue.Add((0, consumer));
				while (queue.Count > 0)
				{
					var current = queue.Min;
					queue.Remove(current);
					if (current.Cost != distances[current.Index]) continue;
					if (current.Index == millI) break;
					foreach (var d in N4)
					{
						int x = current.Index % g.W + d[0], y = current.Index / g.W + d[1];
						if (!g.Inb(x, y)) continue;
						int next = g.Idx(x, y);
						double distance = current.Cost + Cost(next);
						if (distance >= distances[next]) continue;
						distances[next] = distance;
						previous[next] = current.Index;
						queue.Add((distance, next));
					}
				}
				if (double.IsPositiveInfinity(distances[millI])) continue;
				routed++;
				for (int i = previous[millI]; i >= 0 && i != consumer; i = previous[i])
				{
					if (endpoints.Contains(i) || g.PowerWires.ContainsKey(i)) continue;
					bool broken = g.C[i] == FLOOR && occupancy == "abandoned"
						&& RollLabelTable(ref _wireDamageRows, "Cleo_TerraFirma_WarrenWireDamage", new[] { "intact", "broken" }) == "broken";
					g.PowerWires.Add(i, broken);
				}
			}
			Helpers.VerifyLog("WARREN", $"power: {(live ? "live" : "dormant")}, mill={millI}, table={tableI}, machines=[{string.Join(", ", machines)}], routed={routed}/{consumers.Count}, wires={g.PowerWires.Count}, broken={g.PowerWires.Count(p => p.Value)}");
		}

		private static void ApplyPowerWires(Zone zone, Grid g)
		{
			if (g.PowerWires == null) return;
			int rate = GameObjectFactory.Factory.GetBlueprint(MillBlueprint)
				.GetPartParameter("ElectricalPowerTransmission", "ChargeRate", 1000);
			int tier = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(rate / 500.0)));
			int walls = 0, lines = 0, failed = 0;
			foreach (var wire in g.PowerWires)
			{
				Cell cell = zone.GetCell(wire.Key % g.W, wire.Key / g.W);
				var wall = cell.GetWalls().FirstOrDefault();
				if (wall != null)
				{
					if (!wall.HasPart<ModWired>() && !ItemModding.ApplyModification(wall, "ModWired", tier)) { failed++; continue; }
					walls++;
				}
				else
				{
					var line = cell.AddObject("PowerLine");
					if (wire.Value) line.ApplyEffect(new XRL.World.Effects.Broken());
					lines++;
				}
			}
			Helpers.VerifyLog("WARREN", $"power applied: walls={walls}, lines={lines}, failed={failed}");
		}
	}
}
