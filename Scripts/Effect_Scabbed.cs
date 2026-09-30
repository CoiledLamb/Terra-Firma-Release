using System;
using XRL.World.Parts;

namespace XRL.World.Effects
{
	[Serializable]
	public class Cleo_TerraFirma_Scabbed : Effect
	{
		public int Stacks;
		public int Cap = 2;
		public int FlakeInterval = 20;
		public int Countdown;

		public Cleo_TerraFirma_Scabbed()
		{
			Duration = 1;
			DisplayName = "{{w|scabbed}}";
		}

		public override string GetDetails()
		{
			return "+" + Stacks + " AV.";
		}

		public void Harden()
		{
			if (Stacks == 0)
				IComponent<GameObject>.EmitMessage(Object, Object.Poss("girdle") + " hardens.");
			if (Stacks < Cap)
				Stacks++;
			Countdown = FlakeInterval;
			Sync();
		}

		private void Sync()
		{
			StatShifter.SetStatShift("AV", Stacks);
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
			if (!WearingGirdle())
			{
				Object.RemoveEffect(this);
				return base.HandleEvent(E);
			}
			if (--Countdown <= 0)
			{
				Stacks--;
				Countdown = FlakeInterval;
				if (Stacks <= 0)
				{
					IComponent<GameObject>.EmitMessage(Object, Object.IsPlayer()
						? "The last of the mud flakes off."
						: "The last of the mud flakes off " + Object.poss("girdle") + ".");
					Object.RemoveEffect(this);
					return base.HandleEvent(E);
				}
			}
			Sync();
			return base.HandleEvent(E);
		}

		private bool WearingGirdle()
		{
			foreach (GameObject item in Object.GetEquippedObjectsReadonly())
			{
				if (item.HasPart<Cleo_TerraFirma_MudskinGirdle>() && item.IsEquippedProperly())
					return true;
			}
			return false;
		}
	}
}
