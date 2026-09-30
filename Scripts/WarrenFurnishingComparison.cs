using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using XRL;
using XRL.UI;
using XRL.Wish;
using XRL.World;

namespace Cleo.TerraFirma.Scripts
{
	public static partial class WarrenCarver
	{
		private static Dictionary<string, FurnishingPair> _furnishingPairs;
		private static int _furnishingErrors;
		private class RoomPair
		{
			public int Id, Floor, WithTopup, WithoutTopup;
			public string Role;
		}
		private class FurnishingPair
		{
			public int Seed, Topups, WithTopup, WithoutTopup, ChangedCells;
			public string Stage, Occupancy, Layout;
			public List<RoomPair> Rooms = new List<RoomPair>();
		}

		private static void CompareFurnishing(Zone zone, int seed, int? forced, int attempt,
			bool blocks, HashSet<int> aug, Grid original, List<PocketRec> pockets, Result originalResult)
		{
			if (_furnishingPairs == null) return;
			uint savedState = _state;
			int savedNoise = _noiseSeed;
			try
			{
				var alternativeResult = BuildOnce(zone, seed, forced, attempt, blocks, true, aug,
					out Grid alternative, out List<PocketRec> alternativePockets, siteTopup: false);
				if (!alternativeResult.Built || originalResult.Stage != alternativeResult.Stage
					|| originalResult.Occupancy != alternativeResult.Occupancy
					|| !original.C.SequenceEqual(alternative.C) || !original.Host.SequenceEqual(alternative.Host)
					|| !original.Pocket.SequenceEqual(alternative.Pocket) || !original.Art.SequenceEqual(alternative.Art)
					|| !original.Water.SequenceEqual(alternative.Water)
					|| !pockets.Select(p => p.Role).SequenceEqual(alternativePockets.Select(p => p.Role)))
					throw new InvalidOperationException("Furnishing comparison geometry or roles differed; pair excluded.");
				var pair = new FurnishingPair {
					Seed = seed, Stage = originalResult.Stage, Occupancy = originalResult.Occupancy,
					Layout = originalResult.Layout, Topups = original.SiteTopups,
					WithTopup = original.Item.Count(i => i != null), WithoutTopup = alternative.Item.Count(i => i != null),
					ChangedCells = Enumerable.Range(0, original.Item.Length).Count(i => original.Item[i] != alternative.Item[i])
				};
				foreach (var p in pockets)
				{
					var room = new RoomPair { Id = p.Id, Role = p.Role };
					for (int i = 0; i < original.C.Length; i++)
						if (original.Pocket[i] == p.Id && original.C[i] == FLOOR && original.Water[i] == 0)
						{
							room.Floor++;
							if (original.Item[i] != null) room.WithTopup++;
							if (alternative.Item[i] != null) room.WithoutTopup++;
						}
					pair.Rooms.Add(room);
				}
				_furnishingPairs[zone.ZoneID] = pair;
			}
			catch (Exception ex)
			{
				_furnishingPairs.Remove(zone.ZoneID);
				_furnishingErrors++;
				MetricsManager.LogException("TerraFirma furnishing comparison: " + zone.ZoneID, ex);
			}
			finally
			{
				_state = savedState;
				_noiseSeed = savedNoise;
			}
		}

		internal static void CompareWorldFurnishing()
		{
			if (The.Game == null || _furnishingPairs != null) return;
			_furnishingPairs = new Dictionary<string, FurnishingPair>();
			_furnishingErrors = 0;
			try
			{
				WarrenSurvey.RunSurvey(false);
				var pairs = _furnishingPairs.Values.ToList();
				var rooms = pairs.SelectMany(p => p.Rooms).ToList();
				var report = new StringBuilder("Warren furnishing comparison: early site top-up ON / OFF\n");
				report.AppendLine($"Paired sites: {pairs.Count}; rooms: {rooms.Count}; comparison errors: {_furnishingErrors}");
				if (pairs.Count > 0)
				{
					report.AppendLine($"Sites receiving early top-ups: {pairs.Count(p => p.Topups > 0)}; items added by that pass: {pairs.Sum(p => p.Topups)}");
					report.AppendLine($"Final planned items: {pairs.Sum(p => p.WithTopup)} / {pairs.Sum(p => p.WithoutTopup)}");
					report.AppendLine($"Empty rooms: {rooms.Count(r => r.WithTopup == 0)} / {rooms.Count(r => r.WithoutTopup == 0)}");
					report.AppendLine($"Sparse rooms: {rooms.Count(r => r.WithTopup == 0 || (r.Floor >= 20 && r.WithTopup <= 1))} / {rooms.Count(r => r.WithoutTopup == 0 || (r.Floor >= 20 && r.WithoutTopup <= 1))}");
					report.AppendLine($"Rooms with fewer / more items without top-up: {rooms.Count(r => r.WithoutTopup < r.WithTopup)} / {rooms.Count(r => r.WithoutTopup > r.WithTopup)}");
					report.AppendLine($"Sites with changed furnishing: {pairs.Count(p => p.ChangedCells > 0)}; changed cells: {pairs.Sum(p => p.ChangedCells)}");
				}
				report.AppendLine("Sparse = empty, or 20+ dry floor cells with at most one planned item.");
				report.AppendLine("Counts include yard and power props; exclude residents, wires, moss and inscriptions.");
				report.AppendLine("The later room rescue stays ON. Removing early rolls can change subsequent random furnishings.");
				report.AppendLine("Normal sites were built with top-up ON; OFF was scratch-only. Existing sites were skipped.");
				report.Append("Run this BEFORE tfwarrenstats/tfwarrens in a fresh world for a complete sample. Details in Player.log.");
				foreach (var pair in _furnishingPairs.OrderBy(p => p.Key, StringComparer.Ordinal))
					Helpers.VerifyLog("WARREN-DRESS", pair.Key + " " + JsonConvert.SerializeObject(pair.Value));
				Helpers.VerifyLog("WARREN-DRESS", report.ToString());
				Popup.Show(report.ToString());
			}
			finally { _furnishingPairs = null; }
		}
	}
}
