using System;
using XRL.World.AI;

namespace XRL.World.Effects
{
	[Serializable]
	public class Cleo_TerraFirma_Enamored : Effect
	{
		public GameObject Beloved;

		public Cleo_TerraFirma_Enamored()
		{
			DisplayName = "{{R|enamored}}";
		}

		public Cleo_TerraFirma_Enamored(int Duration, GameObject Beloved)
			: this()
		{
			base.Duration = Duration;
			this.Beloved = Beloved;
		}

		public override bool UseStandardDurationCountdown()
		{
			return true;
		}

		public override int GetEffectType()
		{
			return 2;
		}

		public override string GetDetails()
		{
			return "Temporarily allied to " + (Beloved?.t() ?? "someone") + ".";
		}

		public override bool Apply(GameObject Object)
		{
			if (Object?.Brain == null || !GameObject.Validate(ref Beloved))
				return false;
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("LOVE", $"enamored: {Object.Blueprint} now allied to {Beloved.Blueprint} for {Duration} rounds");
			Object.Brain.SetAlliedLeader<AllySummon>(Beloved, 0, Silent: false);
			return true;
		}

		public override void Remove(GameObject Object)
		{
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("LOVE", $"enamored REVERT: {Object?.Blueprint ?? "?"} allegiance to {Beloved?.Blueprint ?? "?"} removed, old grudges resume");
			if (Object?.Brain != null && GameObject.Validate(ref Beloved))
				Object.Brain.RemoveAllegiance<AllySummon>(Beloved);
			base.Remove(Object);
		}
	}
}
