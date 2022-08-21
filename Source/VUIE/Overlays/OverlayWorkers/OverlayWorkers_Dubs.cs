using System;
using HarmonyLib;
using Verse;

namespace VUIE;

public abstract class OverlayWorker_DBH : OverlayWorker_DubsPipes
{
    public override Type SectionLayerType => AccessTools.TypeByName("DubsBadHygiene.SectionLayer_PipeOverlay");
}

public abstract class OverlayWorker_Rimatomics : OverlayWorker_DubsPipes
{
    public override Type SectionLayerType => AccessTools.TypeByName("Rimatomics.SectionLayer_OverlayPipe");
}

public class OverlayWorker_Sewage : OverlayWorker_DBH
{
    public override int PipeType => 0;
}

public class OverlayWorker_Air : OverlayWorker_DBH
{
    public override int PipeType => 1;
}

public class OverlayWorker_HighVoltage : OverlayWorker_Rimatomics
{
    public override int PipeType => 0;
}

public class OverlayWorker_Cooling : OverlayWorker_Rimatomics
{
    public override int PipeType => 1;
}

public class OverlayWorker_Steam : OverlayWorker_Rimatomics
{
    public override int PipeType => 2;
}

public class OverlayWorker_ColdWater : OverlayWorker_Rimatomics
{
    public override int PipeType => 3;
}

public class OverlayWorker_Loom : OverlayWorker_Rimatomics
{
    public override int PipeType => 4;
}

public abstract class OverlayWorker_Rimefeller : OverlayWorker_DubsPipes
{
    public override Type SectionLayerType => AccessTools.TypeByName("Rimefeller.SectionLayer_PipeOverlay");
}

public class OverlayWorker_Oil : OverlayWorker_Rimefeller
{
    public override int PipeType => 0;
}

public class OverlayWorker_OilGrid_Normal : OverlayWorker_OilGrid
{
    public override void OverlayDraw()
    {
        base.OverlayDraw();
        DrawOilGrid();
    }
}

public class OverlayWorker_OilGrid_Deep : OverlayWorker_OilGrid
{
    public override void OverlayDraw()
    {
        base.OverlayDraw();
        DrawDeepOilGrid();
    }
}

public class OverlayWorker_WaterGrid : OverlayWorker
{
    private FastInvokeHandler drawWaterGrid;
    private Func<Map, MapComponent> getHygiene;
    private AccessTools.FieldRef<object, object> getWaterGrid;
    private Type hygieneMapComp;
    private AccessTools.FieldRef<object, bool> towerDraw;


    public override void OverlayDraw()
    {
        base.OverlayDraw();
        var hygiene = getHygiene(Find.CurrentMap);
        towerDraw(hygiene) = true;
        drawWaterGrid(getWaterGrid(hygiene));
    }

    public override void InitInner()
    {
        base.InitInner();
        getHygiene = AccessTools.MethodDelegate<Func<Map, MapComponent>>(AccessTools.Method(AccessTools.TypeByName("DubsBadHygiene.DubUtils"),
            "Hygiene",
            new[] { typeof(Map) }));
        hygieneMapComp = AccessTools.TypeByName("DubsBadHygiene.MapComponent_Hygiene");
        getWaterGrid = AccessTools.FieldRefAccess<object>(hygieneMapComp, "WaterGrid");
        towerDraw = AccessTools.FieldRefAccess<bool>(hygieneMapComp, "MarkTowersForDraw");
        drawWaterGrid = MethodInvoker.GetHandler(AccessTools.Method(AccessTools.TypeByName("DubsBadHygiene.GridLayer"), "MarkForDraw"));
    }
}