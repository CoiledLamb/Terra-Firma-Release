using Cleo.TerraFirma.Scripts;
using ConsoleLib.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using XRL.Core;
using XRL.Rules;
using XRL.UI;
using XRL.World.Anatomy;
using XRL.World.Effects;
using XRL.World.Parts.Skill;

namespace XRL.World.Parts.Mutation
{
	[Serializable]
	public class Cleo_TerraFirma_Stoneshaper : BaseMutation
	{
		private const string COMMAND_ID = "Cleo_TerraFirma_CommandStoneshaper";
		internal const string DEFAULT_BEHAVIOR_BP = "Cleo_TerraFirma_StoneshaperStatueNaturalWeapon";

		private const int BASE_DURATION = 100;
		private const int TURNS_PER_LEVEL = 40;
		private const int INFINITE_DURATION_PLACEHOLDER = -69420;

		private const int BASE_COOLDOWN = 120;

		[SerializeField]
		private GameObject _curStatue = null;

		public GameObject CurStatue
		{
			private set
			{
				_curStatue = value;
			}
			get
			{
				if (_curStatue != null && (_curStatue.IsInGraveyard() || _curStatue.IsNowhere()))
					_curStatue = null;
				return _curStatue;
			}
		}

		public enum WallType
		{
			Stone = 1,
			StoneAndMetal = 2,
			All = 3
		}

		public int StatueLifetimeRemaining;

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteGameObject(_curStatue);
			base.Write(Basis, Writer);
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			_curStatue = Reader.ReadGameObject();
			base.Read(Basis, Reader);
		}

		public override string GetDescription()
		{
			return "You animate earth to fight for you.";
		}

		public override string GetLevelText(int Level)
		{
			WallType wt = GetValidWallsAtLevel(Level);
			int duration = GetDuration(Level);
			return "You can gaze at a creature, then at a wall, to carve an animate statue of that creature.\n" +
				"The statue inherits the creature's body plan and physique (Strength, Agility, Toughness).\n" +
				"It does not inherit mutations, skills, or equipment.\n" +
				"Nearby enemies must make a Willpower save (difficulty {{rules|" + GetDistractionSaveDifficulty(Level) + "}}) or direct their attacks at the statue.\n" +
				"Can target {{rules|" + (wt == WallType.Stone ? "stone" : (wt == WallType.StoneAndMetal ? "stone and metal" : "almost any")) + " walls}}\n" +
				"Statue hit points: {{rules|" + GetHP(Level) + "}}\n" +
				"Statue AV: {{rules|" + GetAV(Level) + "}}\n" +
				"Statue damage increment: {{rules|" + GetNaturalDamage(Level) + "}}\n" +

				(duration == INFINITE_DURATION_PLACEHOLDER
					? "The statue lasts {{rules|until destroyed}}. Carving another releases it.\n"
					: "The statue lasts for {{rules|" + duration + "}} rounds.\n") +
				"Cooldown: " + BASE_COOLDOWN + " rounds";
		}

		public override void CollectStats(Templates.StatCollector stats)
		{
			int duration = GetDuration(Level);
			if (duration == INFINITE_DURATION_PLACEHOLDER)
				stats.Set("Duration", "until destroyed");
			else
				stats.Set("Duration", duration);
			stats.CollectCooldownTurns(MyActivatedAbility(ActivatedAbilityID), BASE_COOLDOWN);
		}

		public override bool Mutate(GameObject GO, int Level = 1)
		{
			ActivatedAbilityID = AddMyActivatedAbility("Stoneshaper", COMMAND_ID, "Physical Mutations");
			return base.Mutate(GO, Level);
		}

		public override bool Unmutate(GameObject GO)
		{
			RemoveMyActivatedAbility(ref ActivatedAbilityID);
			return base.Unmutate(GO);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) &&
				ID != PooledEvent<CommandEvent>.ID &&
				ID != AIGetOffensiveAbilityListEvent.ID)
				return ID == SingletonEvent<EndTurnEvent>.ID;
			return true;
		}

		private const int NPC_WALL_SCAN_RADIUS = 8;
		private const int PLAYER_WALL_SEED_RADIUS = 20;

		public override bool HandleEvent(AIGetOffensiveAbilityListEvent E)
		{
			if (IsMyActivatedAbilityAIUsable(ActivatedAbilityID) && E.Target != null && E.Target.Body != null && FindCarvableWallCellFor(E.Actor) != null)
				E.Add(COMMAND_ID);
			return base.HandleEvent(E);
		}

		private GameObject FindBestMuseFor(GameObject actor, GameObject fallback)
		{
			GameObject best = fallback;
			int bestLevel = fallback != null && fallback.HasStat("Level") ? fallback.GetStatValue("Level") : 0;
			Cell origin = actor?.CurrentCell;
			if (origin == null)
				return best;
			foreach (Cell c in origin.GetLocalAdjacentCells(NPC_WALL_SCAN_RADIUS))
			{
				foreach (GameObject obj in c.Objects)
				{
					if (!obj.IsCreature || obj.Body == null || obj == actor || obj == fallback || obj == CurStatue)
						continue;
					if (!obj.HasStat("Level") || obj.GetStatValue("Level") <= bestLevel)
						continue;
					if (!actor.HasLOSTo(obj))
						continue;
					best = obj;
					bestLevel = obj.GetStatValue("Level");
				}
			}
			return best;
		}

		private Cell FindCarvableWallCell(Cell origin, int radius, bool RequireVisible = false)
		{
			if (origin == null)
				return null;
			WallType validWalls = GetValidWallsAtLevel(Level);
			Cell best = null;
			int bestDist = int.MaxValue;
			foreach (Cell c in origin.GetLocalAdjacentCells(radius))
			{
				int d = origin.DistanceTo(c);
				if (d >= bestDist || !c.HasWall())
					continue;
				if (RequireVisible && !c.IsVisible())
					continue;
				List<GameObject> walls = c.GetWalls();
				if (walls.Count == 0)
					continue;
				GameObject w = walls.First();
				if (w.HasPart<HologramMaterial>() || w.HasPart<Walltrap>() || w.HasPart<Forcefield>() || w.HasTag("Cleo_TerraFirma_NoStoneshape"))
					continue;
				Cleo_TerraFirma_StoneshaperWallProperties props = w.GetPart<Cleo_TerraFirma_StoneshaperWallProperties>();
				if (props == null)
					continue;
				bool isStone = props.MaterialType == "stone";
				bool isMetal = props.MaterialType == "metal";
				if (validWalls == WallType.Stone && !isStone)
					continue;
				if (validWalls == WallType.StoneAndMetal && !isStone && !isMetal)
					continue;
				best = c;
				bestDist = d;
			}
			return best;
		}

		private Cell FindCarvableWallCellFor(GameObject actor)
			=> FindCarvableWallCell(actor?.CurrentCell, NPC_WALL_SCAN_RADIUS);

		public override bool HandleEvent(CommandEvent E)
		{
			if (E.Command != COMMAND_ID)
				return base.HandleEvent(E);
			if (E.Actor != null && !E.Actor.IsPlayer())
			{
				GameObject muse = E.Target;
				if (!GameObject.Validate(ref muse))
					return false;
				muse = FindBestMuseFor(E.Actor, muse);
				if (muse == null || muse.Body == null)
					return false;
				Cell npcWallCell = FindCarvableWallCellFor(E.Actor);
				if (npcWallCell == null)
					return false;
				return CarveFrom(E.Actor, muse, npcWallCell) != null && base.HandleEvent(E);
			}
			if (CurStatue != null)
			{
				if (E.Actor.IsPlayer() && Popup.ShowYesNo("Carving a new statue will return " + CurStatue.t() +
					" to lifeless stone. Anything worn or carried will be entombed within, recoverable by hand. Proceed?") == DialogResult.No)
					return false;
			}
		targetSelection:
			Cell targetCreatureCell = PickDestinationCell(Snap: true, Label: "Choose a cell with a creature to copy.");
			if (targetCreatureCell == null)
				return false;
			GameObject creature = targetCreatureCell.GetCombatTarget(E.Actor, true);
			if (creature == null || !creature.IsCreature)
			{
				E.Actor.Fail("You must target a cell containing a creature.");
				goto targetSelection;
			}
			if (creature.Body == null)
			{
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHAPE", $"no-body fail: {creature.Blueprint} refused cleanly (no cooldown, no wall spent), re-prompting");
				E.Actor.Fail(creature.T() + creature.GetVerb("have") + " no body plan for you to carve.");
				goto targetSelection;
			}

			Cell targetWallCell = targetCreatureCell;
			WallType validWalls = GetValidWallsAtLevel(Level);

		wallSelection:
			Cell wallSeed = FindCarvableWallCell(targetCreatureCell, PLAYER_WALL_SEED_RADIUS, RequireVisible: true) ?? E.Actor.CurrentCell;
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHAPE", $"wall picker seeded at ({wallSeed.X},{wallSeed.Y})" + (wallSeed == E.Actor.CurrentCell ? " (no eligible wall in range, caster fallback)" : ""));
			targetWallCell = PickTarget.ShowPicker(PickTarget.PickStyle.EmptyCell, 1, 9999, wallSeed.X, wallSeed.Y, Label: "Choose a wall to carve the statue from.", UseTarget: false);
			if (targetWallCell == null)
			{
				return false;
			}
			else if (!targetWallCell.HasWall())
			{
				E.Actor.Fail("You must select a tile containing a solid wall.");
				goto wallSelection;
			}

			List<GameObject> walls = targetWallCell.GetWalls();
			GameObject refWall = walls.First();
			if (refWall.HasPart<HologramMaterial>() || refWall.HasPart<Walltrap>() || refWall.HasPart<Forcefield>())
			{
				E.Actor.Fail("You must select a tile containing a solid wall.");
				goto wallSelection;
			}
			if (refWall.HasTag("Cleo_TerraFirma_NoStoneshape"))
			{
				E.Actor.Fail($"No matter how hard you try, {refWall.DisplayName} refuses to be sculpted.");
				goto wallSelection;
			}
			Cleo_TerraFirma_StoneshaperWallProperties wallProps = refWall.GetPart<Cleo_TerraFirma_StoneshaperWallProperties>();
			if (wallProps == null)
			{
				E.Actor.Fail($"No matter how hard you try, {refWall.DisplayName} refuses to be sculpted.");
				goto wallSelection;
			}
			bool isStoneWall = wallProps.MaterialType == "stone";
			bool isMetalWall = wallProps.MaterialType == "metal";

			if (validWalls != WallType.All)
			{
				if (validWalls == WallType.Stone && !isStoneWall)
				{
					E.Actor.Fail("You must target a wall made of stone.");
					goto wallSelection;
				}
				else if (validWalls == WallType.StoneAndMetal && !isStoneWall && !isMetalWall)
				{
					E.Actor.Fail("You must target a wall made of stone or metal.");
					goto wallSelection;
				}
			}

			return CarveFrom(E.Actor, creature, targetWallCell) != null && base.HandleEvent(E);
		}

		internal GameObject CarveFrom(GameObject actor, GameObject creature, Cell targetWallCell)
		{
			if (creature == null || creature.Body == null)
			{
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHAPE", $"no-body fail: {creature?.Blueprint ?? "?"} refused cleanly (no cooldown, no wall spent)");
				if (creature != null)
					actor.Fail(creature.T() + creature.GetVerb("have") + " no body plan for you to carve.");
				return null;
			}
			List<GameObject> walls = targetWallCell.GetWalls();
			if (walls.Count == 0)
				return null;
			GameObject refWall = walls.First();
			Cleo_TerraFirma_StoneshaperWallProperties wallProps = refWall.GetPart<Cleo_TerraFirma_StoneshaperWallProperties>();
			if (wallProps == null)
				return null;

			Lovesick lovesick = actor.GetEffect<Lovesick>();
			GameObject target = creature;
			if (lovesick != null)
			{
				creature = lovesick.Beauty;
				if (creature == null || creature.Body == null)
					creature = target;
			}
			else if (target.HasPart<Lovely>())
				actor.ApplyEffect(new Lovesick(Stat.Random(3000, 3600), target));

			string descriptor = "rough";
			if (Level >= 3)
				descriptor = "fine";
			if (Level >= 6)
				descriptor = "dignified";
			if (Level >= 9)
				descriptor = "exquisite";
			if (Level >= 12)
				descriptor = "masterwork";
			if (Level >= 15)
				descriptor = "immaculate";

			GameObject statue = MakeStatueOfCreatureFromWall(creature, refWall, descriptor, wallProps);
			if (statue == null)
			{
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHAPE", $"statue creation FAILED (null) for muse {creature?.Blueprint ?? "?"}");
				actor.Fail("The stone refuses to take shape.");
				return null;
			}
			AnimateObject.Animate(statue);

			ApplyStatueBuffs(actor, creature, statue, Level, wallProps);

			if (CurStatue != null)
			{
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHAPE", "SWAP: prior statue de-animated (gear entombed) before the new carve");
				XDidY(CurStatue, "return", "wearily to earth", Source: CurStatue, ColorAsBadFor: ParentObject);
				CurStatue.SetStringProperty("NoFollowerDeathPopup", "1");
				DeanimateStatue(CurStatue);
			}
			CurStatue = statue;
			StatueLifetimeRemaining = GetDuration(Level);
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHAPE", $"carved: muse={creature.Blueprint}, wall={refWall?.Blueprint ?? "?"}, Lv{Level}, lifetime={(StatueLifetimeRemaining == INFINITE_DURATION_PLACEHOLDER ? "PERMANENT" : StatueLifetimeRemaining.ToString() + " turns")}");

			void PlaceStatue()
			{
				walls.ForEach(x => x.Obliterate());
				targetWallCell.AddObject(statue, true, Silent: true);
			}
			PlayCarveReveal(targetWallCell, refWall, statue, PlaceStatue);

			if (lovesick != null && actor.IsPlayer())
				Popup.Show($"You attempt to picture {target.the} {target.DisplayName} in your mind, but all you can think about is {lovesick.Beauty.the} {lovesick.Beauty.DisplayName}…!");
			XDidYToZ(actor, $"create a sculpture of", creature, EndMark: "!", ColorAsGoodFor: actor, Source: actor);

			actor.UseEnergy(1000, "Physical Mutation Stoneshaper");
			CooldownMyActivatedAbility(ActivatedAbilityID, BASE_COOLDOWN);
			return statue;
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			if (StatueLifetimeRemaining == INFINITE_DURATION_PLACEHOLDER)
			{
				if (CurStatue == null)
					StatueLifetimeRemaining = 0;
			}
			else if (CurStatue != null)
			{
				StatueLifetimeRemaining--;
				if (StatueLifetimeRemaining <= 0)
				{
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHAPE", "natural expiry: statue de-animates into inert lootable container");
					XDidY(CurStatue, "return", "wearily to earth", Source: CurStatue, ColorAsBadFor: ParentObject);
					DeanimateStatue(CurStatue);
					CurStatue = null;
				}
			}
			else if (StatueLifetimeRemaining > 0)
			{
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHAPE", $"statue died with {StatueLifetimeRemaining} turns of lifetime left");
				StatueLifetimeRemaining = 0;
			}
			return base.HandleEvent(E);
		}

		public static void ApplyStatueBuffs(GameObject Sculptor, GameObject Creature, GameObject Statue, int Lv, Cleo_TerraFirma_StoneshaperWallProperties WallProps)
		{
			bool infiniteDuration = Lv >= 10;
			if (Creature.Body != null)
				Statue.Body.Rebuild(Creature.Body.Anatomy);

			Mutations m = Statue.GetPart<Mutations>();
			if (m != null)
			{
				if (m.MutationList != null)
				{
					foreach (BaseMutation mut in m.MutationList.ToArray())
						m.RemoveMutation(mut);
				}
				m.AddMutation(new NightVision());
			}

			Skills s = Statue.GetPart<Skills>();
			if (s?.SkillList != null)
			{
				foreach (BaseSkill skill in s.SkillList.ToArray())
					s.RemoveSkill(skill);
			}

			Dictionary<string, GameObject> museWeapons = new();
			if (Creature.Body != null)
			{
				foreach (BodyPart part in Creature.Body.LoopParts())
				{
					GameObject museWeapon = part.DefaultBehavior ?? ((part.Equipped != null && part.Equipped.IsNatural()) ? part.Equipped : null);
					if (museWeapon != null && museWeapon.HasTag("UndesirableWeapon"))
						museWeapon = null;
					if (museWeapon != null && part.Type != null && !museWeapons.ContainsKey(part.Type))
						museWeapons[part.Type] = museWeapon;
				}
			}

			var naturalWeaponParts = Statue.Body.LoopParts();
			List<BodyPart> partsToReplace = new();
			foreach (var item in naturalWeaponParts)
			{
				if (item.Type == null || item.Abstract || item.Extrinsic)
					continue;
				if (item.DefaultBehavior != null)
				{
					item.DefaultBehaviorBlueprint = null;
					item.DefaultBehavior = null;
					partsToReplace.Add(item);
				}
				if (item.Equipped != null && item.Equipped.IsNatural())
				{
					if (!partsToReplace.Contains(item))
						partsToReplace.Add(item);
					item.Unequip();
				}
			}

			foreach (var item in Statue.Body.LoopParts())
			{
				if (item.Type == null || item.Abstract || item.Extrinsic)
					continue;
				if (museWeapons.ContainsKey(item.Type) && !partsToReplace.Contains(item))
					partsToReplace.Add(item);
			}

			if (partsToReplace.IsNullOrEmpty())
			{
				partsToReplace.Add(Statue.Body.GetPart("Body", true).First());
			}
			List<string> museMapLines = new();
			foreach (var item in partsToReplace)
			{
				item.DefaultBehaviorBlueprint = DEFAULT_BEHAVIOR_BP;
				item.DefaultBehavior = GameObjectFactory.Factory.CreateUnmodifiedObject(DEFAULT_BEHAVIOR_BP);
				MeleeWeapon wp = item.DefaultBehavior.RequirePart<MeleeWeapon>();
				wp.BaseDamage = GetNaturalDamage(Lv);
				wp.Slot = item.Type;
				if (item.Type != null && museWeapons.TryGetValue(item.Type, out GameObject museWeapon))
				{
					item.DefaultBehavior.Render.DisplayName = museWeapon.Render.DisplayName;
					item.DefaultBehavior.Render.Tile = museWeapon.Render.Tile;
					museMapLines.Add($"{item.Type}\t{museWeapon.Render.DisplayName}\t{museWeapon.Render.Tile}");
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHAPE", $"muse weapon: statue {item.Type} armed as '{museWeapon.Render.DisplayName}' (mutation-formula stats)");
				}
				else
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHAPE", $"muse weapon: statue {item.Type} armed with fallback gravelly fist");
			}
			Statue.Body.RecalculateFirsts();

			string[] statsInherited = { "Strength", "Agility", "Toughness" };
			foreach (string stat in statsInherited)
				Statue.GetStat(stat).BaseValue = Creature.GetStatValue(stat);

			int museGen = Creature.GetIntProperty("Cleo_TerraFirma_StatueCopyGen");
			Statue.SetIntProperty("Cleo_TerraFirma_StatueCopyGen", museGen + 1);
			if (museGen >= 2)
			{
				string dingedStat = statsInherited[Stat.Random(0, statsInherited.Length - 1)];
				Statistic st = Statue.GetStat(dingedStat);
				int loss = Math.Min(Stat.Random(1, 3), st.BaseValue - 1);
				if (loss > 0)
					st.BaseValue -= loss;
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHAPE", $"copy decay: gen {museGen + 1} statue loses {loss} {dingedStat} (muse was gen {museGen})");
				if (Sculptor.IsPlayer())
					XRL.Messages.MessageQueue.AddPlayerMessage("Some of the likeness is lost in the copying.");
			}

			Statue.GetStat("Level").BaseValue = Sculptor.Level;
			if (!infiniteDuration)
				Statue.RemovePart<Leveler>();
			Cleo_TerraFirma_StoneshaperStatueStatsPart statsPart = new(GetHP(Lv) - Statue.GetStatValue("Hitpoints"), (GetAV(Lv)) - Statue.GetStatValue("AV"));
			statsPart.NaturalWeaponDamage = GetNaturalDamage(Lv);
			statsPart.MuseWeaponMap = string.Join("\n", museMapLines);
			Statue.AddPart(statsPart);
			if (WallProps != null)
			{
				if (WallProps.StatName != null)
					Statue.AddBaseStat(WallProps.StatName, WallProps.StatShift);
				if (WallProps.MutationName != null)
					Statue.RequirePart<Mutations>().AddMutation(WallProps.MutationName, WallProps.MutationLevel);
			}

			bool isVain = Creature == Sculptor || Creature.Blueprint == "Asphodel" || Sculptor.HasEffect<Lovesick>();
			if (isVain)
			{
				Statue.SetIntProperty("Cleo_TerraFirma_IsVain", 1);
				Statue.SetGender(Creature.GetGender());
				Statue.SetPronounSet(Creature.GetPronounSet());
			}
			else
			{
				Statue.SetGender("nonspecific");
				Statue.SetPronounSet("it/its");
			}
			Statue.Brain.Allegiance.Clear();
			Statue.Brain.BecomeCompanionOf(Sculptor);
			Statue.RequirePart<ConversationScript>().ConversationID = "Cleo_TerraFirma_ConversationStoneshaperStatue";
			Statue.AddPart(new Distraction
			{
				Source = Statue,
				Original = Sculptor,
				SaveVs = "Stoneshaper Distraction",
				SaveStat = "Willpower",
				SaveTarget = GetDistractionSaveDifficulty(Lv)
			});
		}

		public static void DeanimateStatue(GameObject Statue)
		{
			Statue.PlayWorldSound("Sounds/Damage/sfx_destroy_stone", 1f, Combat: true);
			Cell cell = Statue.CurrentCell;
			GameObject inert = GameObject.Create("Cleo_TerraFirma_StoneshaperStatue");
			inert.Render.DisplayName = Statue.Render.DisplayName;
			inert.Render.RenderString = Statue.Render.RenderString;
			inert.Render.Tile = Statue.Render.Tile;
			inert.Render.ColorString = Statue.Render.ColorString;
			inert.Render.DetailColor = Statue.Render.DetailColor;
			inert.Render.TileColor = Statue.Render.TileColor;
			Description statueDesc = Statue.GetPart<Description>();
			Description inertDesc = inert.GetPart<Description>();
			if (statueDesc != null && inertDesc != null)
				inertDesc._Short = statueDesc._Short;
			StatueHelpers.EntombPossessions(Statue, inert);
			Statue.SetStringProperty("CustomDeathVerb", "expired");
			Statue.Obliterate(Silent: true);
			cell?.AddObject(inert, true, Silent: true);
		}

		public static void PlayCarveReveal(Cell targetWallCell, GameObject refWall, GameObject statue, Action Place)
		{
			Cleo_TerraFirma_StoneRiseFX.Schedule schedule = null;
			Zone zone = targetWallCell.ParentZone;
			if (!Cleo_TerraFirma_StoneRiseFX.HarnessMute && zone != null && zone.IsActive() && targetWallCell.IsVisible())
				schedule = Cleo_TerraFirma_StoneRiseFX.PlayCarveReveal(targetWallCell, refWall);
			if (schedule != null)
			{
				ScreenBuffer buf = ScreenBuffer.GetScrapBuffer1(bLoadFromCurrent: true);
				TextConsole console = Popup._TextConsole;
				float swapAt = schedule.SettleTimes[targetWallCell];
				bool swapped = false;
				int breaksPlayed = 0;
				float t = 0f;
				float end = schedule.Total + 0.08f;
				while (t < end)
				{
					if (!swapped && swapAt <= t)
					{
						Place();
						swapped = true;
					}
					while (breaksPlayed < schedule.BreakTimes.Count && schedule.BreakTimes[breaksPlayed] <= t)
					{
						targetWallCell.PlayWorldSound("Sounds/Damage/sfx_damage_stone", 1f, Combat: true);
						breaksPlayed++;
					}
					XRLCore.Core.RenderBaseToBuffer(buf);
					console.DrawBuffer(buf);
					Thread.Sleep(40);
					t += 0.04f;
				}
				if (!swapped)
					Place();
				Cleo_TerraFirma_Fissure.SmokePuff(targetWallCell, Stat.Random(1, 4));
			}
			else
				Place();
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHAPE", $"carve reveal: {(schedule != null ? schedule.Total.ToString("0.00") + "s, block + 6 debris / 3 breaks" : "instant (unseen or harness-muted)")}");
			statue.Splatter($"&{statue.GetForegroundColor()}*");
			targetWallCell.PlayWorldSound("Sounds/Throw/sfx_throwing_stone_small_impact", 1f, Combat: true);
		}

		public static GameObject MakeStatueOfCreatureFromWall(GameObject Muse, GameObject Wall, string NamePrefix = "", Cleo_TerraFirma_StoneshaperWallProperties WallProperties = null)
		{
			if (!Muse.IsCreature || !Wall.IsWall())
				return null;

			GameObject statue = GameObject.Create("Cleo_TerraFirma_StoneshaperStatue");
			RandomStatue randomStatuePart = statue.RequirePart<RandomStatue>();
			randomStatuePart.Material = WallProperties?.MaterialDisplayName ?? WallProperties?.MaterialType ?? "stone";
			randomStatuePart.SetCreature(Muse);
			statue.DisplayName = $"{(NamePrefix != string.Empty ? $"{NamePrefix} " : "")}{statue.DisplayName}";

			if (NamePrefix == "rough")
			{
				Description roughDesc = statue.GetPart<Description>();
				if (roughDesc != null)
					roughDesc._Short = roughDesc._Short.Replace(" intricately depicts ", " crudely depicts ");
			}

			statue.Physics.FlameTemperature = Wall.Physics.FlameTemperature;
			statue.Physics.VaporTemperature = Wall.Physics.VaporTemperature;
			statue.Physics.FreezeTemperature = Wall.Physics.FreezeTemperature;
			statue.Physics.BrittleTemperature = Wall.Physics.BrittleTemperature;
			statue.Physics.Organic = Wall.Physics.Organic;
			statue.Physics.Conductivity = Wall.Physics.Conductivity;

			statue.Render.ColorString = Wall.Render.ColorString;
			statue.Render.DetailColor = Wall.Render.DetailColor;
			statue.Render.TileColor = Wall.Render.TileColor;

			return statue;
		}

		public static int GetDuration(int Lv) => Lv >= 10 ? INFINITE_DURATION_PLACEHOLDER : BASE_DURATION + (Lv * TURNS_PER_LEVEL);

		public static int GetHP(int Lv) => Lv * 25;

		public static int GetAV(int Lv) => 3 + Mathf.FloorToInt(Lv / 2);

		public static WallType GetValidWallsAtLevel(int Lv)
		{
			if (Lv < 5)
				return WallType.Stone;
			else if (Lv < 10)
				return WallType.StoneAndMetal;
			return WallType.All;
		}

		public static int GetDistractionSaveDifficulty(int Lv)
		{
			return 10 + Lv;
		}

		public static string GetNaturalDamage(int Lv)
		{
			float dice = 1 + MathF.Floor(Lv / 3);
			return $"{dice}d3";
		}

	}
}
