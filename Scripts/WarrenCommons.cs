using System;
using System.Collections.Generic;
using System.Linq;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.World;
using XRL.World.Parts;

namespace Cleo.TerraFirma.Scripts
{
	public static partial class WarrenCarver
	{
		private static Row[] _etchCountRows;
		private static void PlanCommonsEtchings(Grid g, PocketRec p)
		{
			var ring = new List<int>();
			for (int y = p.BoxOut.Y0; y <= p.BoxOut.Y1; y++)
				for (int x = p.BoxOut.X0; x <= p.BoxOut.X1; x++)
				{
					if (x != p.BoxOut.X0 && x != p.BoxOut.X1 && y != p.BoxOut.Y0 && y != p.BoxOut.Y1) continue;
					if (!g.Inb(x, y) || g.C[g.Idx(x, y)] == FLOOR) continue;
					if (N8.Any(d => g.Inb(x + d[0], y + d[1]) && g.C[g.Idx(x + d[0], y + d[1])] == FLOOR
						&& g.Pocket[g.Idx(x + d[0], y + d[1])] == p.Id)) ring.Add(g.Idx(x, y));
				}
			int marks = int.Parse(RollLabelTable(ref _etchCountRows, "Cleo_TerraFirma_WarrenEtchCount", new[] { "2", "3", "4" }));
			g.EtchedWalls = g.EtchedWalls ?? new HashSet<int>();
			while (marks-- > 0 && ring.Count > 0)
			{
				int choice = R(0, ring.Count - 1);
				g.EtchedWalls.Add(ring[choice]);
				ring.RemoveAt(choice);
			}
		}

		private static void ApplyCommonsEtchings(Zone zone, Grid g)
		{
			if (g.EtchedWalls == null || g.EtchedWalls.Count == 0) return;
			MarkovBook.EnsureCorpusLoaded(Cleo_TerraFirma_Etchable.CORPUS);
			if (!MarkovBook.CorpusData.TryGetValue(Cleo_TerraFirma_Etchable.CORPUS, out MarkovChainData data))
			{
				MetricsManager.LogError("TerraFirma: commons inscription corpus unavailable; walls left unetched");
				return;
			}
			int etched = 0;
			foreach (int i in g.EtchedWalls)
			{
				var wall = zone.GetCell(i % g.W, i / g.W)?.GetWalls().FirstOrDefault();
				if (wall == null) continue;
				var part = wall.RequirePart<Cleo_TerraFirma_Etchable>();
				for (int attempt = 0; attempt < 5; attempt++)
				{
					string text = MarkovChain.GenerateShortSentence(data)?.Trim();
					if (string.IsNullOrWhiteSpace(text) || text.Contains("=") || text.Contains("{") || text.Contains("}")) continue;
					text = text.Replace('\u2014', ',');
					if (part.SetGeneratedInscription(text)) etched++;
					break;
				}
			}
			Helpers.VerifyLog("WARREN", $"commons etchings: {etched}/{g.EtchedWalls.Count}");
		}

		private static bool SeatWarrenWeb(Zone zone, Grid g, GameObject web, bool withSpiders)
		{
			var spiderCells = new HashSet<int>();
			var spiderRooms = new HashSet<short>();
			if (withSpiders)
				for (int i = 0; i < g.W * g.H; i++)
					if (g.C[i] == FLOOR && g.Host[i] == 0 && zone.GetCell(i % g.W, i / g.W).HasObjectWithBlueprint("Cave Spider"))
					{
						spiderCells.Add(i);
						if (g.Pocket[i] >= 0) spiderRooms.Add(g.Pocket[i]);
					}
			var candidates = new List<int>();
			var preferred = new List<int>();
			for (int i = 0; i < g.W * g.H; i++)
			{
				if (g.C[i] != FLOOR || g.Host[i] != 0 || g.Art[i] != 0 || g.Water[i] != 0 || g.Tree[i] != 0 || i == g.MouthIdx) continue;
				int x = i % g.W, y = i / g.W;
				bool nearSpider = N4.Any(d => g.Inb(x + d[0], y + d[1]) && spiderCells.Contains(g.Idx(x + d[0], y + d[1])));
				if (withSpiders && !nearSpider && !spiderRooms.Contains(g.Pocket[i])) continue;
				var cell = zone.GetCell(x, y);
				if (cell.IsSolid() || cell.HasObjectWithBlueprint("Web")) continue;
				candidates.Add(i);
				if (withSpiders ? nearSpider : N4.Any(d => g.Inb(x + d[0], y + d[1]) && g.C[g.Idx(x + d[0], y + d[1])] != FLOOR)) preferred.Add(i);
			}
			if (preferred.Count > 0) candidates = preferred;
			if (candidates.Count == 0) return false;
			int selected = Pick(candidates);
			zone.GetCell(selected % g.W, selected / g.W).AddObject(web);
			return true;
		}
	}
}
