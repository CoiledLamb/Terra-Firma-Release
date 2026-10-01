using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.Wish;

namespace Cleo.TerraFirma.Scripts
{
	public static class WarrenSurvey
	{
		private const string StateKey = "Cleo_TerraFirma_WarrenSurvey_Staged_v1";

		private static Dictionary<string, WarrenCarver.Result> Read()
		{
			string json = The.Game.GetStringGameState(StateKey);
			return string.IsNullOrEmpty(json)
				? new Dictionary<string, WarrenCarver.Result>()
				: JsonConvert.DeserializeObject<Dictionary<string, WarrenCarver.Result>>(json);
		}

		private static void Write(Dictionary<string, WarrenCarver.Result> records)
			=> The.Game.SetStringGameState(StateKey, JsonConvert.SerializeObject(records));

		public static void Register(string zoneID)
		{
			if (!Helpers.VERIFY_WATCH) return;
			var records = Read();
			if (records.ContainsKey(zoneID)) return;
			records.Add(zoneID, null);
			Write(records);
		}

		public static void Record(string zoneID, WarrenCarver.Result result)
		{
			if (!Helpers.VERIFY_WATCH) return;
			var records = Read();
			records[zoneID] = result;
			Write(records);
		}

		internal static string Summarize(Dictionary<string, WarrenCarver.Result> records)
		{
			var measured = records.Values.Where(r => r != null).ToList();
			var built = measured.Where(r => r.Built).ToList();
			var failed = measured.Where(r => !r.Built).ToList();
			var text = new StringBuilder("Warren survey: restored generation (staged claims use augmentation)\n");
			text.AppendLine($"Registered claims: {records.Count}; measured: {measured.Count}; unmeasured: {records.Count - measured.Count}");
			text.AppendLine($"Built: {built.Count}; failed: {failed.Count}" +
				(measured.Count > 0 ? $"; success: {100.0 * built.Count / measured.Count:0.0}%" : ""));
			if (built.Count > 0)
			{
				text.AppendLine($"Stages: working {built.Count(r => r.Stage == "working")}, camp {built.Count(r => r.Stage == "camp")}, flourishing {built.Count(r => r.Stage == "flourishing")}");
				text.AppendLine($"Layouts: boxes {built.Count(r => r.Layout == "boxes")}, blocks {built.Count(r => r.Layout == "blocks")}");
				var natural = built.Where(r => r.BuildMethod == "natural").ToList();
				var augmented = built.Where(r => r.BuildMethod == "augmented").ToList();
				var rebuilt = built.Where(r => r.BuildMethod == "rebuild").ToList();
				text.AppendLine($"Build methods: natural {natural.Count}, augmented {augmented.Count}, rebuild {rebuilt.Count}, unknown {built.Count - natural.Count - augmented.Count - rebuilt.Count}");
				if (augmented.Count > 0)
				{
					text.AppendLine($"Augmented sites: cells mean {augmented.Average(r => r.AugmentCells):0.0}, total {augmented.Sum(r => r.AugmentCells)}, max {augmented.Max(r => r.AugmentCells)}");
					text.AppendLine($"Winning growth rounds: mean {augmented.Average(r => r.WinningAugmentRounds):0.0}, max {augmented.Max(r => r.WinningAugmentRounds)}");
				}
				var known = built.Where(r => r.BuildMethod != null).ToList();
				if (known.Count > 0)
					text.AppendLine($"Growth rounds explored: {known.Sum(r => r.ExploredAugmentRounds)} across {known.Count} measured successes (includes discarded growth).");
				if (rebuilt.Count > 0)
					text.AppendLine($"Rebuild footprint: {rebuilt.Sum(r => r.AugmentCells)} cells total, including existing rock.");
				text.AppendLine("Natural = no added rock in the winning plan. Older results have unknown methods; details are logged per zone.");
				text.AppendLine($"Floor cells: mean {built.Average(r => r.Floor):0.0}, min {built.Min(r => r.Floor)}, max {built.Max(r => r.Floor)}");
				text.AppendLine($"Rooms: mean {built.Average(r => r.Pockets):0.0}, min {built.Min(r => r.Pockets)}, max {built.Max(r => r.Pockets)}");
				text.AppendLine($"Aquifer sites: {built.Count(r => r.AquiferPools > 0)}/{built.Count}; pools {built.Sum(r => r.AquiferPools)}, veins {built.Sum(r => r.AquiferVeins)}");
				text.AppendLine("Aquifer gate: 20% by default in XML; terrain and failed sites affect observed frequency.");
			}
			foreach (var group in failed.GroupBy(r => r.Reason.Split('(')[0].Trim()).OrderByDescending(g => g.Count()))
				text.AppendLine($"Failure: {group.Key}: {group.Count()}");
			text.AppendLine("Unmeasured claims are excluded from rates. Existing unrecorded zones are never recarved.");
			text.Append("Use a fresh world for a complete sample. Generation counts do not verify playability.");
			return text.ToString();
		}

		internal static void RunSurvey(bool showReport)
		{
			if (The.Game == null) return;
			foreach (var note in Qud.API.JournalAPI.MapNotes.ToList())
				if (note.Attributes != null && note.Attributes.Contains("stonecarver")) Register(note.ZoneID);
			var records = Read();
			if (records.Count == 0)
			{
				if (showReport) Popup.Show("tfwarrenstats: no registered claims or stonecarver map notes in this world.");
				return;
			}
			MessageQueue.AddPlayerMessage("tfwarrenstats: building unvisited claims. This may take a moment.");
			int generated = 0, errors = 0;
			var timer = System.Diagnostics.Stopwatch.StartNew();
			foreach (var pair in records.OrderBy(p => p.Key, StringComparer.Ordinal))
			{
				if (pair.Value != null || The.ZoneManager.IsZoneBuilt(pair.Key)) continue;
				try
				{
					The.ZoneManager.GetZone(pair.Key);
					generated++;
				}
				catch (Exception ex)
				{
					errors++;
					MetricsManager.LogException("TerraFirma warren survey: " + pair.Key, ex);
				}
			}
			timer.Stop();
			records = Read();
			foreach (var pair in records.OrderBy(p => p.Key, StringComparer.Ordinal))
				Helpers.VerifyLog("WARREN-STATS", pair.Key + " " + JsonConvert.SerializeObject(pair.Value));
			string report = Summarize(records) + $"\nThis survey: {generated} zones loaded, {errors} load errors, {timer.Elapsed.TotalSeconds:0.0}s. Details in Player.log.";
			Helpers.VerifyLog("WARREN-STATS", report);
			if (showReport) Popup.Show(report);
		}
	}
}
