using System;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_CalcifyWallPart : IPart
	{
		public int CurHp;
		public int AvReduction;

		public override bool WantEvent(int ID, int Cascade)
		{
			if (!base.WantEvent(ID, Cascade) &&
				ID != PooledEvent<GetDisplayNameEvent>.ID)
				return ID == SingletonEvent<GetDebugInternalsEvent>.ID;
			return true;
		}

		public override bool HandleEvent(GetDebugInternalsEvent E)
		{
			E.AddEntry(this, nameof(CurHp), CurHp);
			return base.HandleEvent(E);
		}

		public Cleo_TerraFirma_CalcifyWallPart() { }

		public Cleo_TerraFirma_CalcifyWallPart(int curHP, int curAv)
		{
			CurHp = curHP;
			AvReduction = curAv;
		}

		public override bool HandleEvent(GetDisplayNameEvent E)
		{
			E.AddBase("calcified", -5);
			return base.HandleEvent(E);
		}

		public override void Attach()
		{
			if (CurHp != 0)
				StatShifter.SetStatShift("Hitpoints", CurHp, true);
			if (AvReduction != 0)
				StatShifter.SetStatShift("AV", -AvReduction, true);
		}

		public override void Remove()
		{
			StatShifter.RemoveStatShifts();
			if ((CurHp != 0 || AvReduction != 0) && ParentObject.TryGetPart(out Description d))
				d.Short = ParentObject.GetBlueprint().GetPartParameter("Description", "Short", d.Short);
		}
	}
}
