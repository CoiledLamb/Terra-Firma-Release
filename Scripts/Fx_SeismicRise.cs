using System;
using ConsoleLib.Console;
using Kobold;
using UnityEngine;
using XRL.World;
using XRL.World.Parts;
using QudObject = XRL.World.GameObject;

namespace XRL.World.Parts
{
    [Serializable]
    public class Cleo_TerraFirma_SeismicArrival : IPart
    {
        [NonSerialized] public bool Rising;
        [NonSerialized] public long Deadline;

        public void Begin()
        {
            Rising = true;
            Deadline = System.Diagnostics.Stopwatch.GetTimestamp() + 2 * System.Diagnostics.Stopwatch.Frequency;
            ParentObject.Render.Visible = false;
        }

        public void FinishArrival()
        {
            Rising = false;
            if (QudObject.Validate(ParentObject) && ParentObject.Render != null)
                ParentObject.Render.Visible = true;
        }

        public override bool WantEvent(int ID, int cascade) => base.WantEvent(ID, cascade)
            || ID == AfterGameLoadedEvent.ID || ID == EndTurnEvent.ID;

        public override bool HandleEvent(AfterGameLoadedEvent E)
        {
            FinishArrival();
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EndTurnEvent E)
        {
            if (Rising && System.Diagnostics.Stopwatch.GetTimestamp() >= Deadline)
                Cleo.TerraFirma.Scripts.Helpers.VerifyLog("SHUFFLERS-RISE",
                    $"timeout fallback at {ParentObject.CurrentCell?.X},{ParentObject.CurrentCell?.Y}");
            if (!Rising || System.Diagnostics.Stopwatch.GetTimestamp() >= Deadline)
                FinishArrival();
            return base.HandleEvent(E);
        }
    }
}

namespace Cleo.TerraFirma.Scripts
{
    public class SeismicRise : CombatJuiceEntry
    {
        private readonly QudObject Projection;
        private readonly Cell Cell;
        private readonly float Delay, Rise, Distance;
        private readonly Cleo_TerraFirma_SeismicArrival Arrival;
        private ex3DSprite2 Sprite;
        private Vector3 Rest;
        private Color Foreground, Background, Detail;
        private bool Reported;

        public SeismicRise(QudObject projection, Cell cell, float delay, float rise, float distance)
        {
            Projection = projection;
            Cell = cell;
            Delay = Math.Max(0, delay);
            Rise = Math.Max(0.01f, rise);
            Distance = distance;
            duration = Delay + Rise;
            Arrival = projection.GetPart<Cleo_TerraFirma_SeismicArrival>();
            Arrival.Begin();
        }

        private bool CanContinue() => QudObject.Validate(Projection) && Projection.CurrentCell == Cell
            && Cell.ParentZone.IsActive() && Cell.GetFirstWall() == null;

        public override bool canFinishUpToTurn() => t >= duration || !CanContinue()
            || System.Diagnostics.Stopwatch.GetTimestamp() >= Arrival.Deadline;

        private void Report(string reason)
        {
            if (Reported) return;
            Reported = true;
            Helpers.VerifyLog("SHUFFLERS-RISE",
                $"{Cell.X},{Cell.Y}: {reason}; elapsed={t:F3}s delay={Delay:F3}s duration={duration:F3}s; {CheckDetails()}");
        }

        private string CheckDetails()
        {
            bool valid = QudObject.Validate(Projection);
            Cell current = valid ? Projection.CurrentCell : null;
            return $"projectionValid={valid}, sameCell={current == Cell}, " +
                $"currentCell={(current == null ? "none" : current.X + "," + current.Y)}, " +
                $"zoneActive={Cell.ParentZone.IsActive()}, cellVisible={Cell.IsVisible()}, " +
                $"destinationWall={Cell.GetFirstWall()?.Blueprint ?? "none"}, " +
                $"timedOut={System.Diagnostics.Stopwatch.GetTimestamp() >= Arrival.Deadline}";
        }

        public override void start()
        {
            if (!CanContinue()) { Report("invalid at start"); Arrival.FinishArrival(); return; }
            var tile = new Renderable(Projection.Render.Tile, Projection.Render.RenderString, "&C", null, 'c');
            Sprite = SpriteManager.GetPooledSprite(tile, Transparent: true);
            Foreground = Sprite.color;
            Background = Sprite.backcolor;
            Detail = Sprite.detailcolor;
            Sprite.transform.SetParent(GameManager.Instance.TileRoot.transform);
            Rest = GameManager.Instance.getTileCenter(Cell.X, Cell.Y);
            Sprite.transform.position = Rest + new Vector3(0, 0, 100);
        }

        public override void update()
        {
            if (Sprite == null) return;
            if (!CanContinue() || t >= duration || System.Diagnostics.Stopwatch.GetTimestamp() >= Arrival.Deadline)
            {
                Report(!CanContinue() ? "lost validity" : t >= duration ? "completed" : "timeout");
                Arrival.FinishArrival();
                Sprite.transform.position = Rest + new Vector3(0, 0, 100);
                return;
            }
            if (t < Delay || !Cell.IsVisible())
            {
                Sprite.transform.position = Rest + new Vector3(0, 0, 100);
                return;
            }
            Sprite.color = Foreground;
            Sprite.backcolor = Background;
            Sprite.detailcolor = Detail;
            float p = Mathf.Clamp01((t - Delay) / Rise);
            float eased = 1f - (1f - p) * (1f - p);
            Sprite.transform.position = Rest + new Vector3(0, -Distance * (1f - eased), -10);
        }

        public override void finish()
        {
            Report(t >= duration ? "completed" : "forced finish before completion");
            Arrival.FinishArrival();
            if (Sprite != null) { SpriteManager.Return(Sprite); Sprite = null; }
            base.finish();
        }
    }
}
