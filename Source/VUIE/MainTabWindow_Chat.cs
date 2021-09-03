using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class MainTabWindow_Chat : EditWindow
    {
        private Vector2 scrollPos = Vector2.zero;

        public MainTabWindow_Chat()
        {
            drawShadow = false;
            doCloseX = false;
            doWindowBackground = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            soundClose = SoundDefOf.TabClose;
        }

        public override void SetInitialSizeAndPosition()
        {
            windowRect = new Rect(1289f, 528f, 505f, 517f);
        }

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
                var pov = Find.Selector.SingleSelectedThing is not null && entry.Concerns(Find.Selector.SingleSelectedThing)
                    ? Find.Selector.SingleSelectedThing
                    : entry.GetConcerns().FirstOrDefault();
                var time = entry.Tick.ToStringTicksToTime().Colorize(ColoredText.SubtleGrayColor);
                var rect = listing.GetRect(entry.GetTextHeight(pov, viewRect.width - 15f - 24f - 92f));
                Text.Font = GameFont.Tiny;
                var x = rect.x;
                Widgets.Label(new Rect(rect.x, rect.y, 92f, rect.height), time);
                x += 92f;
                Widgets.DrawTextureFitted(new Rect(x, rect.y, 24f, 24f), entry.IconFromPOV(pov), 1f);
                x += 27f;
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(x, rect.y, rect.xMax - x, rect.height), entry.ToGameStringFromPOV(pov));
                TooltipHandler.TipRegion(rect, entry.GetTipString());
                if (alternatingBackground) Widgets.DrawRectFast(rect, new Color(1f, 1f, 1f, ITab_Pawn_Log_Utility.AlternateAlpha));
                Widgets.DrawHighlightIfMouseover(rect);
                if (Mouse.IsOver(rect))
                    foreach (var concern in entry.GetConcerns())
                        TargetHighlighter.Highlight(concern);
                if (Widgets.ButtonInvisible(rect, false))
                {
                    CameraJumper.TryJump(entry.GetConcerns().FirstOrDefault());
                    Find.Selector.selected.Clear();
                    foreach (var concern in entry.GetConcerns()) Find.Selector.Select(concern);
                }

                alternatingBackground = !alternatingBackground;
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }

    public class MainButtonWorker_Chat : MainButtonWorker
    {
        private Window tabWindow;

        public override void Activate()
        {
            tabWindow ??= (Window) Activator.CreateInstance(def.tabWindowClass);
            if (tabWindow.IsOpen) tabWindow.Close();
            else Find.WindowStack.Add(tabWindow);
        }
    }
}