using System;
using System.Collections.Generic;
using System.Diagnostics;
using XRL.Core;
using XRL.World;

namespace Cleo.TerraFirma.Scripts
{
	public static class Cleo_TerraFirma_GeomancyPulseFX
	{
		public const int RING_MS = 80;

		public const int WARP_PARTICLES = 360;
		public const int WARP_RINGS = 2;
		private static readonly float[] RING_SPEED_FACTORS = { 1f, 0.65f };

		public const bool CLASSIC_FRONT = false;
		private const string CLASSIC_GLYPH = "&Wø";
		private const int CLASSIC_LIFE = 6;

		public const string CENTER_GLYPH = "*";
		public const string CENTER_COLOR = "&Y";

		public static bool GlyphsEnabled = true;

		private static readonly Stopwatch Clock = Stopwatch.StartNew();
		public static long NowMS => Clock.ElapsedMilliseconds;

		public static int Play(Cell center, float radius, List<(Cell cell, float dist)> cells)
		{
			if (!GlyphsEnabled || center == null)
				return 0;
			XRLCore.ParticleManager.Add(CENTER_COLOR + CENTER_GLYPH, center.X, center.Y, 0f, 0f, 8, 0f, 0f, 0L);
			if (radius < 1.5f)
				return 0;
			if (CLASSIC_FRONT)
			{
				if (cells == null || cells.Count == 0)
					return 0;
				cells.Sort((a, b) => a.dist.CompareTo(b.dist));
				foreach ((Cell cell, float dist) in cells)
					XRLCore.ParticleManager.Add(CLASSIC_GLYPH, cell.X, cell.Y, 0f, 0f, CLASSIC_LIFE, 0f, 0f, (long)(dist * RING_MS));
				return cells.Count;
			}
			float baseSpeed = 16f / RING_MS;
			int spawned = 0;
			for (int r = 0; r < WARP_RINGS && r < RING_SPEED_FACTORS.Length; r++)
			{
				float speed = baseSpeed * RING_SPEED_FACTORS[r];
				int life = (int)(radius / speed);
				if (life <= 0)
					continue;
				for (int j = 0; j < WARP_PARTICLES; j++)
				{
					double ang = j * (2.0 * Math.PI / WARP_PARTICLES);
					XRLCore.ParticleManager.Add("@", center.X, center.Y,
						(float)Math.Sin(ang) * speed, (float)Math.Cos(ang) * speed, life, 0f, 0f, 0L);
					spawned++;
				}
			}
			return spawned;
		}
	}
}
