using System;
using XRL.World.Capabilities;
using XRL.World.Parts.Mutation;

namespace XRL.World.Effects
{
	[Serializable]
	public class Cleo_TerraFirma_Petrifying : Effect
	{
		private const string STUCK_MESSAGE = "You are turning to stone and stuck in place!";

		public GameObject Initiator;

		public bool Receding;

		public bool Pinned = true;

		public bool SlowCleared;

		public bool Active => Duration > 0 && !Receding;

		public Cleo_TerraFirma_Petrifying()
		{
			Duration = 10;
			DisplayName = "{{y|petrifying}}";
		}

		public Cleo_TerraFirma_Petrifying(GameObject Initiator) : this()
		{
			this.Initiator = Initiator;
		}

		public override string GetDetails()
		{
			if (Active)
				return $"Turning to stone.{(Pinned ? " Held in place." : "")}{(SlowCleared ? "" : $" -{Duration + 1} QN")}";
			return $"Recovering from partial petrification.{(SlowCleared ? "" : $" -{Duration + 1} QN")}";
		}

		public override bool UseStandardDurationCountdown() => !Active;

		public override bool Apply(GameObject Object)
		{
			if (!Object.CanChangeMovementMode("Stuck", Involuntary: true, FrozenOkay: true))
				return false;
			XDidY(Object, "begin", "turning to stone", "!", ColorAsBadFor: Object, Source: Object);
			Object.ParticleText("*petrifying*", ConsequentialColorChar(Initiator ?? null, Object));
			Object.MovementModeChanged("Stuck", true);
			if (Object.IsFlying)
				Flight.Fall(Object);
			return base.Apply(Object);
		}

		public override void Remove(GameObject Object)
		{
			RemoveStatShifts();
			base.Remove(Object);
		}

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("CanChangeBodyPosition");
			Registrar.Register("IsMobile");
			Registrar.Register("LeaveCell");
			Registrar.Register("MovementModeChanged");
			base.Register(Object, Registrar);
		}

		public override bool WantEvent(int ID, int Cascade)
		{
			if (!base.WantEvent(ID, Cascade) &&
				ID != PooledEvent<CanChangeMovementModeEvent>.ID &&
				ID != PooledEvent<GetDisplayNameEvent>.ID &&
				ID != SingletonEvent<EndTurnEvent>.ID)
				return ID == GetNavigationWeightEvent.ID;
			return true;
		}

		public override bool HandleEvent(CanChangeMovementModeEvent E)
		{
			if (Active && Pinned)
			{
				if (E.ShowMessage)
					E.Object.Fail(STUCK_MESSAGE);
				return false;
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetNavigationWeightEvent E)
		{
			if (!E.Flying && Active && Pinned)
			{
				E.Uncacheable = true;
				E.MinWeight(100);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetDisplayNameEvent E)
		{
			if (!E.Reference)
				E.AddTag("[{{y|petrifying}}]", 20);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			if (!Receding && (!GameObject.Validate(Initiator) || Initiator.CurrentZone != base.Object?.CurrentZone))
			{
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("GAZE", "DEAD-GORGON RELEASE: initiator invalid or in another zone mid-gaze, effect self-recedes (the trap fix firing)");
				UpdateData(null, true);
			}
			RefreshDisplayName();
			UpdateStatShifts();
			return base.HandleEvent(E);
		}

		public override bool FireEvent(Event E)
		{
			if (Active && Pinned)
			{
				if (E.ID == "IsMobile")
				{
					if (!Object.IsTryingToJoinPartyLeader())
						return false;
				}
				else if (E.ID == "LeaveCell")
				{
					if (!E.HasFlag("Forced") && E.GetStringParameter("Type") != "Teleporting" && !Object.IsTryingToJoinPartyLeader())
					{
						AddPlayerMessage(STUCK_MESSAGE);
						Object.UseEnergy(1000);
						return false;
					}

				}
				else if (E.ID == "CanChangeBodyPosition")
				{
					if (E.HasFlag("ShowMessage") && Object.IsPlayer())
						Object.Fail(STUCK_MESSAGE);
					return false;
				}

			}
			return base.FireEvent(E);
		}

		public void UpdateData(int? NewDuration = null, bool ShouldRecede = false)
		{
			if (NewDuration != null)
			{
				Duration = Math.Min(100, (int)NewDuration);
			}
			Receding = ShouldRecede;
			if (Receding)
				Pinned = false;
			UpdateStatShifts();
		}

		public void ApplyFailure()
		{
			Pinned = true;
			SlowCleared = false;
			Duration = Math.Min(100, Duration + 10);
			UpdateStatShifts();
		}

		public void ReleasePin()
		{
			Pinned = false;
		}

		public void ClearSlow()
		{
			SlowCleared = true;
			UpdateStatShifts();
		}

		private void RefreshDisplayName()
		{
			DisplayName = "{{y|petrifying}} (" + (!Active ? "receding" : (Pinned ? "held" : "slipping")) + (SlowCleared ? "" : ", -" + Duration + " QN") + ")";
		}

		private void UpdateStatShifts()
		{
			StatShifter.SetStatShift("Speed", SlowCleared ? 0 : -Duration);
		}

		private void RemoveStatShifts()
		{
			StatShifter.RemoveStatShifts();
		}
	}
}
