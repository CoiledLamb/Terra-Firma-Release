using System;
using System.Collections.Generic;
using UnityEngine;
using XRL.Rules;
using XRL.World.Effects;

namespace XRL.World.Parts.Mutation
{
	public class Cleo_TerraFirma_EarthenBarrage : BaseMutation
	{
		private const string COMMAND_ID = "Cleo_TerraFirma_CommandEarthenBarrage";
		private const string COMMAND_ATTUNE_ID = "Cleo_TerraFirma_CommandAttuneStones";
		private const string BLUEPRINT = "Cleo_TerraFirma_EphemeralStone";
		private const string BLUEPRINT_RESONANT = "Cleo_TerraFirma_EphemeralStoneResonant";
		private const int RESONANT_UNLOCK_LEVEL = 10;

		private const int TURNS_PER_CHARGE = 15;

		private const int ROCK_LIFETIME_MIN = 50;
		private const int ROCK_LIFETIME_MAX = 100;

		private const int HELD_SUSTAIN_LEASE = 9999;

		public int Charges;

		public int TurnsSinceLastCharge;

		public bool EverArmed;

		public bool WasOnWorldMap;

		private const int FORMAT_VERSION = 2;

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.Write(Cleo.TerraFirma.Scripts.Helpers.SERIAL_SENTINEL);
			Writer.Write(FORMAT_VERSION);
			base.Write(Basis, Writer);
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			if (Reader.ReadInt32() != Cleo.TerraFirma.Scripts.Helpers.SERIAL_SENTINEL)
				throw new FormatException("Cleo_TerraFirma_EarthenBarrage: pre-sentinel block");
			Reader.ReadInt32();
			base.Read(Basis, Reader);
		}

		public override bool ReadError(Exception Exception, SerializationReader Reader, long Start, int Length)
		{
			try
			{
				Reader.Stream.Position = Start;
				Reader.ReadTokenizedType();
				StatShifter.Load(Reader, ParentObject);
				base.Read(ParentObject, Reader);
				if (Reader.Stream.Position == Start + Length)
				{
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SERIAL", $"EB pre-sentinel local block recovered in place (charges={Charges}, level intact)");
					return true;
				}
			}
			catch
			{
			}
			Charges = 0;
			TurnsSinceLastCharge = 0;
			EverArmed = false;
			WasOnWorldMap = false;
			Reader.Stream.Position = Start + Length;
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SERIAL", $"EB legacy block reset ({Length} byte(s) skipped; beta-layout save -- level re-set expected)");
			return true;
		}

		[NonSerialized]
		public Cleo_TerraFirma_GravelWhorl AssocWhorl;

		public Guid AttuneAbilityID = Guid.Empty;

		public bool Resonant => AttuneAbilityID != Guid.Empty && IsMyActivatedAbilityToggledOn(AttuneAbilityID);

		[NonSerialized]
		private bool Retuning;

		public override string GetDescription()
		{
			return "You form hefty rocks suitable for throwing.";
		}

		public override string GetLevelText(int Level)
		{
			return "You sculpt a stone every {{rules|" + GetTurnsPerCharge() + "}} rounds, up to a reserve of {{rules|" + MaxCharges(Level) + "}}.\n" +
				"Sculpted stones occupy your thrown weapon slot.\n" +
				"Stones not held as a thrown weapon crumble to dust after " + ROCK_LIFETIME_MIN + "-" + ROCK_LIFETIME_MAX + " rounds.\n" +
				"Damage increment: {{rules|" + GetThrowDamage(Level) + "}}\n" +
				"Stone penetration: your Strength modifier, up to {{rules|" + GetPenetrationCap(Level) + "}}\n" +
				"Stones in reserve whorl about you and deflect physical projectiles.\n" +
				"Lasers and other energy attacks pass through the whorl.\n" +
				"Deflect chance: 3% per stone in reserve, +1% per mutation level" +
				(Level >= RESONANT_UNLOCK_LEVEL
					? "\nYou may attune your stones to {{C|resonance}}.\nResonant stones' penetration value is equal to the defender's armor value.\nWhile attuned, your whorl deflects light instead of matter."
					: "\nAt level " + RESONANT_UNLOCK_LEVEL + ", you learn to attune your stones to {{C|resonance}}.");
		}

		public override void CollectStats(Templates.StatCollector stats)
		{
			stats.Set("Damage", GetThrowDamage(Level));
			stats.Set("PenetrationCap", GetPenetrationCap(Level));
			stats.Set("TurnsPerCharge", GetTurnsPerCharge());
			stats.Set("MaxCharges", MaxCharges(Level));
			stats.Set("Lifetime", $"{ROCK_LIFETIME_MIN}-{ROCK_LIFETIME_MAX}");
			stats.Set("DeflectFull", (3 * MaxCharges(Level)) + Level);
		}

		public override bool Mutate(GameObject GO, int Level = 1)
		{
			bool defaultOn = XRL.UI.Options.GetOption("Cleo_TerraFirma_TakeStonesDefaultOn").EqualsNoCase("Yes");
			ActivatedAbilityID = AddMyActivatedAbility("Earthen Barrage", COMMAND_ID, "Physical Mutations", Toggleable: true, DefaultToggleState: defaultOn, IsWorldMapUsable: true);
			Charges = MaxCharges(Level);
			SyncAbilityName();
			SyncAttuneAbility();
			return base.Mutate(GO, Level);
		}

		public override bool Unmutate(GameObject GO)
		{
			RemoveMyActivatedAbility(ref ActivatedAbilityID);
			if (AttuneAbilityID != Guid.Empty)
				RemoveMyActivatedAbility(ref AttuneAbilityID);
			AssocWhorl ??= GO.GetEffect<Cleo_TerraFirma_GravelWhorl>();
			if (AssocWhorl != null)
			{
				GO.RemoveEffect(AssocWhorl);
				AssocWhorl = null;
			}
			foreach (GameObject stone in GO.GetInventoryAndEquipment(IsOurStone))
				if (stone.GetPart<Temporary>() is Temporary lease && lease.Duration > ROCK_LIFETIME_MAX)
					lease.Duration = Stat.Random(ROCK_LIFETIME_MIN, ROCK_LIFETIME_MAX);
			return base.Unmutate(GO);
		}

		public override bool ChangeLevel(int NewLevel)
		{
			SyncAttuneAbility();
			return base.ChangeLevel(NewLevel);
		}

		private void SyncAttuneAbility()
		{
			if (!GameObject.Validate(ParentObject))
				return;
			if (Level >= RESONANT_UNLOCK_LEVEL)
			{
				if (AttuneAbilityID == Guid.Empty)
					AttuneAbilityID = AddMyActivatedAbility("Attune Stones", COMMAND_ATTUNE_ID, "Physical Mutations", Toggleable: true, IsWorldMapUsable: true);
			}
			else if (AttuneAbilityID != Guid.Empty)
			{
				RemoveMyActivatedAbility(ref AttuneAbilityID);
				RetuneHeldStone();
				SyncWhorl();
			}
		}

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) &&
				ID != SingletonEvent<EndTurnEvent>.ID &&
				ID != PooledEvent<CommandEvent>.ID &&
				ID != PooledEvent<CheckExistenceSupportEvent>.ID)
				return ID == PooledEvent<ReplaceThrownWeaponEvent>.ID;
			return true;
		}

		public override bool HandleEvent(CommandEvent E)
		{
			if (E.Command == COMMAND_ATTUNE_ID)
			{
				ToggleMyActivatedAbility(AttuneAbilityID);
				RetuneHeldStone();
				SyncWhorl();
				if (Resonant)
				{
					ParentObject.PlayWorldSound("Sounds/Damage/sfx_damage_gemstone", 1f);
					XDidY(ParentObject, "attune", "the stones to a keening pitch", Source: ParentObject);
				}
				else
				{
					ParentObject.PlayWorldSound("Sounds/Throw/sfx_throwing_stone_small_impact", 1f);
					XDidY(ParentObject, "still", "the stones' hum", Source: ParentObject);
				}
				return base.HandleEvent(E);
			}
			if (E.Command != COMMAND_ID)
				return base.HandleEvent(E);
			ToggleMyActivatedAbility(ActivatedAbilityID);
			if (CanGetStone(true) && IsMyActivatedAbilityToggledOn(ActivatedAbilityID) && E.Actor.GetFirstThrownWeapon() == null)
			{
				Activate();
				XDidY(ParentObject, "retrieve", $"a {GetStoneName(Level)}", Source: ParentObject);
			}
			SyncWhorl();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			GameObject held = ParentObject.GetFirstThrownWeapon();
			if (held != null && (!GameObject.Validate(held) || held.IsInGraveyard()))
			{
				Body body = ParentObject.Body;
				if (body != null)
				{
					foreach (XRL.World.Anatomy.BodyPart bp in body.GetParts())
					{
						if (bp.Equipped == held)
						{
							bp.Unequip();
						}
					}
				}
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("EB", "cleared a dead thrown-slot reference (slot-destroyed stone)");
			}
			if (!ParentObject.IsPlayer())
			{
				if (!IsMyActivatedAbilityToggledOn(ActivatedAbilityID))
					ToggleMyActivatedAbility(ActivatedAbilityID);
				if (IsMyActivatedAbilityToggledOn(ActivatedAbilityID) && ParentObject.GetFirstThrownWeapon() == null && CanGetStone(true))
					Activate();
			}
			else if (!EverArmed && IsMyActivatedAbilityToggledOn(ActivatedAbilityID))
			{
				EverArmed = true;
				if (ParentObject.GetFirstThrownWeapon() == null && CanGetStone(true))
				{
					Activate();
					XDidY(ParentObject, "retrieve", $"a {GetStoneName(Level)}", Source: ParentObject);
				}
			}
			if (ParentObject.OnWorldMap())
			{
				WasOnWorldMap = true;
			}
			else if (WasOnWorldMap)
			{
				WasOnWorldMap = false;
				if (ParentObject.IsPlayer() && IsMyActivatedAbilityToggledOn(ActivatedAbilityID)
					&& ParentObject.GetFirstThrownWeapon() == null && CanGetStone(true))
				{
					Activate();
					XDidY(ParentObject, "retrieve", $"a {GetStoneName(Level)}", Source: ParentObject);
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("EB", "world-map arrival re-arm: slot came back empty, sculpted a fresh stone");
				}
			}
			else if (ParentObject.IsPlayer() && IsMyActivatedAbilityToggledOn(ActivatedAbilityID)
				&& ParentObject.GetFirstThrownWeapon() == null && CanGetStone(true))
			{
				Activate();
				XDidY(ParentObject, "retrieve", $"a {GetStoneName(Level)}", Source: ParentObject);
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("EB", "empty-slot re-arm: thrown slot found empty at end of turn, sculpted a fresh stone");
			}
			GameObject heldPebble = ParentObject.FindEquippedItem(x => IsOurStone(x) && x.IsEquippedAsThrownWeapon());
			if (heldPebble != null)
			{
				Temporary t = heldPebble.GetPart<Temporary>();
				t.Duration = HELD_SUSTAIN_LEASE;
			}
			List<GameObject> pocketed = ParentObject.Inventory?.GetObjectsDirect()?.FindAll(IsOurStone);
			if (pocketed != null && pocketed.Count > 0)
			{
				foreach (GameObject stone in pocketed)
				{
					bool refunded = Charges < MaxCharges(Level);
					if (refunded)
						Charges++;
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("EB", $"pocketed sculpted stone reabsorbed ({(refunded ? "charge refunded, now " + Charges : "reserve full, crumbled")})");
					stone.Destroy(null, Silent: true);
				}
				SyncAbilityName();
				if (ParentObject.IsPlayer())
					XDidY(ParentObject, "return", "the stone to the earth", Source: ParentObject);
			}
			if (Charges < MaxCharges(Level))
			{
				TurnsSinceLastCharge++;
				if (TurnsSinceLastCharge >= GetTurnsPerCharge())
				{
					Charges++;
					TurnsSinceLastCharge = 0;
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("EB", $"regen: +1 charge (now {Charges}/{MaxCharges(Level)}) after {GetTurnsPerCharge()} turns (WIL {ParentObject?.Stat("Willpower") ?? 0})");
					SyncAbilityName();
					if (Charges == 1 && CanGetStone(true) && ParentObject.GetFirstThrownWeapon() == null && IsMyActivatedAbilityToggledOn(ActivatedAbilityID))
					{
						Activate();
						XDidY(ParentObject, "retrieve", $"another {GetStoneName(Level)}", Source: ParentObject);
					}
				}
			}
			SyncAbilityName();
			SyncWhorl();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ReplaceThrownWeaponEvent E)
		{
			if (Retuning)
				return base.HandleEvent(E);
			if (E.PreviouslyEquipped != null && IsOurStone(E.PreviouslyEquipped)
				&& E.PreviouslyEquipped.GetPart<Temporary>() is Temporary flung)
				flung.Duration = Stat.Random(ROCK_LIFETIME_MIN, ROCK_LIFETIME_MAX);
			bool wasStone = E.PreviouslyEquipped != null
				&& (IsOurStone(E.PreviouslyEquipped) || E.PreviouslyEquipped.HasTag("Cleo_TerraFirma_IsAnyStone"));
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("EB", $"replace-thrown event: prev={E.PreviouslyEquipped?.Blueprint ?? "none"} (stone={wasStone}), toggle={IsMyActivatedAbilityToggledOn(ActivatedAbilityID)}, charges={Charges} -> {(wasStone && CanGetStone(true) && IsMyActivatedAbilityToggledOn(ActivatedAbilityID) ? "auto-sculpt replacement" : "no action")}");
			if (wasStone && CanGetStone(true) && IsMyActivatedAbilityToggledOn(ActivatedAbilityID))
			{
				Activate();
				XDidY(ParentObject, "retrieve", $"another {GetStoneName(Level)}", Source: ParentObject);
			}
			SyncAbilityName();
			SyncWhorl();
			return base.HandleEvent(E);
		}

		public bool CanGetStone(bool Silent = false)
		{
			if (!GameObject.Validate(ParentObject) || !ParentObject.IsActivatedAbilityUsable(ActivatedAbilityID))
				return false;
			if (Charges <= 0)
			{
				if (!Silent)
					ParentObject.Fail("You have no stones available to throw.");
				return false;
			}
			var thrownSlots = ParentObject.Body?.GetUnequippedPart("Thrown Weapon");
			if (thrownSlots == null || thrownSlots.Count == 0)
			{
				if (!Silent)
					ParentObject.Fail("You have no empty thrown weapon slots.");
				return false;
			}
			return true;
		}

		public void Activate()
		{
			Charges = Mathf.Max(0, Charges - 1);
			GameObject stone = MakeStone();
			string tile = "", thrownsound = "", impactsound = "";
			GetStoneCosmeticData(Level, ref tile, ref thrownsound, ref impactsound);
			stone.DisplayName = (Resonant ? "{{C|resonant}} " : "") + "sculpted " + GetStoneName(Level);
			stone.Render.Tile = tile;
			stone.SetStringProperty("ThrownSound", thrownsound);
			stone.SetStringProperty("ImpactSound", impactsound);
			ParentObject.ReceiveObject(stone);
			ParentObject.AutoEquip(stone, true, Silent: true);
			SyncAbilityName();
		}

		public GameObject MakeStone()
		{
			GameObject stone = GameObject.Create(Resonant ? BLUEPRINT_RESONANT : BLUEPRINT);
			string scaledDamage = GetThrowDamage(Level);
			if (stone.TryGetPart(out MeleeWeapon mw))
			{
				mw.BaseDamage = scaledDamage;
				mw.PenBonus = 0;
				if (!Resonant)
					mw.MaxStrengthBonus = GetPenetrationCap(Level);
			}
			if (stone.TryGetPart(out ThrownWeapon tw))
			{
				tw.Damage = scaledDamage;
				tw.PenetrationBonus = 0;
				if (!Resonant)
					tw.Penetration = GetPenetrationCap(Level);
			}
			return stone;
		}

		private void SyncAbilityName()
		{
			ActivatedAbilityEntry ent = MyActivatedAbility(ActivatedAbilityID);
			if (ent == null)
				return;
			bool armed = ParentObject?.FindEquippedItem(x => IsOurStone(x) && x.IsEquippedAsThrownWeapon()) != null;
			ent.DisplayName = $"Take Stones ({Charges}/{MaxCharges(Level)}{(armed ? "+1" : "")})";
		}

		public void OnStoneTaken(GameObject Stone)
		{
			if (!GameObject.Validate(ParentObject) || !GameObject.Validate(Stone))
				return;
			if (!IsOurStone(Stone) || !IsMyActivatedAbilityToggledOn(ActivatedAbilityID))
				return;
			if (ParentObject.GetFirstThrownWeapon() != null || (ParentObject.Body?.GetUnequippedPart("Thrown Weapon")?.Count ?? 0) == 0)
				return;
			ParentObject.AutoEquip(Stone, true, Silent: true);
			if (Stone.GetPart<Temporary>() is Temporary lease)
				lease.Duration = HELD_SUSTAIN_LEASE;
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("EB", $"pickup re-arm: {Stone.Blueprint} taken off the ground went straight back into the empty thrown slot");
			SyncAbilityName();
			SyncWhorl();
		}

		public int GetTurnsPerCharge()
		{
			return GetTurnsPerCharge(ParentObject?.Stat("Willpower") ?? 16);
		}

		public static int GetTurnsPerCharge(int Willpower)
		{
			int num = TURNS_PER_CHARGE;
			int pct = (Willpower - 16) * 5;
			if (pct != 0)
				num = num * (100 - pct) / 100;
			return Mathf.Clamp(num, TURNS_PER_CHARGE / 5, TURNS_PER_CHARGE * 5);
		}

		public int GetDeflectChance() => (3 * Charges) + Level;

		public static bool IsOurStone(GameObject Object) => Object?.Blueprint == BLUEPRINT || Object?.Blueprint == BLUEPRINT_RESONANT;

		public void RetuneHeldStone()
		{
			GameObject held = ParentObject?.FindEquippedItem(x => IsOurStone(x) && x.IsEquippedAsThrownWeapon());
			if (held == null)
				return;
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("EB", $"retune in place: {held.Blueprint} -> {(Resonant ? "resonant" : "normal")} at net-zero charge cost");
			Retuning = true;
			try
			{
				held.Obliterate(Silent: true);
			}
			finally
			{
				Retuning = false;
			}
			Charges++;
			Activate();
		}

		public void SyncWhorl()
		{
			if (!GameObject.Validate(ParentObject))
				return;
			if (Charges > 0)
			{
				if (!ParentObject.HasEffect<Cleo_TerraFirma_GravelWhorl>())
				{
					AssocWhorl = new Cleo_TerraFirma_GravelWhorl(GetDeflectChance());
					ParentObject.ApplyEffect(AssocWhorl);
				}
				AssocWhorl ??= ParentObject.GetEffect<Cleo_TerraFirma_GravelWhorl>();
				if (AssocWhorl != null)
					AssocWhorl.Sync(GetDeflectChance(), Charges, Resonant);
			}
			else
			{
				AssocWhorl ??= ParentObject.GetEffect<Cleo_TerraFirma_GravelWhorl>();
				if (AssocWhorl != null)
				{
					ParentObject.RemoveEffect(AssocWhorl);
					AssocWhorl = null;
				}
			}
		}

		private static readonly int[] LADDER_DICE = { 1, 1, 1, 1, 2, 2, 3, 3, 4, 4 };
		private static readonly int[] LADDER_FLAT = { 0, 1, 2, 3, 0, 1, 0, 1, 0, 1 };

		public static string GetThrowDamage(int Lv)
		{
			int dice, bonus;
			if (Lv <= 10)
			{
				int i = Mathf.Clamp(Lv, 1, 10) - 1;
				dice = LADDER_DICE[i];
				bonus = LADDER_FLAT[i];
			}
			else
			{
				dice = Mathf.FloorToInt(Lv / 3) + 1;
				bonus = Lv % 3;
			}
			return $"{dice}d6{(bonus != 0 ? $"+{bonus}" : "")}";
		}

		public static int GetPenetrationCap(int Lv)
		{
			return 1 + Lv / 3;
		}

		public static int MaxCharges(int Lv)
		{
			return (int)(3 + Mathf.Floor(Lv - 1));
		}

		public static string GetStoneName(int Lv)
		{
			if (Lv >= 10)
				return "boulder";
			else if (Lv >= 5)
				return "stone";
			return "pebble";
		}

		public static void GetStoneCosmeticData(int Lv, ref string Tile, ref string ThrownSound, ref string ImpactSound)
		{
			if (Lv >= 10)
			{
				Tile = "Items/sw_mediumboulder.bmp";
				ThrownSound = "Sounds/Throw/sfx_throwing_stone_large_throw";
				ImpactSound = "Sounds/Throw/sfx_throwing_stone_large_impact";
			}
			else if (Lv >= 5)
			{
				Tile = "Items/sw_stone_large.bmp";
				ThrownSound = "Sounds/Throw/sfx_throwing_stone_medium_throw";
				ImpactSound = "Sounds/Throw/sfx_throwing_stone_medium_impact";
			}
			else
			{
				Tile = "Items/sw_smallstone.bmp";
				ThrownSound = "Sounds/Throw/sfx_throwing_stone_small_throw";
				ImpactSound = "Sounds/Throw/sfx_throwing_stone_small_impact";
			}
		}
	}
}
