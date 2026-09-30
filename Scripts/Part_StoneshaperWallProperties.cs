using System;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_StoneshaperWallProperties : IPart
	{
		public string MaterialType;
		public bool Natural;

		public static bool IsNaturalStone(Cleo_TerraFirma_StoneshaperWallProperties props)
		{
			return props != null && props.MaterialType == "stone" && props.Natural;
		}
		public string MaterialDisplayName;
		public string StatName;
		public int StatShift;
		public string MutationName;
		public int MutationLevel;
	}
}
