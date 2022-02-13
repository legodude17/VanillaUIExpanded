using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class MainTabWindow_Overlays : MainTabWindow
    {
        public MainTabWindow_Overlays()
        {
            drawShadow = false;
            doCloseX = false;
            doWindowBackground = false;
        }

        public OverlayDef CurrentOverlay
        {
            get
            {
                var visible = DefDatabase<OverlayDef>.AllDefs.Where(d => d.Worker.DrawToggle && d.Worker.Visible).ToList();
                return visible.Count == 1 ? visible[0] : null;
            }

            set
            {
                foreach (var d in DefDatabase<OverlayDef>.AllDefs) d.Worker.Visible = false;

                if (value != null) value.Worker.Visible = true;
            }
        }

        public override float Margin => 0;
        public override Vector2 RequestedTabSize => new(141f, Mathf.CeilToInt(DefDatabase<OverlayDef>.AllDefs.Count(d => d.Worker.DrawToggle) / 5f) * 33f);

        public override void PostClose()
        {
            base.PostClose();
            UIMod.Settings.Write();
        }

        public override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            windowRect.x = UI.screenWidth - 283f;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var row = new WidgetRow(inRect.x, inRect.y, UIDirection.RightThenDown, inRect.width);
            DoOverlayToggles(row);
        }

        public static void DoOverlayToggles(WidgetRow row)
        {
            foreach (var overlayDef in DefDatabase<OverlayDef>.AllDefs)
                if (overlayDef.Worker.DrawToggle)
                    overlayDef.Worker.DoInterface(row);
                else if (overlayDef.Worker.Visible)
                    overlayDef.Worker.Visible = false;
        }
    }
}