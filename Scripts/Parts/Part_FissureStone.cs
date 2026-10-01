using System;
using Cleo.TerraFirma.Scripts;
using XRL.World.Parts.Mutation;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_FissureStonePart : IGrenade
	{
		public int Level = 2;

		[NonSerialized]
		private bool detonating;

		public override bool SameAs(IPart p)
		{
			if (p is Cleo_TerraFirma_FissureStonePart other && other.Level != Level)
				return false;
			return base.SameAs(p);
		}

		protected override bool DoDetonate(Cell C, GameObject Actor = null, GameObject ApparentTarget = null, bool Indirect = false)
		{
			if (detonating)
				return true;
			detonating = true;
			try
			{
				PlayWorldSound(GetPropertyOrTag("DetonatedSound"), 1f, 0f, Combat: true);
				DidX("shatter", null, "!");
				GameObject actor = Indirect ? null : Actor;
				Helpers.VerifyLog("FSTONE", $"detonation @({C.X},{C.Y}): thrower={Actor?.Blueprint ?? "none"}, indirect={Indirect}, Lv{Level}, dice {Cleo_TerraFirma_Fissure.GetPulseDice(Level, 1)}/{Cleo_TerraFirma_Fissure.GetPulseDice(Level, 2)}/{Cleo_TerraFirma_Fissure.GetPulseDice(Level, 3)}");
				Cleo_TerraFirma_Fissure.RunPulses(C, actor, Actor ?? ParentObject, Level, "FSTONE");
				ParentObject.Destroy(null, Silent: true);
			}
			finally
			{
				detonating = false;
			}
			return true;
		}
	}
}
