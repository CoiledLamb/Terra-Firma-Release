using System;

namespace XRL.World.Effects
{
	[Serializable]
	public class Cleo_TerraFirma_StonehandsBannerEffect : Effect
	{
		public const string NAME = "{{w|rooted}}";

		public Cleo_TerraFirma_StonehandsBannerEffect()
		{
			Duration = 7;
			DisplayName = NAME;
		}

		public override int GetEffectType()
		{
			return 83886082;
		}

		public override string GetStateDescription()
		{
			return NAME;
		}

		public override string GetDetails()
		{
			return "+2 Willpower";
		}

		public override bool Apply(GameObject Object)
		{
			StatShifter.SetStatShift("Willpower", 2);
			return true;
		}

		public override void Remove(GameObject Object)
		{
			StatShifter.RemoveStatShifts();
			base.Remove(Object);
		}
	}
}
