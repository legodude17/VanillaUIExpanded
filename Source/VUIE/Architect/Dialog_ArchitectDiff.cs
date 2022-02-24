using System.Collections.Generic;
using UnityEngine;
using Verse;
using VFECore.UItils;

namespace VUIE
{
    public class Dialog_ArchitectDiff : Window
    {
        private const float DIFF_INDENT = 10f;
        private readonly List<Diff> diffs;
        private readonly ArchitectModule me;
        private readonly Diff parentDiff;
        private float diffCenter;
        private float diffCurX;
        private float diffCurY;
        private bool diffDrawLine;
        private float diffHeight;
        private Vector2 diffScrollPos;
        private Rect viewRect;

        public Dialog_ArchitectDiff(ArchitectModule module, Diff diff)
        {
            doCloseButton = false;
            doCloseX = false;
            me = module;
            parentDiff = diff;
            diffs = diff.children;
        }

        public override Vector2 InitialSize => new(760f, 840f);

        public override void DoWindowContents(Rect inRect)
        {
            var text = "VUIE.Architect.ChangeDetect".Translate(me.SavedStates[me.ActiveIndex].Name);
            var textRect = inRect.TakeTopPart(Text.CalcHeight(text, inRect.width));
            Widgets.Label(textRect, text);

            var buttonsRect = inRect.TakeBottomPart(45f);

            Widgets.DrawMenuSection(new Rect(inRect.x, inRect.y, inRect.width - 20, inRect.height));

            viewRect = new Rect(0, 0, inRect.width - 20, diffHeight).ContractedBy(3f);
            Widgets.BeginScrollView(inRect, ref diffScrollPos, viewRect);

            diffHeight = 0;
            diffCenter = viewRect.width / 2;
            diffCurY = viewRect.y;
            diffCurX = viewRect.x + 3f;
            diffDrawLine = false;

            Widgets.DrawLineVertical(diffCenter, viewRect.y, viewRect.height);

            foreach (var diff in diffs) DrawDiff(diff);

            diffHeight = diffCurY - viewRect.y + 3f;

            Widgets.EndScrollView();

            if (Widgets.ButtonText(buttonsRect.RightHalf().RightHalf().ContractedBy(2.5f), "Yes".Translate()))
            {
                me.SavedStates[me.ActiveIndex] = parentDiff.ApplyTo(me.SavedStates[me.ActiveIndex]);
                ArchitectLoadSaver.EnsureCached(me.SavedStates[me.ActiveIndex]);
                UIMod.Settings.Write();
                Close();
            }

            var copyRect = buttonsRect.RightHalf().LeftHalf().ContractedBy(2.5f);
            if (Widgets.ButtonText(copyRect, "Copy".Translate()))
            {
                ArchitectLoadSaver.EnsureCached(me.AddState(parentDiff.ApplyTo(me.SavedStates[me.ActiveIndex])));
                UIMod.Settings.Write();
                Close();
            }

            TooltipHandler.TipRegionByKey(copyRect, "VUIE.Architect.MakeCopy");

            if (Widgets.ButtonText(buttonsRect.LeftHalf().ContractedBy(2.5f), "No".Translate())) Close();
        }

        private void DrawDiff(Diff diff)
        {
            if (diffDrawLine) Widgets.DrawLineHorizontal(viewRect.x, diffCurY, viewRect.width);
            diffDrawLine = true;
            var label = diff.Label.CapitalizeFirst();
            var height = Text.CalcHeight(label, diffCenter - diffCurX) + 2;
            var left = new Rect(diffCurX, diffCurY, diffCenter - diffCurX, height).ContractedBy(1);
            var right = new Rect(diffCenter + diffCurX, diffCurY, diffCenter - diffCurX, height).ContractedBy(1);

            switch (diff.type)
            {
                case DiffType.Added:
                    Widgets.DrawRectFast(right, new Color(0, 0.39f, 0));
                    Widgets.Label(right, label);
                    break;
                case DiffType.Removed:
                    Widgets.DrawRectFast(left, new Color(0.54f, 0, 0));
                    Widgets.Label(left, label);
                    break;
                case DiffType.Same:
                    Widgets.Label(left, label);
                    Widgets.Label(right, label);
                    break;
            }

            diffCurY += height;

            if (diff.children.NullOrEmpty()) return;

            diffCurX += DIFF_INDENT;

            foreach (var child in diff.children) DrawDiff(child);

            diffCurX -= DIFF_INDENT;
        }
    }
}