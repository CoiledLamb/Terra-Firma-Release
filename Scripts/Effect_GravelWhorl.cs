using System;
using System.Collections.Generic;
using Cleo.TerraFirma.Scripts;
using XRL.Rules;
using XRL.UI;
using XRL.World.Parts.Mutation;

namespace XRL.World.Effects
{
	[Serializable]
	public class Cleo_TerraFirma_GravelWhorl : Effect
	{
		private const string DEBUG_CONTEXT = "GVW";
		private const int RICOCHET_RANGE = 5;

		public int DeflectChance;

		public bool Resonant;

		public static bool DebugForceDeflect;
		public static int DebugForcedOutcome = 1;

		public Cleo_TerraFirma_GravelWhorl()
		{
			Duration = 9999;
			DisplayName = "{{y|gravel whorl}}";
		}

		public Cleo_TerraFirma_GravelWhorl(int deflectChance) : this()
		{
			DeflectChance = deflectChance;
		}

		public override string GetDetails()
		{
			if (Resonant)
				return "Sheathed in keening gravel. Physical projectiles pass through. Lasts while stones remain in reserve.";
			return "Sheathed in whorling gravel. Lasers and other energy attacks pass through. Lasts while stones remain in reserve.";
		}

		public override bool UseStandardDurationCountdown() => false;

		public void Sync(int NewDeflectChance, int Charges, bool NewResonant)
		{
			if (NewDeflectChance != DeflectChance || NewResonant != Resonant)
				Helpers.VerifyLog("GVW", $"sync: chance={NewDeflectChance}% charges={Charges} resonant={NewResonant}");
			DeflectChance = NewDeflectChance;
			Resonant = NewResonant;
			StatShifter.RemoveStatShifts();
		}

		public override bool Apply(GameObject Object)
		{
			Object.RegisterEffectEvent(this, "RefractLight");
			return true;
		}

		public override void Remove(GameObject Object)
		{
			Object.UnregisterEffectEvent(this, "RefractLight");
			StatShifter.RemoveStatShifts();
			base.Remove(Object);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == "RefractLight" && Resonant
				&& E.GetGameObjectParameter("Projectile") == null
				&& (DebugForceDeflect || DeflectChance.in100()))
			{
				Helpers.VerifyLog("GVW", $"BEAM SCATTER: resonant whorl refracted a Lase-type beam (chance={DeflectChance}%)");
				E.SetParameter("By", Object);
				E.SetParameter("Verb", "scatter");
				Cleo_TerraFirma_GravelWhorlFX.PlayRefractTrack(Object.CurrentCell, E.GetIntParameter("Direction"));
				return false;
			}
			return base.FireEvent(E);
		}

		public static bool IsPhysicalProjectile(GameObject Projectile)
		{
			string attrs = Projectile?.GetPart<XRL.World.Parts.Projectile>()?.Attributes;
			if (attrs.IsNullOrEmpty())
				return true;
			return !(attrs.Contains("Light") || attrs.Contains("Laser") || attrs.Contains("Plasma") || attrs.Contains("Electric"));
		}

		public override bool WantEvent(int ID, int Cascade)
		{
			if (!base.WantEvent(ID, Cascade))
				return ID == PooledEvent<DefenderMissileHitEvent>.ID;
			return true;
		}

		public static bool IsLightProjectile(GameObject Projectile)
		{
			string attrs = Projectile?.GetPart<XRL.World.Parts.Projectile>()?.Attributes;
			return !attrs.IsNullOrEmpty() && attrs.Contains("Light");
		}

		public override bool HandleEvent(DefenderMissileHitEvent E)
		{
			if (E.Done || !GameObject.Validate(E.Projectile))
			{
				return base.HandleEvent(E);
			}
			bool deflectable = Resonant ? IsLightProjectile(E.Projectile) : IsPhysicalProjectile(E.Projectile);
			bool procs = deflectable && (DebugForceDeflect || DeflectChance.in100());
			Helpers.VerifyLog("GVW", $"incoming {E.Projectile?.Blueprint ?? "projectile"} (resonant={Resonant}): {(deflectable ? (procs ? "deflect PROC" : $"deflectable, no proc ({DeflectChance}%)") : "wrong type, passes through")}");
			if (procs)
			{
				int outcome = DebugForceDeflect && DebugForcedOutcome > 0
					? (DebugForcedOutcome == 1 ? 1 : DebugForcedOutcome == 2 ? 51 : 81)
					: Stat.Random(1, 100);
				Helpers.VerifyLog("GVW", $"outcome roll {outcome} -> {(outcome <= 50 ? "DEVOUR" : outcome <= 80 ? "SWAT" : "RICOCHET")}");
				if (outcome <= 50)
					Devour(E.Projectile, E.Attacker);
				else if (outcome <= 80)
					Swat(E.Projectile);
				else
					Ricochet(E.Projectile);
				PlayWorldSound("Sounds/Throw/sfx_throwing_stone_small_impact", 1f, Combat: true);
				E.Done = true;
				return false;
			}
			return base.HandleEvent(E);
		}

		private void Devour(GameObject projectile, GameObject attacker)
		{
			string whorlName = Object.Poss(DisplayName.Strip());
			if (projectile == null)
			{
				XDidY(whorlName, Resonant ? "snuffs" : "devours", "the projectile", EndMark: "!", Source: Object);
				Cleo_TerraFirma_GravelWhorlFX.PlayDevourSwirl(Object, null, attacker);
				return;
			}
			XRL.World.Parts.IGrenade grenade = projectile.GetFirstPartDescendedFrom<XRL.World.Parts.IGrenade>();
			string grindSpawn = projectile.GetTag("Cleo_TerraFirma_GrindSpawn");
			if (grenade != null || !string.IsNullOrEmpty(grindSpawn))
			{
				Helpers.VerifyLog("GVW", $"devour: DANGEROUS projectile {projectile.Blueprint} torn apart (grenade={grenade != null}, grindSpawn={grindSpawn ?? "none"})");
				XDidYToZ(whorlName, "tears", projectile, "apart", EndMark: "!", Source: Object);
				if (!string.IsNullOrEmpty(grindSpawn))
				{
					int spawnCount = 1;
					int.TryParse(projectile.GetTag("Cleo_TerraFirma_GrindSpawnCount", "1"), out spawnCount);
					SpawnAround(grindSpawn, Math.Max(1, spawnCount));
				}
				grenade?.Detonate(Object.CurrentCell, attacker ?? Object, Object, Indirect: true);
				if (GameObject.Validate(projectile))
					projectile.Obliterate();
				return;
			}
			if (projectile.HasTag("Cleo_TerraFirma_IsAnyStone"))
			{
				XDidYToZ(whorlName, "devours", projectile, EndMark: "!", Source: Object);
				Cleo_TerraFirma_GravelWhorlFX.PlayDevourSwirl(Object, projectile, attacker);
				Cleo_TerraFirma_EarthenBarrage eb = Object.GetPart<Cleo_TerraFirma_EarthenBarrage>();
				if (eb != null && eb.Charges < Cleo_TerraFirma_EarthenBarrage.MaxCharges(eb.Level))
				{
					eb.Charges++;
					eb.SyncWhorl();
					Helpers.VerifyLog("GVW", $"devour: plain stone {projectile.Blueprint} fed the reserve -> charges now {eb.Charges}");
					if (Object.IsPlayer())
						AddPlayerMessage("The whorl thickens.");
				}
				else
					Helpers.VerifyLog("GVW", $"devour: plain stone {projectile.Blueprint}, NO charge gained (reserve full or no EB part)");
				projectile.Obliterate();
				return;
			}
			Helpers.VerifyLog("GVW", $"devour: non-stone {projectile.Blueprint} OBLITERATED (RULED KEPT)");
			XDidYToZ(whorlName, Resonant ? "snuffs" : "devours", projectile, EndMark: "!", Source: Object);
			Cleo_TerraFirma_GravelWhorlFX.PlayDevourSwirl(Object, projectile, attacker);
			projectile.Obliterate();
		}

		private void SpawnAround(string blueprint, int count)
		{
			Cell cell = Object.CurrentCell;
			if (cell == null)
				return;
			cell.AddObject(GameObject.Create(blueprint));
			List<Cell> open = cell.GetLocalAdjacentCells();
			open.RemoveAll((Cell x) => x.IsSolid());
			for (int i = 1; i < count && open.Count > 0; i++)
			{
				int idx = Stat.Random(0, open.Count - 1);
				open[idx].AddObject(GameObject.Create(blueprint));
				open.RemoveAt(idx);
			}
		}

		private void Swat(GameObject projectile)
		{
			string whorlName = Object.Poss(DisplayName.Strip());
			if (projectile == null)
				XDidY(whorlName, Resonant ? "diffuses" : "deflects", "the projectile", EndMark: "!", Source: Object);
			else if (projectile.HasPart<XRL.World.Parts.Projectile>())
			{
				Helpers.VerifyLog("GVW", $"swat: munition {projectile.Blueprint} fizzled");
				XDidYToZ(whorlName, Resonant ? "diffuses" : "deflects", projectile, EndMark: "!", Source: Object);
				Cleo_TerraFirma_GravelWhorlFX.PlaySwatBeat(Object.CurrentCell, null);
				projectile.Obliterate();
			}
			else
			{
				XDidYToZ(whorlName, "swats", projectile, "aside", EndMark: "!", Source: Object);
				List<Cell> open = Object.CurrentCell?.GetLocalAdjacentCells()?.FindAll(x => !x.IsSolid());
				Cell landing = (open != null && open.Count > 0) ? open[Stat.Random(0, open.Count - 1)] : Object.CurrentCell;
				Helpers.VerifyLog("GVW", $"swat: real item {projectile.Blueprint} batted to ({landing?.X},{landing?.Y}), loot preserved");
				landing?.AddObject(projectile);
				MarkDeflected(projectile, landing);
				Cleo_TerraFirma_GravelWhorlFX.PlaySwatBeat(Object.CurrentCell, landing);
			}
		}

		private void Ricochet(GameObject projectile)
		{
			string whorlName = Object.Poss(DisplayName.Strip());
			Cell origin = Object.CurrentCell;
			Zone zone = origin?.ParentZone;
			if (origin == null || zone == null)
			{
				Swat(projectile);
				return;
			}
			bool ephemeral = projectile == null || projectile.HasPart<XRL.World.Parts.Projectile>();
			string damageDie = ephemeral
				? projectile?.GetPart<XRL.World.Parts.Projectile>()?.BaseDamage
				: (projectile.GetPart<XRL.World.Parts.ThrownWeapon>()?.Damage ?? "1d2");
			if (projectile == null)
			{
				if (Resonant)
					XDidY(whorlName, "refracts", "the projectile", EndMark: "!", Source: Object);
				else
					XDidY(whorlName, "sends", "the projectile careening", EndMark: "!", Source: Object);
			}
			else if (Resonant)
				XDidYToZ(whorlName, "refracts", projectile, EndMark: "!", Source: Object);
			else
				XDidYToZ(whorlName, "sends", projectile, "careening", EndMark: "!", Source: Object);
			double rad = Stat.Random(0, 359) * Math.PI / 180.0;
			int ex = Math.Max(0, Math.Min(zone.Width - 1, origin.X + (int)Math.Round(Math.Cos(rad) * RICOCHET_RANGE)));
			int ey = Math.Max(0, Math.Min(zone.Height - 1, origin.Y + (int)Math.Round(Math.Sin(rad) * RICOCHET_RANGE)));
			Cell landing = origin;
			int traceStep = 0;
			foreach (Point p in Zone.Line(origin.X, origin.Y, ex, ey))
			{
				Cell step = zone.GetCell(p.X, p.Y);
				if (step == null || step == origin)
					continue;
				if (step.IsSolid())
					break;
				landing = step;
				if (!Options.UseParticleVFX && step.InActiveZone)
					The.ParticleManager.Add("&y±", step.X, step.Y, 0f, 0f, 12, 0f, 0f, traceStep * 40L);
				traceStep++;
				GameObject victim = null;
				foreach (GameObject o in step.Objects)
				{
					if (o != Object && o.HasPart("Combat") && o.PhaseMatches(Object))
					{
						victim = o;
						break;
					}
				}
				if (victim != null)
				{
					if (Stat.Random(1, 20) > Stats.GetCombatDV(victim))
					{
						int dmg = damageDie.IsNullOrEmpty() ? 1 : Math.Max(1, damageDie.RollCached());
						Helpers.VerifyLog("GVW", $"ricochet: struck {victim.Blueprint} for {dmg} (die {damageDie ?? "1"})");
						victim.TakeDamage(dmg, "from a careening projectile!", "Physical",
							null, null, Owner: Object, Attacker: null, Source: projectile);
					}
					else
					{
						Helpers.VerifyLog("GVW", $"ricochet: {victim.Blueprint} flinched out of the way");
						IComponent<GameObject>.XDidY(victim, "flinch", "out of the way", "!", null, null, victim);
					}
					break;
				}
			}
			Helpers.VerifyLog("GVW", $"ricochet: {projectile?.Blueprint ?? "munition"} carom ended at ({landing.X},{landing.Y}); {(projectile == null || ephemeral ? "munition spent" : "item lands there")}");
			bool light = projectile != null && IsLightProjectile(projectile);
			if (landing != origin && !light)
				Cleo_TerraFirma_GravelWhorlFX.PlayCaromLanding(landing);
			if (light)
				Cleo_TerraFirma_GravelWhorlFX.PlayBeamRedraw(origin, landing, projectile);
			if (projectile != null)
			{
				if (ephemeral)
					projectile.Obliterate();
				else
				{
					landing.AddObject(projectile);
					MarkDeflected(projectile, landing, ricochet: true);
				}
			}
		}

		private void MarkDeflected(GameObject projectile, Cell landing, bool ricochet = false)
		{
			if (projectile == null || landing == null)
				return;
			XRL.World.Parts.Cleo_TerraFirma_WhorlDeflectedPart mark =
				projectile.RequirePart<XRL.World.Parts.Cleo_TerraFirma_WhorlDeflectedPart>();
			mark.Intended = landing;
			mark.Bounce = Object.CurrentCell;
			mark.Ricochet = ricochet;
		}
	}
}
