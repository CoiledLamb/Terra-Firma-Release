using System;
using System.Collections.Generic;
using Cleo.TerraFirma.Scripts;
using XRL.UI;

namespace XRL.World.WorldBuilders
{
	[JoppaWorldBuilderExtension]
	public class Cleo_TerraFirma_WarrenPlacement : IJoppaWorldBuilderExtension
	{
		public const int WARRENS_PER_WORLD = 25;
		public const int ASSIGN_FL = 9;
		public const int ASSIGN_CAMP = 8;

		public override void OnAfterMutableInit(JoppaWorldBuilder Builder)
		{
			var stages = new List<string>();
			for (int i = 0; i < ASSIGN_FL; i++) stages.Add("flourishing");
			for (int i = 0; i < ASSIGN_CAMP; i++) stages.Add("camp");
			while (stages.Count < WARRENS_PER_WORLD) stages.Add("working");
			var rng = new Random(The.Game.GetWorldSeed("TFWARRENASSIGN"));
			for (int i = stages.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (stages[i], stages[j]) = (stages[j], stages[i]); }
			int placed = 0;
			Builder.AddMutableEncounterToTerrain("DesertCanyon", WARRENS_PER_WORLD,
				delegate(string zoneID, Genkit.Location2D location, XRL.World.Parts.TerrainTravel pTravel)
				{
					string stage = stages[Math.Min(placed, stages.Count - 1)];
					if (Options.ShowOverlandEncounters && pTravel != null)
					{
						pTravel.ParentObject.Render.RenderString = "Q";
						pTravel.ParentObject.Render.SetForegroundColor('y');
					}
					The.ZoneManager.AddZoneBuilder(zoneID, 6000, "Cleo_TerraFirma_WarrenMaker", "Stage", stage);
					WarrenSurvey.Register(zoneID);
					string secretID = Builder.AddSecret(zoneID, "a quarry warren",
						new string[3] { "stonecarver", "settlement", "humanoid" }, "Settlements");
					Builder.AddLocationFinder(zoneID, secretID);
					pTravel?.AddEncounter(new XRL.World.Parts.EncounterEntry(
						"You hear the ring of hammers on stone nearby. Would you like to investigate?",
						zoneID, "", secretID, Optional: true));
					Builder.worldInfo.friendlySettlements.Add(new GeneratedLocationInfo
					{
						name = "a quarry warren",
						targetZone = zoneID,
						zoneLocation = location,
						secretID = secretID
					});
					Builder.mutableMap.SetMutable(location, 0);
					placed++;
				});
			Helpers.VerifyLog("WARREN", $"placement (blind-claim): {placed} claims sited " +
				$"(assigned {ASSIGN_FL} fl / {ASSIGN_CAMP} camp / {WARRENS_PER_WORLD - ASSIGN_FL - ASSIGN_CAMP} working; attainment settles at visit)");
		}
	}
}
