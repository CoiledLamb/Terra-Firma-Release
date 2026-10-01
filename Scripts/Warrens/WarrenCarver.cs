using System;
using System.Collections.Generic;
using System.Linq;
using XRL.World;

namespace Cleo.TerraFirma.Scripts
{
	public static partial class WarrenCarver
	{
		public const int AQUIFER_MIN = 1;
		public const int AQUIFER_MAX = 2;
		public const int POOL_MIN = 6;
		public const int POOL_MAX = 18;
		public const int VEIN_CHANCE = 70;
		public const int BREACH_CHANCE = 45;
		public const int BREACH_MAX = 2;
		public const int BREACH_REACH = 5;
		public const int GARDEN_BREACH_PCT = 75;
		public const int GARDEN_MAX = 2;
		public const int GARDEN_REACH = 6;
		public const int GARDEN_MIN = 4;
		public const int OUTRIDER_POST_RADIUS = 8;
		public const int MOMENTUM = 55;
		public const int WANDER = 22;
		public const int MOUTH_RUN = 6;
		public const int BOX_W0 = 6, BOX_W1 = 15;
		public const int BOX_H0 = 6, BOX_H1 = 9;
		public const int PLAIN_W = 5;
		public const int HALL_W = 9;
		public const int COMMONS_PCT = 95;
		public const int MAX_SPAN = 3;
		public const int LAT_Y = 2;
		public const int CLEARANCE = 1;
		public const int PILLAR_FILL = 62;
		public const int COLUMN_CHANCE = 25;
		public const int SPUR_CHANCE = 35;
		public const int PART_CHANCE = 55;
		public const int PART_MAX = 2;
		public const int STRADDLE = 70;
		public const int PINCH_COST = 400;
		public const int HUG_COST = 260;
		public const int EMPTY_BASE = 200;
		public const int ATTEMPTS = 4;
		public const int MIN_SITE = 70;
		public const int FACE_PREDICTOR = 300;
		public const int FACE_SAMPLE = 40;
		public const int BLOCK_MIN = 50;
		public const int BLOCK_MAX = 95;
		public const int DEN_MAX = 24;
		public const int CAMP_FLOOR = 130;
		public const int FLOUR_FLOOR = 210;
		public const int DRIFT_MAX = 10;
		public const int AUG_TRIES = 9;
		public const int AUG_FUZZ = 35;

		private class Stage
		{
			public string Name;
			public int P0, P1;
			public int C0, C1;
			public int Cross;
			public int Wp0, Wp1;
			public int Area;
			public int Fill0, Fill1;
		}
		private static readonly Stage[] STAGES = {
			new Stage{ Name="working",     P0=3, P1=5, C0=1, C1=2, Cross=2, Wp0=1, Wp1=2, Area=400,  Fill0=1, Fill1=2 },
			new Stage{ Name="camp",        P0=4, P1=7, C0=2, C1=3, Cross=4, Wp0=2, Wp1=3, Area=680,  Fill0=1, Fill1=3 },
			new Stage{ Name="flourishing", P0=6, P1=9, C0=3, C1=4, Cross=7, Wp0=3, Wp1=4, Area=1000, Fill0=2, Fill1=4 },
		};

		private static uint _state;
		private static double Rng()
		{
			unchecked
			{
				uint z = (_state += 0x6D2B79F5u);
				uint t = (z ^ (z >> 15)) * (1u | z);
				t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
				return (t ^ (t >> 14)) / 4294967296.0;
			}
		}
		private static int R(int lo, int hi) => lo + (int)(Rng() * (Math.Max(lo, hi) - lo + 1));
		private static bool Chance(double p) => Rng() < p;
		private static T Pick<T>(IReadOnlyList<T> a) => a[(int)(Rng() * a.Count)];

		public static int SeedFor(string zoneID)
		{
			unchecked
			{
				uint h = 2166136261u;
				foreach (char c in zoneID) { h ^= c; h *= 16777619u; }
				return (int)h;
			}
		}

		private const int SOLID = 0, FLOOR = 1;
		private class Grid
		{
			public int W, H;
			public byte[] C, Art, Host, Water, Aqua, Tree, Region, Marker;
			public short[] Pocket;
			public string[] Item;
			public int SiteTopups;
			public Dictionary<int, bool> PowerWires;
			public HashSet<int> EtchedWalls;
			public string YardBoardText;
			public HashSet<int> Augment;
			public List<int> Defile;
			public int MouthIdx = -1;
			public int[] MouthDist;
			public Grid(int w, int h)
			{
				W = w; H = h;
				int n = w * h;
				C = new byte[n]; Art = new byte[n]; Host = new byte[n]; Water = new byte[n];
				Aqua = new byte[n]; Tree = new byte[n]; Marker = new byte[n];
				Pocket = new short[n];
				for (int i = 0; i < n; i++) Pocket[i] = -1;
			}
			public int Idx(int x, int y) => y * W + x;
			public bool Inb(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
		}
		private static readonly int[][] N4 = { new[]{1,0}, new[]{-1,0}, new[]{0,1}, new[]{0,-1} };
		private static readonly int[][] N8 = { new[]{1,0}, new[]{-1,0}, new[]{0,1}, new[]{0,-1}, new[]{1,1}, new[]{1,-1}, new[]{-1,1}, new[]{-1,-1} };

		private static bool Wf(Grid g, int x, int y) => g.C[g.Idx(x, y)] == FLOOR && g.Host[g.Idx(x, y)] == 0;

		private static bool SideClear(Grid g, int x, int y, int fx, int fy)
		{
			foreach (var d in N4)
			{
				int nx = x + d[0], ny = y + d[1];
				if (nx == fx && ny == fy) continue;
				if (g.Inb(nx, ny) && g.C[g.Idx(nx, ny)] == FLOOR) return false;
			}
			return true;
		}

		private class Box { public int X0, Y0, X1, Y1; }
		private class Pt { public int X, Y; }
		private class PocketRec
		{
			public int Id;
			public Box BoxOut, In;
			public int Through, Partitions, Pillars, Spurs, Entrances;
			public int W, H;
			public string Character, Role;
			public bool Live, Infill;
			public int Depth, DepthRank;
			public List<int> Footprint;
			public List<int[]> PillarCells = new List<int[]>();
			public List<int[]> SpurCells = new List<int[]>();
		}
		private class Artery
		{
			public Pt Mouth;
			public List<Pt> Path;
			public Box Foot;
			public byte[] Mask;
		}

		private static Grid ReadHost(Zone Z)
		{
			var g = new Grid(Z.Width, Z.Height);
			for (int y = 0; y < g.H; y++)
				for (int x = 0; x < g.W; x++)
				{
					Cell c = Z.GetCell(x, y);
					int i = g.Idx(x, y);
					if (c == null) { g.Host[i] = 1; g.Water[i] = 1; continue; }
					if (c.HasWall())
					{
						GameObject wall = c.GetWalls().FirstOrDefault();
						bool natural = XRL.World.Parts.Cleo_TerraFirma_StoneshaperWallProperties.IsNaturalStone(
							wall?.GetPart<XRL.World.Parts.Cleo_TerraFirma_StoneshaperWallProperties>());
						if (!natural)
						{
							g.Host[i] = 1; g.Water[i] = 1;
						}
						continue;
					}
					g.C[i] = FLOOR; g.Host[i] = 1;
					if (c.HasObject("CanyonMarker")) g.Marker[i] = 1;
					if (c.HasObjectWithPart("LiquidVolume")) g.Water[i] = 1;
					else if (c.IsSolid()) g.Tree[i] = 1;
				}
			if (Helpers.VERIFY_WATCH)
			{
				int rock = 0, hard = 0, open = 0, water = 0, trees = 0;
				for (int i = 0; i < g.W * g.H; i++)
				{
					if (g.C[i] == FLOOR) { open++; if (g.Water[i] != 0) water++; if (g.Tree[i] != 0) trees++; }
					else if (g.Host[i] != 0) hard++;
					else rock++;
				}
				Helpers.VerifyLog("WARREN", $"host census: {rock} natural rock, {hard} hard walls, {open} open ({water} water, {trees} trees)");
			}
			return g;
		}

		private class Band { public int Mass; public List<int> Cells; }
		private static Band LargestBand(Grid g)
		{
			var lab = new int[g.W * g.H];
			for (int i = 0; i < lab.Length; i++) lab[i] = -1;
			var comps = new List<List<int>>();
			for (int y = 0; y < g.H; y++)
				for (int x = 0; x < g.W; x++)
				{
					int i0 = g.Idx(x, y);
					if (g.C[i0] == FLOOR || g.Host[i0] != 0 || lab[i0] >= 0) continue;
					int id = comps.Count;
					var acc = new List<int>();
					var st = new Stack<int>(); st.Push(i0); lab[i0] = id;
					while (st.Count > 0)
					{
						int ci = st.Pop(); acc.Add(ci);
						int cx = ci % g.W, cy = ci / g.W;
						foreach (var d in N4)
						{
							int nx = cx + d[0], ny = cy + d[1];
							if (!g.Inb(nx, ny)) continue;
							int ni = g.Idx(nx, ny);
							if (lab[ni] < 0 && g.C[ni] != FLOOR) { lab[ni] = id; st.Push(ni); }
						}
					}
					comps.Add(acc);
				}
			if (comps.Count == 0) return null;
			var best = comps.OrderByDescending(c => c.Count).First();
			return new Band { Mass = best.Count, Cells = best };
		}

		private static bool Touches4(Grid g, int x, int y, Func<int, bool> pred)
			=> N4.Any(d => g.Inb(x + d[0], y + d[1]) && pred(g.Idx(x + d[0], y + d[1])));
		private static bool Touches8(Grid g, int x, int y, Func<int, bool> pred)
			=> N8.Any(d => g.Inb(x + d[0], y + d[1]) && pred(g.Idx(x + d[0], y + d[1])));
		private static bool IsInnerOf(Grid g, int x, int y, Func<int, bool> basis)
			=> !N8.Any(d => !g.Inb(x + d[0], y + d[1]) || !basis(g.Idx(x + d[0], y + d[1])));

		private class RegionRec { public byte[] Mask; public int Size; public List<int> Cells; }
		private static RegionRec GrowRegion(Grid g, Pt mouth, int budget, byte[] connected)
		{
			bool OnRind(int x, int y) => Touches8(g, x, y, i => connected[i] != 0);
			var mask = new byte[g.W * g.H];
			var seen = new byte[g.W * g.H];
			var q = new List<int> { g.Idx(mouth.X, mouth.Y) };
			seen[q[0]] = 1; mask[q[0]] = 1;
			var cells = new List<int> { q[0] };
			int n = 1, head = 0;
			while (head < q.Count && n < budget)
			{
				int ci = q[head++];
				int x = ci % g.W, y = ci / g.W;
				foreach (var d in N4)
				{
					int nx = x + d[0], ny = y + d[1];
					if (!g.Inb(nx, ny)) continue;
					int ni = g.Idx(nx, ny);
					if (seen[ni] != 0) continue;
					if (nx < 1 || ny < 1 || nx > g.W - 2 || ny > g.H - 2) continue;
					if (connected[ni] != 0) continue;
					bool bubble = g.C[ni] == FLOOR;
					bool rind = !bubble && OnRind(nx, ny);
					seen[ni] = 1;
					if (rind) continue;
					q.Add(ni);
					if (bubble) continue;
					mask[ni] = 1; cells.Add(ni); n++;
					if (n >= budget) break;
				}
			}
			return new RegionRec { Mask = mask, Size = n, Cells = cells };
		}

		private static Artery LayArtery(Grid g, Stage S, Pt mouth, Box foot, byte[] mask, List<int> regionCells)
		{
			int nWp = R(S.Wp0, S.Wp1);
			var bands = Enumerable.Range(0, nWp).ToList();
			for (int i = bands.Count - 1; i > 0; i--) { int j = R(0, i); (bands[i], bands[j]) = (bands[j], bands[i]); }
			var wps = new List<Pt>();
			int nC = regionCells.Count;
			foreach (int b in bands)
			{
				int lo = (int)Math.Floor((b + 0.35) * nC / nWp);
				int hi = Math.Min(nC - 1, (int)Math.Floor((double)(b + 1) * nC / nWp) - 1);
				int wi = R(Math.Min(lo, hi), Math.Max(lo, hi));
				if (wi < 0 || wi >= nC) continue;
				int ci = regionCells[wi];
				wps.Add(new Pt { X = ci % g.W, Y = ci / g.W });
			}
			if (wps.Count == 0 && nC > 0)
				wps.Add(new Pt { X = regionCells[nC - 1] % g.W, Y = regionCells[nC - 1] / g.W });

			var path = new List<Pt>();
			var cur = new Pt { X = mouth.X, Y = mouth.Y };
			int[] lastDir = null, wanderDir = null;
			int wander = 0;
			g.C[g.Idx(cur.X, cur.Y)] = FLOOR; g.Art[g.Idx(cur.X, cur.Y)] = 1;
			path.Add(new Pt { X = cur.X, Y = cur.Y });
			foreach (var wp in wps)
			{
				int guard = 0;
				while ((cur.X != wp.X || cur.Y != wp.Y) && guard++ < 400)
				{
					int dx = Math.Sign(wp.X - cur.X), dy = Math.Sign(wp.Y - cur.Y);
					int[] dir = null;
					if (wander > 0) { dir = wanderDir; wander--; }
					else if (path.Count >= MOUTH_RUN && Chance(WANDER / 100.0))
					{
						wanderDir = dx != 0 ? new[] { 0, Chance(.5) ? 1 : -1 } : new[] { Chance(.5) ? 1 : -1, 0 };
						wander = R(1, 3); dir = wanderDir;
					}
					if (dir == null && lastDir != null && Chance(MOMENTUM / 100.0))
					{
						int nx0 = cur.X + lastDir[0], ny0 = cur.Y + lastDir[1];
						if (g.Inb(nx0, ny0) && Math.Abs(nx0 - wp.X) + Math.Abs(ny0 - wp.Y) < Math.Abs(cur.X - wp.X) + Math.Abs(cur.Y - wp.Y))
							dir = lastDir;
					}
					if (dir == null)
					{
						var opts = new List<int[]>();
						if (dx != 0) opts.Add(new[] { dx, 0 });
						if (dy != 0) opts.Add(new[] { 0, dy });
						if (opts.Count == 0) break;
						dir = opts.Count == 1 ? opts[0] : (Chance(.6) ? opts[0] : opts[1]);
					}
					if (wander > 0)
					{
						int tx = cur.X + dir[0], ty = cur.Y + dir[1];
						if (!g.Inb(tx, ty) || mask[g.Idx(tx, ty)] == 0) { wander = 0; continue; }
					}
					int nx = Math.Min(g.W - 1, Math.Max(0, cur.X + dir[0]));
					int ny = Math.Min(g.H - 1, Math.Max(0, cur.Y + dir[1]));
					if (g.Host[g.Idx(nx, ny)] != 0) break;
					if (mask[g.Idx(nx, ny)] == 0) break;
					if (!SideClear(g, nx, ny, cur.X, cur.Y))
					{
						int[] alt = dir[0] != 0
							? new[] { 0, Math.Sign(wp.Y - cur.Y) != 0 ? Math.Sign(wp.Y - cur.Y) : (Chance(.5) ? 1 : -1) }
							: new[] { Math.Sign(wp.X - cur.X) != 0 ? Math.Sign(wp.X - cur.X) : (Chance(.5) ? 1 : -1), 0 };
						int ax = Math.Min(g.W - 1, Math.Max(0, cur.X + alt[0]));
						int ay = Math.Min(g.H - 1, Math.Max(0, cur.Y + alt[1]));
						if (SideClear(g, ax, ay, cur.X, cur.Y)) { nx = ax; ny = ay; dir = alt; }
					}
					cur = new Pt { X = nx, Y = ny }; lastDir = dir;
					g.C[g.Idx(nx, ny)] = FLOOR; g.Art[g.Idx(nx, ny)] = 1;
					path.Add(new Pt { X = nx, Y = ny });
				}
			}
			return new Artery { Mouth = mouth, Path = path, Foot = foot, Mask = mask };
		}

		private static List<PocketRec> FindPockets(Grid g, Artery art, int budget, bool wantHall)
		{
			var pockets = new List<PocketRec>();
			bool Attempt(int w, int h, Pt anchor, bool straddleIt)
			{
				int ox, oy;
				if (straddleIt)
				{
					ox = anchor.X - R(1, Math.Max(1, w - 2));
					oy = anchor.Y - R(1, Math.Max(1, h - 2));
				}
				else
				{
					ox = anchor.X + R(-w + 1, 0) + (Chance(.5) ? 0 : R(-2, 2));
					oy = anchor.Y + (Chance(.5) ? R(-h, -1) : 1);
				}
				int x0 = Math.Max(1, Math.Min(g.W - 2 - w, ox)), y0 = Math.Max(1, Math.Min(g.H - 2 - h, oy));
				var box = new Box { X0 = x0, Y0 = y0, X1 = x0 + w - 1, Y1 = y0 + h - 1 };
				if (box.X1 >= g.W - 1 || box.Y1 >= g.H - 1) return false;
				for (int y = box.Y0; y <= box.Y1; y++)
					for (int x = box.X0; x <= box.X1; x++)
						if (g.Host[g.Idx(x, y)] != 0 || art.Mask[g.Idx(x, y)] == 0) return false;
				foreach (var p in pockets)
					if (!(box.X1 + 1 < p.BoxOut.X0 - 1 || box.X0 - 1 > p.BoxOut.X1 + 1 || box.Y1 + 1 < p.BoxOut.Y0 - 1 || box.Y0 - 1 > p.BoxOut.Y1 + 1)) return false;
				int through = 0;
				for (int y = box.Y0; y <= box.Y1; y++)
					for (int x = box.X0; x <= box.X1; x++)
						if (g.Art[g.Idx(x, y)] != 0) through++;
				if (through == 0)
				{
					bool nearRoad = art.Path.Any(q => q.X >= box.X0 - 3 && q.X <= box.X1 + 3 && q.Y >= box.Y0 - 3 && q.Y <= box.Y1 + 3);
					if (!nearRoad) return false;
				}
				pockets.Add(new PocketRec { BoxOut = box, Through = through, Id = pockets.Count });
				return true;
			}
			if (wantHall && art.Path.Count > 6)
			{
				int frontEnd = Math.Min(art.Path.Count - 1, Math.Max(8, (int)(art.Path.Count * 0.4)));
				for (int t = 0; t < 3000 && pockets.Count == 0; t++)
				{
					int sq = Math.Min(2, t / 800);
					int hi = t < 1500 ? frontEnd : art.Path.Count - 1;
					Attempt(t < 2000 ? R(HALL_W + 2, HALL_W + 4) : HALL_W + 2,
							t < 2000 ? R(Math.Max(5, BOX_H0 - sq), Math.Max(6, BOX_H1 - sq * 2)) : 5,
							art.Path[R(2, hi)], t % 3 > 0);
				}
			}
			int tries = 0;
			while (pockets.Count < budget && tries++ < 900)
			{
				bool early = pockets.Count == 0 && art.Path.Count > 6 && tries < 300;
				Pt anchor = early ? art.Path[R(2, Math.Min(8, art.Path.Count - 1))]
								  : art.Path[R(0, art.Path.Count - 1)];
				int sq = Math.Min(3, tries / 150);
				Attempt(R(Math.Max(5, BOX_W0 - sq), Math.Max(5, BOX_W1 - sq * 3)),
						R(Math.Max(5, BOX_H0 - sq), Math.Max(5, BOX_H1 - sq * 2)),
						anchor, early || Chance(STRADDLE / 100.0));
			}
			return pockets;
		}

		private static void ResolvePocket(Grid g, PocketRec p)
		{
			int x0 = p.BoxOut.X0 + 1, y0 = p.BoxOut.Y0 + 1, x1 = p.BoxOut.X1 - 1, y1 = p.BoxOut.Y1 - 1;
			p.In = new Box { X0 = x0, Y0 = y0, X1 = x1, Y1 = y1 };
			int w = x1 - x0 + 1, h = y1 - y0 + 1;
			for (int y = y0; y <= y1; y++)
				for (int x = x0; x <= x1; x++) { g.C[g.Idx(x, y)] = FLOOR; g.Pocket[g.Idx(x, y)] = (short)p.Id; }
			p.W = w; p.H = h;
			p.Character = w <= PLAIN_W ? "plain" : (w >= HALL_W ? "hall" : "worked");

			p.Partitions = 0;
			if (w >= 6 && h >= 4 && Chance(PART_CHANCE / 100.0))
			{
				int n = R(1, PART_MAX);
				for (int i = 0; i < n; i++)
				{
					if (Chance(.6))
					{
						int px = R(x0 + 2, Math.Max(x0 + 2, x1 - 2));
						bool fromTop = Chance(.5);
						int len = R(2, Math.Max(2, h - 2));
						for (int k = 0; k < len; k++)
						{
							int py = fromTop ? y0 + k : y1 - k;
							if (py < y0 || py > y1 || g.Art[g.Idx(px, py)] != 0) break;
							g.C[g.Idx(px, py)] = SOLID;
						}
					}
					else
					{
						int py = R(y0 + 1, Math.Max(y0 + 1, y1 - 1));
						bool fromLeft = Chance(.5);
						int len = R(2, Math.Max(2, w - 3));
						for (int k = 0; k < len; k++)
						{
							int px = fromLeft ? x0 + k : x1 - k;
							if (px < x0 || px > x1 || g.Art[g.Idx(px, py)] != 0) break;
							g.C[g.Idx(px, py)] = SOLID;
						}
					}
					p.Partitions++;
				}
			}

			p.Pillars = 0; p.Spurs = 0;
			int cl = CLEARANCE;
			bool Free(int x, int y)
			{
				if (x < x0 + cl || x > x1 - cl || y < y0 + cl || y > y1 - cl) return false;
				if (g.C[g.Idx(x, y)] != FLOOR || g.Art[g.Idx(x, y)] != 0) return false;
				foreach (var d in N8)
				{
					int nx = x + d[0], ny = y + d[1];
					if (nx >= x0 && nx <= x1 && ny >= y0 && ny <= y1 && g.C[g.Idx(nx, ny)] == SOLID) return false;
				}
				return true;
			}
			if (p.Character != "plain")
			{
				int sx = MAX_SPAN + 1, sy = LAT_Y;
				int ox = x0 + cl + R(0, Math.Min(sx - 1, 2)), oy = y0 + cl + R(0, Math.Min(sy - 1, 1));
				for (int py = oy; py <= y1 - cl; py += sy)
					for (int px = ox; px <= x1 - cl; px += sx)
					{
						if (!Chance(PILLAR_FILL / 100.0)) continue;
						if (!Free(px, py)) continue;
						g.C[g.Idx(px, py)] = SOLID; p.Pillars++; p.PillarCells.Add(new[] { px, py });
						if (Chance(COLUMN_CHANCE / 100.0))
						{
							int gy = py + (Chance(.5) ? 1 : -1);
							if (Free(px, gy)) { g.C[g.Idx(px, gy)] = SOLID; p.Pillars++; p.PillarCells.Add(new[] { px, gy }); }
						}
					}
				for (int px = ox; px <= x1 - cl; px += sx)
				{
					if (!Chance(SPUR_CHANCE / 100.0)) continue;
					bool fromTop = Chance(.5);
					int len = R(1, 2);
					bool did = false;
					int[] tip = null;
					for (int k = 0; k < len; k++)
					{
						int sy2 = fromTop ? y0 + k : y1 - k;
						if (sy2 < y0 || sy2 > y1 || g.Art[g.Idx(px, sy2)] != 0 || g.C[g.Idx(px, sy2)] != FLOOR) break;
						g.C[g.Idx(px, sy2)] = SOLID; did = true; tip = new[] { px, sy2 };
					}
					if (did) { p.Spurs++; p.SpurCells.Add(tip); }
				}
			}
			p.Entrances = p.Through > 0 ? 1 : 0;
		}

		private static void FinishRoom(Grid g, List<int> comp, Box bb, List<PocketRec> pockets)
		{
			var p = new PocketRec
			{
				Id = pockets.Count,
				BoxOut = new Box { X0 = Math.Max(0, bb.X0 - 1), Y0 = Math.Max(0, bb.Y0 - 1), X1 = Math.Min(g.W - 1, bb.X1 + 1), Y1 = Math.Min(g.H - 1, bb.Y1 + 1) },
				In = new Box { X0 = bb.X0, Y0 = bb.Y0, X1 = bb.X1, Y1 = bb.Y1 },
			};
			p.W = bb.X1 - bb.X0 + 1; p.H = bb.Y1 - bb.Y0 + 1;
			p.Character = p.W <= PLAIN_W ? "plain" : (p.W >= HALL_W ? "hall" : "worked");
			foreach (int i in comp) g.Pocket[i] = (short)p.Id;
			if (p.Character != "plain")
			{
				int sx = MAX_SPAN + 1, sy = LAT_Y;
				int ox = bb.X0 + 1 + R(0, Math.Min(sx - 1, 2)), oy = bb.Y0 + 1 + R(0, Math.Min(sy - 1, 1));
				bool Free2(int x, int y)
				{
					if (!g.Inb(x, y)) return false;
					int i = g.Idx(x, y);
					if (g.Pocket[i] != p.Id || g.C[i] != FLOOR || g.Art[i] != 0) return false;
					foreach (var d in N8)
					{
						int nx = x + d[0], ny = y + d[1];
						if (g.Inb(nx, ny) && g.C[g.Idx(nx, ny)] == SOLID) return false;
					}
					return true;
				}
				for (int py = oy; py <= bb.Y1 - 1; py += sy)
					for (int px = ox; px <= bb.X1 - 1; px += sx)
					{
						if (!Chance(PILLAR_FILL / 100.0)) continue;
						if (!Free2(px, py)) continue;
						g.C[g.Idx(px, py)] = SOLID; p.Pillars++; p.PillarCells.Add(new[] { px, py });
						if (Chance(COLUMN_CHANCE / 100.0))
						{
							int gy = py + (Chance(.5) ? 1 : -1);
							if (Free2(px, gy)) { g.C[g.Idx(px, gy)] = SOLID; p.Pillars++; p.PillarCells.Add(new[] { px, gy }); }
						}
					}
			}
			pockets.Add(p);
		}

		private static void Subdivide(Grid g, List<int> comp, List<PocketRec> pockets, Func<bool> takeHall)
		{
			int x0 = g.W, y0 = g.H, x1 = 0, y1 = 0;
			foreach (int i in comp)
			{
				int x = i % g.W, y = i / g.W;
				if (x < x0) x0 = x; if (x > x1) x1 = x;
				if (y < y0) y0 = y; if (y > y1) y1 = y;
			}
			int w = x1 - x0 + 1, h = y1 - y0 + 1;
			var bb = new Box { X0 = x0, Y0 = y0, X1 = x1, Y1 = y1 };
			if (w >= HALL_W && comp.Count >= HALL_W * 3 && takeHall())
			{ FinishRoom(g, comp, bb, pockets); return; }
			if (comp.Count <= DEN_MAX || (w <= 4 && h <= 4))
			{ FinishRoom(g, comp, bb, pockets); return; }
			bool vert = w > h; if (Chance(.25)) vert = !vert;
			for (int attempt = 0; attempt < 8; attempt++)
			{
				int line = vert ? R(x0 + 2, Math.Max(x0 + 2, x1 - 2)) : R(y0 + 2, Math.Max(y0 + 2, y1 - 2));
				var wallCells = comp.Where(i => vert ? i % g.W == line : i / g.W == line).ToList();
				if (wallCells.Count < 3) continue;
				int gap = wallCells[Chance(.6) ? (Chance(.5) ? 0 : wallCells.Count - 1) : R(1, wallCells.Count - 2)];
				int gap2 = -1;
				if (Chance(.6) && wallCells.Count >= 4)
				{
					var ends = new List<int>();
					if (wallCells[0] != gap) ends.Add(wallCells[0]);
					if (wallCells[wallCells.Count - 1] != gap) ends.Add(wallCells[wallCells.Count - 1]);
					if (ends.Count > 0) gap2 = ends[R(0, ends.Count - 1)];
				}
				foreach (int i in wallCells) g.C[i] = SOLID;
				var rest = comp.Where(i => g.C[i] == FLOOR).ToList();
				var restSet = new HashSet<int>(rest);
				var seen = new HashSet<int>();
				var parts = new List<List<int>>();
				foreach (int i in rest)
				{
					if (seen.Contains(i)) continue;
					var part = new List<int> { i };
					var st = new Stack<int>(); st.Push(i); seen.Add(i);
					while (st.Count > 0)
					{
						int ci = st.Pop(); int cx = ci % g.W, cy = ci / g.W;
						foreach (var d in N4)
						{
							int nx = cx + d[0], ny = cy + d[1];
							if (!g.Inb(nx, ny)) continue;
							int ni = g.Idx(nx, ny);
							if (restSet.Contains(ni) && !seen.Contains(ni)) { seen.Add(ni); part.Add(ni); st.Push(ni); }
						}
					}
					parts.Add(part);
				}
				if (parts.Count < 2 || parts.Any(pt => pt.Count < 8))
				{ foreach (int i in wallCells) g.C[i] = FLOOR; continue; }
				g.C[gap] = FLOOR;
				if (gap2 >= 0) g.C[gap2] = FLOOR;
				foreach (var part in parts) Subdivide(g, part, pockets, takeHall);
				return;
			}
			FinishRoom(g, comp, bb, pockets);
		}

		private static List<PocketRec> CarveBlocks(Grid g, Artery art, Stage S, bool wantHall)
		{
			var pockets = new List<PocketRec>();
			var claimed = new byte[g.W * g.H];
			int nClaims = R(S.C0, S.C1);
			bool hallLeft = wantHall;
			Func<bool> takeHall = () => { if (hallLeft) { hallLeft = false; return true; } return false; };
			for (int c = 0; c < nClaims; c++)
			{
				double t = Math.Min(.95, Math.Max(.05, (c + .5) / nClaims + R(-15, 15) / 100.0));
				var a = art.Path[Math.Min(art.Path.Count - 1, (int)(art.Path.Count * t))];
				int budget = R(BLOCK_MIN, BLOCK_MAX);
				var cells = new List<int[]>();
				int sx2 = a.X, sy2 = a.Y;
				for (int r2 = 0; r2 < 4 && cells.Count < budget; r2++)
				{
					int rw = R(6, 12), rh = R(4, 8);
					int rx = sx2 - R(1, rw - 2), ry = sy2 - R(1, rh - 2);
					for (int y = ry; y < ry + rh; y++)
						for (int x = rx; x < rx + rw; x++)
						{
							if (x < 1 || y < 1 || x >= g.W - 1 || y >= g.H - 1) continue;
							int ni = g.Idx(x, y);
							if (claimed[ni] != 0 || g.Host[ni] != 0) continue;
							if (art.Mask[ni] == 0) continue;
							bool wet = false;
							foreach (var d in N8)
							{
								int wx = x + d[0], wy = y + d[1];
								if (g.Inb(wx, wy) && g.Aqua[g.Idx(wx, wy)] != 0) { wet = true; break; }
							}
							if (wet) continue;
							if (g.C[ni] != FLOOR) { cells.Add(new[] { x, y }); claimed[ni] = 1; }
						}
					sx2 = rx + R(0, rw - 1); sy2 = ry + R(0, rh - 1);
				}
				if (cells.Count < BLOCK_MIN * 0.5) continue;
				var open = new List<int>();
				foreach (var cxy in cells)
				{
					bool ok = true;
					foreach (var d in N8)
					{
						int nx = cxy[0] + d[0], ny = cxy[1] + d[1];
						if (!g.Inb(nx, ny) || claimed[g.Idx(nx, ny)] == 0) { ok = false; break; }
					}
					if (ok) open.Add(g.Idx(cxy[0], cxy[1]));
				}
				foreach (int i in open) g.C[i] = FLOOR;
				var openSet = new HashSet<int>(open);
				var compSeen = new HashSet<int>();
				foreach (int i in open)
				{
					if (compSeen.Contains(i)) continue;
					var comp = new List<int> { i };
					var st = new Stack<int>(); st.Push(i); compSeen.Add(i);
					while (st.Count > 0)
					{
						int ci = st.Pop(); int cx = ci % g.W, cy = ci / g.W;
						foreach (var d in N4)
						{
							int nx = cx + d[0], ny = cy + d[1];
							if (!g.Inb(nx, ny)) continue;
							int ni = g.Idx(nx, ny);
							if (openSet.Contains(ni) && !compSeen.Contains(ni)) { compSeen.Add(ni); comp.Add(ni); st.Push(ni); }
						}
					}
					if (comp.Count >= 8) Subdivide(g, comp, pockets, takeHall);
					else foreach (int ci in comp) g.C[ci] = SOLID;
				}
				var doors = new List<int[]>();
				foreach (var cxy in cells)
				{
					int i = g.Idx(cxy[0], cxy[1]);
					if (g.C[i] == FLOOR) continue;
					foreach (var d in new[] { new[]{1,0}, new[]{0,1} })
					{
						int ax = cxy[0] + d[0], ay = cxy[1] + d[1], bx2 = cxy[0] - d[0], by2 = cxy[1] - d[1];
						if (!g.Inb(ax, ay) || !g.Inb(bx2, by2)) continue;
						int a1 = g.Idx(ax, ay), b1 = g.Idx(bx2, by2);
						bool roomA = g.C[a1] == FLOOR && g.Pocket[a1] >= 0, roadB = g.C[b1] == FLOOR && g.Pocket[b1] < 0;
						bool roomB = g.C[b1] == FLOOR && g.Pocket[b1] >= 0, roadA = g.C[a1] == FLOOR && g.Pocket[a1] < 0;
						if ((roomA && roadB) || (roomB && roadA)) { doors.Add(cxy); break; }
					}
				}
				int want = R(1, 2);
				while (want-- > 0 && doors.Count > 0)
				{
					var dxy = doors[R(0, doors.Count - 1)];
					doors.Remove(dxy);
					g.C[g.Idx(dxy[0], dxy[1])] = FLOOR;
					for (int k = doors.Count - 1; k >= 0; k--)
						if (Math.Abs(doors[k][0] - dxy[0]) + Math.Abs(doors[k][1] - dxy[1]) < 4) doors.RemoveAt(k);
				}
			}
			foreach (var p in pockets)
			{
				for (int y = p.In.Y0; y <= p.In.Y1; y++)
					for (int x = p.In.X0; x <= p.In.X1; x++)
					{
						int i = g.Idx(x, y);
						if (g.Pocket[i] != p.Id || g.C[i] != FLOOR) continue;
						foreach (var d in N4)
						{
							int nx = x + d[0], ny = y + d[1];
							if (!g.Inb(nx, ny)) continue;
							int ni = g.Idx(nx, ny);
							if (g.C[ni] != FLOOR) continue;
							if (g.Art[ni] != 0) p.Through++;
							if (g.Pocket[ni] != p.Id) p.Entrances = 1;
						}
					}
			}
			return pockets;
		}

		private static int _noiseSeed;
		private static double VNoise(double x, double y)
		{
			double Hsh(int a, int b)
			{
				unchecked
				{
					int n = (a * 374761393) ^ (b * 668265263) ^ (_noiseSeed * (int)2246822519u);
					n = (n ^ (int)((uint)n >> 13)) * 1274126177;
					return ((uint)(n ^ (int)((uint)n >> 16)) / 4294967296.0) * 2 - 1;
				}
			}
			int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
			double fx = x - x0, fy = y - y0;
			double sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
			double a2 = Hsh(x0, y0), b2 = Hsh(x0 + 1, y0), c2 = Hsh(x0, y0 + 1), d2 = Hsh(x0 + 1, y0 + 1);
			double t = a2 + (b2 - a2) * sx, u = c2 + (d2 - c2) * sx;
			return t + (u - t) * sy;
		}

		private static double TunnelCost(Grid g, int x, int y, int emptyBase, bool noiseOn)
		{
			if (x == 0 || y == 0 || x == g.W - 1 || y == g.H - 1) return 2000;
			if (g.Host[g.Idx(x, y)] != 0) return 1e6;
			double n = noiseOn ? Math.Abs(VNoise(x / 3.0, y)) * 160 : 0;
			if (Touches4(g, x, y, i => g.Host[i] != 0)) return 900 + n;
			if (g.C[g.Idx(x, y)] == FLOOR) return n + emptyBase;
			double hug = 0;
			foreach (var d in N4) if (g.Inb(x + d[0], y + d[1]) && Wf(g, x + d[0], y + d[1])) hug += HUG_COST;
			bool Open(int a, int b) => g.Inb(a, b) && g.C[g.Idx(a, b)] == FLOOR;
			foreach (var d in new[] { new[]{1,1}, new[]{1,-1}, new[]{-1,1}, new[]{-1,-1} })
				if (Open(x + d[0], y + d[1]) && !Open(x + d[0], y) && !Open(x, y + d[1])) hug += PINCH_COST;
			return 80 + n + hug;
		}

		private static List<int> Tunnel(Grid g, int ax, int ay, int bx, int by, int emptyBase, bool noiseOn, Func<int, int, double> costFn = null)
		{
			int SZ = g.W * g.H;
			var dist = new double[SZ]; for (int i = 0; i < SZ; i++) dist[i] = double.PositiveInfinity;
			var prev = new int[SZ]; for (int i = 0; i < SZ; i++) prev[i] = -1;
			var done = new byte[SZ];
			var heap = new List<(double c, int i)> { (0, ay * g.W + ax) };
			dist[ay * g.W + ax] = 0;
			void Push((double c, int i) v)
			{
				heap.Add(v);
				int i = heap.Count - 1;
				while (i > 0) { int p = (i - 1) >> 1; if (heap[p].c <= heap[i].c) break; (heap[p], heap[i]) = (heap[i], heap[p]); i = p; }
			}
			(double c, int i) Pop()
			{
				var t = heap[0]; var l = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
				if (heap.Count > 0)
				{
					heap[0] = l;
					int i = 0;
					while (true)
					{
						int a2 = 2 * i + 1, b2 = a2 + 1, m = i;
						if (a2 < heap.Count && heap[a2].c < heap[m].c) m = a2;
						if (b2 < heap.Count && heap[b2].c < heap[m].c) m = b2;
						if (m == i) break;
						(heap[m], heap[i]) = (heap[i], heap[m]); i = m;
					}
				}
				return t;
			}
			int goal = by * g.W + bx;
			while (heap.Count > 0)
			{
				var (c, cur) = Pop();
				if (done[cur] != 0) continue;
				done[cur] = 1;
				if (cur == goal) break;
				int cx = cur % g.W, cy = cur / g.W;
				var dirs = new List<int[]>(N4);
				for (int i = dirs.Count - 1; i > 0; i--) { int j = R(0, i); (dirs[i], dirs[j]) = (dirs[j], dirs[i]); }
				foreach (var d in dirs)
				{
					int nx = cx + d[0], ny = cy + d[1];
					if (!g.Inb(nx, ny)) continue;
					int ni = ny * g.W + nx;
					if (done[ni] != 0) continue;
					double w = costFn != null ? costFn(nx, ny) : TunnelCost(g, nx, ny, emptyBase, noiseOn);
					if (w >= 1e6) continue;
					double nc = c + w;
					if (nc < dist[ni]) { dist[ni] = nc; prev[ni] = cur; Push((nc, ni)); }
				}
			}
			if (prev[goal] < 0 && goal != ay * g.W + ax) return null;
			var path = new List<int>();
			int cur2 = goal;
			while (cur2 >= 0) { path.Add(cur2); cur2 = prev[cur2]; }
			path.Reverse();
			return path;
		}

		private static int CarveTunnel(Grid g, List<int> path)
		{
			int cut = 0;
			foreach (int i in path) if (g.C[i] != FLOOR) { g.C[i] = FLOOR; cut++; }
			return cut;
		}

		private static int ConnectPocket(Grid g, PocketRec p, Artery art, int? forceWant = null)
		{
			int want = forceWant ?? (p.Through > 0 ? (Chance(.4) ? 1 : 0) : R(1, 2));
			if (want <= 0) return 0;
			var mine = new List<int[]>();
			for (int y = p.In.Y0; y <= p.In.Y1; y++)
				for (int x = p.In.X0; x <= p.In.X1; x++)
					if (g.C[g.Idx(x, y)] == FLOOR) mine.Add(new[] { x, y });
			if (mine.Count == 0) return 0;
			var targets = art.Path.Where(q => g.C[g.Idx(q.X, q.Y)] == FLOOR).ToList();
			if (targets.Count == 0) return 0;
			int made = 0;
			for (int k = 0; k < want; k++)
			{
				var from = mine[R(0, mine.Count - 1)];
				var to = targets[R(0, targets.Count - 1)];
				var path = Tunnel(g, from[0], from[1], to.X, to.Y, EMPTY_BASE, true);
				if (path == null) continue;
				if (CarveTunnel(g, path) >= 0) { made++; p.Entrances++; }
			}
			return made;
		}

		private static int AddCrosscuts(Grid g, int n)
		{
			int made = 0, tries = 0;
			var floors = new List<int[]>();
			for (int y = 1; y < g.H - 1; y++)
				for (int x = 1; x < g.W - 1; x++)
					if (Wf(g, x, y)) floors.Add(new[] { x, y });
			if (floors.Count < 4) return 0;
			while (made < n && tries++ < 60)
			{
				var a = floors[R(0, floors.Count - 1)];
				var b = floors[R(0, floors.Count - 1)];
				if (Math.Abs(a[0] - b[0]) + Math.Abs(a[1] - b[1]) < 6) continue;
				int d = GraphDist(g, a[0], a[1], b[0], b[1], 60);
				if (d >= 0 && d < 14) continue;
				var path = Tunnel(g, a[0], a[1], b[0], b[1], EMPTY_BASE, true);
				if (path == null) continue;
				if (CarveTunnel(g, path) > 0) made++;
			}
			return made;
		}

		private static int Infill(Grid g, Artery art, List<PocketRec> pockets, int n)
		{
			int made = 0, tries = 0;
			while (made < n && tries++ < 500)
			{
				int w = R(6, 10), h = R(5, 8);
				int lo = Math.Max(1, art.Foot.X0), hi = Math.Min(g.W - 2 - w, art.Foot.X1 - w + 1);
				int lo2 = Math.Max(1, art.Foot.Y0), hi2 = Math.Min(g.H - 2 - h, art.Foot.Y1 - h + 1);
				if (hi < lo || hi2 < lo2) continue;
				var box = new Box { X0 = R(lo, hi), Y0 = R(lo2, hi2) };
				box.X1 = box.X0 + w - 1; box.Y1 = box.Y0 + h - 1;
				if (box.X1 >= g.W - 1 || box.Y1 >= g.H - 1) continue;
				bool ok = true;
				for (int y = box.Y0; y <= box.Y1 && ok; y++)
					for (int x = box.X0; x <= box.X1; x++)
						if (g.C[g.Idx(x, y)] != SOLID || art.Mask[g.Idx(x, y)] == 0) { ok = false; break; }
				if (!ok) continue;
				bool near = false;
				for (int y = box.Y0 - 2; y <= box.Y1 + 2 && !near; y++)
					for (int x = box.X0 - 2; x <= box.X1 + 2; x++)
						if (g.Inb(x, y) && g.C[g.Idx(x, y)] == FLOOR) { near = true; break; }
				if (!near) continue;
				var p = new PocketRec { BoxOut = box, Through = 0, Id = pockets.Count, Infill = true };
				pockets.Add(p);
				ResolvePocket(g, p);
				p.Entrances = 0;
				if (ConnectPocket(g, p, art, 1) == 0)
				{
					for (int y = p.In.Y0; y <= p.In.Y1; y++)
						for (int x = p.In.X0; x <= p.In.X1; x++)
						{ g.C[g.Idx(x, y)] = SOLID; g.Pocket[g.Idx(x, y)] = -1; }
					pockets.RemoveAt(pockets.Count - 1);
					continue;
				}
				made++;
			}
			return made;
		}

		private class AquiferRec { public int Pools, Veins; }
		private static AquiferRec PlaceAquifers(Grid g, Band band)
		{
			var rec = new AquiferRec();
			if (RollLabelTable(ref _aquiferRows, "Cleo_TerraFirma_WarrenAquifers",
				new[] { "dry", "aquifer" }) != "aquifer") return rec;
			int n = R(AQUIFER_MIN, AQUIFER_MAX);
			var interior = band.Cells.Where(i =>
			{
				int x = i % g.W, y = i / g.W;
				if (x <= 4 || y <= 3 || x >= g.W - 5 || y >= g.H - 4) return false;
				return IsInnerOf(g, x, y, i => g.C[i] != FLOOR);
			}).ToList();
			if (interior.Count == 0) return rec;
			void Wet(int x, int y)
			{
				if (!g.Inb(x, y) || x < 1 || y < 1 || x > g.W - 2 || y > g.H - 2) return;
				int i = g.Idx(x, y);
				g.C[i] = FLOOR; g.Host[i] = 1; g.Water[i] = 1; g.Aqua[i] = 1;
			}
			var nodes = new List<int[]>();
			for (int i = 0; i < n; i++)
			{
				int ci = interior[R(0, interior.Count - 1)];
				int cx = ci % g.W, cy = ci / g.W;
				if (g.Water[g.Idx(cx, cy)] != 0) continue;
				int want = R(POOL_MIN, POOL_MAX);
				var q = new List<int[]> { new[] { cx, cy } };
				var seen = new HashSet<int> { g.Idx(cx, cy) };
				int placed = 0;
				while (q.Count > 0 && placed < want)
				{
					int pi = R(0, q.Count - 1);
					var c = q[pi]; q.RemoveAt(pi);
					Wet(c[0], c[1]); placed++;
					foreach (var d in N4)
					{
						int nx = c[0] + d[0], ny = c[1] + d[1];
						if (!g.Inb(nx, ny) || seen.Contains(g.Idx(nx, ny)) || g.C[g.Idx(nx, ny)] == FLOOR) continue;
						seen.Add(g.Idx(nx, ny));
						if (Chance(.65)) q.Add(new[] { nx, ny });
					}
				}
				if (placed > 0) { rec.Pools++; nodes.Add(new[] { cx, cy }); }
			}
			double Geology(int x, int y)
			{
				if (x == 0 || y == 0 || x == g.W - 1 || y == g.H - 1) return 2000;
				if (g.C[g.Idx(x, y)] == FLOOR && g.Aqua[g.Idx(x, y)] == 0) return 1e6;
				return 80 + Math.Abs(VNoise(x / 3.0, y)) * 160;
			}
			foreach (var node in nodes)
			{
				if (!Chance(VEIN_CHANCE / 100.0)) continue;
				var cands = new List<int[]>();
				for (int y = 1; y < g.H - 1; y++)
					for (int x = 1; x < g.W - 1; x++)
						if (g.C[g.Idx(x, y)] != FLOOR && Math.Abs(x - node[0]) + Math.Abs(y - node[1]) < 30) cands.Add(new[] { x, y });
				if (cands.Count == 0) continue;
				var t = cands[R(0, cands.Count - 1)];
				var path = Tunnel(g, node[0], node[1], t[0], t[1], 0, true, Geology);
				if (path == null) continue;
				foreach (int i in path) Wet(i % g.W, i / g.W);
				rec.Veins++;
			}
			return rec;
		}

		private static int BreachAquifers(Grid g)
		{
			if (!Chance(BREACH_CHANCE / 100.0)) return 0;
			var targets = new List<int[]>();
			for (int y = 1; y < g.H - 1; y++)
				for (int x = 1; x < g.W - 1; x++)
				{
					if (g.Aqua[g.Idx(x, y)] == 0) continue;
					foreach (var d in N4)
					{
						int rx = x + d[0], ry = y + d[1];
						if (!g.Inb(rx, ry) || g.C[g.Idx(rx, ry)] == FLOOR) continue;
						for (int dd = 1; dd <= BREACH_REACH; dd++)
						{
							int fx = rx + d[0] * dd, fy = ry + d[1] * dd;
							if (!g.Inb(fx, fy)) break;
							if (g.Host[g.Idx(fx, fy)] != 0) break;
							if (Wf(g, fx, fy)) { targets.Add(new[] { rx, ry, -d[0], -d[1], dd }); break; }
						}
					}
				}
			if (targets.Count == 0) return 0;
			int made = 0, want = R(1, BREACH_MAX);
			for (int k = 0; k < targets.Count && made < want; k++)
			{
				var t = targets[R(0, targets.Count - 1)];
				bool ok = true;
				for (int i = 0; i < t[4]; i++)
				{
					int cx = t[0] + t[2] * i, cy = t[1] + t[3] * i;
					if (!g.Inb(cx, cy) || g.Host[g.Idx(cx, cy)] != 0) { ok = false; break; }
				}
				if (!ok) continue;
				for (int i = 0; i < t[4]; i++) g.C[g.Idx(t[0] + t[2] * i, t[1] + t[3] * i)] = FLOOR;
				made++;
			}
			return made;
		}

		private static int BreachGardens(Grid g, out int found)
		{
			found = 0;
			var live = new byte[g.W * g.H];
			if (g.MouthIdx >= 0)
			{
				var q = new Queue<int>();
				live[g.MouthIdx] = 1; q.Enqueue(g.MouthIdx);
				while (q.Count > 0)
				{
					int c = q.Dequeue(); int cx = c % g.W, cy = c / g.W;
					foreach (var d in N4)
					{
						int nx = cx + d[0], ny = cy + d[1];
						if (!g.Inb(nx, ny)) continue;
						int ni = g.Idx(nx, ny);
						if (live[ni] == 0 && g.C[ni] == FLOOR) { live[ni] = 1; q.Enqueue(ni); }
					}
				}
			}
			var seen = new byte[g.W * g.H];
			var gardens = new List<List<int>>();
			for (int i0 = 0; i0 < g.W * g.H; i0++)
			{
				if (seen[i0] != 0 || live[i0] != 0 || g.C[i0] != FLOOR) continue;
				var region = new List<int> { i0 };
				var st = new Stack<int>(); st.Push(i0); seen[i0] = 1;
				bool pureHost = true, wet = false;
				while (st.Count > 0)
				{
					int c = st.Pop(); int cx = c % g.W, cy = c / g.W;
					if (g.Host[c] == 0) pureHost = false;
					if (g.Water[c] != 0) wet = true;
					foreach (var d in N4)
					{
						int nx = cx + d[0], ny = cy + d[1];
						if (!g.Inb(nx, ny)) continue;
						int ni = g.Idx(nx, ny);
						if (seen[ni] == 0 && live[ni] == 0 && g.C[ni] == FLOOR) { seen[ni] = 1; region.Add(ni); st.Push(ni); }
					}
				}
				if (pureHost && !wet && region.Count >= GARDEN_MIN) gardens.Add(region);
			}
			found = gardens.Count;
			if (gardens.Count == 0) return 0;
			var cuts = new List<int[]>();
			foreach (var region in gardens)
			{
				int[] best = null;
				foreach (int c in region)
				{
					int cx = c % g.W, cy = c / g.W;
					foreach (var d in N4)
					{
						int rx = cx + d[0], ry = cy + d[1];
						if (!g.Inb(rx, ry) || g.C[g.Idx(rx, ry)] == FLOOR) continue;
						for (int dd = 1; dd <= GARDEN_REACH; dd++)
						{
							int fx = rx + d[0] * (dd - 1), fy = ry + d[1] * (dd - 1);
							if (!g.Inb(fx, fy)) break;
							int fi = g.Idx(fx, fy);
							if (g.C[fi] == FLOOR)
							{
								if (live[fi] != 0 && g.Host[fi] == 0 && (best == null || dd - 1 < best[4]))
									best = new[] { rx, ry, d[0], d[1], dd - 1 };
								break;
							}
							if (g.Aqua[fi] != 0 || g.Water[fi] != 0) break;
						}
					}
				}
				if (best != null && best[4] > 0) cuts.Add(best);
			}
			if (cuts.Count == 0) return 0;
			int made = 0, want = Chance(GARDEN_BREACH_PCT / 100.0) ? R(1, GARDEN_MAX) : 0;
			for (int k = 0; k < cuts.Count && made < want; k++)
			{
				int ci = R(0, cuts.Count - 1);
				var t = cuts[ci]; cuts.RemoveAt(ci);
				bool ok = true;
				for (int i = 0; i < t[4]; i++)
					if (g.C[g.Idx(t[0] + t[2] * i, t[1] + t[3] * i)] == FLOOR) { ok = false; break; }
				if (!ok) continue;
				for (int i = 0; i < t[4]; i++) g.C[g.Idx(t[0] + t[2] * i, t[1] + t[3] * i)] = FLOOR;
				made++;
			}
			return made;
		}

		private static int GraphDist(Grid g, int ax, int ay, int bx, int by, int cap)
		{
			var seen = new byte[g.W * g.H];
			var q = new Queue<int[]>();
			q.Enqueue(new[] { ax, ay, 0 });
			seen[g.Idx(ax, ay)] = 1;
			while (q.Count > 0)
			{
				var c = q.Dequeue();
				if (c[0] == bx && c[1] == by) return c[2];
				if (c[2] >= cap) continue;
				foreach (var d in N4)
				{
					int nx = c[0] + d[0], ny = c[1] + d[1];
					if (g.Inb(nx, ny) && seen[g.Idx(nx, ny)] == 0 && g.C[g.Idx(nx, ny)] == FLOOR)
					{ seen[g.Idx(nx, ny)] = 1; q.Enqueue(new[] { nx, ny, c[2] + 1 }); }
				}
			}
			return -1;
		}

		private class Measure0 { public int Loops, Comps, Floor; }
		private static Measure0 MeasureG(Grid g)
		{
			var node = new int[g.W * g.H];
			for (int i = 0; i < node.Length; i++) node[i] = -1;
			int next = 0, floor = 0;
			for (int y = 0; y < g.H; y++)
				for (int x = 0; x < g.W; x++)
				{
					if (!Wf(g, x, y)) continue;
					floor++;
					if (node[g.Idx(x, y)] >= 0) continue;
					int pid = g.Pocket[g.Idx(x, y)], id = next++;
					if (pid < 0) { node[g.Idx(x, y)] = id; continue; }
					var st = new Stack<int[]>(); st.Push(new[] { x, y });
					node[g.Idx(x, y)] = id;
					while (st.Count > 0)
					{
						var c = st.Pop();
						foreach (var d in N4)
						{
							int nx = c[0] + d[0], ny = c[1] + d[1];
							if (g.Inb(nx, ny) && node[g.Idx(nx, ny)] < 0 && Wf(g, nx, ny) && g.Pocket[g.Idx(nx, ny)] == pid)
							{ node[g.Idx(nx, ny)] = id; st.Push(new[] { nx, ny }); }
						}
					}
				}
			int V = next;
			var edges = new HashSet<long>();
			for (int y = 0; y < g.H; y++)
				for (int x = 0; x < g.W; x++)
				{
					if (!Wf(g, x, y)) continue;
					int a = node[g.Idx(x, y)];
					foreach (var d in new[] { new[]{1,0}, new[]{0,1} })
					{
						int nx = x + d[0], ny = y + d[1];
						if (!g.Inb(nx, ny) || !Wf(g, nx, ny)) continue;
						int b = node[g.Idx(nx, ny)];
						if (a != b) edges.Add(a < b ? ((long)a << 20) | (uint)b : ((long)b << 20) | (uint)a);
					}
				}
			var adj = new Dictionary<int, List<int>>();
			foreach (long e in edges)
			{
				int a = (int)(e >> 20), b = (int)(e & 0xFFFFF);
				if (!adj.TryGetValue(a, out var la)) adj[a] = la = new List<int>();
				la.Add(b);
				if (!adj.TryGetValue(b, out var lb)) adj[b] = lb = new List<int>();
				lb.Add(a);
			}
			var seen = new HashSet<int>();
			int C = 0;
			for (int i = 0; i < V; i++)
			{
				if (seen.Contains(i)) continue;
				C++;
				var st = new Stack<int>(); st.Push(i); seen.Add(i);
				while (st.Count > 0)
				{
					int n2 = st.Pop();
					if (adj.TryGetValue(n2, out var l))
						foreach (int m in l) if (!seen.Contains(m)) { seen.Add(m); st.Push(m); }
				}
			}
			return new Measure0 { Loops = edges.Count - V + C, Comps = C, Floor = floor };
		}

		private static int ComponentCount(Grid g)
		{
			var seen = new byte[g.W * g.H];
			int C = 0;
			for (int y = 0; y < g.H; y++)
				for (int x = 0; x < g.W; x++)
				{
					if (!Wf(g, x, y) || seen[g.Idx(x, y)] != 0) continue;
					C++;
					var st = new Stack<int[]>(); st.Push(new[] { x, y }); seen[g.Idx(x, y)] = 1;
					while (st.Count > 0)
					{
						var c = st.Pop();
						foreach (var d in N4)
						{
							int nx = c[0] + d[0], ny = c[1] + d[1];
							if (g.Inb(nx, ny) && seen[g.Idx(nx, ny)] == 0 && Wf(g, nx, ny))
							{ seen[g.Idx(nx, ny)] = 1; st.Push(new[] { nx, ny }); }
						}
					}
				}
			return C;
		}

		private static int TrimSlack(Grid g)
		{
			int trimmed = 0;
			for (int y = 0; y < g.H - 1; y++)
				for (int x = 0; x < g.W - 1; x++)
				{
					var q = new[] { new[]{x,y}, new[]{x+1,y}, new[]{x,y+1}, new[]{x+1,y+1} };
					if (!q.All(c => Wf(g, c[0], c[1]))) continue;
					var cand = q.Where(c => g.Pocket[g.Idx(c[0], c[1])] < 0 && g.Idx(c[0], c[1]) != g.MouthIdx).ToList();
					if (cand.Count == 0) continue;
					foreach (var c in cand)
					{
						g.C[g.Idx(c[0], c[1])] = SOLID;
						if (ComponentCount(g) == 1) { trimmed++; break; }
						g.C[g.Idx(c[0], c[1])] = FLOOR;
					}
				}
			return trimmed;
		}

		private static int RepairConnectivity(Grid g)
		{
			int repairs = 0;
			for (int guard = 0; guard < 16; guard++)
			{
				var lab = new int[g.W * g.H];
				for (int i = 0; i < lab.Length; i++) lab[i] = -1;
				var regions = new List<(List<int[]> cells, double cx, double cy)>();
				for (int y = 0; y < g.H; y++)
					for (int x = 0; x < g.W; x++)
					{
						if (!Wf(g, x, y) || lab[g.Idx(x, y)] >= 0) continue;
						int id = regions.Count;
						var cells = new List<int[]>();
						var st = new Stack<int[]>(); st.Push(new[] { x, y }); lab[g.Idx(x, y)] = id;
						while (st.Count > 0)
						{
							var c = st.Pop(); cells.Add(c);
							foreach (var d in N4)
							{
								int nx = c[0] + d[0], ny = c[1] + d[1];
								if (g.Inb(nx, ny) && lab[g.Idx(nx, ny)] < 0 && Wf(g, nx, ny))
								{ lab[g.Idx(nx, ny)] = id; st.Push(new[] { nx, ny }); }
							}
						}
						regions.Add((cells, cells.Average(c => (double)c[0]), cells.Average(c => (double)c[1])));
					}
				if (regions.Count <= 1) return repairs;
				regions.Sort((a, b) => b.cells.Count.CompareTo(a.cells.Count));
				var joined = new List<(List<int[]> cells, double cx, double cy)> { regions[0] };
				bool progressed = false;
				for (int i = 1; i < regions.Count; i++)
				{
					var r = regions[i];
					joined.Sort((a, b) =>
					{
						double da = (a.cx - r.cx) * (a.cx - r.cx) + (a.cy - r.cy) * (a.cy - r.cy);
						double db = (b.cx - r.cx) * (b.cx - r.cx) + (b.cy - r.cy) * (b.cy - r.cy);
						return da.CompareTo(db);
					});
					var near = joined[0];
					var A = r.cells[R(0, r.cells.Count - 1)];
					var B = near.cells[R(0, near.cells.Count - 1)];
					var path = Tunnel(g, A[0], A[1], B[0], B[1], EMPTY_BASE, false);
					if (path != null && CarveTunnel(g, path) >= 0) { repairs++; progressed = true; }
					joined.Add(r);
				}
				if (!progressed) return repairs;
			}
			return repairs;
		}

		private class RolesRec { public string Tally; public string YardDesc; public Box YardRect; public string YardState; }
		private static RolesRec RegisterRoles(Grid g, Artery art, List<PocketRec> pockets, int si, string occupancy)
		{
			var dist = new int[g.W * g.H];
			for (int i = 0; i < dist.Length; i++) dist[i] = -1;
			var q = new List<int[]> { new[] { art.Mouth.X, art.Mouth.Y } };
			dist[g.MouthIdx] = 0;
			while (q.Count > 0)
			{
				var nq = new List<int[]>();
				foreach (var c in q)
					foreach (var d in N4)
					{
						int nx = c[0] + d[0], ny = c[1] + d[1];
						if (g.Inb(nx, ny) && dist[g.Idx(nx, ny)] < 0 && g.C[g.Idx(nx, ny)] == FLOOR)
						{ dist[g.Idx(nx, ny)] = dist[g.Idx(c[0], c[1])] + 1; nq.Add(new[] { nx, ny }); }
					}
				q = nq;
			}
			g.MouthDist = dist;
			foreach (var p in pockets)
			{
				if (p.Entrances > 0) continue;
				for (int y = p.In.Y0; y <= p.In.Y1 && p.Entrances == 0; y++)
					for (int x = p.In.X0; x <= p.In.X1; x++)
					{
						if (g.Pocket[g.Idx(x, y)] != p.Id || g.C[g.Idx(x, y)] != FLOOR) continue;
						bool found = false;
						foreach (var d in N4)
						{
							int nx = x + d[0], ny = y + d[1];
							if (g.Inb(nx, ny) && g.C[g.Idx(nx, ny)] == FLOOR && g.Pocket[g.Idx(nx, ny)] != p.Id) { found = true; break; }
						}
						if (found) { p.Entrances = 1; break; }
					}
			}
			foreach (var p in pockets)
			{
				int dmin2 = int.MaxValue;
				for (int y = p.In.Y0; y <= p.In.Y1; y++)
					for (int x = p.In.X0; x <= p.In.X1; x++)
					{
						int dd = dist[g.Idx(x, y)];
						if (dd >= 0 && dd < dmin2) dmin2 = dd;
					}
				p.Depth = dmin2 == int.MaxValue ? 9999 : dmin2;
				p.Role = null;
			}
			var ranked = pockets.OrderBy(p => p.Depth).ToList();
			for (int i = 0; i < ranked.Count; i++) ranked[i].DepthRank = i;
			var reach = ranked.Where(p => p.Depth < 9999).ToList();
			int dmin = reach.Count > 0 ? reach[0].Depth : 0;
			int dmax = reach.Count > 0 ? reach[reach.Count - 1].Depth : 0;
			int span = Math.Max(1, dmax - dmin);
			bool Near(PocketRec p) => p.Depth < 9999 && (double)(p.Depth - dmin) / span < 0.34;
			if (si == 2)
			{
				var c = reach.FirstOrDefault(p => p.Character == "hall");
				if (c != null) c.Role = "commons";
			}
			string yardDesc = "none";
			Box yardRect = null;
			bool ClearAt(int i) => g.C[i] == FLOOR && g.Art[i] == 0 && g.Water[i] == 0 && g.Tree[i] == 0 && i != g.MouthIdx;
			Box BestRectMin(HashSet<int> cl, int minW, int minH)
			{
				if (cl.Count == 0) return null;
				int bx0 = g.W, by0 = g.H, bx1 = 0, by1 = 0;
				foreach (int i in cl)
				{
					int x = i % g.W, y = i / g.W;
					if (x < bx0) bx0 = x; if (x > bx1) bx1 = x;
					if (y < by0) by0 = y; if (y > by1) by1 = y;
				}
				if (bx1 < bx0) return null;
				int hw = bx1 - bx0 + 1;
				var hist = new int[hw];
				Box best = null; int bestScore = -1;
				for (int y = by0; y <= by1; y++)
				{
					for (int xi = 0; xi < hw; xi++) hist[xi] = cl.Contains(g.Idx(bx0 + xi, y)) ? hist[xi] + 1 : 0;
					for (int a = 0; a < hw; a++)
					{
						int mh = int.MaxValue;
						for (int b = a; b < hw; b++)
						{
							mh = Math.Min(mh, hist[b]);
							if (mh == 0) break;
							int w2 = b - a + 1;
							if (w2 < minW || mh < minH) continue;
							int cw2 = Math.Min(w2, 9), ch2 = Math.Min(mh, 7), score = cw2 * ch2;
							if (score <= bestScore) continue;
							bestScore = score;
							int rx0 = bx0 + a, rx1 = bx0 + b, ry0 = y - mh + 1, ry1 = y;
							if (w2 > cw2) { if (art.Mouth.X < (rx0 + rx1) / 2.0) rx1 = rx0 + cw2 - 1; else rx0 = rx1 - cw2 + 1; }
							if (mh > ch2) { if (art.Mouth.Y < (ry0 + ry1) / 2.0) ry1 = ry0 + ch2 - 1; else ry0 = ry1 - ch2 + 1; }
							best = new Box { X0 = rx0, Y0 = ry0, X1 = rx1, Y1 = ry1 };
						}
					}
				}
				return best;
			}
			Box BestRect(HashSet<int> cl) => BestRectMin(cl, 5, 5) ?? BestRectMin(cl, 5, 4) ?? BestRectMin(cl, 4, 5);
			string yardState = null;
			PocketRec insidePocket = null; Box insideRect = null;
			foreach (var p in reach)
			{
				if (p.Role != null || p.Through != 0 || p.Character == "hall" || !Near(p)) continue;
				var cl = new HashSet<int>();
				for (int y = p.In.Y0; y <= p.In.Y1; y++)
					for (int x = p.In.X0; x <= p.In.X1; x++)
					{
						int i = g.Idx(x, y);
						if (g.Pocket[i] == p.Id && ClearAt(i)) cl.Add(i);
					}
				var r2 = BestRect(cl);
				if (r2 == null) continue;
				insidePocket = p; insideRect = r2;
				break;
			}
			Box outsideRect = null;
			{
				var cl = new HashSet<int>();
				var seen2 = new byte[g.W * g.H];
				var q2 = new List<int[]> { new[] { art.Mouth.X, art.Mouth.Y } };
				seen2[g.MouthIdx] = 1;
				int visited = 0;
				while (q2.Count > 0 && visited < 320)
				{
					var nq = new List<int[]>();
					foreach (var c in q2)
						foreach (var d in N4)
						{
							int nx = c[0] + d[0], ny = c[1] + d[1];
							if (!g.Inb(nx, ny) || seen2[g.Idx(nx, ny)] != 0 || g.Host[g.Idx(nx, ny)] == 0 || g.Water[g.Idx(nx, ny)] != 0) continue;
							seen2[g.Idx(nx, ny)] = 1; visited++; nq.Add(new[] { nx, ny });
							cl.Add(g.Idx(nx, ny));
						}
					q2 = nq;
				}
				outsideRect = BestRect(cl);
			}
			int AreaOf(Box r2) => (r2.X1 - r2.X0 + 1) * (r2.Y1 - r2.Y0 + 1);
			if (insideRect != null && (outsideRect == null || AreaOf(insideRect) >= AreaOf(outsideRect)))
			{
				insidePocket.Role = "blockyard";
				insidePocket.Footprint = new List<int>();
				for (int y = insideRect.Y0; y <= insideRect.Y1; y++)
					for (int x = insideRect.X0; x <= insideRect.X1; x++) insidePocket.Footprint.Add(g.Idx(x, y));
				yardDesc = $"inside pocket {insidePocket.Id} ({insideRect.X1 - insideRect.X0 + 1}x{insideRect.Y1 - insideRect.Y0 + 1})";
				yardRect = insideRect;
			}
			else if (outsideRect != null)
			{
				yardDesc = $"outside ({outsideRect.X1 - outsideRect.X0 + 1}x{outsideRect.Y1 - outsideRect.Y0 + 1})";
				yardRect = outsideRect;
			}
			if (yardDesc != "none")
			{
				if (occupancy == "abandoned") yardState = Chance(.3) ? "robbed" : "abandoned";
				else if (occupancy == "reclaimed") yardState = "cleared";
				else
				{
					int[][] w3 = { new[]{30,60,10}, new[]{15,65,20}, new[]{5,55,40} };
					int roll3 = R(1, 100);
					yardState = roll3 <= w3[si][0] ? "fresh" : roll3 <= w3[si][0] + w3[si][1] ? "working" : "full";
				}
				yardDesc += " state=" + yardState;
			}
			var deepP = reach.Count > 0 ? reach[reach.Count - 1] : null;
			if (deepP != null && deepP.Role == null) deepP.Role = "face";
			foreach (var p in pockets) if (p.Role == null && p.Character == "hall") p.Role = "face";
			foreach (var p in pockets) if (p.Role == null) p.Role = p.Through == 0 ? "dwelling" : "plain";
			var tally = pockets.GroupBy(p => p.Role).ToDictionary(gr => gr.Key, gr => gr.Count());
			string tstr = string.Join(" ", new[] { "commons", "blockyard", "face", "dwelling", "plain" }
				.Select(role => role + ":" + (tally.TryGetValue(role, out int v) ? v : 0)));
			return new RolesRec { Tally = tstr, YardDesc = yardDesc, YardRect = yardRect, YardState = yardState };
		}

		private class Row { public int Wt; public string Id, Hint; public Row(int w, string id, string hint = null) { Wt = w; Id = id; Hint = hint; } }
		private const string HINT_FACE = "Cleo_TerraFirma_Face";
		private static Row[] _tSocket, _tGalleryBay, _tGalleryPillar, _tGalleryFloor, _tNatural, _tPlainTrace, _tHallTable;
		private static Row[] T_SOCKET => _tSocket ?? (_tSocket = LoadDressTable("Socket"));
		private static Row[] T_GALLERYBAY => _tGalleryBay ?? (_tGalleryBay = LoadDressTable("GalleryBay"));
		private static Row[] T_GALLERYPILLAR => _tGalleryPillar ?? (_tGalleryPillar = LoadDressTable("GalleryPillar"));
		private static Row[] T_GALLERYFLOOR => _tGalleryFloor ?? (_tGalleryFloor = LoadDressTable("GalleryFloor"));
		private static Row[] T_NATURAL => _tNatural ?? (_tNatural = LoadDressTable("Natural"));
		private static Row[] T_PLAINTRACE => _tPlainTrace ?? (_tPlainTrace = LoadDressTable("PlainTrace"));
		private static Row[] T_HALLTABLE => _tHallTable ?? (_tHallTable = LoadDressTable("HallTable"));
		private static Row[] _tPlaza, _tWorkScatter;
		private static Row[] T_PLAZA => _tPlaza ?? (_tPlaza = LoadDressTable("Plaza"));
		private static Row[] T_WORKSCATTER => _tWorkScatter ?? (_tWorkScatter = LoadDressTable("WorkScatter"));
		private static Row[] _tStatues;
		private static Row[] T_STATUES => _tStatues ?? (_tStatues = LoadDressTable("StatueLikeness"));
		private static readonly string[] MONUMENTS = {
			"Village Monument Obelisk", "Village Monument Amphora", "Village Monument Birdhouse",
			"Village Monument Diptych", "Village Monument Stele", "Village Monument Monolith", "Village Monument Orb" };
		private static bool IsPlacementDirective(string id)
			=> id != null && (id.StartsWith("chalkline", StringComparison.Ordinal) || id == "yardboard");

		private static readonly Dictionary<string, bool> _solidCache = new Dictionary<string, bool>();
		private static bool IsSolidItem(string id)
		{
			if (id == null || IsPlacementDirective(id)) return false;
			if (!_solidCache.TryGetValue(id, out bool s))
				_solidCache[id] = s = GameObjectFactory.Factory.GetBlueprint(id)?.GetPartParameter("Physics", "Solid", false) ?? false;
			return s;
		}
		private static Dictionary<string, string[]> _roomPools;
		private static Dictionary<string, string[]> ROOMPOOLS => _roomPools ?? (_roomPools = new Dictionary<string, string[]>
		{
			{ "light", LoadDressPool("Light") },
			{ "storage", LoadDressPool("Storage") },
			{ "seating", LoadDressPool("Seating") },
			{ "sleep", LoadDressPool("Sleep") },
			{ "centre", LoadDressPool("Centre") },
			{ "liquid", LoadDressPool("Liquid") },
			{ "crafts", LoadDressPool("Crafts") },
			{ "hearth", LoadDressPool("Hearth") },
			{ "comfort", LoadDressPool("Comfort") },
			{ "keepsake", LoadDressPool("Keepsake") },
		});
		private static bool IsNothingRow(string bp)
			=> bp == null || bp == "*None" || string.Equals(bp, "nothing", StringComparison.OrdinalIgnoreCase);
		private static string TranslateHint(string h, string table)
		{
			if (h == null) return null;
			if (h == HINT_FACE) return "face";
			if (h == "Center") return "center";
			MetricsManager.LogError("TerraFirma: unknown dress hint '" + h + "' in table '" + table + "' ignored (placement falls back to random)");
			return null;
		}
		private static List<XRL.PopulationObject> ReadPopRows(string name)
		{
			var rows = new List<XRL.PopulationObject>();
			var info = XRL.PopulationManager.ResolvePopulation(name, MissingOkay: true);
			if (info == null) return rows;
			XRL.PopulationList list = info;
			if (!"pickone".Equals(list.Style, StringComparison.OrdinalIgnoreCase))
				list = info.Items.OfType<XRL.PopulationGroup>().FirstOrDefault(gr => "pickone".Equals(gr.Style, StringComparison.OrdinalIgnoreCase));
			if (list == null) return rows;
			foreach (var item in list.Items)
				if (item is XRL.PopulationObject po && !string.IsNullOrEmpty(po.Blueprint))
					rows.Add(po);
			return rows;
		}
		private static Row[] LoadDressTable(string shortName)
		{
			string name = "Cleo_TerraFirma_WarrenDress " + shortName;
			var rows = new List<Row>();
			foreach (var po in ReadPopRows(name))
				rows.Add(new Row((int)Math.Min(po.Weight, int.MaxValue), IsNothingRow(po.Blueprint) ? null : po.Blueprint, TranslateHint(po.Hint, name)));
			if (rows.Count == 0)
			{
				MetricsManager.LogError("TerraFirma: dress table '" + name + "' missing or empty; that register will roll nothing");
				rows.Add(new Row(1, null));
			}
			return rows.ToArray();
		}
		private static string[] LoadDressPool(string shortName)
		{
			string name = "Cleo_TerraFirma_WarrenPool " + shortName;
			var pool = new List<string>();
			foreach (var po in ReadPopRows(name))
			{
				if (IsNothingRow(po.Blueprint)) { MetricsManager.LogError("TerraFirma: nothing row in pool '" + name + "' ignored (room recipes decide counts, pools only pick items)"); continue; }
				long wt = Math.Min(po.Weight, 100);
				for (long k = 0; k < wt; k++) pool.Add(po.Blueprint);
			}
			if (pool.Count == 0)
			{
				MetricsManager.LogError("TerraFirma: room pool '" + name + "' missing or empty; substituting Woven Basket");
				pool.Add("Woven Basket");
			}
			return pool.ToArray();
		}
		private static Row[] _occupancyRows, _stageRows, _aquiferRows;
		private static string RollLabelTable(ref Row[] cache, string table, string[] known)
		{
			if (cache == null)
			{
				var rows = new List<Row>();
				foreach (var po in ReadPopRows(table))
					if (!IsNothingRow(po.Blueprint))
						rows.Add(new Row((int)Math.Min(po.Weight, int.MaxValue), po.Blueprint));
				if (rows.Count == 0)
				{
					MetricsManager.LogError("TerraFirma: config table '" + table + "' missing or empty; built-in uniform fallback in use");
					foreach (string k in known) rows.Add(new Row(1, k));
				}
				cache = rows.ToArray();
			}
			int t = 0; foreach (var r in cache) t += r.Wt;
			double x = Rng() * t;
			string label = cache[cache.Length - 1].Id;
			foreach (var r in cache) { x -= r.Wt; if (x < 0) { label = r.Id; break; } }
			if (Array.IndexOf(known, label) < 0)
			{
				MetricsManager.LogError("TerraFirma: config table '" + table + "' rolled unknown label '" + label + "'; using '" + known[0] + "'");
				return known[0];
			}
			return label;
		}
		private static int StageIndexOf(string name)
		{
			for (int i = 0; i < STAGES.Length; i++) if (STAGES[i].Name == name) return i;
			return 0;
		}
		private class RowSpec { public int N0, N1, Chance; public string Pool, Hint; public bool First; }
		private class RoomType { public int Wt; public string Name; public RowSpec[] Rows; public double Density; }
		private static RowSpec RS(int n0, int n1, string pool, string hint, int chance = 0, bool first = false)
			=> new RowSpec { N0 = n0, N1 = n1, Pool = pool, Hint = hint, Chance = chance, First = first };
		private static readonly RoomType[] ROOMTYPES = {
			new RoomType{ Wt=40, Name="house", Density=0.3, Rows=new[]{ RS(1,1,"light","corner"), RS(1,2,"storage","wall"), RS(0,1,"seating","inside"), RS(0,1,"sleep","wall"), RS(1,1,"centre","wall",30), RS(1,1,"liquid","wall",40), RS(1,1,"keepsake","inside",12) } },
			new RoomType{ Wt=20, Name="storage", Density=0.3, Rows=new[]{ RS(1,1,"light","corner"), RS(1,3,"liquid","wall"), RS(3,4,"storage","wall",0,true) } },
			new RoomType{ Wt=15, Name="crafts", Density=0.3, Rows=new[]{ RS(1,1,"light","corner"), RS(1,2,"seating","wall"), RS(2,4,"crafts","corner",0,true), RS(0,2,"storage","wall") } },
			new RoomType{ Wt=10, Name="seating", Density=0.3, Rows=new[]{ RS(1,1,"light","corner"), RS(2,4,"seating","wall",0,true), RS(0,1,"centre","inside"), RS(1,1,"comfort","inside",35,true), RS(1,1,"liquid","wall",40) } },
			new RoomType{ Wt=8, Name="hearth", Density=0.42, Rows=new[]{ RS(1,1,"light","corner"), RS(1,2,"hearth","wall",0,true), RS(1,2,"liquid","wall"), RS(1,2,"storage","wall"), RS(1,1,"centre","inside",60), RS(0,2,"seating","inside") } },
		};
		private const double DWELLDENSITY = 0.3;

		private static string DressSite(Grid g, Artery art, List<PocketRec> pockets, RolesRec roles, string occupancy, int siteFloor, int si, bool siteTopup = true)
		{
			g.Item = new string[g.W * g.H];
			var placed = new Dictionary<string, int>();
			int items = 0, tableItems = 0, refused = 0, topups = 0;
			int monuments = 0, bunkrooms = 0, bunkBeds = 0, bunkShort = 0, plazas = 0, plazaItems = 0, workScatter = 0;
			bool bunkNone = false;
			var bunk = new HashSet<int>();
			var misses = new List<(Row[] tbl, PocketRec p, List<int[]> cs)>();
			bool SolidItem(int i) => IsSolidItem(g.Item[i]);
			bool IsFree(int i) => g.C[i] == FLOOR && g.Item[i] == null && g.Art[i] == 0 && g.Water[i] == 0 && g.Tree[i] == 0 && i != g.MouthIdx;
			bool SolidOK(int x, int y)
			{
				int home = g.Idx(x, y);
				var ns = new List<int>();
				foreach (var d in N4)
				{
					int nx = x + d[0], ny = y + d[1];
					if (g.Inb(nx, ny) && g.C[g.Idx(nx, ny)] == FLOOR && !SolidItem(g.Idx(nx, ny))) ns.Add(g.Idx(nx, ny));
				}
				if (ns.Count <= 1) return true;
				var seen = new HashSet<int> { home, ns[0] };
				var st = new Stack<int>(); st.Push(ns[0]);
				int found = 1;
				while (st.Count > 0 && found < ns.Count)
				{
					int c = st.Pop(); int cx = c % g.W, cy = c / g.W;
					foreach (var d in N4)
					{
						int nx = cx + d[0], ny = cy + d[1];
						if (!g.Inb(nx, ny)) continue;
						int ni = g.Idx(nx, ny);
						if (seen.Contains(ni) || g.C[ni] != FLOOR || SolidItem(ni)) continue;
						seen.Add(ni); st.Push(ni);
						if (ns.Contains(ni)) found++;
					}
				}
				return found == ns.Count;
			}
			void Put(int i, string id) { g.Item[i] = id; placed[id] = (placed.TryGetValue(id, out int v) ? v : 0) + 1; items++; }
			string WRoll(Row[] tbl)
			{
				int t = 0; foreach (var r in tbl) t += r.Wt;
				double x = Rng() * t;
				foreach (var r in tbl) { x -= r.Wt; if (x < 0) return r.Id == null ? null : (r.Hint != null ? r.Id + "|" + r.Hint : r.Id); }
				return null;
			}
			List<int[]> CellsOf(PocketRec p)
			{
				var cs = new List<int[]>();
				for (int y = p.In.Y0; y <= p.In.Y1; y++)
					for (int x = p.In.X0; x <= p.In.X1; x++)
						if (g.Pocket[g.Idx(x, y)] == p.Id && g.C[g.Idx(x, y)] == FLOOR) cs.Add(new[] { x, y });
				return cs;
			}
			bool Threshold(PocketRec p, int x, int y)
			{
				if (p.Id < 0) return false;
				foreach (var d in N4)
				{
					int nx = x + d[0], ny = y + d[1];
					if (g.Inb(nx, ny) && g.C[g.Idx(nx, ny)] == FLOOR && g.Pocket[g.Idx(nx, ny)] != p.Id) return true;
				}
				return false;
			}
			bool Seat(PocketRec p, string rolled, List<int[]> cs)
			{
				if (rolled == null) return false;
				string item = rolled, hint = null;
				int bar = rolled.IndexOf('|');
				if (bar >= 0) { hint = rolled.Substring(bar + 1); item = rolled.Substring(0, bar); }
				if (item == "Village Monument")
				{
					if (monuments >= 2) item = "Random Stone Statue";
					else { monuments++; item = MONUMENTS[R(0, MONUMENTS.Length - 1)]; }
				}
				if (p.Role == "commons" && item == "Random Stone Statue") item = WRoll(T_STATUES);
				if (item == null) return false;
				bool solid = IsSolidItem(item);
				bool Legal(int[] c2) => IsFree(g.Idx(c2[0], c2[1])) && !Threshold(p, c2[0], c2[1]) && (!solid || SolidOK(c2[0], c2[1]));
				int[] c = null;
				if (hint == "face")
				{
					int best = -1;
					foreach (var cc in cs)
					{
						int dd = g.MouthDist != null ? g.MouthDist[g.Idx(cc[0], cc[1])] : -1;
						if (dd > best && Legal(cc)) { best = dd; c = cc; }
					}
				}
				else if (hint == "center")
				{
					double cx = 0, cy = 0;
					foreach (var cc in cs) { cx += cc[0]; cy += cc[1]; }
					cx /= cs.Count; cy /= cs.Count;
					double best = 1e9;
					foreach (var cc in cs)
					{
						if (!Legal(cc)) continue;
						double dd = (cc[0] - cx) * (cc[0] - cx) + (cc[1] - cy) * (cc[1] - cy);
						if (dd < best) { best = dd; c = cc; }
					}
				}
				if (c == null)
				{
					var ok = cs.Where(Legal).ToList();
					if (ok.Count == 0) { if (solid) refused++; return false; }
					if (item != "Garbage")
					{
						var wa = ok.Where(WallHug).ToList();
						if (wa.Count > 0) ok = wa;
					}
					c = ok[R(0, ok.Count - 1)];
				}
				Put(g.Idx(c[0], c[1]), item); tableItems++;
				if (item == "Garbage" && !Chance(.55))
				{
					var pile = new List<int[]> { c };
					int extra = R(1, 3);
					for (int e = 0; e < extra; e++)
					{
						var ps = pile[R(0, pile.Count - 1)];
						var adj = cs.Where(a => Math.Abs(a[0] - ps[0]) <= 1 && Math.Abs(a[1] - ps[1]) <= 1 && IsFree(g.Idx(a[0], a[1]))).ToList();
						if (adj.Count == 0) continue;
						var nc = adj[R(0, adj.Count - 1)];
						Put(g.Idx(nc[0], nc[1]), "Garbage"); pile.Add(nc);
					}
				}
				return true;
			}
			void DressFace(PocketRec p)
			{
				var cs = CellsOf(p); if (cs.Count == 0) return;
				int rolls = Math.Max(1, Math.Min(4, (int)Math.Round(cs.Count / 18.0)));
				for (int i = 0; i < rolls; i++)
				{
					string rolled = i == 0 ? WRollNo(T_SOCKET) : WRoll(T_SOCKET);
					if (!Seat(p, rolled, cs)) misses.Add((T_SOCKET, p, cs));
				}
			}
			void DressCommons(PocketRec p)
			{
				var cs = CellsOf(p); if (cs.Count == 0) return;
				foreach (var pc in p.PillarCells)
				{
					var adj = cs.Where(a => Math.Abs(a[0] - pc[0]) <= 1 && Math.Abs(a[1] - pc[1]) <= 1).ToList();
					if (adj.Count > 0 && !Seat(p, WRoll(T_GALLERYPILLAR), adj)) misses.Add((T_GALLERYPILLAR, p, adj));
				}
				foreach (var sc in p.SpurCells)
				{
					var adj = cs.Where(a => Math.Abs(a[0] - sc[0]) <= 1 && Math.Abs(a[1] - sc[1]) <= 1).ToList();
					if (adj.Count > 0 && !Seat(p, WRoll(T_GALLERYBAY), adj)) misses.Add((T_GALLERYBAY, p, adj));
				}
				int frolls = Math.Max(1, Math.Min(3, (int)Math.Round(cs.Count / 24.0)));
				for (int fi = 0; fi < frolls; fi++)
					if (!Seat(p, WRoll(T_GALLERYFLOOR), cs)) misses.Add((T_GALLERYFLOOR, p, cs));
				PlanCommonsEtchings(g, p);
			}
			bool WallHug(int[] c2)
			{
				foreach (var d in N4)
				{
					int nx = c2[0] + d[0], ny = c2[1] + d[1];
					if (!g.Inb(nx, ny) || g.C[g.Idx(nx, ny)] != FLOOR) return true;
				}
				return false;
			}
			List<List<int[]>> WallRuns(PocketRec p, List<int[]> cs)
			{
				var ok = cs.Where(c2 => IsFree(g.Idx(c2[0], c2[1])) && !Threshold(p, c2[0], c2[1]) && WallHug(c2)).ToList();
				var key = new HashSet<int>(ok.Select(c2 => g.Idx(c2[0], c2[1])));
				var runs = new List<List<int[]>>();
				for (int axis = 0; axis < 2; axis++)
				{
					var seen = new HashSet<int>();
					foreach (var c2 in ok)
					{
						if (seen.Contains(g.Idx(c2[0], c2[1]))) continue;
						int x = c2[0], y = c2[1];
						while (true)
						{
							int px = axis == 1 ? x : x - 1, py = axis == 1 ? y - 1 : y;
							if (g.Inb(px, py) && key.Contains(g.Idx(px, py))) { x = px; y = py; } else break;
						}
						var run = new List<int[]>();
						while (g.Inb(x, y) && key.Contains(g.Idx(x, y)))
						{
							run.Add(new[] { x, y }); seen.Add(g.Idx(x, y));
							if (axis == 1) y++; else x++;
						}
						runs.Add(run);
					}
				}
				return runs;
			}
			int LongestWallRun(PocketRec p)
			{
				int best = 0;
				foreach (var r in WallRuns(p, CellsOf(p))) if (r.Count > best) best = r.Count;
				return best;
			}
			int SeatRun(PocketRec p, string item, int want, List<int[]> cs)
			{
				var runs = WallRuns(p, cs); if (runs.Count == 0) return 0;
				var cands = runs.Where(r => r.Count >= want).ToList();
				if (cands.Count == 0) { int L = runs.Max(r => r.Count); cands = runs.Where(r => r.Count == L).ToList(); want = L; }
				var run = cands[R(0, cands.Count - 1)];
				int off = R(0, run.Count - want);
				for (int k = 0; k < want; k++) { Put(g.Idx(run[off + k][0], run[off + k][1]), item); tableItems++; }
				return want;
			}
			void DressDwelling(PocketRec p)
			{
				var cs = CellsOf(p); if (cs.Count == 0) return;
				int tot = 0; foreach (var t in ROOMTYPES) tot += t.Wt;
				double x = Rng() * tot; RoomType type = null;
				foreach (var t in ROOMTYPES) { x -= t.Wt; if (x < 0) { type = t; break; } }
				if (type == null) return;
				if (bunk.Contains(p.Id))
				{
					int want2 = R(2, 3), got = SeatRun(p, "Bedroll", want2, cs);
					bunkrooms++; bunkBeds += got;
					if (got < want2) bunkShort++;
				}
				int WallsAround(int[] c2)
				{
					int n = 0;
					foreach (var d in N4)
					{
						int nx = c2[0] + d[0], ny = c2[1] + d[1];
						if (!g.Inb(nx, ny) || g.C[g.Idx(nx, ny)] != FLOOR) n++;
					}
					return n;
				}
				int budget = Math.Max(1, (int)Math.Round(cs.Count * type.Density));
				int roomChests = 0;
				var rows = type.Rows.ToList();
				var fixedRows = new List<RowSpec> { rows[0] };
				rows.RemoveAt(0);
				for (int i = rows.Count - 1; i >= 0; i--) if (rows[i].First) { fixedRows.Add(rows[i]); rows.RemoveAt(i); }
				for (int i = rows.Count - 1; i > 0; i--) { int j = R(0, i); (rows[i], rows[j]) = (rows[j], rows[i]); }
				foreach (var row in fixedRows.Concat(rows))
				{
					if (budget <= 0) break;
					if (row.Chance > 0 && !Chance(row.Chance / 100.0)) continue;
					int want = Math.Min(R(row.N0, row.N1), budget);
					if (row.Pool == "light") want = Math.Min(want, Math.Max(1, budget >> 1));
					for (int w2 = 0; w2 < want && budget > 0; w2++)
					{
						var pool = ROOMPOOLS[row.Pool];
						if (pool.Contains("Oven") && placed.ContainsKey("Oven")) pool = pool.Where(i2 => i2 != "Oven").ToArray();
						string item = pool[R(0, pool.Length - 1)];
						if (item == "Chest" || item == "Cleo_TerraFirma_Quarry Chest") { if (roomChests >= 1) item = "Woven Basket"; else roomChests++; }
						bool solid = IsSolidItem(item);
						bool Legal(int[] c2) => IsFree(g.Idx(c2[0], c2[1])) && !Threshold(p, c2[0], c2[1]) && (!solid || SolidOK(c2[0], c2[1]));
						var cands = cs.Where(c2 =>
						{
							if (!Legal(c2)) return false;
							if (row.Hint == "corner") return WallsAround(c2) >= 2;
							if (row.Hint == "wall") return WallsAround(c2) >= 1;
							return true;
						}).ToList();
						bool heavy = item == "Kiln" || item == "Oven" || item == "Anvil";
						if (cands.Count == 0 && heavy) cands = cs.Where(c2 => Legal(c2) && WallsAround(c2) >= 1).ToList();
						else if (cands.Count == 0) cands = cs.Where(Legal).ToList();
						if (cands.Count == 0) { if (solid || heavy) refused++; break; }
						var c = cands[R(0, cands.Count - 1)];
						Put(g.Idx(c[0], c[1]), item); budget--; tableItems++;
						if (item == "Hookah" || item == "Kiln")
						{
							string orbit = item == "Hookah" ? "Floor Cushion" : "Cleo_TerraFirma_QuarriedBlock";
							int kmax = item == "Hookah" ? R(2, 4) : R(1, 2);
							var slots = new List<int[]>();
							foreach (var d in N4)
							{
								int sxx = c[0] + d[0], syy = c[1] + d[1];
								if (cs.Any(a => a[0] == sxx && a[1] == syy) && IsFree(g.Idx(sxx, syy)) && !Threshold(p, sxx, syy))
									slots.Add(new[] { sxx, syy });
							}
							int k = Math.Max(1, Math.Min(kmax, slots.Count));
							while (k-- > 0 && slots.Count > 0)
							{
								int si2 = R(0, slots.Count - 1);
								var s2 = slots[si2]; slots.RemoveAt(si2);
								Put(g.Idx(s2[0], s2[1]), orbit); tableItems++;
							}
						}
					}
				}
			}
			void DressYard(Box rect, string state, bool outside)
			{
				int x0 = rect.X0, y0 = rect.Y0, x1 = rect.X1, y1 = rect.Y1;
				bool FreeI(int i) => g.C[i] == FLOOR && g.Item[i] == null;
				for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) g.Tree[g.Idx(x, y)] = 0;
				int gmx = art.Mouth.X, gmy = art.Mouth.Y;
				int gapI = -1, gapX = 0, gapY = 0;
				List<int> tpath = null;
				string trailNote = null;
				if (outside)
				{
					bool IsGapCell(int x, int y)
					{
						if (x < x0 || x > x1 || y < y0 || y > y1) return false;
						if (x > x0 && x < x1 && y > y0 && y < y1) return false;
						return !((x == x0 || x == x1) && (y == y0 || y == y1));
					}
					bool PassAt(int i2, bool trees)
					{
						int px3 = i2 % g.W, py3 = i2 / g.W;
						return g.C[i2] == FLOOR && g.Host[i2] != 0 && g.Water[i2] == 0 && (trees || g.Tree[i2] == 0)
							&& !(px3 >= x0 && px3 <= x1 && py3 >= y0 && py3 <= y1);
					}
					int mxc = g.MouthIdx % g.W, myc = g.MouthIdx / g.W;
					foreach (var dd in N4)
					{
						int nx = mxc + dd[0], ny = myc + dd[1];
						if (g.Inb(nx, ny) && IsGapCell(nx, ny))
						{ gapI = g.Idx(nx, ny); gapX = nx; gapY = ny; tpath = new List<int>(); trailNote = "doorstep"; break; }
					}
					int[] prevA = null; int gapFound = -1;
					int Bfs(bool trees)
					{
						var pv = new int[g.W * g.H]; for (int k = 0; k < pv.Length; k++) pv[k] = -2;
						var ql = new List<int>();
						foreach (var dd in N4)
						{
							int nx = mxc + dd[0], ny = myc + dd[1]; if (!g.Inb(nx, ny)) continue;
							int ni = g.Idx(nx, ny);
							if (PassAt(ni, trees) && pv[ni] == -2) { pv[ni] = -1; ql.Add(ni); }
						}
						int head = 0;
						while (head < ql.Count)
						{
							int c2 = ql[head++], cx2 = c2 % g.W, cy2 = c2 / g.W;
							foreach (var dd in N4)
							{
								int nx = cx2 + dd[0], ny = cy2 + dd[1]; if (!g.Inb(nx, ny)) continue;
								if (IsGapCell(nx, ny)) { prevA = pv; gapFound = g.Idx(nx, ny); return c2; }
								int ni = g.Idx(nx, ny);
								if (pv[ni] != -2 || !PassAt(ni, trees)) continue;
								pv[ni] = c2; ql.Add(ni);
							}
						}
						return -1;
					}
					if (gapI < 0)
					{
						int hitI = Bfs(false); if (hitI < 0) hitI = Bfs(true);
						if (hitI >= 0)
						{
							gapI = gapFound; gapX = gapI % g.W; gapY = gapI / g.W;
							tpath = new List<int>();
							for (int c2 = hitI; c2 >= 0; c2 = prevA[c2]) tpath.Add(c2);
							tpath.Reverse();
						}
					}
				}
				if (gapI < 0)
				{
					int gapBest = int.MaxValue, gapRank = 9;
					for (int x = x0; x <= x1; x++)
						for (int y = y0; y <= y1; y++)
						{
							if (x > x0 && x < x1 && y > y0 && y < y1) continue;
							if ((x == x0 || x == x1) && (y == y0 || y == y1)) continue;
							int odx = x == x0 ? -1 : x == x1 ? 1 : 0, ody = y == y0 ? -1 : y == y1 ? 1 : 0;
							int gox = x + odx, goy = y + ody;
							int rank = 2;
							if (g.Inb(gox, goy))
							{
								int goi = g.Idx(gox, goy);
								if (g.C[goi] == FLOOR && g.Host[goi] != 0 && g.Water[goi] == 0)
									rank = g.Tree[goi] != 0 ? 1 : 0;
							}
							int gd = (x - gmx) * (x - gmx) + (y - gmy) * (y - gmy);
							if (rank < gapRank || (rank == gapRank && gd < gapBest))
							{ gapRank = rank; gapBest = gd; gapI = g.Idx(x, y); gapX = x; gapY = y; }
						}
				}
				for (int x = x0; x <= x1; x++)
					for (int y = y0; y <= y1; y++)
					{
						if (x > x0 && x < x1 && y > y0 && y < y1) continue;
						int i = g.Idx(x, y); if (i == gapI || !FreeI(i)) continue;
						if (state == "cleared" && Chance(.35)) continue;
						string id = y == y0 ? (x == x0 ? "chalklineNW" : x == x1 ? "chalklineNE" : "chalklineN")
								 : y == y1 ? (x == x0 ? "chalklineSW" : x == x1 ? "chalklineSE" : "chalklineS")
								 : x == x0 ? "chalklineW" : "chalklineE";
						Put(i, id);
					}
				if (tpath != null && tpath.Count > 0)
				{
					var trailSet = new HashSet<int>(tpath);
					foreach (int c2 in tpath) { g.Tree[c2] = 0; if (g.Item[c2] == null) Put(c2, "DirtPath"); }
					int cairns = 0, until = R(3, 6);
					for (int pi2 = 1; pi2 < tpath.Count; pi2++)
					{
						if (--until > 0) continue;
						until = R(3, 6);
						int c2 = tpath[pi2], cx2 = c2 % g.W, cy2 = c2 / g.W;
						int px3 = tpath[pi2 - 1] % g.W, py3 = tpath[pi2 - 1] / g.W;
						int tdx = cx2 - px3, tdy = cy2 - py3;
						int cside = Chance(.5) ? 1 : -1;
						int cox = cx2 + (-tdy) * cside, coy = cy2 + tdx * cside;
						if (!g.Inb(cox, coy)) continue;
						int coi = g.Idx(cox, coy);
						if (g.C[coi] == FLOOR && g.Host[coi] != 0 && g.Item[coi] == null && g.Water[coi] == 0 && g.Tree[coi] == 0
							&& !trailSet.Contains(coi) && coi != g.MouthIdx)
						{
							Put(coi, occupancy != "in use" && Chance(.5) ? "Cleo_TerraFirma_CairnToppled" : "Cleo_TerraFirma_Cairn");
							cairns++;
						}
					}
					Helpers.VerifyLog("WARREN", $"trail: {tpath.Count} cells, {cairns} cairns, gap {gapX},{gapY}");
				}
				else if (outside)
					Helpers.VerifyLog("WARREN", $"trail: {(trailNote ?? "none (no route)")}, gap {gapX},{gapY}");
				int ix0 = x0 + 1, iy0 = y0 + 1, ix1 = x1 - 1, iy1 = y1 - 1;
				if (ix1 < ix0 || iy1 < iy0) return;
				int wx = Math.Abs(ix0 - art.Mouth.X) <= Math.Abs(ix1 - art.Mouth.X) ? ix0 : ix1;
				bool working = state == "working" || state == "full";
				bool cleared = state == "cleared";
				if (state == "fresh" || working || cleared)
				{
					int benchY = -1;
					if (Chance(.75)) { Put(g.Idx(wx, iy0), "Workbench"); benchY = iy0; }
					if ((working || cleared) && benchY >= 0 && iy0 + 1 <= iy1 && Chance(.7)) Put(g.Idx(wx, iy0 + 1), "Canvas Folding Chair");
					if (working && Chance(.75) && FreeI(g.Idx(wx, iy1))) Put(g.Idx(wx, iy1), "Cleo_TerraFirma_Stone Basket");
				}
				if (Chance(.7))
				{
					int bi = -1;
					for (int y = iy0; y <= iy1 && bi < 0; y++)
						for (int x = ix0; x <= ix1; x++)
							if (FreeI(g.Idx(x, y))) { bi = g.Idx(x, y); break; }
					if (bi >= 0)
					{
						var boardBp = GameObjectFactory.Factory.GetBlueprint("Cleo_TerraFirma_Yard Board");
						string Tpl(string tag)
						{
							string t = boardBp?.GetTag(tag, null);
							if (t == null) MetricsManager.LogError("TerraFirma: yard board template tag '" + tag + "' missing (check ObjectBlueprints.xml); line skipped");
							return t;
						}
						string Dated(string tpl, long t) => tpl.Replace("=day=", Calendar.GetDay(t)).Replace("=month=", Calendar.GetMonth(t));
						long now = Calendar.TotalTimeTicks;
						long Wrap(long t) { long y2 = 438000L; t %= y2; if (t < 0) t += y2; return t; }
						var lines = new List<string>();
						long arrival = Wrap(now - (long)R(45, 320) * 1200);
						string tArr = Tpl("Cleo_TerraFirma_ArrivedLine");
						if (tArr != null) lines.Add(Dated(tArr, arrival));
						if (occupancy != "in use")
						{
							long depart = Wrap(now - (long)R(8, 40) * 1200);
							string tDep = Tpl("Cleo_TerraFirma_DepartedLine");
							if (tDep != null) lines.Add(Dated(tDep, depart));
						}
						if (Chance(.6))
						{
							string tTal = Tpl("Cleo_TerraFirma_TallyLine");
							if (tTal != null) lines.Add(tTal.Replace("=blocks=", siteFloor.ToString()));
						}
						if (lines.Count > 0)
						{
							g.YardBoardText = string.Join("\n", lines);
							Put(bi, "yardboard");
						}
					}
				}
				if (state == "fresh")
				{
					int x = wx + (wx == ix0 ? 1 : -1);
					if (x >= ix0 && x <= ix1)
					{
						int len = R(1, 2);
						for (int k = 0; k < len; k++) { int i = g.Idx(x, iy0 + k); if (FreeI(i)) Put(i, "Cleo_TerraFirma_QuarryStones1"); }
					}
					return;
				}
				int dir = wx == ix0 ? 1 : -1, avail = ix1 - ix0, ih = iy1 - iy0 + 1;
				int nCols = state == "full" ? avail : cleared ? R(1, Math.Min(2, Math.Max(1, avail))) : R(1, Math.Min(3, Math.Max(1, avail)));
				bool gaps = state == "robbed";
				for (int c2 = 0; c2 < nCols; c2++)
				{
					int x = wx + dir * (c2 + 1); if (x < ix0 || x > ix1) break;
					int gi = state == "full" ? Math.Min(3, c2 + 2) : Math.Min(cleared ? 2 : 3, c2 + 1);
					int len = cleared ? R(1, Math.Max(1, ih - 1))
							: state == "full" || gi == 3 ? ih
							: gi == 2 ? R(Math.Max(1, ih - 1), ih)
							: R(1, Math.Max(1, ih - 1));
					for (int k = 0; k < len; k++)
					{
						int i = g.Idx(x, iy0 + k); if (!FreeI(i)) continue;
						if (gaps && Chance(.5)) { if (Chance(.4)) Put(i, "SmallBoulder"); continue; }
						Put(i, "Cleo_TerraFirma_QuarryStones" + gi);
					}
				}
			}
			{
				var dws = pockets.Where(p2 => p2.Role == "dwelling").ToList();
				if (dws.Count > 0)
				{
					int bunks = 1 + ((dws.Count >= 3 && Chance(.35)) ? 1 : 0);
					var scored = dws.Select(p2 => (p: p2, run: LongestWallRun(p2))).Where(s2 => s2.run >= 2).ToList();
					for (int i2 = scored.Count - 1; i2 > 0; i2--) { int j2 = R(0, i2); (scored[i2], scored[j2]) = (scored[j2], scored[i2]); }
					scored = scored.OrderByDescending(s2 => s2.run).ToList();
					foreach (var s2 in scored.Take(bunks)) bunk.Add(s2.p.Id);
					if (bunk.Count == 0) bunkNone = true;
				}
			}
			foreach (var p in pockets)
			{
				if (p.Role == "face") DressFace(p);
				else if (p.Role == "commons") DressCommons(p);
				else if (p.Role == "dwelling") DressDwelling(p);
				else if (p.Role == "plain")
				{
					var cs = CellsOf(p);
					int rolls = Math.Max(1, Math.Min(5, cs.Count / 12 + 1));
					for (int ri = 0; ri < rolls; ri++)
						if (cs.Count > 0 && !Seat(p, WRoll(T_PLAINTRACE), cs)) misses.Add((T_PLAINTRACE, p, cs));
				}
			}
			{
				bool OpenU(int i2)
				{
					if (g.C[i2] != FLOOR || g.Host[i2] != 0 || g.Water[i2] != 0) return false;
					int pid2 = g.Pocket[i2];
					return pid2 < 0 || (pid2 < pockets.Count && pockets[pid2].Role == "plain");
				}
				var pcore = new byte[g.W * g.H];
				for (int y = 1; y < g.H - 1; y++)
					for (int x = 1; x < g.W - 1; x++)
					{
						bool ok = true;
						for (int dy = -1; dy <= 1 && ok; dy++)
							for (int dx = -1; dx <= 1; dx++)
								if (!OpenU(g.Idx(x + dx, y + dy))) { ok = false; break; }
						if (ok) pcore[g.Idx(x, y)] = 1;
					}
				var pseen = new byte[g.W * g.H];
				var plazaRec = new PocketRec { Id = -1, Role = "plaza" };
				for (int i2 = 0; i2 < g.W * g.H; i2++)
				{
					if (pcore[i2] == 0 || pseen[i2] != 0) continue;
					var cores = new List<int> { i2 }; pseen[i2] = 1;
					for (int h2 = 0; h2 < cores.Count; h2++)
					{
						int c2 = cores[h2], x = c2 % g.W, y = c2 / g.W;
						foreach (var d in N4)
						{
							int nx = x + d[0], ny = y + d[1];
							if (!g.Inb(nx, ny)) continue;
							int ni = g.Idx(nx, ny);
							if (pcore[ni] != 0 && pseen[ni] == 0) { pseen[ni] = 1; cores.Add(ni); }
						}
					}
					if (cores.Count < 4) continue;
					var cellSet = new HashSet<int>(cores);
					foreach (var c2 in cores)
					{
						int x = c2 % g.W, y = c2 / g.W;
						for (int dy = -1; dy <= 1; dy++)
							for (int dx = -1; dx <= 1; dx++)
							{
								int ni = g.Idx(x + dx, y + dy);
								if (OpenU(ni)) cellSet.Add(ni);
							}
					}
					var cs3 = cellSet.Select(ci => new[] { ci % g.W, ci / g.W }).ToList();
					int D = si == 2 ? 8 : si == 1 ? 11 : 14;
					int prolls = Math.Max(1, Math.Min(6, (int)Math.Round(cs3.Count / (double)D)));
					int before = items;
					while (prolls-- > 0) Seat(plazaRec, WRoll(T_PLAZA), cs3);
					plazas++; plazaItems += items - before;
				}
			}
			foreach (var p2 in pockets)
			{
				if (p2.Role != "face") continue;
				var cs4 = CellsOf(p2); if (cs4.Count == 0) continue;
				int wrolls = Math.Max(2, Math.Min(6, (int)Math.Round(cs4.Count / 8.0)));
				int before = items;
				while (wrolls-- > 0) Seat(p2, WRoll(T_WORKSCATTER), cs4);
				workScatter += items - before;
			}
			if (roles.YardRect != null) DressYard(roles.YardRect, roles.YardState ?? "working", roles.YardDesc != null && roles.YardDesc.StartsWith("outside"));
			int eligible = pockets.Count(p => p.Role == "face" || p.Role == "commons" || p.Role == "dwelling");
			int wantFloor = Math.Max(2, (int)Math.Round(eligible * 0.45));
			string WRollNo(Row[] tbl)
			{
				int t = 0; foreach (var r in tbl) if (r.Id != null) t += r.Wt;
				if (t == 0) return null;
				double x = Rng() * t;
				foreach (var r in tbl) { if (r.Id == null) continue; x -= r.Wt; if (x < 0) return r.Hint != null ? r.Id + "|" + r.Hint : r.Id; }
				return null;
			}
			int fguard = 0;
			while (siteTopup && tableItems < wantFloor && misses.Count > 0 && fguard++ < 12)
			{
				int mi = R(0, misses.Count - 1);
				var (tbl, p, cs) = misses[mi]; misses.RemoveAt(mi);
				if (Seat(p, WRollNo(tbl), cs)) topups++;
			}
			var segSeen = new byte[g.W * g.H];
			bool Corr(int i2) => g.C[i2] == FLOOR && g.Host[i2] == 0 && g.Water[i2] == 0 && g.Art[i2] == 0 && g.Pocket[i2] < 0 && i2 != g.MouthIdx;
			for (int i0 = 0; i0 < g.W * g.H; i0++)
			{
				if (segSeen[i0] != 0 || !Corr(i0)) continue;
				var seg = new List<int> { i0 };
				var st = new Stack<int>(); st.Push(i0); segSeen[i0] = 1;
				while (st.Count > 0)
				{
					int c = st.Pop(); int cx = c % g.W, cy = c / g.W;
					foreach (var d in N4)
					{
						int nx = cx + d[0], ny = cy + d[1];
						if (!g.Inb(nx, ny)) continue;
						int ni = g.Idx(nx, ny);
						if (segSeen[ni] != 0 || !Corr(ni)) continue;
						segSeen[ni] = 1; seg.Add(ni); st.Push(ni);
					}
				}
				if (seg.Count < 3) continue;
				string rolled = WRoll(T_HALLTABLE);
				if (rolled == null) continue;
				string item = rolled, hint = null;
				int bar = rolled.IndexOf('|');
				if (bar >= 0) { hint = rolled.Substring(bar + 1); item = rolled.Substring(0, bar); }
				var open = seg.Where(i2 => g.Item[i2] == null).ToList();
				if (open.Count == 0) continue;
				if (item == "Cleo_TerraFirma_ChalkLine")
				{
					var walled = new List<(int i, string id)>();
					foreach (int i2 in open)
					{
						int cx = i2 % g.W, cy = i2 / g.W;
						foreach (var (dd, dx, dy) in new[] { ("N", 0, -1), ("S", 0, 1), ("E", 1, 0), ("W", -1, 0) })
							if (g.Inb(cx + dx, cy + dy) && g.C[g.Idx(cx + dx, cy + dy)] != FLOOR) { walled.Add((i2, "chalkline" + dd)); break; }
					}
					if (walled.Count == 0) continue;
					var pick2 = walled[R(0, walled.Count - 1)];
					Put(pick2.i, pick2.id); continue;
				}
				int ci;
				if (hint == "face")
				{
					int best = -1; ci = open[0];
					foreach (int i2 in open)
					{
						int dd2 = g.MouthDist != null ? g.MouthDist[i2] : -1;
						if (dd2 > best) { best = dd2; ci = i2; }
					}
				}
				else ci = open[R(0, open.Count - 1)];
				Put(ci, item);
				if (item == "Garbage" && !Chance(.55))
				{
					int px2 = ci % g.W, py2 = ci / g.W;
					int extra = R(1, 3);
					for (int e = 0; e < extra; e++)
					{
						int nx = px2 + R(-1, 1), ny = py2 + R(-1, 1);
						if (!g.Inb(nx, ny)) continue;
						int ni = g.Idx(nx, ny);
						if (g.C[ni] == FLOOR && g.Host[ni] == 0 && g.Item[ni] == null && g.Water[ni] == 0 && ni != g.MouthIdx)
						{ Put(ni, "Garbage"); px2 = nx; py2 = ny; }
					}
				}
			}
			var natFloors = new List<int>();
			for (int i = 0; i < g.W * g.H; i++)
				if (g.C[i] == FLOOR && g.Host[i] == 0 && g.Water[i] == 0 && g.Item[i] == null && i != g.MouthIdx) natFloors.Add(i);
			int natWant = R(2, 5) + (int)Math.Round(natFloors.Count * 0.03);
			for (int na = 0; na < natWant && natFloors.Count > 0; na++)
			{
				string it = WRoll(T_NATURAL); if (it == null) continue;
				for (int t = 0; t < 8 && natFloors.Count > 0; t++)
				{
					int pi = R(0, natFloors.Count - 1), ci = natFloors[pi];
					if (IsSolidItem(it) && !SolidOK(ci % g.W, ci / g.W)) { refused++; continue; }
					natFloors.RemoveAt(pi);
					if (g.Item[ci] == null) Put(ci, it);
					break;
				}
			}
			{
				var posts = new List<int[]>();
				bool PostOK(int px, int py) => posts.All(q => Math.Max(Math.Abs(q[0] - px), Math.Abs(q[1] - py)) >= 3);
				foreach (var p in pockets)
				{
					if (p.Role != "commons") continue;
					int pocketPosts = 0;
					var cs = CellsOf(p);
					var inC = new HashSet<int>(cs.Select(c2 => g.Idx(c2[0], c2[1])));
					var cand = new List<int[]>();
					var seenD = new HashSet<int>();
					foreach (var c2 in cs)
						foreach (var d in N4)
						{
							int ex = c2[0] + d[0], ey = c2[1] + d[1];
							if (!g.Inb(ex, ey)) continue;
							int ei = g.Idx(ex, ey);
							if (seenD.Contains(ei) || inC.Contains(ei) || g.C[ei] != FLOOR) continue;
							int ox = ex + d[0], oy = ey + d[1];
							if (g.Inb(ox, oy) && g.Pocket[g.Idx(ox, oy)] >= 0 && g.Pocket[g.Idx(ox, oy)] != p.Id) continue;
							seenD.Add(ei); cand.Add(new[] { ex, ey, c2[0], c2[1] });
						}
					var used = new HashSet<int>();
					foreach (var e in cand)
					{
						int ek = g.Idx(e[0], e[1]);
						if (used.Contains(ek)) continue;
						var cq = new List<int[]> { e }; used.Add(ek);
						for (int ci2 = 0; ci2 < cq.Count; ci2++)
							foreach (var o in cand)
							{
								int ok3 = g.Idx(o[0], o[1]);
								if (!used.Contains(ok3) && Math.Abs(o[0] - cq[ci2][0]) <= 2 && Math.Abs(o[1] - cq[ci2][1]) <= 2) { used.Add(ok3); cq.Add(o); }
							}
						if (pocketPosts >= 4) break;
						int ax = e[2] - e[0], ay = e[3] - e[1];
						foreach (var f in new[] { new[] { e[2] - ay, e[3] - ax }, new[] { e[2] + ay, e[3] + ax } })
						{
							if (!g.Inb(f[0], f[1])) continue;
							if (!inC.Contains(g.Idx(f[0], f[1])) || !IsFree(g.Idx(f[0], f[1]))) continue;
							if (!PostOK(f[0], f[1])) continue;
							if (g.Art[g.Idx(f[0], f[1])] != 0) continue;
							Put(g.Idx(f[0], f[1]), "Torchpost"); posts.Add(new[] { f[0], f[1] });
							pocketPosts++;
							break;
						}
					}
				}
				var mq = new Queue<int[]>(); mq.Enqueue(new[] { art.Mouth.X, art.Mouth.Y, 0 });
				var mseen = new HashSet<int> { g.MouthIdx };
				while (mq.Count > 0)
				{
					var m = mq.Dequeue();
					int mi = g.Idx(m[0], m[1]);
					if (g.C[mi] == FLOOR && g.Host[mi] == 0 && g.Item[mi] == null && mi != g.MouthIdx && g.Art[mi] == 0)
					{
						bool hug = false;
						foreach (var d in N4) if (g.Inb(m[0] + d[0], m[1] + d[1]) && g.C[g.Idx(m[0] + d[0], m[1] + d[1])] != FLOOR) hug = true;
						if (hug) { Put(mi, "Torchpost"); break; }
					}
					if (m[2] < 2)
						foreach (var d in N4)
						{
							int nx = m[0] + d[0], ny = m[1] + d[1];
							if (g.Inb(nx, ny) && !mseen.Contains(g.Idx(nx, ny)) && g.C[g.Idx(nx, ny)] == FLOOR)
							{ mseen.Add(g.Idx(nx, ny)); mq.Enqueue(new[] { nx, ny, m[2] + 1 }); }
						}
				}
			}
			if (occupancy != "in use")
			{
				var flipCache = new Dictionary<string, string>();
				string ColdFormOf(string id)
				{
					if (IsPlacementDirective(id)) return null;
					if (flipCache.TryGetValue(id, out string known)) return known;
					string to = null;
					var bp = GameObjectFactory.Factory.GetBlueprint(id);
					if (bp != null)
					{
						string ex = bp.GetPartParameter<string>("Campfire", "ExtinguishBlueprint");
						if (!string.IsNullOrEmpty(ex) && ex != id) to = ex;
						else if (bp.TryGetTag("Cleo_TerraFirma_ColdForm", out string tag))
							to = tag ?? "";
					}
					flipCache[id] = to;
					return to;
				}
				for (int i = 0; i < g.W * g.H; i++)
				{
					string it = g.Item[i];
					if (it == null) continue;
					string to = ColdFormOf(it);
					if (to == null) continue;
					int pid = g.Pocket[i];
					if (pid >= 0 && pid < pockets.Count && pockets[pid].Live) continue;
					g.Item[i] = to.Length == 0 ? null : to;
					if (to.Length == 0) items--;
				}
			}
			if (occupancy == "reclaimed")
			{
				var dens = pockets.OrderByDescending(p => p.DepthRank).Take(Math.Min(3, pockets.Count)).ToList();
				int fires = R(1, 2);
				foreach (var p in dens)
				{
					if (fires <= 0) break;
					var cs = CellsOf(p).Where(c2 => IsFree(g.Idx(c2[0], c2[1]))).ToList();
					if (cs.Count == 0) continue;
					var c = cs[R(0, cs.Count - 1)];
					Put(g.Idx(c[0], c[1]), "Campfire"); fires--;
				}
			}
			bool deadSite = occupancy != "in use";
			var tCells = new List<int>();
			for (int i = 0; i < g.W * g.H; i++)
				if (g.C[i] == FLOOR && g.Host[i] == 0 && g.Water[i] == 0 && g.Item[i] == null && i != g.MouthIdx
					&& (deadSite || g.Art[i] == 0)) tCells.Add(i);
			int tWant = (int)Math.Round(tCells.Count * (deadSite ? 0.048 : 0.012)) + (deadSite ? R(2, 4) : R(0, 2));
			for (int n = 0; n < tWant && tCells.Count > 0; n++)
			{
				int pi = R(0, tCells.Count - 1), ci = tCells[pi]; tCells.RemoveAt(pi);
				if (g.Item[ci] != null) continue;
				Put(ci, "Garbage");
				if (!Chance(.55))
				{
					int px2 = ci % g.W, py2 = ci / g.W;
					int extra = R(1, 3);
					for (int e = 0; e < extra; e++)
					{
						int nx = px2 + R(-1, 1), ny = py2 + R(-1, 1);
						if (!g.Inb(nx, ny)) continue;
						int ni = g.Idx(nx, ny);
						if (g.C[ni] == FLOOR && g.Host[ni] == 0 && g.Item[ni] == null && g.Water[ni] == 0 && ni != g.MouthIdx)
						{ Put(ni, "Garbage"); px2 = nx; py2 = ny; }
					}
				}
			}
			int roomFloored = 0;
			foreach (var p in pockets)
			{
				Row[] tbl = p.Role == "face" ? T_SOCKET
					: p.Role == "plain" ? T_PLAINTRACE
					: p.Role == "commons" ? T_GALLERYFLOOR : null;
				if (tbl == null) continue;
				var cs = CellsOf(p); if (cs.Count == 0) continue;
				int have = 0;
				foreach (var c2 in cs) if (g.Item[g.Idx(c2[0], c2[1])] != null) have++;
				if (have > 1 || (have == 1 && cs.Count < 20)) continue;
				if (Seat(p, WRollNo(tbl), cs)) roomFloored++;
			}
			g.SiteTopups = topups;
			return $"{items} items, {tableItems} table, {topups} topups, {roomFloored} room-floored, {refused} solid-refused, "
				+ $"{bunkrooms} bunkroom ({bunkBeds} beds, {bunkShort} short{(bunkNone ? ", no bunk-able dwelling" : "")}), "
				+ $"{plazas} plaza ({plazaItems} leisure), {workScatter} work-scatter, {monuments} monuments";
		}

		private static int PlaceItems(Zone Z, Grid g)
		{
			if (g.Item == null) return 0;
			int placed = 0;
			for (int i = 0; i < g.W * g.H; i++)
			{
				string id = g.Item[i];
				if (id == null) continue;
				Cell c = Z.GetCell(i % g.W, i / g.W);
				if (c == null || c.HasWall()) continue;
				GameObject obj;
				if (id.StartsWith("chalkline"))
				{
					obj = GameObject.Create("Cleo_TerraFirma_ChalkLine");
					if (obj.Render != null) obj.Render.Tile = id + ".png";
				}
				else if (id == "yardboard")
				{
					obj = GameObject.Create("Cleo_TerraFirma_Yard Board");
					if (g.YardBoardText != null)
						obj.RequirePart<XRL.World.Parts.Cleo_TerraFirma_ChalkWriting>().Text = g.YardBoardText;
				}
				else
				{
					if (GameObjectFactory.Factory.GetBlueprint(id) == null)
					{
						MetricsManager.LogError("TerraFirma: warren dress rolled unknown blueprint '" + id + "' (check PopulationTables.xml); cell left bare");
						continue;
					}
					obj = GameObject.Create(id);
				}
				if ((id.StartsWith("chalkline") || id == "Cleo_TerraFirma_Cairn" || id == "Cleo_TerraFirma_CairnToppled")
					&& obj.Physics != null)
					obj.Physics.Owner = "Cleo_TerraFirma_Quarriers";
				if (obj.Physics != null && (obj.Physics.Takeable || obj.HasPart<XRL.World.Parts.Inventory>())
					&& string.IsNullOrEmpty(obj.Physics.Owner))
					obj.Physics.Owner = "Cleo_TerraFirma_Quarriers";
				c.AddObject(obj);
				placed++;
			}
			return placed;
		}

		public class Result
		{
			public bool Built;
			public string Reason = "";
			public string Stage = "", Occupancy = "", Roles = "", Yard = "";
			public int StageIdx = -1;
			public string Layout = "boxes";
			public int Floor, Pockets, Degraded, Attempt, Seed;
			public int AquiferPools, AquiferVeins;
			public string BuildMethod;
			public int AugmentCells, WinningAugmentRounds, ExploredAugmentRounds;
		}

		private static Result BuildOnce(Zone Z, int seed, int? forced, int attempt, bool blockLayout, bool dress, HashSet<int> aug, out Grid gridOut, out List<PocketRec> pocketsOut, bool siteTopup = true)
		{
			_state = (uint)seed; _noiseSeed = seed;
			gridOut = null; pocketsOut = null;
			var g = ReadHost(Z);
			if (aug != null && aug.Count > 0)
			{
				foreach (int ai in aug) { g.C[ai] = SOLID; g.Host[ai] = 0; g.Tree[ai] = 0; g.Water[ai] = 0; g.Aqua[ai] = 0; }
				g.Augment = aug;
			}
			int si = forced ?? StageIndexOf(RollLabelTable(ref _stageRows, "Cleo_TerraFirma_WarrenStage",
				new[] { "working", "camp", "flourishing" }));
			int wanted = si, degraded = 0;

			var band = LargestBand(g);
			if (band == null) return new Result { Built = false, Reason = "no band", Seed = seed };
			var aq = PlaceAquifers(g, band);
			band = LargestBand(g);
			if (band == null) return new Result { Built = false, Reason = "no band after geology", Seed = seed };

			var connected = new byte[g.W * g.H];
			{
				var cq = new Queue<int>();
				int seeds = 0;
				for (int i = 0; i < g.W * g.H; i++)
					if (g.Marker[i] != 0 && g.C[i] == FLOOR && g.Water[i] == 0)
					{ connected[i] = 1; cq.Enqueue(i); seeds++; }
				bool fallback = seeds == 0;
				if (fallback)
				{
					for (int x = 0; x < g.W; x++)
						foreach (int yy in new[] { 0, g.H - 1 })
							if (g.C[g.Idx(x, yy)] == FLOOR && g.Water[g.Idx(x, yy)] == 0 && connected[g.Idx(x, yy)] == 0)
							{ connected[g.Idx(x, yy)] = 1; cq.Enqueue(g.Idx(x, yy)); }
					for (int y = 0; y < g.H; y++)
						foreach (int xx in new[] { 0, g.W - 1 })
							if (g.C[g.Idx(xx, y)] == FLOOR && g.Water[g.Idx(xx, y)] == 0 && connected[g.Idx(xx, y)] == 0)
							{ connected[g.Idx(xx, y)] = 1; cq.Enqueue(g.Idx(xx, y)); }
				}
				while (cq.Count > 0)
				{
					int ci = cq.Dequeue();
					int cx = ci % g.W, cy = ci / g.W;
					foreach (var d in N4)
					{
						int nx = cx + d[0], ny = cy + d[1];
						if (!g.Inb(nx, ny)) continue;
						int ni = g.Idx(nx, ny);
						if (connected[ni] != 0 || g.C[ni] != FLOOR || g.Water[ni] != 0) continue;
						connected[ni] = 1; cq.Enqueue(ni);
					}
				}
				int connCount = 0; for (int i = 0; i < connected.Length; i++) if (connected[i] != 0) connCount++;
				Helpers.VerifyLog("WARREN", $"host connectivity: {connCount} canyon-connected open cells ({(fallback ? "edge fallback" : seeds + " marker seeds")})");
				if (connCount == 0)
					for (int i = 0; i < connected.Length; i++) if (g.C[i] == FLOOR && g.Water[i] == 0) connected[i] = 1;
			}
			var skin = band.Cells.Where(i =>
			{
				int x = i % g.W, y = i / g.W;
				if (x < 1 || y < 1 || x > g.W - 2 || y > g.H - 2) return false;
				return Touches4(g, x, y, i2 => connected[i2] != 0);
			}).ToList();
			if (skin.Count == 0) return new Result { Built = false, Reason = "band never meets the canyon", Seed = seed };
			for (int i = skin.Count - 1; i > 0; i--) { int j = R(0, i); (skin[i], skin[j]) = (skin[j], skin[i]); }
			if (attempt > 0 && skin.Count > 1)
			{
				int rot = (attempt * 5) % skin.Count;
				var moved = skin.Take(rot).ToList();
				skin.RemoveRange(0, rot);
				skin.AddRange(moved);
			}

			RegionRec region = null; Pt mouth = null;
			foreach (int mi in skin.Take(FACE_SAMPLE))
			{
				var cand = new Pt { X = mi % g.W, Y = mi / g.W };
				var reachR = GrowRegion(g, cand, int.MaxValue, connected);
				if (reachR.Size < FACE_PREDICTOR) continue;
				int sTry = si, deg = 0;
				while (sTry > 0 && STAGES[sTry].Area > reachR.Size) { sTry--; deg++; }
				region = GrowRegion(g, cand, STAGES[sTry].Area, connected);
				si = sTry; degraded = deg; mouth = cand;
				break;
			}
			if (region == null) return new Result { Built = false, Reason = "no face opens onto enough rock", Seed = seed };
			int bx0 = g.W, by0 = g.H, bx1 = 0, by1 = 0;
			foreach (int ci in region.Cells)
			{
				int x = ci % g.W, y = ci / g.W;
				if (x < bx0) bx0 = x; if (x > bx1) bx1 = x;
				if (y < by0) by0 = y; if (y > by1) by1 = y;
			}
			var foot = new Box { X0 = bx0, Y0 = by0, X1 = bx1, Y1 = by1 };
			g.Region = region.Mask;

			string occupancy = RollLabelTable(ref _occupancyRows, "Cleo_TerraFirma_WarrenOccupancy",
				new[] { "in use", "abandoned", "reclaimed" });

			var lobeCells = new List<int>();
			{
				var lseen = new byte[g.W * g.H];
				var lq = new Queue<int>();
				int m0 = g.Idx(mouth.X, mouth.Y);
				lseen[m0] = 1; lq.Enqueue(m0); lobeCells.Add(m0);
				while (lq.Count > 0)
				{
					int ci = lq.Dequeue();
					int lx = ci % g.W, ly = ci / g.W;
					foreach (var d in N4)
					{
						int nx = lx + d[0], ny = ly + d[1];
						if (!g.Inb(nx, ny)) continue;
						int ni = g.Idx(nx, ny);
						if (lseen[ni] != 0 || region.Mask[ni] == 0) continue;
						lseen[ni] = 1; lq.Enqueue(ni); lobeCells.Add(ni);
					}
				}
				Helpers.VerifyLog("WARREN", $"mouth lobe: {lobeCells.Count} of {region.Size} claimable cells");
			}
			var S = STAGES[si];
			var art = LayArtery(g, S, mouth, foot, region.Mask, lobeCells);
			g.MouthIdx = g.Idx(art.Mouth.X, art.Mouth.Y);
			bool wantHall = si == 2 && Chance(COMMONS_PCT / 100.0);
			List<PocketRec> pockets;
			if (blockLayout)
			{
				pockets = CarveBlocks(g, art, S, wantHall);
			}
			else
			{
				pockets = FindPockets(g, art, R(S.P0, S.P1), wantHall);
				foreach (var p in pockets) ResolvePocket(g, p);
				foreach (var p in pockets) ConnectPocket(g, p, art);
			}
			foreach (var p in pockets) p.Live = occupancy == "in use";
			int crosscuts = AddCrosscuts(g, S.Cross);
			int filled = Infill(g, art, pockets, R(S.Fill0, S.Fill1));
			int breaches = BreachAquifers(g);
			int repairs = RepairConnectivity(g);
			int trimmed = TrimSlack(g);
			var m = MeasureG(g);
			if (m.Floor < MIN_SITE || pockets.Count < 2)
				return new Result { Built = false, Reason = $"too little got built ({m.Floor} floor, {pockets.Count} rooms)", Seed = seed };
			int[] floorMin = { 0, CAMP_FLOOR, FLOUR_FLOOR };
			while (si > 0 && m.Floor < floorMin[si]) { si--; degraded++; }
			var roles = RegisterRoles(g, art, pockets, si, occupancy);
			int gardenBreaches = BreachGardens(g, out int gardensFound);
			if (gardensFound > 0)
				Helpers.VerifyLog("WARREN", $"gardens: {gardensFound} sealed, breached {gardenBreaches}");
			if (dress)
			{
				string dressStats = DressSite(g, art, pockets, roles, occupancy, m.Floor, si, siteTopup);
				Helpers.VerifyLog("WARREN", $"dress: {dressStats}");
			}
			{
				bool Pass(int i2) => (g.C[i2] == FLOOR && g.Host[i2] != 0) || g.Water[i2] != 0;
				int lmx = g.MouthIdx % g.W, lmy = g.MouthIdx / g.W;
				var lseen2 = new byte[g.W * g.H]; var lq = new List<int>();
				foreach (var d in N4)
				{
					int nx = lmx + d[0], ny = lmy + d[1]; if (!g.Inb(nx, ny)) continue;
					int i2 = g.Idx(nx, ny);
					if (Pass(i2) && lseen2[i2] == 0) { lseen2[i2] = 1; lq.Add(i2); }
				}
				int lh = 0; bool touchesEdge = false;
				while (lh < lq.Count && !touchesEdge)
				{
					int c2 = lq[lh++], cx = c2 % g.W, cy = c2 / g.W;
					if (cx == 0 || cy == 0 || cx == g.W - 1 || cy == g.H - 1) { touchesEdge = true; break; }
					foreach (var d in N4)
					{
						int nx = cx + d[0], ny = cy + d[1]; if (!g.Inb(nx, ny)) continue;
						int i2 = g.Idx(nx, ny);
						if (lseen2[i2] == 0 && Pass(i2)) { lseen2[i2] = 1; lq.Add(i2); }
					}
				}
				if (!touchesEdge && lq.Count > 0)
				{
					bool WFloor(int i2) => g.C[i2] == FLOOR && g.Host[i2] == 0;
					double DCost(int i2)
					{
						int x2 = i2 % g.W, y2 = i2 / g.W;
						if (WFloor(i2)) return 25;
						if (g.C[i2] != FLOOR)
						{
							if (Touches4(g, x2, y2, WFloor)) return 1e6;
							return 2;
						}
						return 0.5;
					}
					var ldist = new double[g.W * g.H]; for (int k = 0; k < ldist.Length; k++) ldist[k] = double.MaxValue;
					var lprev = new int[g.W * g.H]; for (int k = 0; k < lprev.Length; k++) lprev[k] = -1;
					var ldone = new byte[g.W * g.H];
					var lheap = new List<(double, int)>();
					void LPush((double, int) v) { lheap.Add(v); int a2 = lheap.Count - 1; while (a2 > 0) { int p2 = (a2 - 1) >> 1; if (lheap[p2].Item1 <= lheap[a2].Item1) break; (lheap[p2], lheap[a2]) = (lheap[a2], lheap[p2]); a2 = p2; } }
					(double, int) LPop() { var t2 = lheap[0]; var l2 = lheap[lheap.Count - 1]; lheap.RemoveAt(lheap.Count - 1); if (lheap.Count > 0) { lheap[0] = l2; int a2 = 0; for (; ; ) { int b2 = 2 * a2 + 1, c3 = b2 + 1, m2 = a2; if (b2 < lheap.Count && lheap[b2].Item1 < lheap[m2].Item1) m2 = b2; if (c3 < lheap.Count && lheap[c3].Item1 < lheap[m2].Item1) m2 = c3; if (m2 == a2) break; (lheap[m2], lheap[a2]) = (lheap[a2], lheap[m2]); a2 = m2; } } return t2; }
					foreach (int s2 in lq) { ldist[s2] = 0; LPush((0, s2)); }
					int lhit = -1;
					while (lheap.Count > 0)
					{
						var (lc, lcur) = LPop(); if (ldone[lcur] != 0) continue; ldone[lcur] = 1;
						int cx = lcur % g.W, cy = lcur / g.W;
						if (cx == 0 || cy == 0 || cx == g.W - 1 || cy == g.H - 1) { lhit = lcur; break; }
						foreach (var d in N4)
						{
							int nx = cx + d[0], ny = cy + d[1]; if (!g.Inb(nx, ny)) continue;
							int ni = g.Idx(nx, ny); if (ldone[ni] != 0) continue;
							double w2 = DCost(ni); if (w2 >= 1e6) continue;
							double nc = lc + w2;
							if (nc < ldist[ni]) { ldist[ni] = nc; lprev[ni] = lcur; LPush((nc, ni)); }
						}
					}
					if (lhit >= 0)
					{
						g.Defile = g.Defile ?? new List<int>();
						for (int c2 = lhit; c2 >= 0; c2 = lprev[c2])
							if (g.C[c2] != FLOOR) { g.C[c2] = FLOOR; g.Host[c2] = 1; g.Tree[c2] = 0; g.Defile.Add(c2); }
						Helpers.VerifyLog("WARREN", $"mouth-access law: shore stranded, defile of {g.Defile.Count} cells cut to the zone edge");
					}
					else Helpers.VerifyLog("WARREN", "mouth-access law: shore stranded and NO defile route found");
				}
			}
			if (dress) LayPowerGrid(g, pockets, roles, si, occupancy);
			Helpers.VerifyLog("WARREN", $"carve: band {band.Mass}, mouth ({mouth.X},{mouth.Y}), stage {STAGES[si].Name} (wanted {STAGES[wanted].Name}, degraded {degraded}), " +
				$"{pockets.Count} pockets, floor {m.Floor}, loops {m.Loops}, comps {m.Comps}, crosscuts {crosscuts}, infill {filled}, " +
				$"aquifers {aq.Pools}p/{aq.Veins}v, breaches {breaches}, repairs {repairs}, trimmed {trimmed}, layout {(blockLayout ? "blocks" : "boxes")}");
			Helpers.VerifyLog("WARREN", $"roles: {roles.Tally}; yard {roles.YardDesc}; occupancy {occupancy}");
			gridOut = g; pocketsOut = pockets;
			return new Result
			{
				Built = true, Stage = STAGES[si].Name, StageIdx = si, Occupancy = occupancy,
				Roles = roles.Tally, Yard = roles.YardDesc, Layout = blockLayout ? "blocks" : "boxes",
				Floor = m.Floor, Pockets = pockets.Count, Degraded = degraded, Attempt = attempt, Seed = seed,
				AquiferPools = aq.Pools, AquiferVeins = aq.Veins,
				BuildMethod = aug != null && aug.Count > 0 ? "augmented" : "natural",
				AugmentCells = aug?.Count ?? 0,
			};
		}

		public static Result Build(Zone Z, int seed, int? forcedStage = null, bool apply = true, HashSet<int> aug = null)
		{
			Result last = null;
			for (int a = 0; a < ATTEMPTS; a++)
			{
				var r = BuildOnce(Z, seed, forcedStage, a, false, apply, aug, out Grid g, out List<PocketRec> pockets);
				if (r.Built)
				{
					if (apply) { CompareFurnishing(Z, seed, forcedStage, a, false, aug, g, pockets, r); ApplyToZone(Z, g); PopulateSite(Z, g, pockets, r); }
					return r;
				}
				last = r;
				if (r.Reason.StartsWith("too little got built"))
				{
					var r2 = BuildOnce(Z, seed, forcedStage, a, true, apply, aug, out Grid g2, out List<PocketRec> pockets2);
					if (r2.Built)
					{
						Helpers.VerifyLog("WARREN", $"blocks rescued the face (attempt {a}, boxes built {r.Reason})");
						if (apply) { CompareFurnishing(Z, seed, forcedStage, a, true, aug, g2, pockets2, r2); ApplyToZone(Z, g2); PopulateSite(Z, g2, pockets2, r2); }
						return r2;
					}
					last = r2;
				}
				if (r.Reason == "no band" || r.Reason == "no band after geology" || r.Reason == "band never meets the canyon") break;
			}
			Helpers.VerifyLog("WARREN", $"no site: {last?.Reason ?? "?"} (zone {Z.ZoneID}, seed {seed})");
			return last ?? new Result { Built = false, Reason = "no attempt ran", Seed = seed };
		}

		private static int GrowAugment(Zone Z, HashSet<int> aug, int assigned, bool desperate)
		{
			var g = ReadHost(Z);
			if (aug.Count > 0) foreach (int ai in aug) { g.C[ai] = SOLID; g.Host[ai] = 0; g.Tree[ai] = 0; g.Water[ai] = 0; }
			bool DryFloor(int i2) => g.C[i2] == FLOOR && g.Host[i2] != 0 && g.Water[i2] == 0;
			var lab = new int[g.W * g.H]; for (int k = 0; k < lab.Length; k++) lab[k] = -1;
			var comps = new List<List<int>>();
			for (int s2 = 0; s2 < g.W * g.H; s2++)
			{
				if (lab[s2] >= 0 || g.C[s2] == FLOOR || g.Host[s2] != 0) continue;
				int id2 = comps.Count; var cells = new List<int>();
				var st = new Stack<int>(); st.Push(s2); lab[s2] = id2;
				while (st.Count > 0)
				{
					int c2 = st.Pop(); cells.Add(c2);
					int cx = c2 % g.W, cy = c2 / g.W;
					foreach (var d in N4)
					{
						int nx = cx + d[0], ny = cy + d[1]; if (!g.Inb(nx, ny)) continue;
						int ni = g.Idx(nx, ny);
						if (lab[ni] < 0 && g.C[ni] != FLOOR && g.Host[ni] == 0) { lab[ni] = id2; st.Push(ni); }
					}
				}
				comps.Add(cells);
			}
			if (comps.Count == 0) return 0;
			comps.Sort((a, b) => b.Count.CompareTo(a.Count));
			List<int> bandCells = null;
			foreach (var c2 in comps)
				if (c2.Any(i2 => Touches4(g, i2 % g.W, i2 / g.W, DryFloor))) { bandCells = c2; break; }
			bandCells = bandCells ?? comps[0];
			int have = 0;
			var connected = new byte[g.W * g.H];
			for (int k = 0; k < connected.Length; k++) if (DryFloor(k)) connected[k] = 1;
			var skinCells = bandCells.Where(i2 =>
			{
				int x2 = i2 % g.W, y2 = i2 / g.W;
				return x2 >= 1 && y2 >= 1 && x2 <= g.W - 2 && y2 <= g.H - 2 && Touches4(g, x2, y2, k2 => connected[k2] != 0);
			}).Take(10).ToList();
			foreach (int s2 in skinCells)
			{
				var reach = GrowRegion(g, new Pt { X = s2 % g.W, Y = s2 / g.W }, int.MaxValue, connected);
				if (reach.Size > have) have = reach.Size;
			}
			int need = (int)Math.Ceiling(STAGES[assigned].Area * 1.25);
			int want = need - have;
			if (want <= 0) want = (int)Math.Ceiling(STAGES[assigned].Area * 0.15);
			int FloorComps()
			{
				var seen = new byte[g.W * g.H]; int n2 = 0;
				for (int s3 = 0; s3 < g.W * g.H; s3++)
				{
					if (seen[s3] != 0 || !DryFloor(s3)) continue;
					n2++; var q3 = new List<int> { s3 }; seen[s3] = 1; int h3 = 0;
					while (h3 < q3.Count)
					{
						int c3 = q3[h3++], cx = c3 % g.W, cy = c3 / g.W;
						foreach (var d in N4)
						{
							int nx = cx + d[0], ny = cy + d[1]; if (!g.Inb(nx, ny)) continue;
							int ni = g.Idx(nx, ny);
							if (seen[ni] == 0 && DryFloor(ni)) { seen[ni] = 1; q3.Add(ni); }
						}
					}
				}
				return n2;
			}
			int baseComps = FloorComps();
			var q = new List<int>(); var inq = new byte[g.W * g.H];
			void Enq(int x2, int y2)
			{
				if (x2 < 1 || y2 < 1 || x2 > g.W - 2 || y2 > g.H - 2) return;
				int i2 = g.Idx(x2, y2); if (inq[i2] != 0) return;
				if (DryFloor(i2)) { inq[i2] = 1; q.Add(i2); return; }
				if (desperate && g.Water[i2] != 0) { inq[i2] = 1; q.Add(i2); }
			}
			foreach (int i2 in bandCells)
				foreach (var d in N4) if (g.Inb(i2 % g.W + d[0], i2 / g.W + d[1])) Enq(i2 % g.W + d[0], i2 / g.W + d[1]);
			int head = 0, added = 0;
			var fuzzed = new byte[g.W * g.H];
			while (head < q.Count && added < want)
			{
				int i2 = q[head++], x2 = i2 % g.W, y2 = i2 / g.W;
				if (!desperate && AUG_FUZZ > 0 && fuzzed[i2] == 0 && Chance(AUG_FUZZ / 100.0)) { fuzzed[i2] = 1; q.Add(i2); continue; }
				byte wasTree = g.Tree[i2], wasWater = g.Water[i2], wasHost = g.Host[i2];
				g.C[i2] = SOLID; g.Host[i2] = 0; g.Tree[i2] = 0; g.Water[i2] = 0;
				if (wasWater == 0 && FloorComps() > baseComps)
				{ g.C[i2] = FLOOR; g.Host[i2] = wasHost; g.Tree[i2] = wasTree; g.Water[i2] = wasWater; continue; }
				aug.Add(i2); added++;
				foreach (var d in N4) if (g.Inb(x2 + d[0], y2 + d[1])) Enq(x2 + d[0], y2 + d[1]);
			}
			return added;
		}

		public static Result BuildAugmented(Zone Z, int seed, int assigned)
		{
			var aug = new HashSet<int>(); Result r = null; int rounds = 0; bool desperate = false;
			HashSet<int> bestAug = null; int bestStage = -1, bestRounds = 0;
			for (int t = 0; t <= AUG_TRIES; t++)
			{
				r = Build(Z, seed, assigned, apply: false, aug.Count > 0 ? aug : null);
				if (r.Built && r.StageIdx > bestStage) { bestStage = r.StageIdx; bestAug = new HashSet<int>(aug); bestRounds = rounds; }
				if (r.Built && r.StageIdx >= assigned) { bestAug = new HashSet<int>(aug); bestRounds = rounds; break; }
				if (t == AUG_TRIES) break;
				int grown = GrowAugment(Z, aug, assigned, desperate);
				if (grown == 0 && !desperate) { desperate = true; grown = GrowAugment(Z, aug, assigned, true); }
				if (grown == 0) break;
				rounds++;
			}
			if (bestAug != null)
			{
				var final = Build(Z, seed, assigned, apply: true, bestAug.Count > 0 ? bestAug : null);
				final.WinningAugmentRounds = bestRounds;
				final.ExploredAugmentRounds = rounds;
				Helpers.VerifyLog("WARREN", $"augment: {bestAug.Count} cells over {rounds} rounds -> {final.Stage} (claimed {STAGES[assigned].Name})");
				return final;
			}
			Helpers.VerifyLog("WARREN", $"augment ladder exhausted ({rounds} rounds) - REBUILD RUNG fires for {Z.ZoneID}");
			var rebuilt = RebuildZone(Z, seed, assigned);
			rebuilt.ExploredAugmentRounds = rounds;
			return rebuilt;
		}

		private static Result RebuildZone(Zone Z, int seed, int assigned)
		{
			var g0 = ReadHost(Z);
			var aug = new HashSet<int>();
			for (int i2 = 0; i2 < g0.W * g0.H; i2++) aug.Add(i2);
			for (int x2 = 0; x2 < g0.W; x2++)
				foreach (int y2 in new[] { 0, g0.H - 1 })
					if (g0.C[g0.Idx(x2, y2)] == FLOOR && g0.Host[g0.Idx(x2, y2)] != 0 && g0.Water[g0.Idx(x2, y2)] == 0) aug.Remove(g0.Idx(x2, y2));
			for (int y2 = 1; y2 < g0.H - 1; y2++)
				foreach (int x2 in new[] { 0, g0.W - 1 })
					if (g0.C[g0.Idx(x2, y2)] == FLOOR && g0.Host[g0.Idx(x2, y2)] != 0 && g0.Water[g0.Idx(x2, y2)] == 0) aug.Remove(g0.Idx(x2, y2));
			var r = Build(Z, seed, assigned, apply: true, aug);
			r.BuildMethod = "rebuild";
			r.AugmentCells = aug.Count;
			Helpers.VerifyLog("WARREN", r.Built
				? $"rebuild rung: {Z.ZoneID} stood a {r.Stage} on the maximal band ({aug.Count} cells recast)"
				: $"rebuild rung FAILED for {Z.ZoneID}: {r.Reason} - claim will not stand");
			return r;
		}

		private static void ApplyToZone(Zone Z, Grid g)
		{
			int carved = 0, pools = 0, padded = 0;
			if (g.Augment != null)
			{
				int walled = 0, openedIn = 0, displaced = 0;
				foreach (int i in g.Augment)
				{
					Cell c = Z.GetCell(i % g.W, i / g.W);
					if (c == null) continue;
					foreach (GameObject o in c.GetObjectsWithPart("Combat").ToList()) { c.RemoveObject(o); displaced++; }
					foreach (GameObject o in c.GetObjectsWithPart("LiquidVolume").ToList()) o.Obliterate();
					foreach (GameObject o in c.GetObjectsWithPart("PlantProperties").ToList()) o.Obliterate();
					if (g.C[i] != FLOOR)
					{
						if (!c.HasWall()) { c.AddObject(GameObject.Create("Shale")); walled++; }
					}
					else openedIn++;
				}
				Helpers.VerifyLog("WARREN", $"augment applied: {g.Augment.Count} cells ({walled} walled, {openedIn} opened into the dig, {displaced} combat objects removed)");
			}
			if (g.Defile != null && g.Defile.Count > 0)
			{
				int cut = 0;
				foreach (int i in g.Defile)
				{
					Cell c = Z.GetCell(i % g.W, i / g.W);
					if (c == null) continue;
					foreach (GameObject wall in c.GetWalls().ToList()) wall.Obliterate();
					cut++;
				}
				Helpers.VerifyLog("WARREN", $"defile applied: {cut} cells opened toward the zone edge");
			}
			for (int y = 0; y < g.H; y++)
				for (int x = 0; x < g.W; x++)
				{
					int i = g.Idx(x, y);
					if (g.Aqua[i] != 0)
					{
						Cell c = Z.GetCell(x, y);
						if (c == null) continue;
						foreach (GameObject wall in c.GetWalls().ToList()) wall.Obliterate();
						if (!c.HasObjectWithPart("LiquidVolume")) { c.AddObject(GameObject.Create("DeepBrackishPool")); pools++; }
						continue;
					}
					if (g.C[i] == FLOOR && g.Host[i] == 0)
					{
						Cell c = Z.GetCell(x, y);
						if (c == null) continue;
						foreach (GameObject wall in c.GetWalls().ToList()) wall.Obliterate();
						carved++;
						if (i != g.MouthIdx && g.Water[i] == 0)
						{
							c.AddObject(GameObject.Create("DirtFloor"));
							padded++;
						}
					}
				}
			ApplyCommonsEtchings(Z, g);
			int dressed = PlaceItems(Z, g);
			ApplyPowerWires(Z, g);
			Helpers.VerifyLog("WARREN", $"applied: {carved} cells carved ({padded} padded), {pools} pool cells, {dressed} dress items");
			if (Helpers.VERIFY_WATCH && g.MouthIdx >= 0)
			{
				int mx = g.MouthIdx % g.W, my = g.MouthIdx / g.W;
				var names = new List<string>();
				foreach (var d in N8)
				{
					Cell nc = Z.GetCell(mx + d[0], my + d[1]);
					if (nc == null) continue;
					GameObject top = nc.GetWalls().FirstOrDefault() ?? nc.GetFirstObjectWithPart("LiquidVolume");
					names.Add($"({mx + d[0]},{my + d[1]}) {(top != null ? top.Blueprint : (nc.IsSolid() ? "solid?" : "open"))}");
				}
				Helpers.VerifyLog("WARREN", $"mouth ({mx},{my}) neighbors at apply: {string.Join(" | ", names)}");
			}
		}

		private static void PopulateSite(Zone Z, Grid g, List<PocketRec> pockets, Result r)
		{
			List<int[]> FreeCellsOf(PocketRec p)
			{
				var cs = new List<int[]>();
				for (int y = p.In.Y0; y <= p.In.Y1; y++)
					for (int x = p.In.X0; x <= p.In.X1; x++)
					{
						int i = g.Idx(x, y);
						if (g.Pocket[i] != p.Id || g.C[i] != FLOOR || g.Water[i] != 0) continue;
						Cell c = Z.GetCell(x, y);
						if (c == null || c.IsSolid() || c.HasObjectWithPart("Combat")) continue;
						cs.Add(new[] { x, y });
					}
				return cs;
			}
			bool Seat(GameObject who, List<PocketRec> prefer)
			{
				foreach (var p in prefer)
				{
					var cs = FreeCellsOf(p);
					if (cs.Count == 0) continue;
					var c = cs[R(0, cs.Count - 1)];
					Z.GetCell(c[0], c[1]).AddObject(who);
					return true;
				}
				var any = new List<int>();
				for (int i = 0; i < g.W * g.H; i++)
					if (g.C[i] == FLOOR && g.Host[i] == 0 && g.Water[i] == 0)
					{
						Cell c = Z.GetCell(i % g.W, i / g.W);
						if (c != null && !c.IsSolid() && !c.HasObjectWithPart("Combat")) any.Add(i);
					}
				if (any.Count == 0) return false;
				int pick = any[R(0, any.Count - 1)];
				Z.GetCell(pick % g.W, pick / g.W).AddObject(who);
				return true;
			}
			List<PocketRec> ByRole(string role, bool deepFirst)
			{
				var list = pockets.Where(p => p.Role == role).ToList();
				list = deepFirst ? list.OrderByDescending(p => p.DepthRank).ToList() : list.OrderBy(p => p.DepthRank).ToList();
				list.AddRange(pockets.Where(p => p.Role != role));
				return list;
			}
			bool SeatRandom(GameObject who2, List<PocketRec> cands)
			{
				var open = cands.ToList();
				while (open.Count > 0)
				{
					int pi = R(0, open.Count - 1);
					var p = open[pi]; open.RemoveAt(pi);
					var cs = FreeCellsOf(p);
					if (cs.Count == 0) continue;
					var c = cs[R(0, cs.Count - 1)];
					Z.GetCell(c[0], c[1]).AddObject(who2);
					return true;
				}
				return false;
			}
			bool[] mouthReach = null;
			bool[] MouthReach()
			{
				if (mouthReach != null) return mouthReach;
				var reach = new bool[g.W * g.H];
				var fq = new Queue<int>();
				reach[g.MouthIdx] = true; fq.Enqueue(g.MouthIdx);
				while (fq.Count > 0)
				{
					int cur = fq.Dequeue();
					foreach (var d in N8)
					{
						int nx = cur % g.W + d[0], ny = cur / g.W + d[1];
						if (!g.Inb(nx, ny)) continue;
						int ni = g.Idx(nx, ny);
						if (reach[ni] || g.C[ni] != FLOOR || g.Water[ni] != 0 || g.Tree[ni] != 0) continue;
						reach[ni] = true; fq.Enqueue(ni);
					}
				}
				return mouthReach = reach;
			}
			GameObject posted = null;
			bool SeatOutsidePost(GameObject who2)
			{
				if (g.MouthIdx < 0) return false;
				int mx = g.MouthIdx % g.W, my = g.MouthIdx / g.W;
				var reach = MouthReach();
				var posts = new List<int>();
				for (int y = Math.Max(0, my - OUTRIDER_POST_RADIUS); y <= Math.Min(g.H - 1, my + OUTRIDER_POST_RADIUS); y++)
					for (int x = Math.Max(0, mx - OUTRIDER_POST_RADIUS); x <= Math.Min(g.W - 1, mx + OUTRIDER_POST_RADIUS); x++)
					{
						int i = g.Idx(x, y);
						if (!reach[i] || g.C[i] != FLOOR || g.Host[i] == 0 || g.Water[i] != 0) continue;
						Cell c = Z.GetCell(x, y);
						if (c == null || c.IsSolid() || c.HasObjectWithPart("Combat")) continue;
						posts.Add(i);
					}
				if (posts.Count == 0) return false;
				int pick = posts[R(0, posts.Count - 1)];
				Z.GetCell(pick % g.W, pick / g.W).AddObject(who2);
				posted = who2;
				return true;
			}
			void StampPatrols()
			{
				var outriders = Z.GetObjectsWithPart("Cleo_TerraFirma_OutriderPatrol");
				if (outriders.Count == 0 || g.MouthIdx < 0) return;
				var dials = outriders[0].GetPart<XRL.World.Parts.Cleo_TerraFirma_OutriderPatrol>();
				int mx = g.MouthIdx % g.W, my = g.MouthIdx / g.W;
				int sectors = Math.Max(2, dials.BeatPoints);
				var bySector = new List<int>[sectors];
				var reach = MouthReach();
				for (int y = Math.Max(0, my - dials.BeatRadius); y <= Math.Min(g.H - 1, my + dials.BeatRadius); y++)
					for (int x = Math.Max(0, mx - dials.BeatRadius); x <= Math.Min(g.W - 1, mx + dials.BeatRadius); x++)
					{
						int i = g.Idx(x, y);
						if (Math.Max(Math.Abs(x - mx), Math.Abs(y - my)) < dials.BeatInner) continue;
						if (!reach[i] || g.C[i] != FLOOR || g.Host[i] == 0 || g.Water[i] != 0) continue;
						Cell c = Z.GetCell(x, y);
						if (c == null || c.IsSolid()) continue;
						double a = Math.Atan2(y - my, x - mx) + Math.PI;
						int s = Math.Min(sectors - 1, (int)(a / (2 * Math.PI) * sectors));
						(bySector[s] ??= new List<int>()).Add(i);
					}
				var beat = new List<string>();
				foreach (var cands in bySector)
					if (cands != null)
					{
						int i = cands[R(0, cands.Count - 1)];
						beat.Add((i % g.W) + "," + (i / g.W));
					}
				if (beat.Count < 2)
				{
					Helpers.VerifyLog("PATROL", $"no beat: {beat.Count} waypoint(s) around the mouth");
					return;
				}
				var inside = new List<int>();
				for (int i = 0; i < g.W * g.H; i++)
					if (g.C[i] == FLOOR && g.Host[i] == 0 && g.Water[i] == 0)
					{
						Cell c = Z.GetCell(i % g.W, i / g.W);
						if (c != null && !c.IsSolid()) inside.Add(i);
					}
				string beatStr = string.Join(";", beat);
				foreach (var o in outriders)
				{
					var p = o.GetPart<XRL.World.Parts.Cleo_TerraFirma_OutriderPatrol>();
					p.Beat = beatStr;
					if (inside.Count > 0)
					{
						int ri = inside[R(0, inside.Count - 1)];
						p.Rest = (ri % g.W) + "," + (ri / g.W);
					}
				}
				posted?.GetPart<XRL.World.Parts.Cleo_TerraFirma_OutriderPatrol>()?.TakeDuty(XRL.The.Game?.TimeTicks ?? 0);
				Helpers.VerifyLog("PATROL", $"beat of {beat.Count} around mouth ({mx},{my}): {beatStr}; {outriders.Count} outrider(s), posted {(posted != null ? "yes" : "no")}");
			}
			int outposts = 0;
			Genkit.LocationList warrenArea = null;
			Genkit.LocationList WarrenArea()
			{
				if (warrenArea != null) return warrenArea;
				var locs = new List<Genkit.Location2D>();
				for (int i = 0; i < g.W * g.H; i++)
					if (g.C[i] == FLOOR && g.Host[i] == 0 && g.Water[i] == 0)
						locs.Add(Genkit.Location2D.Get(i % g.W, i / g.W));
				return warrenArea = new Genkit.LocationList(locs);
			}
			bool merchantPlaced = false;
			bool SeatMerchant(GameObject who)
			{
				if (merchantPlaced) return false;
				var walls = new List<Cell>();
				var seen = new HashSet<int>();
				for (int i = 0; i < g.W * g.H; i++)
				{
					if (g.C[i] != FLOOR || g.Host[i] != 0 || g.Water[i] != 0) continue;
					int x = i % g.W, y = i / g.W;
					Cell approach = Z.GetCell(x, y);
					if (approach == null || approach.IsSolid()) continue;
					foreach (var d in N4)
					{
						int wx = x + d[0], wy = y + d[1];
						if (!g.Inb(wx, wy)) continue;
						Cell wall = Z.GetCell(wx, wy);
						if (wall == null || !wall.HasWall() || wall.HasObjectWithPart("Combat")) continue;
						if (seen.Add(g.Idx(wx, wy))) walls.Add(wall);
					}
				}
				if (walls.Count == 0) return false;
				Cell selected = walls[R(0, walls.Count - 1)];
				selected.AddObject(who);
				merchantPlaced = true;
				Helpers.VerifyLog("WARREN", $"vine merchant seated on wall ({selected.X},{selected.Y})");
				return true;
			}
			bool SeatByHint(GameObject who, string hint)
			{
				if (hint == "Cleo_TerraFirma_Web" || hint == "Cleo_TerraFirma_SpiderWeb")
					return SeatWarrenWeb(Z, g, who, hint == "Cleo_TerraFirma_SpiderWeb");
				if (hint == "Cleo_TerraFirma_MerchantWall") return SeatMerchant(who);
				var engineHints = new List<string>();
				string trade = null, denRole = null; int tradePct = 100, outsidePct = -1; bool den = false;
				foreach (string raw in (hint ?? "").Split(','))
				{
					string t = raw.Trim();
					if (t.Length == 0) continue;
					else if (t.StartsWith("Cleo_TerraFirma_Den"))
					{
						den = true;
						var parts = t.Split(':');
						if (parts.Length >= 2) denRole = parts[1];
					}
					else if (t.StartsWith("Cleo_TerraFirma_Trade:"))
					{
						var parts = t.Split(':');
						trade = parts[1];
						if (parts.Length < 3 || !int.TryParse(parts[2], out tradePct))
						{
							tradePct = 100;
							MetricsManager.LogError("TerraFirma: Trade hint '" + t + "' has no readable percent; treated as 100");
						}
					}
					else if (t.StartsWith("Cleo_TerraFirma_OutsidePost"))
					{
						var parts = t.Split(':');
						if (parts.Length < 2 || !int.TryParse(parts[1], out outsidePct)) outsidePct = 100;
					}
					else engineHints.Add(t);
				}
				if (den) return Seat(who, denRole != null ? ByRole(denRole, deepFirst: true) : pockets.OrderByDescending(p => p.DepthRank).ToList());
				if (trade != null && Chance(tradePct / 100.0))
				{
					if (outsidePct >= 0 && Chance(outsidePct / 100.0) && SeatOutsidePost(who)) { outposts++; return true; }
					var mine = pockets.Where(p => p.Role == trade).ToList();
					if (mine.Count > 0 && SeatRandom(who, mine)) return true;
				}
				if (engineHints.Count > 0
					&& XRL.World.ZoneBuilders.ZoneBuilderSandbox.PlaceObjectInArea(Z, WarrenArea(), who, 0, 0, string.Join(",", engineHints)))
					return true;
				return SeatRandom(who, pockets) || Seat(who, pockets);
			}
			string overseerNote = "none rolled";
			int PlaceTable(string table, Dictionary<string, int> census2)
			{
				int placed = 0;
				foreach (var entry in XRL.PopulationManager.Generate(table, (Dictionary<string, string>)null))
					for (int n = 0; n < entry.Number; n++)
					{
						var who = GameObject.Create(entry.Blueprint);
						if (who == null) continue;
						if (!string.IsNullOrEmpty(entry.Builder))
							GameObjectFactory.ApplyBuilder(who, new GamePartBlueprint("XRL.World.ObjectBuilders", entry.Builder));
						if (!SeatByHint(who, entry.Hint)) { who.Obliterate(); continue; }
						placed++;
						if (entry.Blueprint == "Cleo_TerraFirma_Overseer") overseerNote = who.GetReferenceDisplayName();
						if (census2 != null) census2[entry.Blueprint] = (census2.TryGetValue(entry.Blueprint, out int v) ? v : 0) + 1;
					}
				return placed;
			}
			if (r.Occupancy == "abandoned" || r.Occupancy == "reclaimed")
			{
				int denned = r.Occupancy == "reclaimed" ? PlaceTable("Cleo_TerraFirma_WarrenReclaimers", null) : 0;
				var visitors = new Dictionary<string, int>();
				int guests = PlaceTable("Cleo_TerraFirma_WarrenVisitors", visitors);
				int webs = PlaceTable("Cleo_TerraFirma_WarrenSparseWebs", null);
				if (visitors.ContainsKey("Cave Spider")) webs += PlaceTable("Cleo_TerraFirma_WarrenSpiderWebs", null);
				Helpers.VerifyLog("WARREN", $"population: {r.Occupancy}, miners={denned}, visitors={guests} ({string.Join(", ", visitors.Select(v => v.Value + " " + v.Key))}), webs={webs}");
				return;
			}
			var census = new Dictionary<string, int>();
			string[] crewTables = { "Cleo_TerraFirma_WarrenCrew Working", "Cleo_TerraFirma_WarrenCrew Camp", "Cleo_TerraFirma_WarrenCrew Flourishing" };
			int carvers = PlaceTable(crewTables[Math.Max(0, Math.Min(crewTables.Length - 1, r.StageIdx))], census);
			StampPatrols();
			Helpers.VerifyLog("WARREN", $"population: in use, stage {r.Stage}, overseer {overseerNote}, " +
				$"{carvers} crew, {outposts} posted outside ({string.Join(", ", census.Select(kv => kv.Value + " " + kv.Key.Replace("Cleo_TerraFirma_", "")))})");
		}
	}
}
