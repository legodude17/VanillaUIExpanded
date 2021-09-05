using RimWorld;
using Verse;

namespace VUIE
{
    [DefOf]
    public class UIDefOf
    {
        public static PlaySettingDef UI_EditMode;
        public static MainButtonDef VUIE_Overlays;
        public static KeyBindingDef VUIE_CycleOverlay;

        static UIDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(UIDefOf));
        }
    }

    [DefOf]
    public static class OverlayDefOf
    {
        public static OverlayDef Beauty;
        public static OverlayDef Lighting;
        public static OverlayDef WalkSpeed;
        public static OverlayDef Usage;
        public static OverlayDef Cleanliness;
        public static OverlayDef PlantGrowth;
        public static OverlayDef Smoothable;
        public static OverlayDef WindBlockers;

        static OverlayDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(OverlayDefOf));
        }
    }
}