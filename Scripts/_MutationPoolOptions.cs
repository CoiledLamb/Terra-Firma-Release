using XRL;
using XRL.UI;

namespace Cleo.TerraFirma.Scripts
{
	[HasModSensitiveStaticCache]
	[HasCallAfterGameLoaded]
	public static class MutationPoolOptions
	{
		static readonly string[,] Gates =
		{
			{ "Cleo_TerraFirma_PoolGeomancy", "Geomancy" },
			{ "Cleo_TerraFirma_PoolCalcify", "Calcify" },
			{ "Cleo_TerraFirma_PoolEarthenBarrage", "Earthen Barrage" },
			{ "Cleo_TerraFirma_PoolStoneshaper", "Stoneshaper" },
			{ "Cleo_TerraFirma_PoolGorgonsGaze", "Gorgon's Gaze" },
			{ "Cleo_TerraFirma_PoolFissure", "Fissure" },
		};

		[PreGameCacheInit]
		[CallAfterGameLoaded]
		public static void Stamp()
		{
			MutationFactory.CheckInit();
			for (int i = 0; i < Gates.GetLength(0); i++)
			{
				var entry = MutationFactory.GetMutationEntryByName(Gates[i, 1]);
				if (entry == null)
					continue;
				bool off = Options.GetOption(Gates[i, 0]).EqualsNoCase("No");
				entry.Hidden = off;
				entry.ExcludeFromPool = off;
				if (off)
					Helpers.VerifyLog("POOL", $"{Gates[i, 1]} gated out of mutation pools ({Gates[i, 0]}=No)");
			}
		}
	}
}
