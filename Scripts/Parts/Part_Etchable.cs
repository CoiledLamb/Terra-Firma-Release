using System;
using System.Collections.Generic;
using ConsoleLib.Console;
using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.World.Anatomy;
using Cleo.TerraFirma.Scripts;

namespace XRL.World.Parts
{
	[Serializable]
	public class Cleo_TerraFirma_Etchable : IPart
	{
		public const string COMMAND = "Cleo_TerraFirma_Etch";
		public const string TOOL_TAG = "Cleo_TerraFirma_EtchTool";
		public const string CORPUS = "LibraryCorpus.json";
		public const string TYPED_CHARS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,!?'-:;()";
		public const int TYPED_MAX = 60;
		private const int GARBLE_TIER_CEILING = 6;
		private const int GARBLE_PCT_PER_TIER = 4;
		private const int IMPROVISED_TIER_PENALTY = 2;

		public string Inscription;

		[NonSerialized]
		public Dictionary<int, string> LimbInscriptions = new Dictionary<int, string>();

		private const int FORMAT_VERSION = 3;

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.Write(Helpers.SERIAL_SENTINEL);
			Writer.Write(FORMAT_VERSION);
			WriteLimbs(Writer);
			base.Write(Basis, Writer);
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			if (Reader.ReadInt32() != Helpers.SERIAL_SENTINEL)
				throw new FormatException("Cleo_TerraFirma_Etchable: pre-sentinel block");
			Reader.ReadInt32();
			ReadLimbs(Reader);
			base.Read(Basis, Reader);
		}

		private void WriteLimbs(SerializationWriter Writer)
		{
			Writer.Write(LimbInscriptions.Count);
			foreach (KeyValuePair<int, string> kv in LimbInscriptions)
			{
				Writer.Write(kv.Key);
				Writer.Write(kv.Value);
			}
		}

		private void ReadLimbs(SerializationReader Reader)
		{
			int n = Reader.ReadInt32();
			if (n < 0 || n > 1024)
				throw new FormatException("Cleo_TerraFirma_Etchable: implausible limb count " + n);
			LimbInscriptions = new Dictionary<int, string>(n);
			for (int i = 0; i < n; i++)
			{
				int key = Reader.ReadInt32();
				LimbInscriptions[key] = Reader.ReadString();
			}
		}

		public override bool ReadError(Exception Exception, SerializationReader Reader, long Start, int Length)
		{
			try
			{
				Reader.Stream.Position = Start;
				Reader.ReadTokenizedType();
				StatShifter.Load(Reader, ParentObject);
				ReadLimbs(Reader);
				base.Read(ParentObject, Reader);
				if (Reader.Stream.Position == Start + Length)
				{
					Helpers.VerifyLog("SERIAL", $"Etchable v2 legacy block recovered in place ({LimbInscriptions.Count} limb inscription(s), inscription {(Inscription.IsNullOrEmpty() ? "empty" : "kept")})");
					return true;
				}
			}
			catch
			{
			}
			Inscription = null;
			LimbInscriptions = new Dictionary<int, string>();
			Reader.Stream.Position = Start + Length;
			Helpers.VerifyLog("SERIAL", $"Etchable legacy block reset ({Length} byte(s) skipped quietly)");
			return true;
		}

		public override bool SameAs(IPart p)
		{
			return false;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID
				|| ID == GetShortDescriptionEvent.ID
				|| ID == PooledEvent<GetDisplayNameEvent>.ID;
		}

		public bool IsEtchableSurface()
		{
			if (ParentObject.IsWall())
			{
				Cleo_TerraFirma_StoneshaperWallProperties props = ParentObject.GetPart<Cleo_TerraFirma_StoneshaperWallProperties>();
				return props != null && props.MaterialType != "metal";
			}
			return true;
		}

		public static bool HasEtchTools(GameObject actor)
		{
			return GetEtchToolTier(actor) >= 0;
		}

		public static int GetEtchToolTier(GameObject actor)
		{
			if (actor == null)
				return -1;
			int striker = -1, edge = -1;
			foreach (GameObject item in actor.GetInventoryAndEquipmentReadonly())
			{
				string kind = item.GetTag(TOOL_TAG);
				bool isStriker = kind == "Mallet";
				bool isEdge = kind == "Chisel";
				bool purposeMade = isStriker || isEdge;
				if (!purposeMade)
				{
					string skill = item.GetPart<MeleeWeapon>()?.Skill;
					if (skill == "Cudgel")
						isStriker = true;
					else if (skill == "ShortBlades")
						isEdge = true;
				}
				int tier = purposeMade
					? item.GetTier()
					: Math.Max(0, item.GetTier() - IMPROVISED_TIER_PENALTY);
				if (isStriker)
					striker = Math.Max(striker, tier);
				if (isEdge)
					edge = Math.Max(edge, tier);
			}
			return (striker < 0 || edge < 0) ? -1 : Math.Min(striker, edge);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (E.Actor != null && E.Actor.IsPlayer() && IsEtchableSurface() && HasEtchTools(E.Actor))
				E.AddAction("Etch", "etch", COMMAND, null, 'e', FireOnActor: false);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == COMMAND && E.Actor != null && E.Actor.IsPlayer())
				AttemptEtch(E.Actor);
			return base.HandleEvent(E);
		}

		private void AttemptEtch(GameObject actor)
		{
			if (!IsEtchableSurface())
				return;
			if (actor.DistanceTo(ParentObject) > 1)
			{
				actor.Fail("You cannot reach " + ParentObject.t() + ".");
				return;
			}
			int toolTier = GetEtchToolTier(actor);
			if (toolTier < 0)
			{
				actor.Fail("You need a hammer and a knife to etch.");
				return;
			}
			BodyPart limb = null;
			List<BodyPart> limbs = GetEtchableLimbs();
			if (limbs != null && limbs.Count > 0)
			{
				string[] limbNames = new string[limbs.Count];
				for (int i = 0; i < limbs.Count; i++)
					limbNames[i] = limbs[i].GetCardinalName();
				int pick = Popup.PickOption(
					Intro: "Choose a body part to etch.",
					Options: limbNames,
					AllowEscape: true);
				if (pick < 0)
					return;
				limb = limbs[pick];
			}
			bool reEtch = limb != null ? LimbInscriptions.ContainsKey(limb.ID) : !Inscription.IsNullOrEmpty();
			int choice = Popup.PickOption(
				Intro: reEtch ? "Something is already etched here. Etching again strikes it away." : "What would you like to etch?",
				Options: new string[] { "Etch a message.", "Write from memory." },
				AllowEscape: true);
			string text = null;
			if (choice == 0)
			{
				text = Popup.AskString("Etch your message. For example: \"live and drink\".", "", "Sounds/UI/ui_notification", TYPED_CHARS, null, TYPED_MAX);
				if (text.IsNullOrEmpty())
					return;
			}
			else if (choice == 1)
			{
				MarkovBook.EnsureCorpusLoaded(CORPUS);
				if (!MarkovBook.CorpusData.TryGetValue(CORPUS, out MarkovChainData data))
				{
					IComponent<GameObject>.AddPlayerMessage("You can't think of anything to etch.");
					return;
				}
				for (int i = 0; i < 5; i++)
				{
					text = MarkovChain.GenerateShortSentence(data).TrimEnd(' ');
					if (!text.Contains("="))
						break;
				}
			}
			else
			{
				return;
			}
			int garbled;
			text = GarbleByTier(text, toolTier, out garbled);
			if (garbled > 0)
				IComponent<GameObject>.AddPlayerMessage("Your rough tools slip, and some of the carving is lost.");
			if (reEtch)
				IComponent<GameObject>.AddPlayerMessage("You strike the old face away in a thin sheet of stone.");
			bool firstEtch = Inscription.IsNullOrEmpty() && LimbInscriptions.Count == 0;
			if (limb != null)
				LimbInscriptions[limb.ID] = text;
			else
				Inscription = text;
			string oldTile;
			char oldFg, oldDetail;
			CaptureFace(out oldTile, out oldFg, out oldDetail);
			if (firstEtch)
				ApplyEtchedLook();
			PlayEtchReveal(oldTile, oldFg, oldDetail);
			XRL.Core.XRLCore.Core.RenderBase();
			ParentObject.PlayWorldSound("Sounds/Damage/sfx_damage_stone");
			if (limb != null)
				Popup.Show("You etch \"" + text + "\" into " + Grammar.MakePossessive(ParentObject.t()) + " " + limb.GetOrdinalName() + ".");
			else
				Popup.Show("You etch \"" + text + "\" into " + ParentObject.t() + ".");
			actor.UseEnergy(1000, "Etching");
			Cleo.TerraFirma.Scripts.Helpers.VerifyLog("ETCH", $"{(reEtch ? "re-etched" : "etched")} {ParentObject.Blueprint}{(limb != null ? " [" + limb.GetOrdinalName() + "]" : "")} ({(choice == 0 ? "typed" : "markov")}, kit tier {toolTier}, {garbled} garbled): \"{text}\"");
		}

		private static string GarbleByTier(string text, int tier, out int garbled)
		{
			garbled = 0;
			int chance = (GARBLE_TIER_CEILING - tier) * GARBLE_PCT_PER_TIER;
			if (chance <= 0 || text.IsNullOrEmpty())
				return text;
			string slipped = Grammar.Obfuscate(text, chance);
			if (slipped == text)
				return text;
			if (slipped.Length != text.Length)
				garbled = text.Length;
			else
				for (int i = 0; i < text.Length; i++)
					if (slipped[i] != text[i])
						garbled++;
			return slipped;
		}

		public bool SetGeneratedInscription(string text)
		{
			if (string.IsNullOrWhiteSpace(text) || !Inscription.IsNullOrEmpty() || LimbInscriptions.Count != 0) return false;
			Inscription = text;
			ApplyEtchedLook();
			return true;
		}

		private void ApplyEtchedLook()
		{
			Render render = ParentObject.Render;
			if (render == null || render.DetailColor.IsNullOrEmpty())
				return;
			string source = !render.TileColor.IsNullOrEmpty() ? render.TileColor : render.ColorString;
			if (source.IsNullOrEmpty())
				return;
			int amp = source.IndexOf('&');
			if (amp < 0 || amp + 1 >= source.Length)
				return;
			char fg = source[amp + 1];
			char detail = render.DetailColor[0];
			if (fg == detail)
				return;
			render.DetailColor = fg.ToString();
			render.TileColor = SwapForeground(render.TileColor, detail);
			render.ColorString = SwapForeground(render.ColorString, detail);
		}

		private static string SwapForeground(string spec, char fg)
		{
			if (spec.IsNullOrEmpty())
				return spec;
			char[] chars = spec.ToCharArray();
			for (int i = 0; i < chars.Length - 1; i++)
			{
				if (chars[i] == '&')
					chars[i + 1] = fg;
			}
			return new string(chars);
		}

		private void CaptureFace(out string tile, out char fg, out char detail)
		{
			Render render = ParentObject.Render;
			tile = render?.Tile;
			fg = 'y';
			detail = 'K';
			if (render == null)
				return;
			string tc = !render.TileColor.IsNullOrEmpty() ? render.TileColor : render.ColorString;
			if (!tc.IsNullOrEmpty())
			{
				int amp = tc.IndexOf('&');
				if (amp >= 0 && amp + 1 < tc.Length)
					fg = tc[amp + 1];
			}
			if (!render.DetailColor.IsNullOrEmpty())
				detail = render.DetailColor[0];
		}

		private void PlayEtchReveal(string tile, char fg, char detail)
		{
			Cell cell = ParentObject.CurrentCell;
			if (cell == null || !cell.IsVisible() || tile.IsNullOrEmpty())
				return;
			Cleo_TerraFirma_StoneRiseFX.PlayEtchReveal(cell, tile, fg, detail);
		}

		private List<BodyPart> GetEtchableLimbs()
		{
			Body body = ParentObject.Body;
			if (body == null)
				return null;
			List<BodyPart> limbs = null;
			foreach (BodyPart part in body.GetParts())
			{
				if (part.Contact && !part.Abstract)
					(limbs ??= new List<BodyPart>()).Add(part);
			}
			return limbs;
		}

		public override bool HandleEvent(GetDisplayNameEvent E)
		{
			if ((!Inscription.IsNullOrEmpty() || LimbInscriptions.Count > 0) && (!E.Understood() || !E.Object.HasProperName))
				E.AddAdjective("etched");
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			if (!Inscription.IsNullOrEmpty())
			{
				if (E.Infix.Length > 0)
					E.Infix.Append("\n");
				E.Infix.Append("\nWords are chiseled into the surface. They read:\n\n\"").Append(Inscription).Append("\"\n");
			}
			if (LimbInscriptions.Count > 0 && ParentObject.Body is Body body)
			{
				foreach (KeyValuePair<int, string> kv in LimbInscriptions)
				{
					BodyPart part = body.GetPartByID(kv.Key);
					if (part == null)
						continue;
					if (E.Infix.Length > 0)
						E.Infix.Append("\n");
					E.Infix.Append("\nChiseled into ").Append(ParentObject.its).Append(" ").Append(part.GetOrdinalName())
						.Append(": \"").Append(kv.Value).Append("\"\n");
				}
			}
			return base.HandleEvent(E);
		}
	}
}
