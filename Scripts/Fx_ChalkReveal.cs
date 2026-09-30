using ConsoleLib.Console;
using Kobold;
using System.Threading;
using UnityEngine;
using XRL;
using XRL.Core;
using XRL.Rules;
using XRL.UI;
using XRL.World;

namespace Cleo.TerraFirma.Scripts
{
	public static class Cleo_TerraFirma_ChalkRevealFX
	{
		public const float SWAP_SEC = 0.20f;
		private const float FRAG_STAGGER = 0.05f;
		private const float FRAG_LEAVE = 0.10f;
		internal const float FRAG_DRIFT = 3f;
		private const int MOTE_COUNT = 5;
		private const float MOTE_LIFE = 0.30f;
		private const float MOTE_R0 = 2f;
		private const float MOTE_R1 = 11f;
		internal const float MOTE_RISE = 3f;
		private const float MOTE_SCALE_MIN = 0.22f;
		private const float MOTE_SCALE_MAX = 0.30f;
		private const float MOTE_STAGGER = 0.03f;
		private const int QUAD_PUFF_COUNT = 2;
		private const float QUAD_PUFF_R0 = 1f;
		private const float QUAD_PUFF_R1 = 7f;
		private const int SCUFF_COUNT = 4;
		private const int BOARD_SCUFF_COUNT = 6;
		private const float BOARD_FACE_X = 5f;
		private const float BOARD_FACE_Y_MIN = 2f;
		private const float BOARD_FACE_Y_MAX = 8f;
		private const float BOARD_PUFF_R1 = 3f;
		private const string MOTE_TILE = "Items/sw_smallstone.bmp";
		internal const float FADE_OUT_FRAC = 0.25f;
		internal const float MASK_Z = -10f;
		internal const float MOTE_Z = -12f;
		internal const float PARK_Z = 100f;
		private const float COVER_HOLD = 0.10f;
		private static readonly Color CHALK_GREY = new Color(177f / 255f, 201f / 255f, 195f / 255f);

		public static float Play(Cell target)
		{
			return PlayCore(target, inverted: false);
		}

		public static float PlaySmear(Cell target)
		{
			return PlayCore(target, inverted: true);
		}

		public static void PlayScuff(Cell target, int count = SCUFF_COUNT, float r1 = QUAD_PUFF_R1)
		{
			if (target == null)
				return;
			GameManager gm = GameManager.Instance;
			if (gm == null)
				return;
			Zone zone = target.ParentZone;
			if (zone == null || !zone.IsActive() || !target.IsVisible())
				return;
			Vector3 center = gm.getTileCenter(target.X, target.Y);
			IRenderable mote = new Cleo_TerraFirma_StoneTileRenderable(MOTE_TILE, 'Y', 'Y', 'k');
			for (int m = 0; m < count; m++)
			{
				float ang = Stat.RandomCosmetic(0, 359) * (Mathf.PI / 180f);
				float scale = Mathf.Lerp(MOTE_SCALE_MIN, MOTE_SCALE_MAX, Stat.RandomCosmetic(0, 100) * 0.01f);
				CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceChalkMote(mote, center, ang, (m % 2) * 0.02f, MOTE_LIFE, scale, QUAD_PUFF_R0, r1, MoteTint()), async: true);
			}
		}

		public static void PlayBoardScuff(Cell target)
		{
			if (target == null)
				return;
			GameManager gm = GameManager.Instance;
			if (gm == null)
				return;
			Zone zone = target.ParentZone;
			if (zone == null || !zone.IsActive() || !target.IsVisible())
				return;
			Vector3 center = gm.getTileCenter(target.X, target.Y);
			IRenderable mote = new Cleo_TerraFirma_StoneTileRenderable(MOTE_TILE, 'Y', 'Y', 'k');
			for (int m = 0; m < BOARD_SCUFF_COUNT; m++)
			{
				Vector3 spawn = center + new Vector3(
					Stat.RandomCosmetic(-(int)BOARD_FACE_X, (int)BOARD_FACE_X),
					Stat.RandomCosmetic((int)BOARD_FACE_Y_MIN, (int)BOARD_FACE_Y_MAX),
					0f);
				float ang = Stat.RandomCosmetic(0, 359) * (Mathf.PI / 180f);
				float scale = Mathf.Lerp(MOTE_SCALE_MIN, MOTE_SCALE_MAX, Stat.RandomCosmetic(0, 100) * 0.01f);
				CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceChalkMote(mote, spawn, ang, (m % 3) * MOTE_STAGGER, MOTE_LIFE, scale, 0f, BOARD_PUFF_R1, Color.white), async: true);
			}
		}

		private static float PlayCore(Cell target, bool inverted)
		{
			if (target == null)
				return 0f;
			GameManager gm = GameManager.Instance;
			if (gm == null)
				return 0f;
			Zone zone = target.ParentZone;
			if (zone == null || !zone.IsActive() || !target.IsVisible())
				return 0f;
			if (!XRLCore.RenderFloorTextures || target.PaintTile.IsNullOrEmpty())
				return 0f;
			char fg = FirstColorChar(target.PaintTileColor.IsNullOrEmpty() ? target.PaintColorString : target.PaintTileColor, 'y');
			char detail = FirstColorChar(target.PaintDetailColor, 'k');
			IRenderable floor = new Cleo_TerraFirma_StoneTileRenderable(target.PaintTile, fg, detail, 'k');
			Vector3 center = gm.getTileCenter(target.X, target.Y);
			int[] order = { 0, 1, 2, 3 };
			for (int i = 3; i > 0; i--)
			{
				int j = Stat.RandomCosmetic(0, i);
				(order[i], order[j]) = (order[j], order[i]);
			}
			IRenderable mote = new Cleo_TerraFirma_StoneTileRenderable(MOTE_TILE, 'Y', 'Y', 'k');
			float moveBase = inverted ? 0f : SWAP_SEC;
			float settle = inverted ? 3 * FRAG_STAGGER + FRAG_LEAVE : SWAP_SEC;
			for (int q = 0; q < 4; q++)
			{
				float moveAt = moveBase + order[q] * FRAG_STAGGER;
				float holdAfter = inverted ? settle + COVER_HOLD - (moveAt + FRAG_LEAVE) : 0f;
				CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceChalkMaskFrag(floor, center, q, moveAt, FRAG_LEAVE, coverAllOnFallback: q == 0, assembleIn: inverted, holdAfter: holdAfter), async: true);
				float puffAt = inverted ? moveAt + FRAG_LEAVE : moveAt;
				Vector3 quadOffset = new Vector3(q % 2 == 0 ? -4f : 4f, q / 2 == 0 ? 6f : -6f, 0f);
				for (int m = 0; m < QUAD_PUFF_COUNT; m++)
				{
					float ang = Stat.RandomCosmetic(0, 359) * (Mathf.PI / 180f);
					float scale = Mathf.Lerp(MOTE_SCALE_MIN, MOTE_SCALE_MAX, Stat.RandomCosmetic(0, 100) * 0.01f);
					CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceChalkMote(mote, center + quadOffset, ang, puffAt + m * 0.02f, MOTE_LIFE, scale, QUAD_PUFF_R0, QUAD_PUFF_R1, MoteTint()), async: true);
				}
			}
			for (int m = 0; m < MOTE_COUNT; m++)
			{
				float ang = (m + 0.5f) * (Mathf.PI * 2f / MOTE_COUNT) + Stat.RandomCosmetic(-30, 30) * 0.01f;
				float scale = Mathf.Lerp(MOTE_SCALE_MIN, MOTE_SCALE_MAX, Stat.RandomCosmetic(0, 100) * 0.01f);
				CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceChalkMote(mote, center, ang, settle + (m % 3) * MOTE_STAGGER, MOTE_LIFE, scale, MOTE_R0, MOTE_R1, MoteTint()), async: true);
			}
			return settle;
		}

		public static void Hold(float seconds)
		{
			ScreenBuffer buf = ScreenBuffer.GetScrapBuffer1(bLoadFromCurrent: true);
			TextConsole console = Popup._TextConsole;
			float t = 0f;
			while (t < seconds)
			{
				XRLCore.Core.RenderBaseToBuffer(buf);
				console.DrawBuffer(buf);
				Thread.Sleep(40);
				t += 0.04f;
			}
		}

		private static Color MoteTint()
		{
			return Stat.RandomCosmetic(0, 1) == 0 ? Color.white : CHALK_GREY;
		}

		private static char FirstColorChar(string paint, char fallback)
		{
			if (paint.IsNullOrEmpty())
				return fallback;
			for (int i = 0; i < paint.Length; i++)
			{
				char c = paint[i];
				if (c != '&' && c != '^')
					return c;
			}
			return fallback;
		}
	}

	public class Cleo_TerraFirma_JuiceChalkMaskFrag : CombatJuiceEntry
	{
		private IRenderable Tile;
		private ex3DSprite2 Target;
		private Vector3 Center;
		private int Quad;
		private float LeaveAt;
		private float LeaveSec;
		private bool CoverAllOnFallback;
		private bool AssembleIn;
		private float HoldAfter;
		private bool Dead;
		private bool FallbackWhole;
		private Vector3 QuadOffset;
		private Vector3 OrigScale;
		private bool ScaleTouched;
		private exTextureInfo BaseInfo;
		private exTextureInfo SubInfo;
		private Color Foreground;
		private Color Background;
		private Color Detail;
		private Color DetailNudge;

		public Cleo_TerraFirma_JuiceChalkMaskFrag(IRenderable tile, Vector3 center, int quad, float leaveAt, float leaveSec, bool coverAllOnFallback, bool assembleIn = false, float holdAfter = 0f)
		{
			Tile = tile;
			Center = center;
			Quad = quad;
			LeaveAt = leaveAt;
			LeaveSec = Mathf.Max(0.01f, leaveSec);
			CoverAllOnFallback = coverAllOnFallback;
			AssembleIn = assembleIn;
			HoldAfter = Mathf.Max(0f, holdAfter);
			duration = leaveAt + this.LeaveSec + this.HoldAfter + 0.02f;
		}

		public override bool canFinishUpToTurn() => false;

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
			ScaleTouched = false;
			BaseInfo = Target.textureInfo;
			SubInfo = null;
			Dead = false;
			FallbackWhole = false;
			int col = Quad % 2;
			int row = Quad / 2;
			if (BaseInfo != null && !BaseInfo.rotated && !BaseInfo.isDiced)
			{
				SubInfo = Cleo_TerraFirma_StoneRiseFX.CloneSubRect(BaseInfo, col == 0 ? 0f : 0.5f, row == 0 ? 0.5f : 0f, 0.5f, 0.5f);
				Target.textureInfo = SubInfo;
				QuadOffset = new Vector3(col == 0 ? -4f : 4f, row == 0 ? 6f : -6f, 0f);
			}
			else if (CoverAllOnFallback)
			{
				FallbackWhole = true;
				QuadOffset = Vector3.zero;
			}
			else
			{
				Dead = true;
				transform.position = Center + new Vector3(0f, 0f, Cleo_TerraFirma_ChalkRevealFX.PARK_Z);
				return;
			}
			transform.position = AssembleIn
				? Center + new Vector3(0f, 0f, Cleo_TerraFirma_ChalkRevealFX.PARK_Z)
				: Center + QuadOffset + new Vector3(0f, 0f, Cleo_TerraFirma_ChalkRevealFX.MASK_Z);
		}

		public override void update()
		{
			if (Target == null || Dead)
				return;
			Target.color = Foreground;
			Target.backcolor = Background;
			Target.detailcolor = DetailNudge;
			Target.detailcolor = Detail;
			Vector3 assembled = Center + QuadOffset + new Vector3(0f, 0f, Cleo_TerraFirma_ChalkRevealFX.MASK_Z);
			Vector3 parked = Center + new Vector3(0f, 0f, Cleo_TerraFirma_ChalkRevealFX.PARK_Z);
			if (t >= LeaveAt + LeaveSec + HoldAfter)
			{
				Target.transform.position = parked;
				return;
			}
			if (t < LeaveAt)
			{
				Target.transform.position = AssembleIn ? parked : assembled;
				return;
			}
			float p = (t - LeaveAt) / LeaveSec;
			if (p >= 1f)
			{
				Target.transform.position = AssembleIn ? assembled : parked;
				return;
			}
			float ease = 1f - (1f - p) * (1f - p);
			float slide = AssembleIn ? 1f - ease : ease;
			if (FallbackWhole)
			{
				float coverFrac = AssembleIn ? ease : 1f - ease;
				Target.gameObject.transform.localScale = OrigScale * Mathf.Max(0.01f, coverFrac);
				ScaleTouched = true;
				Target.transform.position = Center + new Vector3(0f, 0f, Cleo_TerraFirma_ChalkRevealFX.MASK_Z);
			}
			else
			{
				Vector3 dir = QuadOffset.normalized;
				Target.transform.position = assembled + dir * (Cleo_TerraFirma_ChalkRevealFX.FRAG_DRIFT * slide);
			}
		}

		public override void finish()
		{
			if (Target != null)
			{
				if (ScaleTouched)
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

	public class Cleo_TerraFirma_JuiceChalkMote : CombatJuiceEntry
	{
		private IRenderable Tile;
		private ex3DSprite2 Target;
		private Vector3 Center;
		private float Angle;
		private float StartAt;
		private float Life;
		private float Scale;
		private float R0;
		private float R1;
		private Color Tint;
		private Color TintNudge;
		private Vector3 OrigScale;
		private Color Background;

		public Cleo_TerraFirma_JuiceChalkMote(IRenderable tile, Vector3 center, float angle, float startAt, float life, float scale, float r0, float r1, Color tint)
		{
			Tile = tile;
			Center = center;
			Angle = angle;
			StartAt = startAt;
			Life = Mathf.Max(0.01f, life);
			Scale = scale;
			R0 = r0;
			R1 = r1;
			Tint = tint;
			TintNudge = new Color(tint.r, tint.g, tint.b, 0.996f);
			duration = startAt + this.Life + 0.02f;
		}

		public override bool canFinishUpToTurn() => false;

		public override void start()
		{
			Target = SpriteManager.GetPooledSprite(Tile, Transparent: true);
			Background = Target.backcolor;
			Transform transform = Target.gameObject.transform;
			transform.SetParent(GameManager.Instance.TileRoot.transform);
			OrigScale = transform.localScale;
			transform.position = Center + new Vector3(0f, 0f, Cleo_TerraFirma_ChalkRevealFX.PARK_Z);
		}

		public override void update()
		{
			if (Target == null)
				return;
			float tm = t - StartAt;
			if (tm < 0f || tm >= Life)
			{
				Target.transform.position = Center + new Vector3(0f, 0f, Cleo_TerraFirma_ChalkRevealFX.PARK_Z);
				return;
			}
			Target.color = Tint;
			Target.detailcolor = TintNudge;
			Target.detailcolor = Tint;
			Target.backcolor = Background;
			float p = tm / Life;
			float ease = 1f - (1f - p) * (1f - p);
			float r = Mathf.Lerp(R0, R1, ease);
			float fade = 1f;
			float fadeStart = 1f - Cleo_TerraFirma_ChalkRevealFX.FADE_OUT_FRAC;
			if (p > fadeStart)
				fade = 1f - (p - fadeStart) / Cleo_TerraFirma_ChalkRevealFX.FADE_OUT_FRAC;
			Target.gameObject.transform.localScale = OrigScale * (Scale * Mathf.Max(0.01f, fade));
			Target.transform.position = Center + new Vector3(
				Mathf.Cos(Angle) * r,
				Mathf.Sin(Angle) * r + Cleo_TerraFirma_ChalkRevealFX.MOTE_RISE * ease,
				Cleo_TerraFirma_ChalkRevealFX.MOTE_Z);
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
}
