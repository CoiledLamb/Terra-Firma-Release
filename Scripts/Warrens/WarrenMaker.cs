using Cleo.TerraFirma.Scripts;
using XRL;
using XRL.Messages;
using XRL.Wish;

namespace XRL.World.ZoneBuilders
{
	public class Cleo_TerraFirma_WarrenMaker
	{
		public string Stage;

		public bool BuildZone(Zone Z)
		{
			try
			{
				int? forced = Stage == "flourishing" ? 2 : Stage == "camp" ? 1 : Stage == "working" ? 0 : (int?)null;
				var r = forced.HasValue
					? WarrenCarver.BuildAugmented(Z, WarrenCarver.SeedFor(Z.ZoneID), forced.Value)
					: WarrenCarver.Build(Z, WarrenCarver.SeedFor(Z.ZoneID));
				WarrenSurvey.Record(Z.ZoneID, r);
				if (r.Built)
					Helpers.VerifyLog("WARREN", $"builder done: {Z.ZoneID} -> {r.Stage}, {r.Occupancy}, attempt {r.Attempt}, {r.Layout}");
				else
					SettleRumor(Z);
			}
			catch (System.Exception x)
			{
				Helpers.VerifyLog("WARREN", $"EXCEPTION in builder for {Z.ZoneID}: {x}");
				MetricsManager.LogException("Cleo_TerraFirma_WarrenMaker", x);
				WarrenSurvey.Record(Z.ZoneID, new WarrenCarver.Result
				{
					Reason = "exception: " + x.GetType().Name,
					Seed = WarrenCarver.SeedFor(Z.ZoneID)
				});
				SettleRumor(Z);
			}
			return true;
		}

		private static void SettleRumor(Zone Z)
		{
			Qud.API.JournalMapNote note = null;
			foreach (var n in Qud.API.JournalAPI.MapNotes)
				if (n.ZoneID == Z.ZoneID && n.Attributes != null && n.Attributes.Contains("stonecarver")) { note = n; break; }
			if (note == null) return;
			Qud.API.JournalAPI.DeleteMapNote(note);
			Cell c00 = Z.GetCell(0, 0);
			if (c00 != null)
			{
				foreach (GameObject o in new System.Collections.Generic.List<GameObject>(c00.GetObjectsWithPart("LocationFinder")))
				{
					var lf = o.GetPart<XRL.World.Parts.LocationFinder>();
					if (lf != null && Qud.API.JournalAPI.GetMapNote(lf.ID) == null)
						o.Obliterate();
				}
			}
			Helpers.VerifyLog("WARREN", $"rumor erased: {Z.ZoneID} stops existing as a destination");
		}
	}

}
