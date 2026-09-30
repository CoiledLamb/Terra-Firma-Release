using System;
using XRL.UI;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_ChalkboardWritable : IPart
	{
		public const string WRITE_COMMAND = "Cleo_TerraFirma_WriteChalkboard";
		public const string CORPUS = "LibraryCorpus.json";

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID
				|| ID == CanSmartUseEvent.ID
				|| ID == CommandSmartUseEvent.ID;
		}

		private bool CanWrite(GameObject actor)
		{
			return actor != null && actor != ParentObject && ParentObject.Understood()
				&& actor.PhaseMatches(ParentObject) && actor.FlightCanReach(ParentObject)
				&& Cleo_TerraFirma_ChalkBox.FindCarriedBox(actor) != null;
		}

		public override bool HandleEvent(CanSmartUseEvent E)
		{
			if (CanWrite(E.Actor))
			{
				return false;
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(CommandSmartUseEvent E)
		{
			if (CanWrite(E.Actor))
			{
				AttemptWrite(E.Actor);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (E.Actor != null && Cleo_TerraFirma_ChalkBox.FindCarriedBox(E.Actor) != null)
			{
				E.AddAction("Write", "write with chalk", WRITE_COMMAND, null, 'w', FireOnActor: false, Default: 5);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == WRITE_COMMAND && E.Actor != null)
			{
				if (AttemptWrite(E.Actor))
				{
					E.RequestInterfaceExit();
				}
			}
			return base.HandleEvent(E);
		}

		private bool AttemptWrite(GameObject actor)
		{
			if (actor != null && actor.IsPlayer())
			{
				Cleo_TerraFirma_ChalkBox box = Cleo_TerraFirma_ChalkBox.FindCarriedBox(actor);
				if (box == null)
				{
					Popup.ShowFail("You have no chalk.");
					return false;
				}
				int choice = Popup.PickOption(
					Intro: "What would you like to write?",
					Options: new string[] { "Write a message.", "Write from memory." },
					AllowEscape: true);
				string text = null;
				if (choice == 0)
				{
					text = Popup.AskString("Chalk your message. For example: \"live and drink\".", "", "Sounds/UI/ui_notification",
						Cleo_TerraFirma_Etchable.TYPED_CHARS, null, Cleo_TerraFirma_Etchable.TYPED_MAX);
					if (text.IsNullOrEmpty())
					{
						return false;
					}
				}
				else if (choice == 1)
				{
					MarkovBook.EnsureCorpusLoaded(CORPUS);
					if (!MarkovBook.CorpusData.TryGetValue(CORPUS, out MarkovChainData data))
					{
						IComponent<GameObject>.AddPlayerMessage("You can't think of anything to write.");
						return false;
					}
					for (int i = 0; i < 5; i++)
					{
						text = MarkovChain.GenerateShortSentence(data).TrimEnd(' ');
						if (!text.Contains("="))
						{
							break;
						}
					}
				}
				else
				{
					return false;
				}
				Cleo_TerraFirma_ChalkWriting writing = ParentObject.RequirePart<Cleo_TerraFirma_ChalkWriting>();
				writing.Text = text;
				Popup.Show("You chalk \"" + text + "\" onto " + ParentObject.t() + ".");
				Cleo.TerraFirma.Scripts.Cleo_TerraFirma_ChalkRevealFX.PlayBoardScuff(ParentObject.CurrentCell);
				Cleo.TerraFirma.Scripts.Helpers.VerifyLog("CHALK", $"chalkboard written ({(choice == 0 ? "typed" : "markov")})");
				actor.UseEnergy(1000, "Item WriteChalkboard");
				box.ConsumeUse(actor);
				return true;
			}
			return false;
		}
	}

	[Serializable]
	public class Cleo_TerraFirma_ChalkWriting : IPart
	{
		public string Text;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == GetShortDescriptionEvent.ID;
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			if (!Text.IsNullOrEmpty())
			{
				if (E.Infix.Length > 0)
				{
					E.Infix.Append("\n");
				}
				E.Infix.Append("\n").Append("Chalk is scrawled across the surface. It reads: \n\n\"")
					.Append("{{Y|").Append(Text).Append("}}").Append("\"\n");
			}
			return base.HandleEvent(E);
		}
	}
}
