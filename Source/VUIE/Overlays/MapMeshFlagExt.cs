using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace VUIE;

public class MapMeshFlagExtDirtier
{
    private MethodInfo Original => AccessTools.Method(typeof(Pawn), nameof(Pawn.Tick));
    private MethodInfo Patch => AccessTools.Method(GetType(), nameof(RecordUsage));

    public void DoPatches(Harmony harm)
    {
        harm.Patch(Original, postfix: new HarmonyMethod(Patch));
    }

    public void UndoPatches(Harmony harm)
    {
        harm.Unpatch(Original, Patch);
    }

    public static void RecordUsage(Pawn __instance)
    {
        if (!__instance.Spawned || !__instance.IsColonist || __instance.Map != Find.CurrentMap) return;
        if (OverlayWorker_Usage.Grid[__instance.Position]++ >= byte.MaxValue - 1)
        {
            var min = OverlayWorker_Usage.Grid.grid.Min();
            for (var i = 0; i < __instance.Map.cellIndices.NumGridCells; i++) OverlayWorker_Usage.Grid[i] -= min;
        }

        if (__instance.IsHashIntervalTick(360)) __instance.Map.mapDrawer.MapMeshDirty(__instance.Position, OverlayWorker_Usage.UsageFlag, false, false);
    }
}

[Flags]
public enum MapMeshFlagExt
{
    Usage = 1024
}