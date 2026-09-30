using System;
using Cleo.TerraFirma.Scripts;
using XRL.UI;
using XRL.World.Capabilities;
using XRL.World.Effects;

namespace XRL.World.Parts.Mutation
{
	[Serializable]
	public class Cleo_TerraFirma_GorgonsGaze : BaseMutation
	{
		private const string COMMAND_ID = "Cleo_TerraFirma_CommandGorgonsGaze";
		private const int NUM_OF_SAVES = 10;
		private const int SUCCESSES_TO_BREAK = 3;
		private const int STONE_MARK_MIN_FAILS = 4;
		private const int QUICKNESS_LOSS_PER_SAVE = 10;

		private const int BASE_COOLDOWN = 80;

		public GameObject Target;

		public int CurFailedSaves;

		public int CurSuccessfulSaves;

		[NonSerialized]
		public Cleo_TerraFirma_Petrifying AssocEffect;

		[NonSerialized]
		private Cleo_TerraFirma_PetrifyBandState BandFx;

		public override string GetDescription()
		{
			return "Your unblinking gaze stops creatures in their tracks.";
		}

		public override string GetLevelText(int Level)
		{
			return "You transfix an opponent with your unblinking gaze and slowly turn them to stone.\n" +
				"Taking any action other than passing the turn will break your gaze.\n" +
				"Each round your target makes a Toughness save. Each failure advances petrification by one stage. Each success reverses one stage.\n" +
				"Save difficulty: {{rules|" + GetSaveDifficulty(Level) + "}} + your Willpower modifier, +1 per failed save\n" +
				"Each failed save holds your target in place and applies -" + QUICKNESS_LOSS_PER_SAVE + " Quickness.\n" +
				"Three successful saves break the gaze.\n" +
				"Targets that fail " + NUM_OF_SAVES + " saves are permanently turned to stone.\n" +
				"Targets that escape after " + STONE_MARK_MIN_FAILS + " or more failures are left partially petrified for " + Cleo_TerraFirma_PartiallyPetrified.DURATION + " rounds.\n" +
				"Partial petrification: -" + Cleo_TerraFirma_PartiallyPetrified.MOVE_SPEED_PENALTY + " move speed, -" + Cleo_TerraFirma_PartiallyPetrified.QUICKNESS_PENALTY + " Quickness, +" + Cleo_TerraFirma_PartiallyPetrified.AV_BONUS + " AV\n" +
				"Range: sight\n" +
				"Cooldown: " + BASE_COOLDOWN + " rounds";
		}

		public override bool Mutate(GameObject GO, int Level = 1)
		{
			ActivatedAbilityID = AddMyActivatedAbility("Gorgon's Gaze", COMMAND_ID, "Physical Mutations");
			return base.Mutate(GO, Level);
		}

		public override bool Unmutate(GameObject GO)
		{
			RemoveMyActivatedAbility(ref ActivatedAbilityID);
			if (AssocEffect != null)
				AssocEffect.Receding = true;
			Cleo_TerraFirma_GorgonPetrifyFX.Release(BandFx, ShrinkOut: true);
			BandFx = null;
			return base.Unmutate(GO);
		}

		public override void CollectStats(Templates.StatCollector stats, int Level)
		{
			stats.Set("SavesToPetrify", NUM_OF_SAVES);
			stats.Set("BreakSuccesses", SUCCESSES_TO_BREAK);
			stats.Set("MarkFails", STONE_MARK_MIN_FAILS);
			stats.Set("Range", "sight");
			stats.Set("SaveDifficulty", GetSaveDifficulty(Level));
			stats.Set("QuicknesLoss", QUICKNESS_LOSS_PER_SAVE);
			stats.CollectCooldownTurns(MyActivatedAbility(ActivatedAbilityID), BASE_COOLDOWN);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) &&
				ID != SingletonEvent<BeginTakeActionEvent>.ID &&
				ID != SingletonEvent<BeforeTakeActionEvent>.ID &&
				ID != PooledEvent<CommandEvent>.ID &&
				ID != AIGetOffensiveAbilityListEvent.ID &&
				ID != SingletonEvent<EndTurnEvent>.ID)
				return ID == SingletonEvent<UseEnergyEvent>.ID;
			return true;
		}

		public override bool HandleEvent(AIGetOffensiveAbilityListEvent E)
		{
			if (Target == null && E.Target != null && E.Distance <= GetRange(Level) &&
				IsMyActivatedAbilityAIUsable(ActivatedAbilityID) && !E.Target.HasEffect<Cleo_TerraFirma_Petrifying>())
				E.Add(COMMAND_ID);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(BeforeTakeActionEvent E)
		{
			if (Target != null && !ParentObject.IsPlayer())
			{
				ParentObject.PassTurn();
				return false;
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(CommandEvent E)
		{
			if (E.Command != COMMAND_ID)
				return base.HandleEvent(E);
			GameObject victim;
			if (E.Actor != null && !E.Actor.IsPlayer())
			{
				victim = E.Target;
				if (!GameObject.Validate(ref victim) || victim == ParentObject || victim.Brain == null || victim.HasEffect<Cleo_TerraFirma_Petrifying>())
					return false;
			}
			else
			{
				Cell cell = PickDestinationCell(GetRange(Level), RequireCombat: true, Label: "Choose a target to gaze at.");
				if (cell == null)
					return false;
				victim = cell.GetCombatTarget(ParentObject, true);
				if (victim == null)
					return false;
				if (victim == ParentObject)
				{
					if (victim.IsPlayer())
					{
						Popup.Show("You gaze at yourself.");
						Popup.Show("Everything seems to be in order.");
					}
					return false;
				}
				if (victim.HasEffect<Cleo_TerraFirma_Petrifying>())
				{
					if (E.Actor.IsPlayer())
						Popup.ShowFail($"{victim.T()} is already being petrified! Wait your turn!");
					return false;
				}
				if (victim.Brain == null)
				{
					if (E.Actor.IsPlayer())
						Popup.ShowFail($"You cannot transfix {victim.t()}.");
					return false;
				}
			}
			UseEnergy(1000, $"Physical Mutation {Name}");
			CooldownMyActivatedAbility(ActivatedAbilityID, BASE_COOLDOWN);
			SoundManager.PreloadClipSet("Sounds/Abilities/sfx_ability_sunderMind_abort");
			SoundManager.PreloadClipSet("Sounds/Abilities/sfx_ability_sunderMind_dig");
			ParentObject.PlayWorldSound("Sounds/Abilities/sfx_ability_sunderMind_attack");
			victim.PlayWorldSound("Sounds/Abilities/sfx_ability_sunderMind_attack");
			BeginGaze(victim);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(BeginTakeActionEvent E)
		{
			TickGaze();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(UseEnergyEvent E)
		{
			if (Target != null && (!E.Passive || (E.Type != null && !E.Type.Contains("Pass"))))
			{
				Helpers.VerifyLog("GAZE", $"caster acted (energy type: {E.Type ?? "?"}) -> gaze breaks at {CurFailedSaves}F/{CurSuccessfulSaves}S");
				CancelGaze($"You blink, releasing {Target.t()} from your gaze!");
			}
			return base.HandleEvent(E);
		}

		public static int GetSaveDifficulty(int Lv)
		{
			return 11 + Lv;
		}

		public static int GetRange(int Lv) => 80;

		public void BeginGaze(GameObject Victim)
		{
			Cleo_TerraFirma_GorgonPetrifyFX.Release(BandFx, ShrinkOut: false);
			BandFx = null;
			Target = Victim;
			CurFailedSaves = 0;
			CurSuccessfulSaves = 0;
			Helpers.VerifyLog("GAZE", $"BEGIN: {ParentObject.Blueprint} Lv{Level} gazes at {Victim.Blueprint}; base DC {GetSaveDifficulty(Level)} + caster WIL mod, +1/fail");
			XDidY(ParentObject, "level", $"an unblinking gaze at {Victim.t()}", "!", ColorAsGoodFor: ParentObject, ColorAsBadFor: Victim);
			if (Victim.IsPlayer())
			{
				Popup.Show($"{ParentObject.T()} {The.Player.DescribeDirectionToward(ParentObject)}{ParentObject.GetVerb("begin")} turning you to stone with an unblinking gaze!");
				AutoAct.Interrupt(ParentObject, true, true);
			}
			AssocEffect = new Cleo_TerraFirma_Petrifying(ParentObject);
			Target.ApplyEffect(AssocEffect, ParentObject);
			ParentObject.PlayWorldSound("Sounds/Abilities/sfx_ability_sunderMind_attack");
			Target.PlayWorldSound("Sounds/Abilities/sfx_ability_sunderMind_attack");
		}

		public void TickGaze()
		{
			if (Target == null || !Target.IsValid() || Target.IsInGraveyard())
			{
				CancelGaze(Silent: true);
				return;
			}
			if (ParentObject.CurrentZone != Target.CurrentZone)
			{
				CancelGaze($"{Target.T()} exits the reach of your unblinking gaze, breaking its hold.");
				return;
			}
			if (ParentObject.HasEffect<Stun>() || ParentObject.HasEffect<StunGasStun>() || ParentObject.HasEffect<Confused>() || ParentObject.HasEffect<Asleep>() || ParentObject.HasEffect<Paralyzed>())
			{
				Helpers.VerifyLog("GAZE", $"caster incapacitated (stun/confusion/sleep/paralysis) -> gaze breaks at {CurFailedSaves}F/{CurSuccessfulSaves}S");
				CancelGaze($"You cannot hold your unblinking gaze!");
				return;
			}
			if (Target.Brain == null)
			{
				Helpers.VerifyLog("GAZE", $"brainless target {Target.Blueprint} -> gaze releases (lockout self-heal)");
				CancelGaze(Silent: true);
				return;
			}
			AssocEffect ??= Target.GetEffect<Cleo_TerraFirma_Petrifying>();
			Target.Brain.Attacked(ParentObject);
			bool targetResists = Target.MakeSave("Toughness", GetSaveDifficulty(Level) + CurFailedSaves, ParentObject, "Willpower", Vs: Name, IgnoreNatural20: !Target.IsPlayer(), Source: ParentObject);
			Helpers.VerifyLog("GAZE", $"tick: {Target.Blueprint} save vs DC {GetSaveDifficulty(Level) + CurFailedSaves}+WIL -> {(targetResists ? "RESIST" : "FAIL")} (tally now {CurFailedSaves + (targetResists ? 0 : 1)}F/{CurSuccessfulSaves + (targetResists ? 1 : 0)}S)");
			if (targetResists)
			{
				CurSuccessfulSaves++;
				if (CurSuccessfulSaves >= SUCCESSES_TO_BREAK)
				{
					Helpers.VerifyLog("GAZE", $"3rd success: gaze BROKEN by {Target.Blueprint}{(CurFailedSaves >= STONE_MARK_MIN_FAILS ? " (stone-mark eligible at " + CurFailedSaves + " fails)" : "")}");
					CancelGaze($"{Target.T()} shakes off your unblinking gaze entirely, breaking its hold!");
					return;
				}
				Target.PlayWorldSound("Sounds/Damage/sfx_damage_stone", 1f, Combat: true);
				if (CurSuccessfulSaves == 1)
				{
					Helpers.VerifyLog("GAZE", "1st success: pin released");
					AssocEffect?.ReleasePin();
					XDidY(Target, "wrench", "free of the stone's grip", "!", ColorAsGoodFor: Target, ColorAsBadFor: ParentObject);
				}
				else
				{
					Helpers.VerifyLog("GAZE", "2nd success: slow cleared");
					AssocEffect?.ClearSlow();
					XDidY(Target, "shake", "the stiffness from " + Target.its + " limbs", "!", ColorAsGoodFor: Target, ColorAsBadFor: ParentObject);
				}
				UpdateBandFx("success");
				return;
			}
			AssocEffect?.ApplyFailure();
			CurFailedSaves++;
			if (CurFailedSaves >= NUM_OF_SAVES)
			{
				Helpers.VerifyLog("GAZE", $"10th fail: {Target.Blueprint} PETRIFIED (statue + entombed possessions)");
				Petrify(ParentObject, Target);
				CurFailedSaves = 0;
				CancelGaze(Silent: true);
			}
			else
			{
				ParentObject.PlayWorldSound("Sounds/Abilities/sfx_ability_sunderMind_dig");
				Target.PlayWorldSound("Sounds/Abilities/sfx_ability_sunderMind_dig");
				Cleo_TerraFirma_GorgonPetrifyFX.RockPops(Target, 2 + CurFailedSaves / 4);
				UpdateBandFx("fail");
				if (ParentObject.IsPlayer())
					DidX("continue", "to gaze", "…", ColorAsGoodFor: ParentObject, ColorAsBadFor: Target, Source: ParentObject);
			}
		}

		private void UpdateBandFx(string why)
		{
			int net = Math.Max(0, CurFailedSaves - CurSuccessfulSaves);
			float frac = Cleo_TerraFirma_GorgonPetrifyFX.FracForNet(net);
			BandFx = Cleo_TerraFirma_GorgonPetrifyFX.Show(BandFx, Target, frac);
			Helpers.VerifyLog("GAZE", $"band ({why}): net {net} -> frac {frac:0.00}{(BandFx == null ? " [no sprite: muted/unseen/text-mode]" : "")}");
		}

		public void CancelGaze(string Reason = "", bool Silent = false)
		{
			Cleo_TerraFirma_GorgonPetrifyFX.Release(BandFx, ShrinkOut: true);
			BandFx = null;
			if (AssocEffect == null && GameObject.Validate(Target))
				AssocEffect = Target.GetEffect<Cleo_TerraFirma_Petrifying>();
			if (!Silent)
				ParentObject.PlayWorldSound("Sounds/Abilities/sfx_ability_sunderMind_abort");
			if (CurFailedSaves >= STONE_MARK_MIN_FAILS && GameObject.Validate(Target) && !Target.IsInGraveyard())
			{
				Helpers.VerifyLog("GAZE", $"cancel at {CurFailedSaves} fails: STONE-MARK applied to {Target.Blueprint} (partially petrified, 500 rds)");
				if (AssocEffect != null)
					Target.RemoveEffect(AssocEffect);
				Target.ApplyEffect(new Cleo_TerraFirma_PartiallyPetrified(), ParentObject);
				Target.PlayWorldSound("Sounds/Damage/sfx_damage_stone", 1f, Combat: true);
				XDidY(Target, "remain", "partially petrified", "!", ColorAsBadFor: Target, Source: ParentObject);
			}
			else
				AssocEffect?.UpdateData(null, true);
			CurFailedSaves = 0;
			CurSuccessfulSaves = 0;
			if (ParentObject.IsPlayer() && !Silent && Reason != string.Empty)
				AddPlayerMessage(Reason, ConsequentialColorChar(null, ParentObject));
			if (Target != null && !Silent)
				XDidY(Target, "stop", "turning to stone", "!", ColorAsGoodFor: Target, ColorAsBadFor: ParentObject);
			Target = null;
			AssocEffect = null;
		}

		public static void Petrify(GameObject Medusa, GameObject Victim)
		{
			Cleo_TerraFirma_GorgonPetrifyFX.RockPops(Victim, 10, Burst: true);
			Victim.PlayWorldSound("burn_blast", 1f, Combat: true);
			if (Victim.IsPlayer())
				Achievement.TURNED_STONE.Unlock();
			Victim.SetIntProperty("SuppressCorpseDrops", 1);
			Cell curCell = Victim.CurrentCell;
			GameObject statue = StoneGaze.CreateStatueOf(Victim);
			Cleo.TerraFirma.Scripts.StatueHelpers.EntombPossessions(Victim, statue);
			XDidYToZ(Medusa, "turn", Victim, $"to stone with {Medusa.poss("unblinking gaze")}", "!", ColorAsGoodFor: Medusa, ColorAsBadFor: Victim, Source: Medusa);
			if (Victim.Die(Medusa, null, $"You were turned to stone by the unblinking gaze of {Medusa.an()}.",
				$"{Victim.It + Victim.GetVerb("were", true, true)} @@turned to stone by the unblinking gaze of {Medusa.an()}."))
			{
				curCell.AddObject(statue);
			}
			else
			{
				Cleo.TerraFirma.Scripts.StatueHelpers.ReturnPossessions(statue, Victim);
				Victim.ModIntProperty("SuppressCorpseDrops", -1, RemoveIfZero: true);
				statue.Obliterate();
			}
		}
	}
}
