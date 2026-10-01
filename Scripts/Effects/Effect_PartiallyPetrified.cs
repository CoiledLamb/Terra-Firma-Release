using System;

namespace XRL.World.Effects
{
	[Serializable]
	public class Cleo_TerraFirma_PartiallyPetrified : Effect
	{
		public const int DURATION = 500;
		public const int MOVE_SPEED_PENALTY = 20;
		public const int QUICKNESS_PENALTY = 10;
		public const int AV_BONUS = 2;

		public Cleo_TerraFirma_PartiallyPetrified()
		{
			Duration = DURATION;
			DisplayName = "{{y|partially petrified}}";
		}

		public override string GetDetails()
		{
			return $"Partly stone. -{MOVE_SPEED_PENALTY} move speed. -{QUICKNESS_PENALTY} Quickness. +{AV_BONUS} AV.";
		}

		public override bool UseStandardDurationCountdown() => true;

		public override bool Render(RenderEvent E)
		{
			if (Duration > 0)
				E.DetailColor = "y";
			return base.Render(E);
		}

		public override bool Apply(GameObject Object)
		{
			if (Object.TryGetEffect<Cleo_TerraFirma_PartiallyPetrified>(out var existing))
			{
				existing.Duration = DURATION;
				return false;
			}
			return base.Apply(Object);
		}

		public override void Remove(GameObject Object)
		{
			StatShifter.RemoveStatShifts();
			base.Remove(Object);
		}

		public override bool WantEvent(int ID, int Cascade)
		{
			if (!base.WantEvent(ID, Cascade))
				return ID == SingletonEvent<EndTurnEvent>.ID;
			return true;
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			StatShifter.SetStatShift("MoveSpeed", MOVE_SPEED_PENALTY);
			StatShifter.SetStatShift("Speed", -QUICKNESS_PENALTY);
			StatShifter.SetStatShift("AV", AV_BONUS);
			return base.HandleEvent(E);
		}
	}
}
