using System;
using XRL.World.Anatomy;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_GaslightGauntlets : IPart
	{
		public string FistObject = "Cleo_TerraFirma_GaslightFist";
		public string ChargedName = "{{gaslight|gaslight}} gauntlets";
		public string UnchargedName = "gaslight gauntlets";
		public int PunchCharge = 100;
		public int CarveCharge = 1000;

		[NonSerialized]
		private bool Unequipping;

		public override bool SameAs(IPart p)
		{
			return false;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != EquippedEvent.ID && ID != UnequippedEvent.ID
				&& ID != PooledEvent<RegenerateDefaultEquipmentEvent>.ID)
				return ID == PooledEvent<GetDisplayNameEvent>.ID;
			return true;
		}

		public bool CanPay(int Charge)
		{
			return ParentObject.TestCharge(Charge, LiveOnly: false, 0L);
		}

		public override bool HandleEvent(GetDisplayNameEvent E)
		{
			if (E.DB.PrimaryBase == UnchargedName && CanPay(PunchCharge))
				E.ReplacePrimaryBase(ChargedName);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EquippedEvent E)
		{
			E.Actor?.Body?.UpdateBodyParts();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(UnequippedEvent E)
		{
			Unequipping = true;
			try
			{
				E.Actor?.Body?.UpdateBodyParts();
			}
			finally
			{
				Unequipping = false;
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(RegenerateDefaultEquipmentEvent E)
		{
			if (!Unequipping && E.Body != null && ParentObject.Equipped == E.Body.ParentObject && ParentObject.IsEquippedProperly())
				ApplyFists(E.Body);
			return base.HandleEvent(E);
		}

		private void ApplyFists(Body Body)
		{
			string dependency = ParentObject.EquippedOn()?.DependsOn;
			if (string.IsNullOrEmpty(dependency))
				return;
			foreach (BodyPart hand in Body.GetPart("Hand"))
			{
				if (hand.Extrinsic || hand.SupportsDependent != dependency)
					continue;
				if (hand.DefaultBehavior?.Blueprint == FistObject)
					continue;
				hand.DefaultBehavior?.Obliterate();
				hand.DefaultBehavior = GameObject.Create(FistObject);
				hand.DefaultBehavior.SetStringProperty("TemporaryDefaultBehavior", "Cleo_TerraFirma_GaslightGauntlets");
			}
		}

		public static GameObject FindWorn(GameObject Actor)
		{
			if (Actor == null)
				return null;
			foreach (GameObject item in Actor.GetEquippedObjectsReadonly())
			{
				if (item.HasPart<Cleo_TerraFirma_GaslightGauntlets>() && item.IsEquippedProperly())
					return item;
			}
			return null;
		}
	}

	[Serializable]
	public class Cleo_TerraFirma_GaslightFist : IPart
	{
		public int PenetrationBonus = 5;

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade))
				return ID == GetWeaponMeleePenetrationEvent.ID;
			return true;
		}

		public override bool HandleEvent(GetWeaponMeleePenetrationEvent E)
		{
			if (E.Weapon == ParentObject && E.Attacker != null)
			{
				GameObject worn = Cleo_TerraFirma_GaslightGauntlets.FindWorn(E.Attacker);
				var gauntlets = worn?.GetPart<Cleo_TerraFirma_GaslightGauntlets>();
				if (gauntlets != null && worn.UseCharge(gauntlets.PunchCharge, LiveOnly: false, 0L))
				{
					E.PenetrationBonus += PenetrationBonus;
					E.MaxPenetrationBonus += PenetrationBonus;
				}
			}
			return base.HandleEvent(E);
		}
	}
}
