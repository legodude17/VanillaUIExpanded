using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace VUIE;

public static class OverlayController
{
    private static readonly List<OverlayWorker> visible = new();
    private static readonly List<OverlayWorker> updateInvisible = new();

    public static void RegisterUpdateInvisible(OverlayWorker worker)
    {
        updateInvisible.Add(worker);
    }

    public static void RemoveUpdate(OverlayWorker worker)
    {
        updateInvisible.Remove(worker);
    }

    public static void Notify_Visible(OverlayWorker worker)
    {
        visible.Add(worker);
    }

    public static void Notify_Invisible(OverlayWorker worker)
    {
        visible.Remove(worker);
    }

    public static void Notify_ThingChanged(Thing __instance)
    {
        if (__instance.Map == Find.CurrentMap)
            foreach (var worker in visible)
                worker.Notify_BuildingChanged(__instance);
    }

    public static void Notify_CurrentMapChanged()
    {
        foreach (var worker in visible) worker.Notify_Map(Find.CurrentMap);
    }

    public static void Notify_MapMeshDirty(Map ___map, IntVec3 loc, MapMeshFlag dirtyFlags, bool regenAdjacentCells, bool regenAdjacentSections)
    {
        if (___map == Find.CurrentMap)
            foreach (var worker in visible)
                worker.Notify_MapMeshChanged(loc, dirtyFlags, regenAdjacentCells, regenAdjacentSections);
    }

    public static void OverlaysOnGUI()
    {
        if (WorldRendererUtility.WorldRenderedNow || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;
        foreach (var worker in visible) worker.OverlayOnGUI();
    }

    public static void OverlaysUpdate()
    {
        if (WorldRendererUtility.WorldRenderedNow || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;
        foreach (var worker in visible) worker.OverlayUpdate();
        foreach (var worker in updateInvisible) worker.OverlayUpdate();
        foreach (var worker in visible) worker.OverlayDraw();
    }
}