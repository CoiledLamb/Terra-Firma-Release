using System;
using XRL.World.Parts.Mutation;

namespace XRL.World.Effects
{
	[Serializable]
	public class Cleo_TerraFirma_CookingDomainCaveMoss_UnitEarthenBarrage : ProceduralCookingEffectUnitMutation<Cleo_TerraFirma_EarthenBarrage>
	{
		public Cleo_TerraFirma_CookingDomainCaveMoss_UnitEarthenBarrage()
		{
			AddedTier = "1-2";
			BonusTier = "2-3";
		}
	}

	[Serializable]
	public class Cleo_TerraFirma_CookingDomainCaveMoss_OnThrowStone : ProceduralCookingEffectWithTrigger
	{
		public const string THREW_STONE = "Cleo_TerraFirma_ThrewStone";

		public override string GetTriggerDescription()
		{
			return "whenever @thisCreature throw@s a stone, there's a 50% chance";
		}

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register(THREW_STONE);
			base.Register(Object, Registrar);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == THREW_STONE && 50.in100())
				Trigger();
			return base.FireEvent(E);
		}
	}

	[Serializable]
	public class Cleo_TerraFirma_CookingDomainCaveMoss_Pebble_ProceduralCookingTriggeredAction : ProceduralCookingTriggeredAction
	{
		public string PebbleBlueprint = "Cleo_TerraFirma_MossPebble";

		public override string GetDescription()
		{
			return "@they gain a sculpted pebble.";
		}

		public override void Apply(GameObject Subject)
		{
			if (Subject == null)
				return;
			GameObject pebble = GameObject.Create(PebbleBlueprint);
			Subject.ReceiveObject(pebble);
			if (Subject.GetFirstThrownWeapon() == null)
				Subject.AutoEquip(pebble, true, Silent: true);
		}
	}
}

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_StoneThrowSignal : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == AfterThrownEvent.ID;
		}

		public override bool HandleEvent(AfterThrownEvent E)
		{
			E.Actor?.FireEvent(Event.New(XRL.World.Effects.Cleo_TerraFirma_CookingDomainCaveMoss_OnThrowStone.THREW_STONE, "Object", ParentObject));
			return base.HandleEvent(E);
		}
	}
}
