using System;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_StoneRearmOnTakePart : IPart
	{
		[NonSerialized]
		private bool CameFromCell;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == TakenEvent.ID;
		}

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("BeginBeingTaken");
			base.Register(Object, Registrar);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == "BeginBeingTaken")
				CameFromCell = ParentObject.CurrentCell != null;
			return base.FireEvent(E);
		}

		public override bool HandleEvent(TakenEvent E)
		{
			if (CameFromCell)
			{
				CameFromCell = false;
				E.Actor?.GetPart<Mutation.Cleo_TerraFirma_EarthenBarrage>()?.OnStoneTaken(ParentObject);
			}
			return base.HandleEvent(E);
		}
	}
}
