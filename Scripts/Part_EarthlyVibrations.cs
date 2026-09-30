using Cleo.TerraFirma.Scripts;
using System;
using XRL.World.Effects;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_PartGeomancyScan : IPart
	{
		private const string DEBUG_CONTEXT = "GMS";

		public int Duration;

		public Cleo_TerraFirma_PartGeomancyScan()
		{
			Duration = 2;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade))
				return ID == SingletonEvent<EndTurnEvent>.ID;
			return true;
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			Duration--;
			Helpers.DebugLog($"earthly vibrations on {ParentObject.DisplayName}: {Duration}", DEBUG_CONTEXT);
			if (Duration <= 0)
			{
				ParentObject.RemovePart(this);
				Helpers.DebugLog($"-> removing…", DEBUG_CONTEXT);
			}
			else
			{
				if (ParentObject.IsCreature)
				{
					Helpers.DebugLog($"-> is creature…", DEBUG_CONTEXT);
					if (ParentObject.TryGetEffect<Cleo_TerraFirma_EarthlyVibrations>(out var vibin))
					{
						vibin.Duration = 2;
						Helpers.DebugLog($"--> maintaining effect", DEBUG_CONTEXT);
					}
					else
					{
						ParentObject.ApplyEffect(new Cleo_TerraFirma_EarthlyVibrations());
						Helpers.DebugLog($"--> adding inner vibrations effect", DEBUG_CONTEXT);
					}
				}
				else
					Helpers.DebugLog($"-> is not creature", DEBUG_CONTEXT);
			}
			return base.HandleEvent(E);
		}
	}
}
