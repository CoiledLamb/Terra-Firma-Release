using System;
using System.Collections.Generic;
using ConsoleLib.Console;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace Cleo.TerraFirma.Scripts
{
	public static class Cleo_TerraFirma_CarvingPicker
	{
		public const string LAST_LOOKED_STATE = "Cleo_TerraFirma_CarvingLastLooked";
		private const int SEARCH_MAX_RESULTS = 40;

		public static bool IsCarvable(GameObjectBlueprint BP)
		{
			return BP != null && BP.HasTag("Creature") && !BP.HasTag("NoFigurine")
				&& EncountersAPI.IsEligibleForDynamicEncounters(BP);
		}

		public static bool IsLookCarvable(GameObjectBlueprint BP)
		{
			return BP != null && !BP.IsBaseBlueprint() && BP.HasTag("Creature") && !BP.HasTag("NoFigurine");
		}

		public static string Pick(string Title)
		{
			while (true)
			{
				var labels = new List<string>();
				var icons = new List<IRenderable>();
				var actions = new List<string>();

				GameObjectBlueprint last = GetLastLooked();
				if (last != null)
				{
					labels.Add("Carve the last creature you looked at: " + last.CachedDisplayNameStripped + ".");
					icons.Add(new Renderable(last));
					actions.Add("last");
				}
				labels.Add("Choose a creature native to this area.");
				icons.Add(null);
				actions.Add("local");
				labels.Add("Search for a creature by name.");
				icons.Add(null);
				actions.Add("search");
				labels.Add("Choose a creature at random.");
				icons.Add(null);
				actions.Add("random");

				int choice = Popup.PickOption(Title, "What would you like to carve?", "", "Sounds/UI/ui_notification",
					labels.ToArray(), Hotkeys(labels.Count), icons.ToArray(), Spacing: 1, AllowEscape: true);
				if (choice < 0)
					return null;

				string result = null;
				switch (actions[choice])
				{
					case "last":
						return last.Name;
					case "local":
						result = PickFromList(Title, LocalFauna(), "Choose a creature native to this area.", "There are no creatures native to this area.");
						break;
					case "search":
						result = Search(Title);
						break;
					case "random":
						return EncountersAPI.GetACreatureBlueprint(IsCarvable);
				}
				if (result != null)
					return result;
			}
		}

		public static List<GameObjectBlueprint> LocalFauna()
		{
			var list = new List<GameObjectBlueprint>();
			string terrain = The.Player?.CurrentZone?.GetTerrainObject()?.GetTag("Terrain");
			if (string.IsNullOrEmpty(terrain))
				return list;
			string table = "DynamicObjectsTable:" + terrain + "_Creatures";
			foreach (GameObjectBlueprint bp in GameObjectFactory.Factory.BlueprintList)
			{
				if (bp.HasTag(table) && IsCarvable(bp))
					list.Add(bp);
			}
			return Dedupe(list);
		}

		private static string Search(string Title)
		{
			string query = Popup.AskString("Enter the name of a creature.", "", "Sounds/UI/ui_notification", null, null, 40, 1, ReturnNullForEscape: true);
			if (string.IsNullOrWhiteSpace(query))
				return null;
			query = query.Trim().ToLower();
			var matches = new List<GameObjectBlueprint>();
			foreach (GameObjectBlueprint bp in GameObjectFactory.Factory.BlueprintList)
			{
				if (IsCarvable(bp) && bp.CachedDisplayNameStrippedLC.Contains(query))
					matches.Add(bp);
			}
			matches = Dedupe(matches);
			matches.Sort((a, b) =>
			{
				int ra = Rank(a.CachedDisplayNameStrippedLC, query), rb = Rank(b.CachedDisplayNameStrippedLC, query);
				return ra != rb ? ra.CompareTo(rb) : string.Compare(a.CachedDisplayNameStrippedLC, b.CachedDisplayNameStrippedLC, StringComparison.Ordinal);
			});
			if (matches.Count > SEARCH_MAX_RESULTS)
				matches.RemoveRange(SEARCH_MAX_RESULTS, matches.Count - SEARCH_MAX_RESULTS);
			return PickFromList(Title, matches, "Choose a creature.", "You don't know of any creature by that name.");
		}

		private static int Rank(string Name, string Query)
		{
			if (Name == Query)
				return 0;
			return Name.StartsWith(Query) ? 1 : 2;
		}

		private static string PickFromList(string Title, List<GameObjectBlueprint> Options, string Intro, string EmptyMessage)
		{
			if (Options.Count == 0)
			{
				Popup.Show(EmptyMessage);
				return null;
			}
			var labels = new string[Options.Count];
			var icons = new IRenderable[Options.Count];
			for (int i = 0; i < Options.Count; i++)
			{
				labels[i] = Options[i].CachedDisplayNameStripped;
				icons[i] = new Renderable(Options[i]);
			}
			int choice = Popup.PickOption(Title, Intro, "", "Sounds/UI/ui_notification", labels, Hotkeys(labels.Length), icons, AllowEscape: true);
			return choice < 0 ? null : Options[choice].Name;
		}

		private static List<GameObjectBlueprint> Dedupe(List<GameObjectBlueprint> List)
		{
			var seen = new HashSet<string>();
			var result = new List<GameObjectBlueprint>();
			foreach (GameObjectBlueprint bp in List)
			{
				if (seen.Add(bp.CachedDisplayNameStrippedLC))
					result.Add(bp);
			}
			result.Sort((a, b) => string.Compare(a.CachedDisplayNameStrippedLC, b.CachedDisplayNameStrippedLC, StringComparison.Ordinal));
			return result;
		}

		private static char[] Hotkeys(int Count)
		{
			var keys = new char[Count];
			char c = 'a';
			for (int i = 0; i < Count; i++)
				keys[i] = c <= 'z' ? c++ : ' ';
			return keys;
		}

		private static GameObjectBlueprint GetLastLooked()
		{
			string name = The.Game?.GetStringGameState(LAST_LOOKED_STATE);
			if (string.IsNullOrEmpty(name))
				return null;
			GameObjectBlueprint bp = GameObjectFactory.Factory.GetBlueprintIfExists(name);
			return IsLookCarvable(bp) ? bp : null;
		}
	}
}

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_LookMemory : IPart
	{
		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("LookedAt");
			base.Register(Object, Registrar);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == "LookedAt")
			{
				GameObject seen = E.GetGameObjectParameter("Object");
				bool keep = seen != null && seen != ParentObject && seen.IsCreature
					&& Cleo.TerraFirma.Scripts.Cleo_TerraFirma_CarvingPicker.IsLookCarvable(seen.GetBlueprint());
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CARVE", $"LookedAt {seen?.Blueprint ?? "null"} creature={seen?.IsCreature} kept={keep}");
				if (keep)
					The.Game?.SetStringGameState(Cleo.TerraFirma.Scripts.Cleo_TerraFirma_CarvingPicker.LAST_LOOKED_STATE, seen.Blueprint);
			}
			return base.FireEvent(E);
		}
	}
}

namespace Cleo.TerraFirma.Scripts
{
	[XRL.PlayerMutator]
	[XRL.HasCallAfterGameLoaded]
	public class Cleo_TerraFirma_LookMemoryInstaller : XRL.IPlayerMutator
	{
		public void mutate(GameObject player)
		{
			player?.RequirePart<Cleo_TerraFirma_LookMemory>();
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CARVE", $"look memory attached at new game: {player?.HasPart<Cleo_TerraFirma_LookMemory>()}");
		}

		[XRL.CallAfterGameLoaded]
		public static void OnLoaded()
		{
			The.Player?.RequirePart<Cleo_TerraFirma_LookMemory>();
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CARVE", $"look memory attached on load: {The.Player?.HasPart<Cleo_TerraFirma_LookMemory>()}");
		}
	}
}
