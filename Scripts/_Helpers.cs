using XRL.Messages;
using XRL.UI;

namespace Cleo.TerraFirma.Scripts
{
	public class Helpers
	{
		public static void DebugLog(string Msg, string Context, string Color = null, bool Capitalize = true)
		{
			if (Options.GetOption("Cleo_TerraFirma_DebugLog").EqualsNoCase("Yes"))
				MessageQueue.AddPlayerMessage($"[{Context}] {Msg}", Color, Capitalize);
		}

		public const bool VERIFY_WATCH = false;

		public static void VerifyLog(string Context, string Msg)
		{
			if (VERIFY_WATCH)
				MetricsManager.LogInfo($"[TF-VERIFY] [{Context}] {Msg}");
		}

		public const int SERIAL_SENTINEL = unchecked((int)0xC1E0E7C4);
	}
}
