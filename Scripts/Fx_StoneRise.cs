using System;
using System.Collections.Generic;
using ConsoleLib.Console;
using Kobold;
using UnityEngine;
using XRL.Rules;
using XRL.World;

namespace Cleo.TerraFirma.Scripts
{
	public class Cleo_TerraFirma_JuiceStoneRise : CombatJuiceEntry
	{
		private IRenderable Tile;
		private ex3DSprite2 Target;
		private Vector3 Base;
		private float Delay;
		private float Rise;
		private float Hold;
		private float Sink;
		private float BuriedY;
		private float EmergeY;
		private bool VanishAfterHold;
		private Color Foreground;
		private Color Background;
		private Color Detail;
		private Color DetailNudge;

		public Cleo_TerraFirma_JuiceStoneRise(IRenderable tile, Vector3 basePos, float delay, float rise, float hold, float sink, float buriedDepth, float emergeHeight, bool vanishAfterHold = false)
		{
			Tile = tile;
			Base = basePos;
			Delay = delay;
			Rise = Mathf.Max(0.01f, rise);
			Hold = hold;
			Sink = Mathf.Max(0.01f, sink);
			BuriedY = -buriedDepth;
			EmergeY = -buriedDepth + emergeHeight;
			VanishAfterHold = vanishAfterHold;
			duration = delay + this.Rise + hold + this.Sink;
		}

		public override void start()
		{
			Target = SpriteManager.GetPooledSprite(Tile, Transparent: false);
			Foreground = Target.color;
			Background = Target.backcolor;
			Detail = Target.detailcolor;
			DetailNudge = new Color(Detail.r, Detail.g, Detail.b, 0.996f);
			Transform transform = Target.gameObject.transform;
			transform.SetParent(GameManager.Instance.TileRoot.transform);
			transform.position = Base + new Vector3(0f, BuriedY, 100f);
		}

		public override void update()
		{
			if (Target == null)
				return;
			Target.color = Foreground;
			Target.backcolor = Background;
			Target.detailcolor = DetailNudge;
			Target.detailcolor = Detail;
			float p = t - Delay;
			if (p < 0f)
				return;
			float y;
			if (p <= Rise)
				y = Mathf.Lerp(BuriedY, EmergeY, Easing.SineEaseOut(Mathf.Clamp01(p / Rise)));
			else if (p <= Rise + Hold)
				y = EmergeY;
			else
			{
				if (VanishAfterHold)
				{
					Target.transform.position = Base + new Vector3(0f, BuriedY, 100f);
					return;
				}
				float q = Mathf.Clamp01((p - Rise - Hold) / Sink);
				if (q >= 1f)
				{
					Target.transform.position = Base + new Vector3(0f, BuriedY, 100f);
					return;
				}
				y = Mathf.Lerp(EmergeY, BuriedY, Easing.SineEaseInOut(q));
			}
			Target.transform.position = Base + new Vector3(0f, y, -10f);
		}

		public override void finish()
		{
			if (Target != null)
			{
				SpriteManager.Return(Target);
				Target = null;
			}
			base.finish();
		}
	}

	public class Cleo_TerraFirma_JuiceCarveBlock : CombatJuiceEntry
	{
		private IRenderable Tile;
		private ex3DSprite2 Target;
		private Vector3 Center;
		private float FirstBreak;
		private float BreakStep;
		private Vector3 OrigScale;
		private bool ScaleFallback;
		private exTextureInfo BaseInfo;
		private exTextureInfo[] SubInfos;
		private int Applied;
		private Color Foreground;
		private Color Background;
		private Color Detail;
		private Color DetailNudge;

		public Cleo_TerraFirma_JuiceCarveBlock(IRenderable tile, Vector3 center, float firstBreak, float breakStep)
		{
			Tile = tile;
			Center = center;
			FirstBreak = firstBreak;
			BreakStep = Mathf.Max(0.01f, breakStep);
			duration = firstBreak + 2f * this.BreakStep + 0.02f;
		}

		public override void start()
		{
			Target = SpriteManager.GetPooledSprite(Tile, Transparent: false);
			Foreground = Target.color;
			Background = Target.backcolor;
			Detail = Target.detailcolor;
			DetailNudge = new Color(Detail.r, Detail.g, Detail.b, 0.996f);
			Transform transform = Target.gameObject.transform;
			transform.SetParent(GameManager.Instance.TileRoot.transform);
			OrigScale = transform.localScale;
			ScaleFallback = false;
			Applied = 0;
			BaseInfo = Target.textureInfo;
			SubInfos = null;
			if (BaseInfo != null && !BaseInfo.rotated && !BaseInfo.isDiced)
			{
				SubInfos = new exTextureInfo[2];
				SubInfos[0] = Cleo_TerraFirma_StoneRiseFX.CloneSubRect(BaseInfo, 0f, 0f, 1f, 2f / 3f);
				SubInfos[1] = Cleo_TerraFirma_StoneRiseFX.CloneSubRect(BaseInfo, 0f, 0f, 1f, 1f / 3f);
			}
			transform.position = Center + new Vector3(0f, 0f, -10f);
		}

		public override void update()
		{
			if (Target == null)
				return;
			Target.color = Foreground;
			Target.backcolor = Background;
			Target.detailcolor = DetailNudge;
			Target.detailcolor = Detail;
			int broken = t < FirstBreak ? 0 : Mathf.Min(3, 1 + (int)((t - FirstBreak) / BreakStep));
			if (broken >= 3)
			{
				Target.transform.position = Center + new Vector3(0f, 0f, 100f);
				return;
			}
			if (broken != Applied)
			{
				Applied = broken;
				if (SubInfos != null)
					Target.textureInfo = SubInfos[broken - 1];
				else
				{
					Target.gameObject.transform.localScale = Vector3.Scale(OrigScale, new Vector3(1f, (3 - broken) / 3f, 1f));
					ScaleFallback = true;
				}
			}
			float sy = (3 - broken) / 3f;
			Target.transform.position = Center + new Vector3(0f, -12f * (1f - sy), -10f);
		}

		public override void finish()
		{
			if (Target != null)
			{
				if (ScaleFallback)
					Target.gameObject.transform.localScale = OrigScale;
				if (SubInfos != null && BaseInfo != null)
					Target.textureInfo = BaseInfo;
				SpriteManager.Return(Target);
				Target = null;
			}
			if (SubInfos != null)
			{
				for (int i = 0; i < SubInfos.Length; i++)
				{
					if (SubInfos[i] != null)
						UnityEngine.Object.Destroy(SubInfos[i]);
				}
				SubInfos = null;
			}
			base.finish();
		}
	}

	public class Cleo_TerraFirma_JuiceStoneChunk : CombatJuiceEntry
	{
		private IRenderable Tile;
		private ex3DSprite2 Target;
		private Vector3 Rest;
		private float DriftX;
		private float DropAt;
		private float Air;
		private int Row;
		private int Col;
		private Vector3 OrigScale;
		private bool ScaleFallback;
		private exTextureInfo BaseInfo;
		private exTextureInfo SubInfo;
		private Color Foreground;
		private Color Background;
		private Color Detail;
		private Color DetailNudge;
		private const float BOUNCE_V0 = 100f;
		private const float BOUNCE_G = 1000f;
		private static readonly Vector3 DEBRIS_SCALE = new Vector3(0.4f, 0.4f, 1f);

		public Cleo_TerraFirma_JuiceStoneChunk(IRenderable tile, Vector3 rest, float driftX, float dropAt, float air, int row, int col)
		{
			Tile = tile;
			Rest = rest;
			DriftX = driftX;
			DropAt = dropAt;
			Air = Mathf.Max(0.01f, air);
			Row = row;
			Col = col;
			duration = dropAt + this.Air + 0.02f;
		}

		public override void start()
		{
			Target = SpriteManager.GetPooledSprite(Tile, Transparent: false);
			Foreground = Target.color;
			Background = Target.backcolor;
			Detail = Target.detailcolor;
			DetailNudge = new Color(Detail.r, Detail.g, Detail.b, 0.996f);
			Transform transform = Target.gameObject.transform;
			transform.SetParent(GameManager.Instance.TileRoot.transform);
			OrigScale = transform.localScale;
			ScaleFallback = false;
			BaseInfo = Target.textureInfo;
			SubInfo = null;
			if (BaseInfo != null && !BaseInfo.rotated && !BaseInfo.isDiced)
			{
				SubInfo = Cleo_TerraFirma_StoneRiseFX.CloneSubRect(BaseInfo, Col == 0 ? 0f : 0.5f, (2 - Row) / 3f, 0.5f, 1f / 3f);
				Target.textureInfo = SubInfo;
			}
			else
			{
				transform.localScale = Vector3.Scale(OrigScale, DEBRIS_SCALE);
				ScaleFallback = true;
			}
			transform.position = Rest + new Vector3(0f, 0f, 100f);
		}

		public override void update()
		{
			if (Target == null)
				return;
			Target.color = Foreground;
			Target.backcolor = Background;
			Target.detailcolor = DetailNudge;
			Target.detailcolor = Detail;
			float p = t - DropAt;
			if (p < 0f)
			{
				Target.transform.position = Rest + new Vector3(0f, 0f, 100f);
				return;
			}
			if (p >= Air)
			{
				Target.transform.position = Rest + new Vector3(0f, 0f, 100f);
				return;
			}
			float y = BOUNCE_V0 * p - 0.5f * BOUNCE_G * p * p;
			Target.transform.position = Rest + new Vector3(DriftX * (p / Air), y, -11f);
		}

		public override void finish()
		{
			if (Target != null)
			{
				if (ScaleFallback)
					Target.gameObject.transform.localScale = OrigScale;
				if (SubInfo != null && BaseInfo != null)
					Target.textureInfo = BaseInfo;
				SpriteManager.Return(Target);
				Target = null;
			}
			if (SubInfo != null)
			{
				UnityEngine.Object.Destroy(SubInfo);
				SubInfo = null;
			}
			base.finish();
		}
	}

	public class Cleo_TerraFirma_StoneTileRenderable : IRenderable
	{
		private string TilePath;
		private char Fg;
		private char Detail;
		private char Bg;

		public Cleo_TerraFirma_StoneTileRenderable(string tilePath, char fg, char detail, char bg)
		{
			TilePath = tilePath;
			Fg = fg;
			Detail = detail;
			Bg = bg;
		}

		public string getTile() => TilePath;
		public string getRenderString() => "#";
		public string getColorString() => "&" + Fg + "^" + Bg;
		public string getTileColor() => "&" + Fg + "^" + Bg;
		public char getDetailColor() => Detail;
		public ColorChars getColorChars() => new ColorChars { foreground = Fg, background = Bg, detail = Detail };
		public bool getHFlip() => false;
		public bool getVFlip() => false;
	}

	public static class Cleo_TerraFirma_StoneRiseFX
	{
		public static bool HarnessMute;

		public const string ISOLATED_MASK = "00000000";
		private const string DEFAULT_ATLAS = "Assets_Content_Textures_Tiles_";
		private const string DEFAULT_EXT = ".bmp";

		private static string ComposeTile(string paint, string atlas, string ext, string mask)
		{
			if (paint.IsNullOrEmpty())
				paint = "wall_rock";
			if (atlas.IsNullOrEmpty())
				atlas = DEFAULT_ATLAS;
			if (ext.IsNullOrEmpty())
				ext = DEFAULT_EXT;
			return atlas + paint + "-" + mask + ext;
		}

		public class Schedule
		{
			public float Total;
			public Dictionary<Cell, float> SettleTimes = new Dictionary<Cell, float>();
			public Dictionary<Cell, float> GapTimes = new Dictionary<Cell, float>();
			public List<float> BreakTimes = new List<float>();
			public List<(float time, float mag)> ShakeTimes = new List<(float time, float mag)>();
		}
		public const float BEAT_SEC = 0.25f;
		public const float BOB_RISE = 0.09f;
		private const float BOB_HOLD = 0.03f;
		private const float BOB_SINK = 0.10f;
		private const float RIM_STAGGER = 0.18f;
		private const float RIM_RISE = 0.14f;
		private const float RIM_HOLD = 0.10f;
		private const float RIM_SINK = 0.06f;
		private const int TRAVEL_PLATE_MIN = 2;
		private const int TRAVEL_PLATE_MAX = 4;
		private const int TRAVEL_SHARD_CHANCE = 12;
		private const int RIM_PLATE_MIN = 3;
		private const int RIM_PLATE_MAX = 6;
		private const int RIM_SHARD_CHANCE = 8;
		private const float CALC_LEAD_SEC = 0.10f;
		private const float CALC_STEP_SEC = 0.07f;
		private const float CARVE_SWAP_SEC = 0.20f;
		private const float CARVE_FIRST_BREAK_SEC = 0.35f;
		private const float CARVE_BREAK_STEP_SEC = 0.30f;
		private const float CARVE_AIR_SEC = 0.40f;
		private const float CHUNK_DRIFT = 3f;
		private const int PLATE_JITTER_STEPS = 5;
		private const float BURIED_DEPTH = 20f;
		private const float PULSE1_EMERGE = 14f;
		private static readonly float[] BEAT_AMP = { 1f, 0.5f, 0.25f };
		private const int REBOB_PCT = 50;
		private const float SHAKE_MAG_1 = 0.3f;
		private const float SHAKE_MAG_STEP = 0.1f;
		private const float RIM_EMERGE = 20f;

		public static Schedule Play(Cell origin, int radius, ICollection<Cell> reached, ICollection<Cell> gaps, string paintValue, string paintAtlas, string paintExt, char paintFg, char paintDetail, char paintBg, Func<int, int, int, bool> onRing)
		{
			if (origin == null)
				return null;
			GameManager gm = GameManager.Instance;
			if (gm == null)
				return null;
			int beats = radius - 1;
			float rimStart = beats * BEAT_SEC;
			Schedule schedule = new Schedule();
			schedule.Total = rimStart + RIM_STAGGER + RIM_RISE + RIM_HOLD + 0.04f;
			List<Cell> rimFabric = new List<Cell>();
			List<Cell> rimGaps = new List<Cell>();
			List<Cell>[] shells = new List<Cell>[radius];
			foreach (Cell c in reached)
			{
				if (!c.IsVisible())
					continue;
				int dx = c.X - origin.X;
				int dy = c.Y - origin.Y;
				if (onRing(dx, dy, radius))
				{
					if (gaps != null && gaps.Contains(c))
						rimGaps.Add(c);
					else
						rimFabric.Add(c);
					continue;
				}
				for (int r = 1; r < radius; r++)
				{
					if (onRing(dx, dy, r))
					{
						(shells[r] ?? (shells[r] = new List<Cell>())).Add(c);
						break;
					}
				}
			}
			List<List<Cell>>[] shellPlates = new List<List<Cell>>[radius];
			for (int r = 1; r < radius; r++)
			{
				if (shells[r] != null)
					shellPlates[r] = CutPlates(SortByAngle(shells[r], origin), TRAVEL_PLATE_MIN, TRAVEL_PLATE_MAX, TRAVEL_SHARD_CHANCE);
			}
			List<List<Cell>> rimPlates = CutPlates(SortByAngle(rimFabric, origin), RIM_PLATE_MIN, RIM_PLATE_MAX, RIM_SHARD_CHANCE);
			Helpers.VerifyLog("FIS", $"anim tiles composed painter-style: {(paintAtlas.IsNullOrEmpty() ? DEFAULT_ATLAS : paintAtlas)}{(paintValue.IsNullOrEmpty() ? "wall_rock" : paintValue)}-<mask>{(paintExt.IsNullOrEmpty() ? DEFAULT_EXT : paintExt)}");
			int enqueued = 0;
			for (int beat = 1; beat <= beats; beat++)
			{
				float beatStart = (beat - 1) * BEAT_SEC;
				float amp = PULSE1_EMERGE * BEAT_AMP[Math.Min(beat, BEAT_AMP.Length) - 1];
				schedule.ShakeTimes.Add((beatStart, SHAKE_MAG_1 - (beat - 1) * SHAKE_MAG_STEP));
				for (int r = 1; r <= beat && r < radius; r++)
				{
					if (shellPlates[r] == null)
						continue;
					foreach (List<Cell> plate in shellPlates[r])
					{
						if (r < beat && Stat.Random(1, 100) > REBOB_PCT)
							continue;
						float delay = beatStart + 0.01f * Stat.Random(0, PLATE_JITTER_STEPS);
						enqueued += EnqueuePlate(plate, gm, paintValue, paintAtlas, paintExt, paintFg, paintDetail, paintBg,
							delay, BOB_RISE, BOB_HOLD, BOB_SINK, amp, schedule, Settle: false);
					}
				}
			}
			if (beats < BEAT_AMP.Length)
				schedule.ShakeTimes.Add((rimStart, SHAKE_MAG_1 - beats * SHAKE_MAG_STEP));
			foreach (List<Cell> plate in rimPlates)
			{
				float delay = rimStart + 0.01f * Stat.Random(0, (int)(RIM_STAGGER * 100f));
				enqueued += EnqueuePlate(plate, gm, paintValue, paintAtlas, paintExt, paintFg, paintDetail, paintBg,
					delay, RIM_RISE, RIM_HOLD, RIM_SINK, RIM_EMERGE, schedule, Settle: true);
			}
			string gapTile = ComposeTile(paintValue, paintAtlas, paintExt, ISOLATED_MASK);
			foreach (Cell c in rimGaps)
			{
				float delay = rimStart + 0.01f * Stat.Random(0, (int)(RIM_STAGGER * 100f));
				IRenderable tile = new Cleo_TerraFirma_StoneTileRenderable(gapTile, paintFg, paintDetail, 'k');
				CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceStoneRise(tile, gm.getTileCenter(c.X, c.Y), delay, RIM_RISE, 0.04f, RIM_SINK, BURIED_DEPTH, RIM_EMERGE * 0.7f), async: true);
				schedule.GapTimes[c] = delay + RIM_RISE + 0.04f;
				enqueued++;
			}
			return enqueued > 0 ? schedule : null;
		}

		public static Schedule PlayWallRise(List<Cell> cells, List<string> blueprints)
		{
			GameManager gm = GameManager.Instance;
			if (gm == null || cells == null || cells.Count == 0)
				return null;
			string[] tiles = new string[cells.Count];
			char[] fgs = new char[cells.Count];
			char[] details = new char[cells.Count];
			char[] bgs = new char[cells.Count];
			for (int i = 0; i < cells.Count; i++)
			{
				ResolveWallDressing(i < blueprints.Count ? blueprints[i] : null, out string paint, out string atlas, out string ext, out fgs[i], out details[i], out bgs[i]);
				tiles[i] = ComposeTile(paint, atlas, ext, MaskForSet(cells, i));
			}
			Schedule schedule = new Schedule();
			int enqueued = 0;
			for (int i = 0; i < cells.Count; i++)
			{
				if (!cells[i].IsVisible())
					continue;
				float delay = CALC_LEAD_SEC + i * CALC_STEP_SEC;
				IRenderable tile = new Cleo_TerraFirma_StoneTileRenderable(tiles[i], fgs[i], details[i], 'k');
				CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceStoneRise(tile, gm.getTileCenter(cells[i].X, cells[i].Y), delay, RIM_RISE, RIM_HOLD, RIM_SINK, BURIED_DEPTH, RIM_EMERGE, vanishAfterHold: true), async: true);
				schedule.SettleTimes[cells[i]] = delay + RIM_RISE + RIM_HOLD;
				schedule.Total = Mathf.Max(schedule.Total, delay + RIM_RISE + RIM_HOLD);
				enqueued++;
			}
			return enqueued > 0 ? schedule : null;
		}

		public static Schedule PlayCarveReveal(Cell cell, XRL.World.GameObject wall)
		{
			GameManager gm = GameManager.Instance;
			if (gm == null || cell == null || wall == null)
				return null;
			ResolveWallDressing(wall.Blueprint, out string paint, out string atlas, out string ext, out char fg, out char detail, out char _);
			string liveTc = wall.Render?.TileColor;
			if (liveTc.IsNullOrEmpty())
				liveTc = wall.Render?.ColorString;
			if (!liveTc.IsNullOrEmpty())
			{
				int amp = liveTc.IndexOf('&');
				if (amp >= 0 && amp + 1 < liveTc.Length)
					fg = liveTc[amp + 1];
			}
			string liveDc = wall.Render?.DetailColor;
			if (!liveDc.IsNullOrEmpty())
				detail = liveDc[0];
			string blockTile = wall.Render?.Tile;
			if (blockTile.IsNullOrEmpty())
				blockTile = ComposeTile(paint, atlas, ext, ISOLATED_MASK);
			Vector3 center = gm.getTileCenter(cell.X, cell.Y);
			Schedule schedule = new Schedule();
			CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceCarveBlock(
				new Cleo_TerraFirma_StoneTileRenderable(blockTile, fg, detail, 'k'),
				center, CARVE_FIRST_BREAK_SEC, CARVE_BREAK_STEP_SEC), async: true);
			for (int row = 0; row < 3; row++)
			{
				float dropAt = CARVE_FIRST_BREAK_SEC + row * CARVE_BREAK_STEP_SEC;
				for (int col = 0; col < 2; col++)
				{
					Vector3 rest = center + new Vector3(col == 0 ? -4f : 4f, 8f - row * 8f, 0f);
					IRenderable tile = new Cleo_TerraFirma_StoneTileRenderable(blockTile, fg, detail, 'k');
					CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceStoneChunk(tile, rest, col == 0 ? -CHUNK_DRIFT : CHUNK_DRIFT, dropAt, CARVE_AIR_SEC, row, col), async: true);
				}
				schedule.BreakTimes.Add(dropAt);
			}
			schedule.SettleTimes[cell] = CARVE_SWAP_SEC;
			schedule.Total = CARVE_FIRST_BREAK_SEC + 2f * CARVE_BREAK_STEP_SEC + CARVE_AIR_SEC + 0.05f;
			return schedule;
		}

		public static void PlayEtchReveal(Cell cell, string tile, char fg, char detail)
		{
			GameManager gm = GameManager.Instance;
			if (gm == null || cell == null || tile.IsNullOrEmpty() || HarnessMute)
				return;
			Vector3 center = gm.getTileCenter(cell.X, cell.Y);
			CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceCarveBlock(
				new Cleo_TerraFirma_StoneTileRenderable(tile, fg, detail, 'k'),
				center, CARVE_FIRST_BREAK_SEC, CARVE_BREAK_STEP_SEC), async: true);
			for (int row = 0; row < 3; row++)
			{
				float dropAt = CARVE_FIRST_BREAK_SEC + row * CARVE_BREAK_STEP_SEC;
				for (int col = 0; col < 2; col++)
				{
					Vector3 rest = center + new Vector3(col == 0 ? -4f : 4f, 8f - row * 8f, 0f);
					IRenderable tileChunk = new Cleo_TerraFirma_StoneTileRenderable(tile, fg, detail, 'k');
					CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceStoneChunk(tileChunk, rest, col == 0 ? -CHUNK_DRIFT : CHUNK_DRIFT, dropAt, CARVE_AIR_SEC, row, col), async: true);
				}
			}
		}

		public static exTextureInfo CloneSubRect(exTextureInfo src, float fx, float fy, float fw, float fh)
		{
			exTextureInfo sub = ScriptableObject.CreateInstance<exTextureInfo>();
			sub.texture = src.texture;
			sub.ShaderMode = src.ShaderMode;
			sub.rawWidth = src.rawWidth;
			sub.rawHeight = src.rawHeight;
			int nx = src.x + Mathf.RoundToInt(src.width * fx);
			int ny = src.y + Mathf.RoundToInt(src.height * fy);
			sub.x = nx;
			sub.y = ny;
			sub.width = Mathf.Clamp(Mathf.RoundToInt(src.width * fw), 1, src.x + src.width - nx);
			sub.height = Mathf.Clamp(Mathf.RoundToInt(src.height * fh), 1, src.y + src.height - ny);
			return sub;
		}

		private static string MaskForSet(List<Cell> set, int idx)
		{
			char[] mask = ISOLATED_MASK.ToCharArray();
			for (int j = 0; j < set.Count; j++)
			{
				if (j != idx)
					SetMaskBit(mask, set[idx], set[j]);
			}
			return new string(mask);
		}

		private static void ResolveWallDressing(string blueprint, out string paint, out string atlas, out string ext, out char fg, out char detail, out char bg)
		{
			paint = "wall_rock";
			atlas = null;
			ext = null;
			fg = 'y';
			detail = 'K';
			bg = 'K';
			GameObjectBlueprint b = blueprint == null ? null : GameObjectFactory.Factory.GetBlueprintIfExists(blueprint);
			if (b == null)
				return;
			string p = b.GetTag("PaintedWall");
			if (!p.IsNullOrEmpty())
				paint = p.Contains(",") ? p.Split(',')[0] : p;
			atlas = b.GetTag("PaintedWallAtlas", null);
			ext = b.GetTag("PaintedWallExtension", null);
			string tc = b.GetPartParameter<string>("Render", "TileColor");
			if (tc.IsNullOrEmpty())
				tc = b.GetPartParameter<string>("Render", "ColorString");
			if (!tc.IsNullOrEmpty())
			{
				int amp = tc.IndexOf('&');
				if (amp >= 0 && amp + 1 < tc.Length)
					fg = tc[amp + 1];
			}
			string dc = b.GetPartParameter<string>("Render", "DetailColor");
			if (!dc.IsNullOrEmpty())
				detail = dc[0];
			string cs = b.GetPartParameter<string>("Render", "ColorString");
			if (!cs.IsNullOrEmpty())
			{
				int caret = cs.IndexOf('^');
				if (caret >= 0 && caret + 1 < cs.Length)
					bg = cs[caret + 1];
			}
		}

		private static int EnqueuePlate(List<Cell> plate, GameManager gm, string paintValue, string atlas, string ext, char fg, char detail, char bg,
			float Delay, float Rise, float Hold, float Sink, float Emerge, Schedule schedule, bool Settle)
		{
			int enqueued = 0;
			for (int i = 0; i < plate.Count; i++)
			{
				string tilePath = ComposeTile(paintValue, atlas, ext, MaskFor(plate, i));
				Cell c = plate[i];
				IRenderable tile = new Cleo_TerraFirma_StoneTileRenderable(tilePath, fg, detail, 'k');
				CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceStoneRise(tile, gm.getTileCenter(c.X, c.Y), Delay, Rise, Hold, Sink, BURIED_DEPTH, Emerge), async: true);
				if (Settle)
					schedule.SettleTimes[c] = Delay + Rise + Hold;
				enqueued++;
			}
			return enqueued;
		}

		private static string MaskFor(List<Cell> plate, int idx)
		{
			if (plate.Count == 1)
				return ISOLATED_MASK;
			char[] mask = ISOLATED_MASK.ToCharArray();
			Cell c = plate[idx];
			SetMaskBit(mask, c, idx > 0 ? plate[idx - 1] : null);
			SetMaskBit(mask, c, idx < plate.Count - 1 ? plate[idx + 1] : null);
			return new string(mask);
		}

		private static void SetMaskBit(char[] mask, Cell c, Cell neighbor)
		{
			if (neighbor == null)
				return;
			int ddx = neighbor.X - c.X;
			int ddy = neighbor.Y - c.Y;
			if (ddx < -1 || ddx > 1 || ddy < -1 || ddy > 1 || (ddx == 0 && ddy == 0))
				return;
			int bit;
			if (ddx == 0) bit = ddy < 0 ? 0 : 4;
			else if (ddx > 0) bit = ddy < 0 ? 1 : (ddy == 0 ? 2 : 3);
			else bit = ddy < 0 ? 7 : (ddy == 0 ? 6 : 5);
			mask[bit] = '1';
		}

		private static List<Cell> SortByAngle(List<Cell> cells, Cell origin)
		{
			List<Cell> sorted = new List<Cell>(cells);
			sorted.Sort((a, b) => Math.Atan2(a.Y - origin.Y, a.X - origin.X).CompareTo(Math.Atan2(b.Y - origin.Y, b.X - origin.X)));
			return sorted;
		}

		private static List<List<Cell>> CutPlates(List<Cell> ring, int plateMin, int plateMax, int shardChance)
		{
			List<List<Cell>> plates = new List<List<Cell>>();
			int n = ring.Count;
			if (n == 0)
				return plates;
			bool Touching(Cell a, Cell b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) == 1;
			int seam = 0;
			for (int i = 0; i < n; i++)
			{
				if (!Touching(ring[i], ring[(i + n - 1) % n]))
				{
					seam = i;
					break;
				}
			}
			List<List<Cell>> runs = new List<List<Cell>>();
			List<Cell> run = new List<Cell> { ring[seam] };
			for (int k = 1; k < n; k++)
			{
				Cell c = ring[(seam + k) % n];
				if (Touching(c, run[run.Count - 1]))
					run.Add(c);
				else
				{
					runs.Add(run);
					run = new List<Cell> { c };
				}
			}
			runs.Add(run);
			foreach (List<Cell> r in runs)
			{
				int i = 0;
				while (i < r.Count)
				{
					int len = Stat.Random(1, 100) <= shardChance ? 1 : Stat.Random(plateMin, plateMax);
					len = Math.Min(len, r.Count - i);
					plates.Add(r.GetRange(i, len));
					i += len;
				}
			}
			return plates;
		}
	}
}
