using ConsoleLib.Console;
using Kobold;
using UnityEngine;
using XRL;
using XRL.Core;
using XRL.Rules;
using XRL.UI;
using XRL.World;

namespace Cleo.TerraFirma.Scripts
{
	public static class Cleo_TerraFirma_GravelWhorlFX
	{
		private const int FLECK_COUNT = 16;
		private const float SWIRL_SEC = 0.9f;
		private const float REVOLUTIONS = 1.6f;
		private const float FLECK_SCALE_MIN = 0.22f;
		private const float FLECK_SCALE_MAX = 0.34f;
		private const string FLECK_TILE = "Items/sw_smallstone.bmp";
		private const int REFRACT_TRACK_CELLS = 5;
		private const int EMBER_COUNT = 16;
		private const float EMBER_R0 = 2f;
		private const float EMBER_R1 = 15f;
		private const float EMBER_SPAN_RAD = 2.2f;
		private const float EMBER_STAGGER_SEC = 0.03f;
		private const float EMBER_TRAVEL_SEC = 0.5f;
		private const float EMBER_SCALE_MIN = 0.16f;
		private const float EMBER_SCALE_MAX = 0.26f;

		public static void PlayDevourSwirl(XRL.World.GameObject defender, XRL.World.GameObject projectile, XRL.World.GameObject attacker = null)
		{
			Cell cell = defender?.CurrentCell;
			if (cell == null || GameManager.Instance == null || !Options.UseTiles || !cell.IsVisible())
				return;
			char fg = 'y', dt = 'Y';
			string projTile = null;
			XRL.World.Parts.Render r = projectile?.Render;
			if (r != null)
			{
				string cs = !r.TileColor.IsNullOrEmpty() ? r.TileColor : r.ColorString;
				if (!cs.IsNullOrEmpty())
				{
					int amp = cs.IndexOf('&');
					if (amp >= 0 && amp + 1 < cs.Length)
						fg = cs[amp + 1];
				}
				dt = r.DetailColor.IsNullOrEmpty() ? fg : r.DetailColor[0];
				projTile = r.Tile;
			}
			if (XRL.World.Effects.Cleo_TerraFirma_GravelWhorl.IsLightProjectile(projectile))
			{
				PlayDevourLight(cell, fg, projectile.GetPropertyOrTag("ProjectileVFXConfiguration"));
				return;
			}
			Helpers.VerifyLog("GVW", $"devour swirl: {FLECK_COUNT} flecks in &{fg}/{dt}, tile={(projTile.IsNullOrEmpty() ? "fleck-only" : projTile)}");
			Vector3 center = GameManager.Instance.getTileCenter(cell.X, cell.Y);
			float omega = REVOLUTIONS * 2f * Mathf.PI / SWIRL_SEC;
			for (int i = 0; i < FLECK_COUNT; i++)
			{
				string tile = (!projTile.IsNullOrEmpty() && i % 3 == 0) ? projTile : FLECK_TILE;
				Cleo_TerraFirma_CreatureSnapshotRenderable look = new Cleo_TerraFirma_CreatureSnapshotRenderable(
					tile, fg, dt, 'k', Stat.Random(0, 1) == 1, Stat.Random(0, 1) == 1);
				float lat = Mathf.Asin(2f * (i + 0.5f) / FLECK_COUNT - 1f);
				float phase0 = 0.0174533f * Stat.Random(0, 359);
				float scale = FLECK_SCALE_MIN + 0.01f * Stat.Random(0, (int)((FLECK_SCALE_MAX - FLECK_SCALE_MIN) * 100f));
				float omegaJitter = omega * (0.01f * Stat.Random(90, 110));
				CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceWhorlFleck(look, center, lat, phase0, omegaJitter, scale, SWIRL_SEC), async: true);
			}
		}

		public static void PlayDevourLight(Cell cell, char fg, string projectileVfxConfig = null)
		{
			if (cell == null || GameManager.Instance == null || !Options.UseTiles || !cell.IsVisible())
				return;
			Helpers.VerifyLog("GVW", $"devour ember-catch: light munition's end sparks seized (&{fg})");
			Color? beamA = null, beamB = null;
			if (!projectileVfxConfig.IsNullOrEmpty())
			{
				foreach (string kv in projectileVfxConfig.Split(new string[] { ";;" }, System.StringSplitOptions.RemoveEmptyEntries))
				{
					if (kv.StartsWith("beamColor0::") && UnityEngine.ColorUtility.TryParseHtmlString(kv.Substring("beamColor0::".Length), out Color c0))
						beamA = c0;
					else if (kv.StartsWith("beamColor1::") && UnityEngine.ColorUtility.TryParseHtmlString(kv.Substring("beamColor1::".Length), out Color c1))
						beamB = c1;
				}
			}
			Vector3 center = GameManager.Instance.getTileCenter(cell.X, cell.Y);
			CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceSparkJack(
				center, EMBER_STAGGER_SEC * (EMBER_COUNT - 1) + EMBER_TRAVEL_SEC + 0.1f), async: true);
			for (int i = 0; i < EMBER_COUNT; i++)
			{
				Cleo_TerraFirma_CreatureSnapshotRenderable look = new Cleo_TerraFirma_CreatureSnapshotRenderable(
					FLECK_TILE, fg, 'W', 'k', Stat.Random(0, 1) == 1, Stat.Random(0, 1) == 1);
				float phase0 = 0.0174533f * Stat.Random(0, 359);
				float scale = EMBER_SCALE_MIN + 0.01f * Stat.Random(0, (int)((EMBER_SCALE_MAX - EMBER_SCALE_MIN) * 100f));
				CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceWhorlArc(
					look, center, phase0, 1, EMBER_SPAN_RAD, EMBER_R0, EMBER_R0, EMBER_R1, EMBER_R1,
					i * EMBER_STAGGER_SEC, EMBER_TRAVEL_SEC, scale, beamA, beamB), async: true);
			}
		}

		public static void PlayRicochetFlight(XRL.World.GameObject weapon, Cell bounce, Cell landing, float delaySec)
		{
			if (weapon == null || bounce == null || landing == null || landing == bounce
				|| GameManager.Instance == null || !bounce.InActiveZone)
				return;
			char fg = 'y', dt = 'Y';
			string tile = null;
			XRL.World.Parts.Render r = weapon.Render;
			if (r != null)
			{
				string cs = !r.TileColor.IsNullOrEmpty() ? r.TileColor : r.ColorString;
				if (!cs.IsNullOrEmpty())
				{
					int amp = cs.IndexOf('&');
					if (amp >= 0 && amp + 1 < cs.Length)
						fg = cs[amp + 1];
				}
				dt = r.DetailColor.IsNullOrEmpty() ? fg : r.DetailColor[0];
				tile = r.Tile;
			}
			if (tile.IsNullOrEmpty())
				tile = FLECK_TILE;
			Cleo_TerraFirma_CreatureSnapshotRenderable look = new Cleo_TerraFirma_CreatureSnapshotRenderable(
				tile, fg, dt, 'k', false, false);
			Vector3 b = GameManager.Instance.getTileCenter(bounce.X, bounce.Y);
			Vector3 e = GameManager.Instance.getTileCenter(landing.X, landing.Y);
			Helpers.VerifyLog("GVW", $"rico flight: bounce at ({bounce.X},{bounce.Y}) then thrown leg to ({landing.X},{landing.Y}), delay {delaySec:0.00}s");
			CombatJuiceManager.enqueueEntry(new Cleo_TerraFirma_JuiceStoneBounce(look, b, e, delaySec), async: true);
		}

		public static void PlayBeamRedraw(Cell origin, Cell landing, XRL.World.GameObject projectile)
		{
			if (origin == null || landing == null || landing == origin || projectile == null || !Options.UseParticleVFX)
				return;
			if (CombatJuiceManager.instance == null)
				return;
			string vfx = projectile.GetPropertyOrTag("ProjectileVFX");
			if (vfx.IsNullOrEmpty())
				return;
			MissileWeaponVFXConfiguration cfg = MissileWeaponVFXConfiguration.next();
			MissileWeaponVFXConfiguration.MissileVFXPathDefinition path = cfg.GetPath(0);
			path.addStep(origin.Location);
			path.addStep(landing.Location);
			cfg.setPathProjectileVFX(0, vfx, projectile.GetPropertyOrTag("ProjectileVFXConfiguration"));
			Helpers.VerifyLog("GVW", $"rico beam redraw: {vfx} from ({origin.X},{origin.Y}) to ({landing.X},{landing.Y})");
			CombatJuiceEntryMissileWeaponVFX entry = new CombatJuiceEntryMissileWeaponVFX();
			entry.configure(cfg);
			entry.turn = CombatJuice.juiceTurn;
			entry.async = true;
			lock (CombatJuiceManager.instance.queue)
			{
				CombatJuiceManager.instance.waiting.Add(entry);
			}
		}

		public static void PlaySwatBeat(Cell origin, Cell landing)
		{
			if (landing == null || landing == origin)
			{
				GritBlips(origin, 2);
				return;
			}
			GritBlips(landing, 3);
		}

		public static void PlayCaromLanding(Cell landing)
		{
			GritBlips(landing, 3);
		}

		private static void GritBlips(Cell cell, int count)
		{
			if (cell == null || !cell.InActiveZone)
				return;
			for (int i = 0; i < count; i++)
				XRLCore.ParticleManager.Add("&y±",
					cell.X + 0.1f * Stat.Random(-3, 3), cell.Y + 0.1f * Stat.Random(-2, 2),
					0.015f * Stat.Random(-4, 4), 0.015f * Stat.Random(-3, 2), 12, 0f, 0f, i * 50L);
		}

		public static void PlayRefractTrack(Cell origin, int directionDeg)
		{
			if (origin == null || !origin.InActiveZone)
				return;
			Zone zone = origin.ParentZone;
			float rad = directionDeg * Mathf.PI / 180f;
			float dx = Mathf.Sin(rad), dy = Mathf.Cos(rad);
			XRLCore.ParticleManager.Add("&W*", origin.X, origin.Y, 0f, 0f, 8, 0f, 0f, 0L);
			float x = origin.X, y = origin.Y;
			for (int i = 1; i <= REFRACT_TRACK_CELLS; i++)
			{
				x += dx; y += dy;
				Cell step = zone.GetCell((int)x, (int)y);
				if (step == null || step.IsSolid())
					break;
				if (step.InActiveZone)
					XRLCore.ParticleManager.Add(i % 2 == 0 ? "&Y*" : "&y*", step.X, step.Y, 0f, 0f, 12, 0f, 0f, i * 30L);
			}
		}
	}

	public class Cleo_TerraFirma_JuiceWhorlFleck : CombatJuiceEntry
	{
		private IRenderable Tile;
		private ex3DSprite2 Target;
		private Vector3 Center;
		private float Lat;
		private float Phase0;
		private float Omega;
		private float Scale;
		private float Life;
		private Vector3 OrigScale;
		private Color ColorA;
		private Color ColorB;
		private Color Background;
		private const float SPHERE_RX = 12f;
		private const float SPHERE_RY = 10f;
		private const float TILT_RAD = 0.21f;
		private const float DEPTH_SCALE_MIN = 0.6f;
		private const float SPAWN_FRAC = 0.15f;
		private const float EASE_IN_SEC = 0.22f;
		private const float FADE_OUT_FRAC = 0.2f;
		private const float WOBBLE_PX = 1.5f;
		private const float FLICKER_HZ = 10f;

		public Cleo_TerraFirma_JuiceWhorlFleck(IRenderable tile, Vector3 center, float lat, float phase0, float omega, float scale, float life)
		{
			Tile = tile;
			Center = center;
			Lat = lat;
			Phase0 = phase0;
			Omega = omega;
			Scale = scale;
			Life = life;
			duration = life + 0.02f;
		}

		public override bool canFinishUpToTurn() => false;

		public override void start()
		{
			Target = SpriteManager.GetPooledSprite(Tile, Transparent: true);
			ColorA = Target.color;
			ColorB = Target.detailcolor;
			Background = Target.backcolor;
			Transform transform = Target.gameObject.transform;
			transform.SetParent(GameManager.Instance.TileRoot.transform);
			OrigScale = transform.localScale;
			transform.localScale = OrigScale * Scale;
			transform.position = Center + new Vector3(0f, 0f, Cleo_TerraFirma_GorgonPetrifyFX.Z_CHUNK);
		}

		public override void update()
		{
			if (Target == null)
				return;
			if (t >= Life)
			{
				Target.transform.position = Center + new Vector3(0f, 0f, Cleo_TerraFirma_GorgonPetrifyFX.Z_PARKED);
				return;
			}
			bool flip = ((int)((t + Phase0) * FLICKER_HZ)) % 2 == 0;
			Target.color = flip ? ColorA : ColorB;
			Target.detailcolor = flip ? ColorB : ColorA;
			Target.backcolor = Background;
			float ease = Mathf.Clamp01(t / EASE_IN_SEC);
			ease = 1f - (1f - ease) * (1f - ease);
			float grow = Mathf.Lerp(SPAWN_FRAC, 1f, ease);
			float wobble = WOBBLE_PX * Mathf.Sin(t * 13f + Phase0 * 3f);
			float bandR = grow * SPHERE_RX * Mathf.Cos(Lat) + wobble;
			float bandY = grow * SPHERE_RY * Mathf.Sin(Lat) + wobble * 0.4f;
			float theta = Phase0 + Omega * t;
			float depth = Mathf.Sin(theta);
			float depthScale = Mathf.Lerp(DEPTH_SCALE_MIN, 1f, (depth + 1f) * 0.5f);
			float fade = 1f;
			float fadeStart = Life * (1f - FADE_OUT_FRAC);
			if (t > fadeStart)
				fade = 1f - (t - fadeStart) / (Life - fadeStart);
			Target.transform.localScale = OrigScale * (Scale * depthScale * Mathf.Max(0.01f, fade));
			float x = bandR * Mathf.Cos(theta);
			float tx = x * Mathf.Cos(TILT_RAD) - bandY * Mathf.Sin(TILT_RAD);
			float ty = x * Mathf.Sin(TILT_RAD) + bandY * Mathf.Cos(TILT_RAD);
			Target.transform.position = Center + new Vector3(tx, ty,
				Cleo_TerraFirma_GorgonPetrifyFX.Z_CHUNK - depth);
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

	public class Cleo_TerraFirma_JuiceStoneBounce : CombatJuiceEntry
	{
		private IRenderable Tile;
		private ex3DSprite2 Target;
		private Vector3 Bounce;
		private Vector3 End;
		private float Delay;
		private float Travel;
		private Vector3 OrigScale;
		private Quaternion OrigRotation;
		private float SpinDegPerSec;
		private const float ARC_PX = 10f;
		private const float SPEED = 350f;

		public Cleo_TerraFirma_JuiceStoneBounce(IRenderable tile, Vector3 bounce, Vector3 end, float delaySec)
		{
			Tile = tile;
			Bounce = bounce;
			End = end;
			Delay = Mathf.Max(0f, delaySec);
			Travel = Vector3.Distance(bounce, end) / SPEED;
			SpinDegPerSec = (Stat.Random(0, 1) == 0 ? 1f : -1f) * Stat.Random(240, 480);
			duration = Delay + Travel + 0.02f;
		}

		public override bool canFinishUpToTurn() => false;

		public override void start()
		{
			Target = SpriteManager.GetPooledSprite(Tile, Transparent: true);
			Transform transform = Target.gameObject.transform;
			transform.SetParent(GameManager.Instance.TileRoot.transform);
			OrigScale = transform.localScale;
			OrigRotation = transform.localRotation;
			transform.position = Bounce + new Vector3(0f, 0f, Cleo_TerraFirma_GorgonPetrifyFX.Z_PARKED);
		}

		public override void update()
		{
			if (Target == null)
				return;
			Transform transform = Target.transform;
			if (t < Delay || t >= Delay + Travel)
			{
				transform.position = Bounce + new Vector3(0f, 0f, Cleo_TerraFirma_GorgonPetrifyFX.Z_PARKED);
				return;
			}
			float u = t - Delay;
			float v = Mathf.Clamp01(u / Mathf.Max(0.01f, Travel));
			Vector3 p = Vector3.Lerp(Bounce, End, v);
			p.y += Mathf.Sin(v * Mathf.PI) * ARC_PX;
			p.z = Bounce.z + Cleo_TerraFirma_GorgonPetrifyFX.Z_CHUNK;
			transform.position = p;
			transform.localRotation = Quaternion.Euler(0f, 0f, SpinDegPerSec * u);
		}

		public override void finish()
		{
			if (Target != null)
			{
				Target.gameObject.transform.localScale = OrigScale;
				Target.gameObject.transform.localRotation = OrigRotation;
				SpriteManager.Return(Target);
				Target = null;
			}
			base.finish();
		}
	}

	public class Cleo_TerraFirma_JuiceSparkJack : CombatJuiceEntry
	{
		private Vector3 Center;
		private UnityEngine.GameObject Jacked;
		private const float SCAN_SEC = 0.3f;
		private const float MATCH_SQR_PX = 16f;

		public Cleo_TerraFirma_JuiceSparkJack(Vector3 center, float coverSec)
		{
			Center = center;
			duration = coverSec;
		}

		public override bool canFinishUpToTurn() => false;

		public override void update()
		{
			if (Jacked != null || t >= SCAN_SEC)
				return;
			foreach (laser_renderer r in UnityEngine.Object.FindObjectsOfType<laser_renderer>())
			{
				if (r.impactVfx != null && (r.impactVfx.transform.position - Center).sqrMagnitude < MATCH_SQR_PX)
				{
					Jacked = r.impactVfx;
					Jacked.SetActive(false);
					return;
				}
			}
			foreach (vls_renderer r in UnityEngine.Object.FindObjectsOfType<vls_renderer>())
			{
				if (r.impactVfx != null && (r.impactVfx.transform.position - Center).sqrMagnitude < MATCH_SQR_PX)
				{
					Jacked = r.impactVfx;
					Jacked.SetActive(false);
					return;
				}
			}
		}

		public override void finish()
		{
			if (Jacked != null)
			{
				Jacked.SetActive(true);
				Jacked = null;
			}
			base.finish();
		}
	}

	public class Cleo_TerraFirma_JuiceWhorlArc : CombatJuiceEntry
	{
		private IRenderable Tile;
		private ex3DSprite2 Target;
		private Vector3 Center;
		private float StartAngle;
		private int Side;
		private float SpanRad;
		private float R0x;
		private float R0y;
		private float R1x;
		private float R1y;
		private float Delay;
		private float TravelSec;
		private float Scale;
		private Vector3 OrigScale;
		private Color ColorA;
		private Color ColorB;
		private Color Background;
		private Color? OverrideA;
		private Color? OverrideB;
		private const float SHRINK_TAIL_FRAC = 0.3f;
		private const float FLICKER_HZ = 10f;

		public Cleo_TerraFirma_JuiceWhorlArc(IRenderable tile, Vector3 center, float startAngle, int side, float spanRad, float r0x, float r0y, float r1x, float r1y, float delay, float travelSec, float scale, Color? overrideA = null, Color? overrideB = null)
		{
			Tile = tile;
			Center = center;
			StartAngle = startAngle;
			Side = side;
			SpanRad = spanRad;
			R0x = r0x;
			R0y = r0y;
			R1x = r1x;
			R1y = r1y;
			Delay = delay;
			TravelSec = travelSec;
			Scale = scale;
			OverrideA = overrideA;
			OverrideB = overrideB;
			duration = delay + travelSec + 0.02f;
		}

		public override bool canFinishUpToTurn() => false;

		public override void start()
		{
			Target = SpriteManager.GetPooledSprite(Tile, Transparent: true);
			ColorA = OverrideA ?? Target.color;
			ColorB = OverrideB ?? Target.detailcolor;
			Background = Target.backcolor;
			Transform transform = Target.gameObject.transform;
			transform.SetParent(GameManager.Instance.TileRoot.transform);
			OrigScale = transform.localScale;
			transform.localScale = OrigScale * Scale;
			transform.position = Center + new Vector3(0f, 0f, Cleo_TerraFirma_GorgonPetrifyFX.Z_PARKED);
		}

		public override void update()
		{
			if (Target == null)
				return;
			if (t < Delay || t >= Delay + TravelSec)
			{
				Target.transform.position = Center + new Vector3(0f, 0f, Cleo_TerraFirma_GorgonPetrifyFX.Z_PARKED);
				return;
			}
			bool flip = ((int)((t + StartAngle) * FLICKER_HZ)) % 2 == 0;
			Target.color = flip ? ColorA : ColorB;
			Target.detailcolor = flip ? ColorB : ColorA;
			Target.backcolor = Background;
			float s = (t - Delay) / TravelSec;
			float a = StartAngle + Side * SpanRad * s;
			float rx = Mathf.Lerp(R0x, R1x, s);
			float ry = Mathf.Lerp(R0y, R1y, s);
			float fade = s > 1f - SHRINK_TAIL_FRAC ? (1f - s) / SHRINK_TAIL_FRAC : 1f;
			Target.transform.localScale = OrigScale * (Scale * Mathf.Max(0.01f, fade));
			Target.transform.position = Center + new Vector3(rx * Mathf.Cos(a), -ry * Mathf.Sin(a), Cleo_TerraFirma_GorgonPetrifyFX.Z_CHUNK);
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
