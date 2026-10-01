using System;
using System.Collections.Generic;
using XRL.Wish;

namespace XRL.World.Parts
{
    [Serializable]
    public class Cleo_TerraFirma_SeismicShufflers : IPoweredPart
    {
        public int PreviewDuration = 2;
        public int PassiveRadius = 1;
        public int PeekChargeUse = 100;
        public float RiseSeconds = 0.25f;
        public int RiseStaggerMs = 250;
        public float RisePixels = 8f;
        public string PeekTile;
        public string PeekColor = "&y";
        public char PeekDetailColor = 'C';
        public Guid AbilityID;
        public string CommandID;
        public List<GameObject> Projections = new List<GameObject>();

        public Cleo_TerraFirma_SeismicShufflers()
        {
            ChargeUse = 1;
            WorksOnEquipper = true;
        }

        public override bool SameAs(IPart p) => false;

        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade) || ID == EquippedEvent.ID || ID == UnequippedEvent.ID;
        }

        public override bool HandleEvent(EquippedEvent E)
        {
            if (E.Item == ParentObject && E.Item.IsEquippedProperly() && E.Actor.IsPlayer())
            {
                E.Actor.RegisterEvent(this, EnteredCellEvent.ID, 0, Serialize: true);
                E.Actor.RegisterEvent(this, CommandEvent.ID, 0, Serialize: true);
                if (AbilityID == Guid.Empty)
                {
                    CommandID = "Cleo_TerraFirma_SeismicPeek_" + Guid.NewGuid().ToString("N");
                    AbilityID = E.Actor.AddActivatedAbility("Seismic Peek", CommandID, "Items",
                        "You briefly project holograms of nearby walls one stratum below you.\n\n" +
                        $"Duration: {PreviewDuration} rounds\nLow charge draw on activate.", "~");
                }
                var ability = E.Actor.GetPart<ActivatedAbilities>()?.GetAbility(AbilityID);
                if (ability != null && !string.IsNullOrEmpty(PeekTile))
                    ability.UITileDefault = new ConsoleLib.Console.Renderable(PeekTile, "~", PeekColor, null, PeekDetailColor);
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(UnequippedEvent E)
        {
            if (E.Item == ParentObject)
            {
                E.Actor.UnregisterEvent(this, EnteredCellEvent.ID);
                E.Actor.UnregisterEvent(this, CommandEvent.ID);
                E.Actor.RemoveActivatedAbility(ref AbilityID);
                ClearProjections();
            }
            return base.HandleEvent(E);
        }

        private GameObject Wearer => ParentObject.IsEquippedProperly() ? ParentObject.Equipped : null;

        private static Cell Below(Cell cell)
        {
            if (cell == null || cell.OnWorldMap() || cell.ParentZone.IsWorldMap())
                return null;
            return cell.GetCellFromDirectionGlobal("D", bLocalOnly: false, bLiveZonesOnly: false);
        }

        public override bool HandleEvent(EnteredCellEvent E)
        {
            GameObject wearer = Wearer;
            if (wearer != null && wearer.IsPlayer() && E.Object == wearer && !E.Forced && !E.System)
            {
                Cell below = Below(wearer.CurrentCell);
                if (below != null && IsReady(UseCharge: true))
                {
                    int mapped = 0;
                    for (int dy = -PassiveRadius; dy <= PassiveRadius; dy++)
                    for (int dx = -PassiveRadius; dx <= PassiveRadius; dx++)
                    {
                        int x = below.X + dx, y = below.Y + dy;
                        if (x < 0 || y < 0 || x >= below.ParentZone.Width || y >= below.ParentZone.Height)
                            continue;
                        below.ParentZone.GetCell(x, y).SetExplored();
                        mapped++;
                    }
                    Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHUFFLERS",
                        $"mapped {mapped} cells around {below.ParentZone.ZoneID}@{below.X},{below.Y}");
                }
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(CommandEvent E)
        {
            if (E.Command != CommandID || string.IsNullOrEmpty(CommandID))
                return base.HandleEvent(E);
            GameObject wearer = Wearer;
            if (wearer == null || !wearer.IsPlayer())
                return false;
            foreach (GameObject projection in Projections)
                if (GameObject.Validate(projection) && projection.CurrentCell != null)
                    return wearer.Fail("Wait for the current projection to disappear.");
            Cell origin = wearer.CurrentCell;
            Cell below = Below(origin);
            if (below == null)
                return wearer.Fail("There is no lower stratum to scan here.");
            if (!IsReady(UseCharge: true, ChargeUse: PeekChargeUse))
                return wearer.Fail(ParentObject.Does("click", int.MaxValue, null, null, "merely") + ".");

            ClearProjections();
            int walls = 0;
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                if (Math.Abs(dx) == 2 && Math.Abs(dy) == 2)
                    continue;
                int x = origin.X + dx, y = origin.Y + dy;
                if (x < 0 || y < 0 || x >= origin.ParentZone.Width || y >= origin.ParentZone.Height
                    || x >= below.ParentZone.Width || y >= below.ParentZone.Height)
                    continue;
                GameObject wall = below.ParentZone.GetCell(x, y).GetFirstWall();
                if (wall?.Render == null)
                    continue;
                GameObject projection = GameObject.Create("Cleo_TerraFirma_SeismicProjection");
                projection.Render.Tile = wall.Render.Tile;
                projection.Render.RenderString = wall.Render.RenderString;
                projection.Render.HFlip = wall.Render.HFlip;
                projection.Render.VFlip = wall.Render.VFlip;
                projection.GetPart<Temporary>().Duration = Math.Max(1, PreviewDuration);
                projection.GetPart<Temporary>().LastTurn = The.Game.Turns;
                Cell target = origin.ParentZone.GetCell(x, y);
                Cleo.TerraFirma.Scripts.SeismicRise arrival = null;
                string skip = RiseSeconds <= 0 ? "animation disabled"
                    : !target.ParentZone.IsActive() ? "inactive zone"
                    : target.GetFirstWall() != null ? "real wall in destination"
                    : y + 1 < target.ParentZone.Height && target.ParentZone.GetCell(x, y + 1).GetFirstWall() != null
                        ? "real wall immediately south" : null;
                if (skip == null)
                    arrival = new Cleo.TerraFirma.Scripts.SeismicRise(projection, target,
                        XRL.Rules.Stat.RandomCosmetic(0, Math.Max(0, RiseStaggerMs)) / 1000f,
                        RiseSeconds, RisePixels);
                else
                    Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHUFFLERS-RISE", $"{x},{y}: skipped ({skip})");
                target.AddObject(projection);
                if (arrival != null)
                    CombatJuiceManager.enqueueEntry(arrival, async: true);
                Projections.Add(projection);
                walls++;
            }
            Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHUFFLERS",
                $"peek {below.ParentZone.ZoneID}: {walls} projected walls, {PreviewDuration} turns");
            if (walls == 0)
                wearer.ShowFailure("The scan finds no walls immediately below.");
            wearer.UseEnergy(1000, "Item Seismic Peek");
            return false;
        }

        private void ClearProjections()
        {
            foreach (GameObject projection in Projections)
                if (GameObject.Validate(projection))
                    projection.Obliterate(Silent: true);
            Projections.Clear();
        }
    }
}
