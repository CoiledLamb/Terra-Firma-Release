using System;
using System.Collections.Generic;
using XRL.UI;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_PiezoelectricMill : IPoweredPart
	{
		public int ChargePerPound = 50;
		public int FeedEnergy = 1000;
		private const string FeedCommand = "Cleo_TerraFirma_FeedPiezoelectricMill";

		public Cleo_TerraFirma_PiezoelectricMill()
		{
			ChargeUse = 0;
			WorksOnSelf = true;
			IsBootSensitive = false;
		}

		public override bool SameAs(IPart other)
		{
			var mill = other as Cleo_TerraFirma_PiezoelectricMill;
			return mill != null && mill.ChargePerPound == ChargePerPound
				&& mill.FeedEnergy == FeedEnergy && base.SameAs(other);
		}

		public override bool WantEvent(int ID, int cascade) => base.WantEvent(ID, cascade)
			|| ID == GetInventoryActionsEvent.ID || ID == InventoryActionEvent.ID;

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Feed", "feed a stone", FeedCommand, null, 'f', FireOnActor: false);
			return base.HandleEvent(E);
		}

		private bool IsFood(GameObject stone) => stone != null && stone != ParentObject
			&& stone.HasTagOrProperty(Cleo_TerraFirma_GrinderCell.STONE_TAG)
			&& !stone.HasPart<Temporary>() && !stone.IsCreature
			&& string.IsNullOrEmpty(stone.Physics?.Owner);

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command != FeedCommand || E.Actor == null) return base.HandleEvent(E);
			var actorCell = E.Actor.CurrentCell;
			var millCell = ParentObject.CurrentCell;
			if (actorCell == null || millCell == null || actorCell.ParentZone != millCell.ParentZone
				|| Math.Abs(actorCell.X - millCell.X) > 1 || Math.Abs(actorCell.Y - millCell.Y) > 1) return false;
			var cell = ParentObject.GetPart<EnergyCell>();
			if (cell == null || !IsReady(UseCharge: false, IgnoreCharge: true))
			{
				if (E.Actor.IsPlayer()) Popup.ShowFail(ParentObject.Does("are") + " not running.");
				return false;
			}
			if (cell.Charge >= cell.MaxCharge || ChargePerPound <= 0)
			{
				if (E.Actor.IsPlayer()) Popup.ShowFail(ParentObject.T() + " cannot store any more charge.");
				return false;
			}
			var stones = E.Actor.GetInventoryDirect(IsFood);
			int carried = stones.Count;
			var origin = E.Actor.CurrentCell;
			if (origin != null)
			{
				var reach = new List<Cell>(origin.GetLocalAdjacentCells());
				reach.Add(origin);
				foreach (var c in reach)
					foreach (var stone in c.Objects)
						if (IsFood(stone) && !stones.Contains(stone)) stones.Add(stone);
			}
			if (stones.Count == 0)
			{
				if (E.Actor.IsPlayer()) Popup.ShowFail("There is no stone in reach to feed it.");
				return false;
			}
			int choice = 0;
			if (E.Actor.IsPlayer())
			{
				var options = new List<string>();
				for (int i = 0; i < stones.Count; i++)
					options.Add(stones[i].DisplayName + (i >= carried ? " {{K|(nearby)}}" : ""));
				choice = Popup.PickOption(Intro: "Feed which stone?", Options: options.ToArray(), AllowEscape: true);
				if (choice < 0) return false;
			}
			var pick = stones[choice];
			int burst = (int)Math.Min(int.MaxValue, (long)Math.Max(1, pick.Physics?.Weight ?? 0) * ChargePerPound);
			int accepted = Math.Min(burst, cell.MaxCharge - cell.Charge);
			if (accepted < burst && E.Actor.IsPlayer()
				&& Popup.ShowYesNo("Only " + accepted + " of " + burst + " charge will fit. Feed the stone anyway?") != DialogResult.Yes) return false;
			pick = pick.SplitFromStack() ?? pick;
			string name = pick.t();
			if (!pick.Destroy()) return false;
			cell.AddCharge(accepted);
			if (E.Actor.IsPlayer()) IComponent<GameObject>.AddPlayerMessage("You feed " + name + " into " + ParentObject.t() + ".");
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("MILL", $"burst={burst}, stored={accepted}, charge={cell.Charge}/{cell.MaxCharge}");
			E.Actor.UseEnergy(FeedEnergy, "Item FeedPiezoelectricMill");
			E.RequestInterfaceExit();
			return base.HandleEvent(E);
		}
	}
}
