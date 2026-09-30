using HarmonyLib;
using System;
using XRL;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;
using XRL.World.Skills.Cooking;

namespace Cleo.TerraFirma.Scripts.Patches
{
	[HarmonyPatch]
	class HarmonyPatches
	{
		[HarmonyPostfix]
		[HarmonyPatch(typeof(Scanning), nameof(Scanning.HasScanningFor))]
		[HarmonyPatch(new Type[] { typeof(GameObject), typeof(GameObject) })]
		static void Scanning_GetScanEpistemicStatus(GameObject who, GameObject obj, ref bool __result)
		{
			if (!__result && obj.HasPart<Cleo_TerraFirma_PartGeomancyScan>())
				__result = true;
		}

	}
}
