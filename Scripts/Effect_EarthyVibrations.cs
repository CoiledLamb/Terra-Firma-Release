using System;

namespace XRL.World.Effects
{
	[Serializable]
	public class Cleo_TerraFirma_EarthlyVibrations : Effect
	{
		public Cleo_TerraFirma_EarthlyVibrations()
		{
			Duration = 2;
			DisplayName = "{{y|earthly vibrations}}";
		}

		public Cleo_TerraFirma_EarthlyVibrations(int duration) : this()
		{
			Duration = duration;
		}

		public override string GetDetails()
		{
			return "Shaking from inside. Exact statistics visible to all creatures.";
		}

		public override bool UseStandardDurationCountdown() => true;
	}
}
