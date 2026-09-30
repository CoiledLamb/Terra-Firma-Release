using System;
using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.World.Effects;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_GlowsphereBreak : IPart
	{
		public const string COMMAND = "Cleo_TerraFirma_BreakGlowsphere";

		public string ShardBlueprint = "Cleo_TerraFirma_GlowstoneShard";
		public string BreakYield = "2-3";
		public string ThrowYield = "1-3";
		public string GlowDuration = "30-60";

		public override bool SameAs(IPart p)
		{
			return p is Cleo_TerraFirma_GlowsphereBreak o
				&& o.ShardBlueprint == ShardBlueprint && o.BreakYield == BreakYield
				&& o.ThrowYield == ThrowYield && o.GlowDuration == GlowDuration && base.SameAs(p);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID
				|| ID == AfterThrownEvent.ID;
		}

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("ThrownProjectileHit");
			base.Register(Object, Registrar);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Break", "break", COMMAND, null, 'b', FireOnActor: false);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == COMMAND && E.Actor != null)
			{
				if (E.Actor.IsPlayer()
					&& Popup.ShowYesNo("Really break your glowsphere?") != DialogResult.Yes)
				{
					return false;
				}
				GameObject sphere = ParentObject.SplitFromStack() ?? ParentObject;
				int count = BreakYield.RollCached();
				for (int i = 0; i < count; i++)
				{
					E.Actor.ReceiveObject(GameObject.Create(ShardBlueprint));
				}
				if (E.Actor.IsPlayer())
				{
					Popup.Show("The glowsphere breaks into " + Grammar.Cardinal(count) + " glowing fragments.");
				}
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHARD", $"glowsphere broken into {count} fragments by {E.Actor.Blueprint}");
				E.Actor.UseEnergy(1000, "Item BreakGlowsphere");
				sphere.Destroy();
				E.RequestInterfaceExit();
			}
			return base.HandleEvent(E);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == "ThrownProjectileHit")
			{
				GameObject target = E.GetGameObjectParameter("Defender");
				if (GameObject.Validate(target) && target.IsCreature && !target.HasEffect<Luminous>())
				{
					target.ApplyEffect(new Luminous(GlowDuration.RollCached()));
					Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHARD", $"thrown glowsphere marked {target.Blueprint} phosphorescent");
				}
			}
			return base.FireEvent(E);
		}

		public override bool HandleEvent(AfterThrownEvent E)
		{
			Cell cell = ParentObject.CurrentCell;
			if (cell != null)
			{
				int count = ThrowYield.RollCached();
				for (int i = 0; i < count; i++)
				{
					GameObject fragment = GameObject.Create(ShardBlueprint);
					cell.AddObject(fragment);
					fragment.GetPart<MoteProperties>()?.Light();
				}
				if (IComponent<GameObject>.Visible(ParentObject))
				{
					IComponent<GameObject>.AddPlayerMessage("The glowsphere shatters, scattering glowing fragments.");
				}
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHARD", $"thrown glowsphere shattered into {count} fragments");
				ParentObject.Destroy();
			}
			return base.HandleEvent(E);
		}
	}
}
