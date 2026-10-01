using System.Collections.Generic;
using ConsoleLib.Console;
using Kobold;
using UnityEngine;
using XRL;
using XRL.Rules;
using XRL.UI;
using XRL.World;

namespace Cleo.TerraFirma.Scripts
{
	public class Cleo_TerraFirma_PetrifyBandState
	{
		public volatile bool Superseded;

		public volatile bool ShrinkOut;

		public volatile bool Dead;

		public volatile int PackedCell;

		public volatile float TargetFrac;

		public int Gen;
	}

	public class Cleo_TerraFirma_CreatureSnapshotRenderable : IRenderable
	{
		private string TilePath;
		private char Fg;
		private char Detail;
		private char Bg;
		private bool HFlip;
		private bool VFlip;

		public Cleo_TerraFirma_CreatureSnapshotRenderable(string tilePath, char fg, char detail, char bg, bool hFlip, bool vFlip)
		{
			TilePath = tilePath;
			Fg = fg;
			Detail = detail;
			Bg = bg;
			HFlip = hFlip;
			VFlip = vFlip;
		}

		public string getTile() => TilePath;
		public string getRenderString() => "@";
		public string getColorString() => "&" + Fg + "^" + Bg;
		public string getTileColor() => "&" + Fg + "^" + Bg;
		public char getDetailColor() => Detail;
		public ColorChars getColorChars() => new ColorChars { foreground = Fg, background = Bg, detail = Detail };
		public bool getHFlip() => HFlip;
		public bool getVFlip() => VFlip;
	}

	public class Cleo_TerraFirma_JuicePetrifyBand : CombatJuiceEntry
	{
		private IRenderable Tile;
		private Cleo_TerraFirma_PetrifyBandState State;
		private int Gen;
		private ex3DSprite2 Target;
		private float Shown;
		private Vector3 OrigScale;
		private bool ScaleFallback;
		private exTextureInfo BaseInfo;
		private bool SubRectable;
		private bool HFlipped;
		private Color Foreground;
		private Color Background;
		private Color Detail;
		private Color DetailNudge;
		private ex3DSprite2[] Band;
		private Dictionary<int, exTextureInfo>[] BandCache;
		private int[] AppliedBandPx;
		private ex3DSprite2[] Line;
		private Color[] LineFg;
		private Color[] LineDetail;
		private Dictionary<int, exTextureInfo>[] LineCache;
		private int[] AppliedLinePx;
		private Color StoneGray;

		public Cleo_TerraFirma_JuicePetrifyBand(IRenderable tile, Cleo_TerraFirma_PetrifyBandState state, Color mixer, Color creatureDetail)
		{
			Tile = tile;
			State = state;
			Gen = state.Gen;
			duration = Cleo_TerraFirma_GorgonPetrifyFX.LIFE_CAP_SEC;
			StoneGray = ConsoleLib.Console.ColorUtility.ColorMap.GetValue(Cleo_TerraFirma_GorgonPetrifyFX.STONE_DETAIL);
			LineFg = new Color[Cleo_TerraFirma_GorgonPetrifyFX.LINE_ROWS];
			LineDetail = new Color[Cleo_TerraFirma_GorgonPetrifyFX.LINE_ROWS];
			for (int r = 0; r < Cleo_TerraFirma_GorgonPetrifyFX.LINE_ROWS; r++)
			{
				float darken = r * Cleo_TerraFirma_GorgonPetrifyFX.LINE_ROW_DARKEN;
				LineFg[r] = Color.Lerp(mixer, StoneGray, darken);
				LineDetail[r] = Color.Lerp(creatureDetail, StoneGray, darken);
			}
		}

		public override bool canFinishUpToTurn() => false;

		public override void start()
		{
			Vector3 parked = CellCenter() + new Vector3(0f, 0f, Cleo_TerraFirma_GorgonPetrifyFX.Z_PARKED);
			Target = SpriteManager.GetPooledSprite(Tile, Transparent: true);
			Foreground = Target.color;
			Background = Target.backcolor;
			Detail = Target.detailcolor;
			DetailNudge = new Color(Detail.r, Detail.g, Detail.b, 0.996f);
			Transform transform = Target.gameObject.transform;
			transform.SetParent(GameManager.Instance.TileRoot.transform);
			OrigScale = transform.localScale;
			ScaleFallback = false;
			Shown = 0f;
			BaseInfo = Target.textureInfo;
			SubRectable = BaseInfo != null && !BaseInfo.rotated && !BaseInfo.isDiced;
			HFlipped = Tile.getHFlip();
			transform.position = parked;
			Band = null;
			BandCache = null;
			AppliedBandPx = null;
			Line = null;
			LineCache = null;
			AppliedLinePx = null;
			if (SubRectable)
			{
				int cols = Cleo_TerraFirma_GorgonPetrifyFX.SERRATION_COLS;
				int rows = Cleo_TerraFirma_GorgonPetrifyFX.LINE_ROWS;
				Band = new ex3DSprite2[cols];
				BandCache = new Dictionary<int, exTextureInfo>[cols];
				AppliedBandPx = new int[cols];
				Band[0] = Target;
				for (int c = 0; c < cols; c++)
				{
					if (c > 0)
					{
						Band[c] = SpriteManager.GetPooledSprite(Tile, Transparent: true);
						Transform bt = Band[c].gameObject.transform;
						bt.SetParent(GameManager.Instance.TileRoot.transform);
						bt.position = parked;
					}
					BandCache[c] = new Dictionary<int, exTextureInfo>();
				}
				Line = new ex3DSprite2[rows * cols];
				LineCache = new Dictionary<int, exTextureInfo>[rows * cols];
				AppliedLinePx = new int[rows * cols];
				for (int i = 0; i < Line.Length; i++)
				{
					Line[i] = SpriteManager.GetPooledSprite(Tile, Transparent: true);
					LineCache[i] = new Dictionary<int, exTextureInfo>();
					Transform lt = Line[i].gameObject.transform;
					lt.SetParent(GameManager.Instance.TileRoot.transform);
					lt.position = parked;
				}
			}
		}

		public override void update()
		{
			if (Target == null)
				return;
			if (Band != null)
			{
				for (int c = 0; c < Band.Length; c++)
				{
					Band[c].color = Foreground;
					Band[c].backcolor = Background;
					Band[c].detailcolor = DetailNudge;
					Band[c].detailcolor = Detail;
				}
			}
			else
			{
				Target.color = Foreground;
				Target.backcolor = Background;
				Target.detailcolor = DetailNudge;
				Target.detailcolor = Detail;
			}
			if (Line != null)
			{
				int cols = Cleo_TerraFirma_GorgonPetrifyFX.SERRATION_COLS;
				for (int i = 0; i < Line.Length; i++)
				{
					int r = i / cols;
					float breath = Cleo_TerraFirma_GorgonPetrifyFX.LINE_SHIMMER_AMP *
						(0.5f + 0.5f * Mathf.Sin(t * (2f * Mathf.PI / Cleo_TerraFirma_GorgonPetrifyFX.LINE_SHIMMER_SEC) + r * Cleo_TerraFirma_GorgonPetrifyFX.LINE_SHIMMER_PHASE));
					Color fg = Color.Lerp(LineFg[r], StoneGray, breath);
					Color dt = Color.Lerp(LineDetail[r], StoneGray, breath);
					Line[i].color = fg;
					Line[i].backcolor = Background;
					Line[i].detailcolor = new Color(dt.r, dt.g, dt.b, 0.996f);
					Line[i].detailcolor = dt;
				}
			}
			if (State.Superseded || Gen != Cleo_TerraFirma_GorgonPetrifyFX.GlobalGen)
			{
				Die();
				return;
			}
			float goal = State.ShrinkOut ? 0f : State.TargetFrac;
			float rate = State.ShrinkOut ? Cleo_TerraFirma_GorgonPetrifyFX.SHRINK_OUT_PER_SEC : Cleo_TerraFirma_GorgonPetrifyFX.GROW_PER_SEC;
			Shown = Mathf.MoveTowards(Shown, goal, rate * Time.deltaTime);
			if (State.ShrinkOut && Shown <= 0.001f)
			{
				Die();
				return;
			}
			Vector3 center = CellCenter();
			int fullH = BaseInfo != null ? BaseInfo.height : 24;
			int px = Mathf.Clamp(Mathf.RoundToInt(Shown * fullH), 0, fullH);
			if (px <= 0)
			{
				ParkAll(center);
				return;
			}
			if (SubRectable)
			{
				int cols = Band.Length;
				float colWFrac = 1f / cols;
				float colW = BaseInfo.width / (float)cols;
				float halfW = BaseInfo.width * 0.5f;
				float halfH = fullH * 0.5f;
				for (int c = 0; c < cols; c++)
				{
					int colPx = Mathf.Max(1, px - SerrationDepth(c, px));
					float baseX = -halfW + (c + 0.5f) * colW;
					float worldX = HFlipped ? -baseX : baseX;
					if (colPx != AppliedBandPx[c])
					{
						if (!BandCache[c].TryGetValue(colPx, out exTextureInfo sub))
						{
							sub = Cleo_TerraFirma_StoneRiseFX.CloneSubRect(BaseInfo, c * colWFrac, 0f, colWFrac, colPx / (float)fullH);
							BandCache[c][colPx] = sub;
						}
						Band[c].textureInfo = sub;
						AppliedBandPx[c] = colPx;
					}
					Band[c].transform.position = center + new Vector3(worldX, -halfH + colPx * 0.5f, Cleo_TerraFirma_GorgonPetrifyFX.Z_FRONT);
					if (Line != null)
					{
						for (int r = 0; r < Cleo_TerraFirma_GorgonPetrifyFX.LINE_ROWS; r++)
						{
							int i = r * cols + c;
							int rowTop = colPx - r;
							int rowBottom = rowTop - 1;
							if (rowBottom < 0)
							{
								Line[i].transform.position = center + new Vector3(0f, 0f, Cleo_TerraFirma_GorgonPetrifyFX.Z_PARKED);
								continue;
							}
							if (rowTop != AppliedLinePx[i])
							{
								if (!LineCache[i].TryGetValue(rowTop, out exTextureInfo slice))
								{
									slice = Cleo_TerraFirma_StoneRiseFX.CloneSubRect(BaseInfo, c * colWFrac, rowBottom / (float)fullH, colWFrac, 1f / fullH);
									LineCache[i][rowTop] = slice;
								}
								Line[i].textureInfo = slice;
								AppliedLinePx[i] = rowTop;
							}
							Line[i].transform.position = center + new Vector3(worldX, -halfH + rowBottom + 0.5f, Cleo_TerraFirma_GorgonPetrifyFX.Z_LINE);
						}
					}
				}
			}
			else
			{
				Target.gameObject.transform.localScale = new Vector3(OrigScale.x, OrigScale.y * Shown, OrigScale.z);
				ScaleFallback = true;
				Target.transform.position = center + new Vector3(0f, -Cleo_TerraFirma_GorgonPetrifyFX.HALF_TILE * (1f - Shown), Cleo_TerraFirma_GorgonPetrifyFX.Z_FRONT);
			}
		}

		private static int SerrationDepth(int col, int px)
		{
			unchecked
			{
				uint h = (uint)(col * 374761393 + px * 668265263);
				h ^= h >> 13;
				h *= 1274126177u;
				h ^= h >> 16;
				return (int)(h % (uint)(Cleo_TerraFirma_GorgonPetrifyFX.SERRATION_DEPTH_PX + 1));
			}
		}

		private Vector3 CellCenter()
		{
			int packed = State.PackedCell;
			return gameManager.getTileCenter(packed % 1000, packed / 1000);
		}

		private void ParkAll(Vector3 center)
		{
			Vector3 parked = center + new Vector3(0f, 0f, Cleo_TerraFirma_GorgonPetrifyFX.Z_PARKED);
			if (Band != null)
			{
				for (int c = 0; c < Band.Length; c++)
					Band[c].transform.position = parked;
			}
			else
				Target.transform.position = parked;
			if (Line != null)
			{
				for (int i = 0; i < Line.Length; i++)
					Line[i].transform.position = parked;
			}
		}

		private void Die()
		{
			ParkAll(CellCenter());
			duration = 0f;
		}

		public override void finish()
		{
			if (Band != null)
			{
				for (int c = 0; c < Band.Length; c++)
				{
					if (Band[c] == null)
						continue;
					if (BaseInfo != null && AppliedBandPx[c] > 0)
						Band[c].textureInfo = BaseInfo;
					SpriteManager.Return(Band[c]);
					Band[c] = null;
				}
				Band = null;
				Target = null;
			}
			else if (Target != null)
			{
				if (ScaleFallback)
					Target.gameObject.transform.localScale = OrigScale;
				SpriteManager.Return(Target);
				Target = null;
			}
			if (Line != null)
			{
				for (int i = 0; i < Line.Length; i++)
				{
					if (Line[i] == null)
						continue;
					if (BaseInfo != null && AppliedLinePx[i] > 0)
						Line[i].textureInfo = BaseInfo;
					SpriteManager.Return(Line[i]);
					Line[i] = null;
				}
				Line = null;
			}
			if (BandCache != null)
			{
				for (int c = 0; c < BandCache.Length; c++)
				{
					if (BandCache[c] == null)
						continue;
					foreach (exTextureInfo sub in BandCache[c].Values)
					{
						if (sub != null)
							Object.Destroy(sub);
					}
				}
				BandCache = null;
			}
			if (LineCache != null)
			{
				for (int i = 0; i < LineCache.Length; i++)
				{
					if (LineCache[i] == null)
						continue;
					foreach (exTextureInfo slice in LineCache[i].Values)
					{
						if (slice != null)
							Object.Destroy(slice);
					}
				}
				LineCache = null;
			}
			State.Dead = true;
			base.finish();
		}
	}

	public class Cleo_TerraFirma_JuicePetrifyChunk : CombatJuiceEntry
	{
		private IRenderable Tile;
		private ex3DSprite2 Target;
		private Vector3 Origin;
		private float Vx;
		private float Vy0;
		private float Air;
		private float Scale;
		private Vector3 OrigScale;
		private Color Foreground;
		private Color Background;
		private Color Detail;
		private Color DetailNudge;
		private const float GRAVITY = 1000f;

		public Cleo_TerraFirma_JuicePetrifyChunk(IRenderable tile, Vector3 origin, float vx, float vy0, float air, float scale)
		{
			Tile = tile;
			Origin = origin;
			Vx = vx;
			Vy0 = vy0;
			Air = Mathf.Max(0.01f, air);
			Scale = scale;
			duration = this.Air + 0.02f;
		}

		public override bool canFinishUpToTurn() => false;

		public override void start()
		{
			Target = SpriteManager.GetPooledSprite(Tile, Transparent: true);
			Foreground = Target.color;
			Background = Target.backcolor;
			Detail = Target.detailcolor;
			DetailNudge = new Color(Detail.r, Detail.g, Detail.b, 0.996f);
			Transform transform = Target.gameObject.transform;
			transform.SetParent(GameManager.Instance.TileRoot.transform);
			OrigScale = transform.localScale;
			transform.localScale = OrigScale * Scale;
			transform.position = Origin + new Vector3(0f, 0f, Cleo_TerraFirma_GorgonPetrifyFX.Z_CHUNK);
		}

		public override void update()
		{
			if (Target == null)
				return;
			Target.color = Foreground;
			Target.backcolor = Background;
			Target.detailcolor = DetailNudge;
			Target.detailcolor = Detail;
			if (t >= Air)
			{
				Target.transform.position = Origin + new Vector3(0f, 0f, Cleo_TerraFirma_GorgonPetrifyFX.Z_PARKED);
				return;
			}
			float y = Vy0 * t - 0.5f * GRAVITY * t * t;
			Target.transform.position = Origin + new Vector3(Vx * t, y, Cleo_TerraFirma_GorgonPetrifyFX.Z_CHUNK);
		}

		public override void finish()
		{
			if (Target != null)
			{
				Target.gameObject.transform.localScale = OrigScale;
				SpriteManager.Return(Target);
				Target = null;
			}
			base.finish();
		}
	}

	[HasCallAfterGameLoaded]
	public static class Cleo_TerraFirma_GorgonPetrifyFX
	{
		public static bool HarnessMute;

		public static volatile int GlobalGen;

		public const char STONE_FG = 'y';
		public const char STONE_DETAIL = 'K';
		public const bool BAND_POROUS = true;
		private const float BAND_MIN_FRAC = 0.12f;
		private const float BAND_MAX_FRAC = 0.92f;
		public const float GROW_PER_SEC = 0.45f;
		public const float SHRINK_OUT_PER_SEC = 1.6f;
		public const float LIFE_CAP_SEC = 3600f;
		public const float Z_FRONT = -10f;
		public const float Z_CHUNK = -11f;
		public const float Z_PARKED = 100f;
		public const float HALF_TILE = 12f;
		private const string CHUNK_TILE_SMALL = "Items/sw_smallstone.bmp";
		private const string CHUNK_TILE_LARGE = "Items/sw_stone_large.bmp";
		private const int CHUNK_LARGE_PCT = 30;
		private const float CHUNK_SCALE_MIN = 0.20f;
		private const float CHUNK_SCALE_MAX = 0.35f;
		public const int SERRATION_COLS = 4;
		public const int SERRATION_DEPTH_PX = 2;
		public const int LINE_ROWS = 2;
		public const float LINE_MIX = 0.5f;
		public const float LINE_ROW_DARKEN = 0.4f;
		public const float LINE_SHIMMER_SEC = 1.4f;
		public const float LINE_SHIMMER_AMP = 0.35f;
		public const float LINE_SHIMMER_PHASE = 0.9f;
		public const float Z_LINE = -10.5f;

		[CallAfterGameLoaded]
		public static void AfterGameLoaded()
		{
			GlobalGen++;
		}

		public static float FracForNet(int net)
		{
			if (net <= 0)
				return 0f;
			return BAND_MIN_FRAC + (BAND_MAX_FRAC - BAND_MIN_FRAC) * Mathf.Clamp01((net - 1) / 8f);
		}

		public static Cleo_TerraFirma_PetrifyBandState Show(Cleo_TerraFirma_PetrifyBandState state, XRL.World.GameObject target, float frac)
		{
			Cell cell = target?.CurrentCell;
			if (HarnessMute || cell == null || GameManager.Instance == null || !Options.UseTiles || !cell.IsVisible())
			{
				Release(state, ShrinkOut: false);
				return null;
			}
			if (state != null && !state.Dead && !state.Superseded && !state.ShrinkOut)
			{
				state.PackedCell = cell.X + cell.Y * 1000;
				state.TargetFrac = frac;
				return state;
			}
			Release(state, ShrinkOut: false);
			RenderEvent re = target.RenderForUI();
			if (re == null || re.Tile.IsNullOrEmpty())
				return null;
			Cleo_TerraFirma_PetrifyBandState fresh = new Cleo_TerraFirma_PetrifyBandState
			{
				PackedCell = cell.X + cell.Y * 1000,
				TargetFrac = frac,
				Gen = GlobalGen,
			};
			WaterlineColors(re, out Color mixer, out Color creatureDetail);
			CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuicePetrifyBand(
				new Cleo_TerraFirma_CreatureSnapshotRenderable(re.Tile, STONE_FG, BAND_POROUS ? 'k' : STONE_DETAIL, 'k', re.HFlip, re.VFlip),
				fresh, mixer, creatureDetail), async: true);
			return fresh;
		}

		private static void WaterlineColors(RenderEvent re, out Color mixer, out Color creatureDetail)
		{
			char fg = 'y';
			string cs = re.ColorString;
			if (!cs.IsNullOrEmpty())
			{
				int amp = cs.IndexOf('&');
				if (amp >= 0 && amp + 1 < cs.Length)
					fg = cs[amp + 1];
			}
			char dt = re.DetailColor.IsNullOrEmpty() ? fg : re.DetailColor[0];
			Color main = ConsoleLib.Console.ColorUtility.ColorMap.GetValue(fg);
			creatureDetail = ConsoleLib.Console.ColorUtility.ColorMap.GetValue(dt);
			mixer = Color.Lerp(main, creatureDetail, LINE_MIX);
		}

		public static void RockPops(XRL.World.GameObject target, int count, bool Burst = false)
		{
			Cell cell = target?.CurrentCell;
			if (HarnessMute || cell == null || GameManager.Instance == null || !Options.UseTiles || !cell.IsVisible())
				return;
			Vector3 center = GameManager.Instance.getTileCenter(cell.X, cell.Y);
			for (int i = 0; i < count; i++)
			{
				string tile = Stat.Random(1, 100) <= CHUNK_LARGE_PCT ? CHUNK_TILE_LARGE : CHUNK_TILE_SMALL;
				Cleo_TerraFirma_CreatureSnapshotRenderable look = new Cleo_TerraFirma_CreatureSnapshotRenderable(
					tile, STONE_FG, STONE_DETAIL, 'k', Stat.Random(0, 1) == 1, Stat.Random(0, 1) == 1);
				Vector3 origin = center + new Vector3(Stat.Random(-5, 5), Stat.Random(-8, 2), 0f);
				float vx = Stat.Random(12, Burst ? 42 : 28) * (Stat.Random(0, 1) == 1 ? 1f : -1f);
				float vy0 = Stat.Random(Burst ? 90 : 60, Burst ? 150 : 110);
				float air = 0.01f * Stat.Random(Burst ? 40 : 32, Burst ? 55 : 45);
				float scale = CHUNK_SCALE_MIN + 0.01f * Stat.Random(0, (int)((CHUNK_SCALE_MAX - CHUNK_SCALE_MIN) * 100f));
				CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuicePetrifyChunk(look, origin, vx, vy0, air, scale), async: true);
			}
		}

		public static void Release(Cleo_TerraFirma_PetrifyBandState state, bool ShrinkOut)
		{
			if (state == null || state.Dead)
				return;
			if (ShrinkOut)
				state.ShrinkOut = true;
			else
				state.Superseded = true;
		}
	}
}
