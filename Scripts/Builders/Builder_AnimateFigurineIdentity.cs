using System;
using XRL.World.Parts;

namespace XRL.World.ObjectBuilders
{
	[Serializable]
	public class Cleo_TerraFirma_AnimateFigurineIdentity : IObjectBuilder
	{
		public string Population = "Cleo_TerraFirma_AnimateFigurineIdentity";

		public override void Apply(GameObject Object, string Context)
		{
			string blueprint = PopulationManager.RollOneFrom(Population)?.Blueprint;
			if (string.IsNullOrEmpty(blueprint))
				throw new InvalidOperationException("Animate figurine identity population returned no blueprint.");
			GameObject figurine = GameObject.Create(blueprint);
			try
			{
				Object.Render.DisplayName = "animate " + figurine.DisplayNameOnlyStripped;
				Object.GetPart<Description>()._Short = figurine.GetPart<Description>()._Short;
			}
			finally
			{
				figurine.Obliterate(Silent: true);
			}
		}
	}
}
