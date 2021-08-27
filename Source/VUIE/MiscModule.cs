using HarmonyLib;
using Verse;

namespace VUIE
{
    public class MiscModule : Module
    {
        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.GetTooltip)), postfix: new HarmonyMethod(typeof(MiscModule), nameof(AddPawnInfo)));
        }

        public static void AddPawnInfo(Pawn __instance, ref TipSignal __result)
        {
            var text = __result.text;
            if (__instance.CurJob != null) text += "\n" + __instance.jobs.curDriver.GetReport().CapitalizeFirst();

            if (__instance.needs?.mood != null)
            {
                if (__instance.InMentalState) text += "\n" + __instance.MentalState.InspectLine;
                else if (__instance.mindState.mentalBreaker.BreakExtremeIsImminent) text += "\nFeeling depressed";
                else if (__instance.mindState.mentalBreaker.BreakMajorIsImminent) text += "\nFeeling unhappy";
                else if (__instance.mindState.mentalBreaker.BreakMinorIsImminent) text += "\nFeeling stressed";
                else if (__instance.needs.mood.CurLevel > 0.9f) text += "\nFeeling happy";
                else if (__instance.needs.mood.CurLevel > 0.65f) text += "\nFeeling content";
                else __result.text += "\nFeeling indifferent";
            }

            if (__instance.Inspired) text += "\n" + __instance.Inspiration.InspectLine;

            if (text != __result.text) __result = new TipSignal(text, __result.uniqueId, __result.priority);
        }
    }
}