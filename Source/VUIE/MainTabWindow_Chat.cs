using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class MainTabWindow_Chat : MainTabWindow
    {
        private Vector2 scrollPos = Vector2.zero;

        public override Vector2 RequestedTabSize => new(1010f, 640f);

        public override void DoWindowContents(Rect inRect)
        {
            inRect = inRect.ContractedBy(7f);
            var viewRect = new Rect(0, 0, inRect.width - 25f, Find.PlayLog.AllEntries.Count * 28f);
            var listing = new Listing_Standard();
            Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);
            listing.Begin(viewRect);
            var alternatingBackground = true;
            foreach (var entry in Find.PlayLog.AllEntries)
            {
                var row = listing.StartRow();
                var y = row.curY;
                Text.Font = GameFont.Tiny;
                var pov = Find.Selector.SingleSelectedThing is not null && entry.Concerns(Find.Selector.SingleSelectedThing)
                    ? Find.Selector.SingleSelectedThing
                    : entry.GetConcerns().FirstOrDefault();
                row.Label(entry.Tick.ToStringTicksToTime().Colorize(ColoredText.SubtleGrayColor));
                row.Gap(12f);
                row.Icon(entry.IconFromPOV(pov));
                row.Gap(3f);
                Text.Font = GameFont.Small;
                TooltipHandler.TipRegion(row.Label(entry.ToGameStringFromPOV(pov)), entry.GetTipString());
                row.Gap(12f);

                var rect = new Rect(listing.curX, listing.curY, row.FinalX - row.startX, row.FinalY + 24f + row.CellGap - y);
                if (alternatingBackground) Widgets.DrawRectFast(rect, new Color(1f, 1f, 1f, ITab_Pawn_Log_Utility.AlternateAlpha));
                Widgets.DrawHighlightIfMouseover(rect);
                if (Mouse.IsOver(rect))
                    foreach (var concern in entry.GetConcerns())
                        TargetHighlighter.Highlight(concern);
                if (Widgets.ButtonInvisible(rect, false))
                {
                    CameraJumper.TryJump(entry.GetConcerns().FirstOrDefault());
                    foreach (var o in Find.Selector.selected) Find.Selector.Deselect(o);
                    foreach (var concern in entry.GetConcerns()) Find.Selector.Select(concern);
                }

                listing.EndRow(row);
                alternatingBackground = !alternatingBackground;
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}