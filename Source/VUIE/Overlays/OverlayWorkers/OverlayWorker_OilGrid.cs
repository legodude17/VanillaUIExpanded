using System;
using HarmonyLib;
using Verse;

namespace VUIE;

public abstract class OverlayWorker_OilGrid : OverlayWorker
{
    private static FastInvokeHandler drawOilGrid;
    private static AccessTools.FieldRef<object, object> getDeepOilGrid;
    private static AccessTools.FieldRef<object, object> getOilGrid;
    private static Func<Map, MapComponent> getRimefeller;
    private static MapComponent rimefeller;
    private static Type rimefellerMapComp;
    private static AccessTools.FieldRef<object, bool> towerDraw;
    protected static MapComponent Rimefeller => rimefeller ??= getRimefeller(Find.CurrentMap);

    public override void OverlayUpdate()
    {
        base.OverlayUpdate();
        rimefeller = null;
    }

    public override void OverlayDraw()
    {
        base.OverlayDraw();
        towerDraw(Rimefeller) = true;
    }

    public override void InitInner()
    {
        base.InitInner();
        if (getRimefeller is not null) return;
        getRimefeller = AccessTools.MethodDelegate<Func<Map, MapComponent>>(AccessTools.Method(AccessTools.TypeByName("Rimefeller.DubUtils"),
            "Rimefeller"));
        rimefellerMapComp = AccessTools.TypeByName("Rimefeller.MapComponent_Rimefeller");
        getDeepOilGrid = AccessTools.FieldRefAccess<object>(rimefellerMapComp, "DeepOilGrid");
        getOilGrid = AccessTools.FieldRefAccess<object>(rimefellerMapComp, "OilGrid");
        towerDraw = AccessTools.FieldRefAccess<bool>(rimefellerMapComp, "MarkTowersForDraw");
        drawOilGrid = MethodInvoker.GetHandler(AccessTools.Method(AccessTools.TypeByName("Rimefeller.OilGrid"), "MarkFieldsForDraw"));
    }

    protected void DrawOilGrid()
    {
        drawOilGrid(getOilGrid(Rimefeller));
    }

    protected void DrawDeepOilGrid()
    {
        drawOilGrid(getDeepOilGrid(Rimefeller));
    }
}