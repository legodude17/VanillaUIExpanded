using System;
using HarmonyLib;
using Verse;

namespace VUIE
{
    public class ModCompatModule : Module
    {
        public override void DoPatches(Harmony harm)
        {
            if (ModLister.HasActiveModWithName("Blueprints"))
                harm.Patch(AccessTools.Method(AccessTools.TypeByName("Blueprints.Designator_Blueprint"), "GroupsWith"),
                    finalizer: new HarmonyMethod(GetType(), nameof(GroupsWithFinalizer)));
        }

        public static Exception GroupsWithFinalizer(Exception __exception, ref bool __result)
        {
            if (__exception is not NullReferenceException) return __exception;
            __result = false;
            return null;
        }
    }
}