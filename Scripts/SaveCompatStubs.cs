using System;

namespace XRL.World.Parts
{
	public class Cleo_TerraFirma_FallenRockPart : IPart
	{
		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			throw new NotSupportedException("quarantined part; saved data skipped");
		}

		public override bool ReadError(Exception Exception, SerializationReader Reader, long Start, int Length)
		{
			Reader.Stream.Position = Start + Length;
			return true;
		}

		public override void FinalizeRead(SerializationReader Reader)
		{
			base.FinalizeRead(Reader);
			if (ParentObject?.Blueprint == null)
				return;
			string current = GameObjectFactory.Factory.GetBlueprintIfExists(ParentObject.Blueprint)?.GetPartParameter<string>("Description", "Short");
			Description description = ParentObject.GetPart<Description>();
			if (!string.IsNullOrEmpty(current) && description != null)
				description._Short = current;
		}
	}
}
