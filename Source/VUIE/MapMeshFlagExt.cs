using System;
using System.Linq;
using HarmonyLib;
using Verse;

namespace VUIE
{
    public class MapMeshFlagExtDirtier
    {
        public void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.Tick)), postfix: new HarmonyMethod(GetType(), nameof(RecordUsage)));
        }

        public static void RecordUsage(Pawn __instance)
        {
            if (!__instance.Spawned || !__instance.IsColonist || !OverlayDefOf.Usage.Worker.DrawToggle) return;
            if (SectionLayer_Usage.Grids[__instance.Map][__instance.Position]++ >= byte.MaxValue - 1)
            {
                var min = SectionLayer_Usage.Grids[__instance.Map].grid.Min();
                for (var i = 0; i < __instance.Map.cellIndices.NumGridCells; i++) SectionLayer_Usage.Grids[__instance.Map][i] -= min;
            }

            if (__instance.IsHashIntervalTick(360)) __instance.Map.mapDrawer.MapMeshDirty(__instance.Position, SectionLayer_Usage.UsageFlag, false, false);
        }
    }

    [Flags]
    public enum MapMeshFlagExt
    {
        Usage = 1024
    }
}