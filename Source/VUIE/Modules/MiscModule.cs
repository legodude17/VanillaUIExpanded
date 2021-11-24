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

            if (__instance.InMentalState) text += "\n" + __instance.MentalState.InspectLine;
            else if (__instance.needs?.mood != null)
                text += "\n" + (__instance.needs.mood.CurLevel switch
                {
                    _ when __instance.mindState.mentalBreaker.BreakExtremeIsImminent => "VUIE.Moods.Extreme",
                    _ when __instance.mindState.mentalBreaker.BreakMajorIsImminent => "VUIE.Moods.Major",
                    _ when __instance.mindState.mentalBreaker.BreakMinorIsImminent => "VUIE.Moods.Minor",
                    > 0.9f => "VUIE.Moods.VeryHigh",
                    > 0.65f => "VUIE.Moods.High",
                    _ => "VUIE.Moods.Neutral"
                }).Translate();

            if (__instance.Inspired) text += "\n" + __instance.Inspiration.InspectLine;

            if (text != __result.text) __result = new TipSignal(text, __result.uniqueId, __result.priority);
        }
    }
}