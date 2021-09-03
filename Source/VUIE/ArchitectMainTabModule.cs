using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class ArchitectMainTabModule : Module
    {
        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(MainTabWindow_Architect), nameof(MainTabWindow_Architect.DoWindowContents)),
                new HarmonyMethod(typeof(ArchitectMainTabModule), nameof(DoEdit)));
        }

        public static bool DoEdit(MainTabWindow_Architect __instance, Rect inRect)
        {
            if (UIDefOf.UI_EditMode.Worker.Active)
            {
                Text.Font = GameFont.Small;
                var num = inRect.width / 2f;
                var num2 = 0f;
                var num3 = 0f;
                var num4 = 0f;
                var architectCategoryTab = __instance.OpenTab();
                if (KeyBindingDefOf.Accept.KeyDownEvent)
                {
                    if (__instance.quickSearchWidget.filter.Active && architectCategoryTab != null && architectCategoryTab.UniqueSearchMatch != null)
                        __instance.forceActivatedCommand = architectCategoryTab.UniqueSearchMatch;
                    else
                        __instance.Close();

                    Event.current.Use();
                }

                foreach (var tab in __instance.desPanelsCached)
                {
                    var rect = new Rect(num2 * num, num3 * 32f, num, 32f);
                    var height = rect.height;
                    rect.height = height + 1f;
                    if (num2 == 0f) rect.width += 1f;

                    var labelColor = tab.AnySearchMatches ? null : new Color?(MainTabWindow_Architect.NoMatchColor);
                    string label = tab.def.LabelCap;
                    if (Widgets.ButtonTextSubtle(rect, label, 0f, 8f, SoundDefOf.Mouseover_Category, new Vector2(-1f, -1f), labelColor,
                        __instance.quickSearchWidget.filter.Active && architectCategoryTab == tab))
                        __instance.ClickedCategory(tab);

                    if (__instance.selectedDesPanel != tab) UIHighlighter.HighlightOpportunity(rect, tab.def.cachedHighlightClosedTag);

                    num4 = Mathf.Max(num4, rect.yMax);
                    num2 += 1f;
                    if (num2 > 1f)
                    {
                        num2 = 0f;
                        num3 += 1f;
                    }
                }

                var rect2 = new Rect(0f, num4 + 1f, inRect.width, 24f);
                __instance.quickSearchWidget.OnGUI(rect2, __instance.CacheSearchState);
                if (!__instance.didInitialUnfocus)
                {
                    UI.UnfocusCurrentControl();
                    __instance.didInitialUnfocus = true;
                }

                return false;
            }

            return true;
        }
    }
}