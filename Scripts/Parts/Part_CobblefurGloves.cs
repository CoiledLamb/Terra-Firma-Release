using System;
using XRL.Rules;
using XRL.World.Anatomy;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_CobblefurGloves : IPart
	{
		public int Chance = 50;

		public override bool SameAs(IPart p)
		{
			return p is Cleo_TerraFirma_CobblefurGloves o && o.Chance == Chance && base.SameAs(p);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != EquippedEvent.ID)
				return ID == UnequippedEvent.ID;
			return true;
		}

		public override bool HandleEvent(EquippedEvent E)
		{
			if (E.Item.IsEquippedProperly())
				E.Actor.RegisterEvent(this, DefenderMissileHitEvent.ID, 0, Serialize: true);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(UnequippedEvent E)
		{
			E.Actor.UnregisterEvent(this, DefenderMissileHitEvent.ID);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(DefenderMissileHitEvent E)
		{
			GameObject wearer = ParentObject?.Equipped;
			GameObject rock = E.Projectile;
			if (wearer != null && rock != null && E.Launcher == null
				&& !E.Done && GameObject.Validate(rock) && !rock.IsInGraveyard()
				&& Stat.Random(1, 100) <= Chance)
			{
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("GLOV", $"{wearer.Blueprint} catch PROC on {rock.Blueprint} ({Chance}%)");
				IComponent<GameObject>.XDidYToZ(wearer, "snatch", rock, "out of the air", null, null, null, wearer);
				if (wearer.TakeObject(rock, NoStack: false, Silent: true, EnergyCost: 0))
				{
					BodyPart slot = wearer.GetFirstBodyPart("Thrown Weapon");
					if (slot != null && slot.Equipped == null)
						wearer.FireEvent(Event.New("CommandEquipObject", "Object", rock, "BodyPart", slot));
				}
				if (rock.GetPartDescendedFrom<IGrenade>() != null)
				{
					Cleo_TerraFirma_CaughtGrenade fuse = rock.RequirePart<Cleo_TerraFirma_CaughtGrenade>();
					fuse.Catcher = wearer;
					fuse.FuseTurns = 1;
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("GLOV", $"caught LIVE GRENADE {rock.Blueprint}, fuse ticking");
				}
				E.Done = true;
				return false;
			}
			return base.HandleEvent(E);
		}
	}

	[Serializable]
	public class Cleo_TerraFirma_CaughtGrenade : IPart
	{
		public int FuseTurns = 1;
		public GameObject Catcher;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == SingletonEvent<EndTurnEvent>.ID
				|| ID == PooledEvent<BeforeDetonateEvent>.ID;
		}

		public override bool HandleEvent(BeforeDetonateEvent E)
		{
			if (FuseTurns > 0 && GameObject.Validate(ref Catcher))
			{
				Cell here = ParentObject.CurrentCell;
				bool inCatcherReach = ParentObject.Equipped == Catcher || ParentObject.InInventory == Catcher
					|| (here != null && here == Catcher.CurrentCell);
				if (inCatcherReach)
				{
					if (here != null)
					{
						ParentObject.RemoveFromContext();
						Catcher.ReceiveObject(ParentObject);
					}
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("GLOV", $"grenade detonation held off, fuse {FuseTurns}");
					return false;
				}
			}
			ParentObject.RemovePart(this);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			FuseTurns--;
			if (FuseTurns < 0)
			{
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("GLOV", "caught grenade fuse expired");
				IGrenade grenade = ParentObject.GetPartDescendedFrom<IGrenade>();
				GameObject catcher = Catcher;
				ParentObject.RemovePart(this);
				grenade?.Detonate(null, catcher);
			}
			return base.HandleEvent(E);
		}
	}
}
