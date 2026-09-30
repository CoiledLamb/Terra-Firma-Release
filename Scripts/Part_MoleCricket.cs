using System;
using System.Collections.Generic;
using XRL.Rules;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_MoleCricket : IPart
	{
		public int PeckChance = 35;

		public string PeckDamage = "1d2";

		public int DigChance = 25;

		public string LastWearerPos;

		public GameObject DigTarget;

		public string DigDamage = "2d4";

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == SingletonEvent<EndTurnEvent>.ID;
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			GameObject wearer = ParentObject.Equipped;
			if (GameObject.Validate(ref wearer) && wearer.CurrentCell != null)
			{
				GameObject prey = FindAdjacentHostile(wearer);
				if (prey != null && PeckChance.in100())
				{
					int dmg = Math.Max(1, PeckDamage.RollCached());
					prey.TakeDamage(dmg, "from " + ParentObject.t() + "!", "Physical",
						null, null, Owner: wearer, Attacker: wearer, Source: ParentObject);
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CRICKET", $"pecked {prey.Blueprint} for {dmg}");
				}
				else if (prey == null && IsWearerIdle(wearer) && DigChance.in100())
				{
					if (!GameObject.Validate(ref DigTarget) || !IsAdjacentWall(wearer, DigTarget))
					{
						DigTarget = PickAdjacentNaturalWall(wearer);
					}
					if (DigTarget != null)
					{
						int dmg = Math.Max(1, DigDamage.RollCached());
						DigTarget.TakeDamage(dmg, "from scraping claws.", "Physical",
							null, null, Owner: wearer, Attacker: null, Source: ParentObject, Accidental: true);
						Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CRICKET", $"scraped {DigTarget.Blueprint} for {dmg}");
					}
				}
			}
			return base.HandleEvent(E);
		}

		private bool IsWearerIdle(GameObject wearer)
		{
			Cell cell = wearer.CurrentCell;
			string pos = cell.ParentZone?.ZoneID + ":" + cell.X + "," + cell.Y;
			bool stationary = pos == LastWearerPos;
			LastWearerPos = pos;
			return stationary && wearer.Target == null;
		}

		private static GameObject FindAdjacentHostile(GameObject wearer)
		{
			foreach (Cell cell in wearer.CurrentCell.GetLocalAdjacentCells())
			{
				GameObject target = cell.GetCombatTarget(wearer);
				if (target != null && target.IsCreature && target.IsHostileTowards(wearer))
				{
					return target;
				}
			}
			return null;
		}

		private static bool IsNaturalWall(GameObject obj)
		{
			Cleo_TerraFirma_StoneshaperWallProperties props = obj.GetPart<Cleo_TerraFirma_StoneshaperWallProperties>();
			return props != null && Cleo_TerraFirma_StoneshaperWallProperties.IsNaturalStone(props);
		}

		private static bool IsAdjacentWall(GameObject wearer, GameObject wall)
		{
			Cell wallCell = wall.CurrentCell;
			return wallCell != null && wearer.CurrentCell != null
				&& wallCell.ParentZone == wearer.CurrentCell.ParentZone
				&& wearer.CurrentCell.GetLocalAdjacentCells().Contains(wallCell)
				&& IsNaturalWall(wall);
		}

		private static GameObject PickAdjacentNaturalWall(GameObject wearer)
		{
			List<GameObject> candidates = new List<GameObject>();
			foreach (Cell cell in wearer.CurrentCell.GetLocalAdjacentCells())
			{
				foreach (GameObject obj in cell.Objects)
				{
					if (IsNaturalWall(obj))
					{
						candidates.Add(obj);
					}
				}
			}
			if (candidates.Count == 0)
			{
				return null;
			}
			return candidates[Stat.Random(0, candidates.Count - 1)];
		}
	}
}
