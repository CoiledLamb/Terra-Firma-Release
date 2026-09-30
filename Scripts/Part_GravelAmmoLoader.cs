using System;
using System.Collections.Generic;
using Cleo.TerraFirma.Scripts;
using XRL.UI;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_GravelAmmoLoader : IPoweredPart
	{
		public string StoneBlueprints = "Small Stone,Large Stone";
		public string ProjectileObject = "Cleo_TerraFirma_GravelProjectile";
		public int WeightPerBurst = 5;
		public int ReloadEnergy = 1000;
		public int RemainingWeight;
		public int LoadedWeight;
		private const string LOAD = "Cleo_TerraFirma_LoadGravelShotgun";

		public Cleo_TerraFirma_GravelAmmoLoader()
		{
			ChargeUse = 0;
			IsBootSensitive = false;
			IsEMPSensitive = false;
			IsPowerSwitchSensitive = false;
			NameForStatus = "FiringMechanism";
			WorksOnEquipper = true;
		}

		public int Bursts => WeightPerBurst > 0 ? RemainingWeight / WeightPerBurst : 0;
		public override bool SameAs(IPart p) => false;
		public override bool AllowStaticRegistration() => true;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == CommandReloadEvent.ID || ID == NeedsReloadEvent.ID
				|| ID == GetInventoryActionsEvent.ID || ID == InventoryActionEvent.ID
				|| ID == GetExtrinsicWeightEvent.ID || ID == GetDisplayNameEvent.ID
				|| ID == GetAmmoCountAvailableEvent.ID || ID == CheckLoadAmmoEvent.ID
				|| ID == LoadAmmoEvent.ID || ID == CheckReadyToFireEvent.ID
				|| ID == GetNotReadyToFireMessageEvent.ID || ID == AIWantUseWeaponEvent.ID
				|| ID == GetMissileWeaponStatusEvent.ID || ID == GetProjectileBlueprintEvent.ID
				|| ID == GetMissileWeaponProjectileEvent.ID || ID == StripContentsEvent.ID;
		}

		public bool Accepts(GameObject stone)
		{
			if (stone == null || WeightPerBurst <= 0) return false;
			int weight = stone.GetPart<Physics>()?.Weight ?? 0;
			if (weight < WeightPerBurst || weight % WeightPerBurst != 0) return false;
			foreach (string blueprint in StoneBlueprints.Split(','))
				if (stone.Blueprint == blueprint.Trim()) return true;
			return false;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (RemainingWeight == 0) E.AddAction("Load Stone", "load stone", LOAD, null, 'o');
			return base.HandleEvent(E);
		}
		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == LOAD && E.Actor != null)
				CommandReloadEvent.Execute(E.Actor, ParentObject, null, FreeAction: false, FromDialog: true);
			return base.HandleEvent(E);
		}
		public override bool HandleEvent(NeedsReloadEvent E)
		{
			if (E.Skip != this && (E.Weapon == null || E.Weapon == ParentObject)
				&& RemainingWeight == 0 && ParentObject.IsEquippedProperly()) return false;
			return base.HandleEvent(E);
		}
		public override bool HandleEvent(CommandReloadEvent E)
		{
			if (E.Pass < 2 || E.Actor == null || (E.Weapon != null && E.Weapon != ParentObject)
				|| E.CheckedForReload.Contains(this)) return base.HandleEvent(E);
			E.CheckedForReload.Add(this);
			if (E.MinimumCharge > 0 || (E.Weapon != ParentObject && !ParentObject.IsEquippedProperly())
				|| !(ParentObject.GetPart<MissileWeapon>()?.FiresManually ?? true)) return base.HandleEvent(E);
			if (RemainingWeight > 0)
			{
				if (E.Weapon == ParentObject && E.Actor.IsPlayer()) Popup.ShowFail("The chamber still holds gravel.");
				return base.HandleEvent(E);
			}
			E.NeededReload.Add(this);
			var stones = new List<GameObject>();
			foreach (GameObject item in E.Actor.GetInventory()) if (Accepts(item)) stones.Add(item);
			if (stones.Count == 0)
			{
				if (E.Actor.IsPlayer()) Popup.ShowFail("You have no stone that fits the chamber.");
				return base.HandleEvent(E);
			}
			GameObject chosen = stones[0];
			if (E.Actor.IsPlayer() && stones.Count > 1)
				chosen = PickItem.ShowPicker(stones, null, PickItem.PickItemDialogStyle.SelectItemDialog, E.Actor);
			if (chosen == null || !chosen.ConfirmUseImportant(E.Actor, "grind")) return base.HandleEvent(E);
			E.TriedToReload.Add(this);
			ParentObject.SplitFromStack();
			GameObject stone = chosen.SplitFromStack() ?? chosen;
			int weight = stone.GetPart<Physics>().Weight;
			var remove = Event.New("CommandRemoveObject");
			remove.SetParameter("Object", stone);
			remove.SetFlag("ForEquip", State: true);
			if (!E.Actor.FireEvent(remove)) { stone.CheckStack(); return base.HandleEvent(E); }
			stone.Obliterate();
			RemainingWeight = LoadedWeight = weight;
			FlushTransientCaches();
			ParentObject.FlushWeightCaches();
			E.Reloaded.Add(this);
			if (!E.ObjectsReloaded.Contains(ParentObject)) E.ObjectsReloaded.Add(ParentObject);
			E.EnergyCost(ReloadEnergy);
			PlayWorldSound(ParentObject.GetPropertyOrTag("ReloadSound"));
			if (E.Actor.IsPlayer()) IComponent<GameObject>.AddPlayerMessage("You load a stone into " + ParentObject.t() + ".");
			Helpers.VerifyLog("GRAVEL", $"loaded weight={weight}, bursts={Bursts}");
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetExtrinsicWeightEvent E)
		{
			E.Weight += RemainingWeight;
			return base.HandleEvent(E);
		}
		public override bool HandleEvent(GetAmmoCountAvailableEvent E)
		{
			E.Register(Bursts);
			return base.HandleEvent(E);
		}
		private bool Ready() => Bursts > 0 && !IsDisabled(UseCharge: false);
		public override bool HandleEvent(CheckLoadAmmoEvent E)
		{
			if (!Ready()) { E.Message = Bursts == 0 ? "The gravel chamber is empty." : "The grinder shotgun cannot fire."; return false; }
			return base.HandleEvent(E);
		}
		public override bool HandleEvent(CheckReadyToFireEvent E) => Ready() && base.HandleEvent(E);
		public override bool HandleEvent(GetNotReadyToFireMessageEvent E)
		{
			if (Bursts == 0) E.Message = "The gravel chamber is empty.";
			return base.HandleEvent(E);
		}
		public override bool HandleEvent(LoadAmmoEvent E)
		{
			if (!Ready()) { E.Message = "The grinder shotgun cannot fire."; return false; }
			GameObject projectile = GameObject.Create(ProjectileObject, 0, 0, null, null, null, "Projectile");
			if (projectile == null) return false;
			E.Projectile = projectile;
			RemainingWeight -= WeightPerBurst;
			FlushTransientCaches();
			ParentObject.FlushWeightCaches();
			Helpers.VerifyLog("GRAVEL", $"fired burst, weight={RemainingWeight}, bursts={Bursts}");
			return base.HandleEvent(E);
		}
		public override bool HandleEvent(AIWantUseWeaponEvent E)
		{
			if (IsDisabled(UseCharge: false)) return false;
			if (Bursts == 0)
			{
				bool available = false;
				if (E.Actor != null) foreach (GameObject item in E.Actor.GetInventory()) if (Accepts(item)) { available = true; break; }
				if (!available) return false;
			}
			return base.HandleEvent(E);
		}
		public override bool HandleEvent(GetMissileWeaponStatusEvent E)
		{
			if (E.Override == null)
			{
				if (E.Status != null) { E.Status.ammoRemaining = Bursts; E.Status.ammoTotal = WeightPerBurst > 0 ? LoadedWeight / WeightPerBurst : 0; }
				E.Items.Append(Bursts == 0 ? " [{{K|empty}}]" : " [" + Bursts + "]");
			}
			return base.HandleEvent(E);
		}
		public override bool HandleEvent(GetProjectileBlueprintEvent E) { E.Blueprint = ProjectileObject; return base.HandleEvent(E); }
		public override bool HandleEvent(GetMissileWeaponProjectileEvent E) { E.Blueprint = ProjectileObject; return false; }
		public override bool HandleEvent(GetDisplayNameEvent E)
		{
			if (E.Understood())
			{
				if (E.Cutoff >= 1100)
					E.AddMissileWeaponDamageTag(GetMissileWeaponPerformanceEvent.GetFor(ParentObject.Equipped, ParentObject));
				if (!E.Reference)
					E.AddTag(Bursts == 0 ? "{{y|[{{K|empty}}]}}"
						: "{{y|[gravel burst x" + Bursts + "]}}", -5);
			}
			return base.HandleEvent(E);
		}
		public override bool HandleEvent(StripContentsEvent E)
		{
			RemainingWeight = LoadedWeight = 0;
			FlushTransientCaches();
			ParentObject.FlushWeightCaches();
			return base.HandleEvent(E);
		}
	}
}
