using System;
using XRL.UI;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_ChalkLine : IPart
	{
		public const string SMEAR_COMMAND = "Cleo_TerraFirma_SmearChalkLine";

		public int TrampleSmearChance = 5;

		public string StrokeDir;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID
				|| ID == GetNavigationWeightEvent.ID
				|| ID == ObjectEnteredCellEvent.ID;
		}

		public override bool HandleEvent(GetNavigationWeightEvent E)
		{
			if (E.Smart)
				E.MinWeight(2);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ObjectEnteredCellEvent E)
		{
			GameObject walker = E.Object;
			if (walker != null && walker.IsCreature && !walker.IsFlying
				&& walker.GetMatterPhase() <= 1 && walker.PhaseMatches(ParentObject))
			{
				string owner = ParentObject.Physics?.Owner;
				bool ownerMatched = !owner.IsNullOrEmpty()
					&& (walker.HasTagOrProperty(owner) || walker.DisplayNameOnly == owner || walker.BelongsToFaction(owner));
				if (!ownerMatched && XRL.Rules.Stat.Random(1, 100) <= TrampleSmearChance)
				{
					if (!owner.IsNullOrEmpty())
					{
						ParentObject.Physics.BroadcastForHelp(walker);
						Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CHALK", $"owned chalk line ({owner}) trampled by {walker.Blueprint}, help broadcast");
					}
					else
					{
						Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CHALK", $"chalk line trampled by {walker.Blueprint}");
					}
					if (walker.IsPlayer())
					{
						IComponent<GameObject>.AddPlayerMessage("Your steps smear the chalk line.");
					}
					else if (ParentObject.IsVisible())
					{
						IComponent<GameObject>.AddPlayerMessage(walker.Does("smear") + " the chalk line underfoot.");
					}
					Cell cell = ParentObject.CurrentCell;
					Cleo.TerraFirma.Scripts.Cleo_TerraFirma_ChalkRevealFX.PlayScuff(cell);
					ParentObject.Destroy();
					RetileNeighbors(cell);
				}
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Smear", "smear away", SMEAR_COMMAND, null, 's', FireOnActor: false);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == SMEAR_COMMAND && E.Actor != null)
			{
				string owner = ParentObject.Physics?.Owner;
				if (!owner.IsNullOrEmpty())
				{
					ParentObject.Physics.BroadcastForHelp(E.Actor);
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CHALK", $"owned chalk line ({owner}) smeared by {E.Actor.Blueprint}, help broadcast");
				}
				else
				{
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CHALK", $"chalk line smeared by {E.Actor.Blueprint}");
				}
				if (E.Actor.IsPlayer())
				{
					IComponent<GameObject>.AddPlayerMessage("You smear the chalk line away.");
				}
				Cell cell = ParentObject.CurrentCell;
				E.Actor.UseEnergy(1000, "Item SmearChalkLine");
				float cover = Cleo.TerraFirma.Scripts.Cleo_TerraFirma_ChalkRevealFX.PlaySmear(cell);
				if (cover > 0f)
				{
					Cleo.TerraFirma.Scripts.Cleo_TerraFirma_ChalkRevealFX.Hold(cover);
				}
				ParentObject.Destroy();
				RetileNeighbors(cell);
				E.RequestInterfaceExit();
			}
			return base.HandleEvent(E);
		}

		public static void RetileNeighbors(Cell cell)
		{
			if (cell == null)
			{
				return;
			}
			foreach (string d in XRL.Rules.Directions.CardinalDirectionList)
			{
				Cell neighbor = cell.GetCellFromDirection(d);
				GameObject line = neighbor?.GetFirstObjectWithPart(nameof(Cleo_TerraFirma_ChalkLine));
				line?.GetPart<Cleo_TerraFirma_ChalkLine>()?.Retile();
			}
		}

		public void Retile()
		{
			Cell cell = ParentObject.CurrentCell;
			Render render = ParentObject.GetPart<Render>();
			if (cell == null || render == null)
			{
				return;
			}
			bool n = HasChalk(cell, "N"), s = HasChalk(cell, "S"), e = HasChalk(cell, "E"), w = HasChalk(cell, "W");
			string tile = "chalkline" + PieceFor(n, s, e, w, cell, StrokeDir) + ".png";
			if (render.Tile != tile)
			{
				render.Tile = tile;
				RetileNeighbors(cell);
			}
		}

		private static bool HasChalk(Cell cell, string dir)
		{
			return cell.GetCellFromDirection(dir)?.GetFirstObjectWithPart(nameof(Cleo_TerraFirma_ChalkLine)) != null;
		}

		private static string PieceFor(bool n, bool s, bool e, bool w, Cell cell, string strokeDir)
		{
			if (n && s && e && w)
			{
				return "X";
			}
			if (e && w && s)
			{
				return "TN";
			}
			if (e && w && n)
			{
				return "TS";
			}
			if (n && s && w)
			{
				return "TE";
			}
			if (n && s && e)
			{
				return "TW";
			}
			if (n && s)
			{
				return Vertical(cell);
			}
			if (w && e)
			{
				return Horizontal(cell);
			}
			if (s && w)
			{
				return "NE";
			}
			if (s && e)
			{
				return "NW";
			}
			if (n && w)
			{
				return "SE";
			}
			if (n && e)
			{
				return "SW";
			}
			if (n || s)
			{
				return Vertical(cell);
			}
			if (w || e)
			{
				return Horizontal(cell);
			}
			switch (strokeDir)
			{
				case "N":
				case "S":
					return Vertical(cell);
				case "NE":
				case "NW":
				case "SE":
				case "SW":
					return strokeDir;
				default:
					return Horizontal(cell);
			}
		}

		private const int RUN_WALK_MAX = 80;

		private static string Horizontal(Cell cell)
		{
			return RunEdgeRole(cell, "W", "E", "S", "N", "N", "S") ?? "N";
		}

		private static string Vertical(Cell cell)
		{
			return RunEdgeRole(cell, "N", "S", "E", "W", "W", "E") ?? "W";
		}

		private static string RunEdgeRole(Cell cell, string dir1, string dir2, string turnA, string turnB, string roleA, string roleB)
		{
			foreach (string d in new string[] { dir1, dir2 })
			{
				Cell c = cell;
				for (int i = 0; i < RUN_WALK_MAX && c != null; i++)
				{
					bool a = HasChalk(c, turnA);
					bool b = HasChalk(c, turnB);
					if (a && b)
					{
						break;
					}
					if (a)
					{
						return roleA;
					}
					if (b)
					{
						return roleB;
					}
					c = c.GetCellFromDirection(d);
					if (c?.GetFirstObjectWithPart(nameof(Cleo_TerraFirma_ChalkLine)) == null)
					{
						break;
					}
				}
			}
			return null;
		}
	}
}
