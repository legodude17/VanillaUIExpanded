using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE;

public class OverlayWorker_Gas : OverlayWorker
{
    private Action showGasGrid;

    public override void OverlayDraw()
    {
        base.OverlayDraw();
        showGasGrid();
    }

    public override void InitInner()
    {
        base.InitInner();
        showGasGrid = AccessTools.MethodDelegate<Action>(
            AccessTools.Method(AccessTools.TypeByName("GasNetwork.Overlay.SectionLayer_GasNetwork"), "DrawGasGridOverlayThisFrame"));
    }
}

public class OverlayWorker_Beauty : OverlayWorker
{
    public override void OverlayUpdate()
    {
        base.OverlayUpdate();
        if (ShowNumbers) Current.Game.playSettings.showBeauty = true;
    }

    public override float? ValueForCell(IntVec3 c) => BeautyUtility.CellBeauty(c, Map);

    public override Color? ColorForCell(IntVec3 c)
    {
        // Credit to Uuugggg's TD Enhancement Pack for this algorithm
        var amount = BeautyUtility.CellBeauty(c, Map);

        var good = amount > 0;
        amount = amount > 0 ? amount / 50 : -amount / 10;

        var baseColor = good ? Color.green : Color.red;
        baseColor.a = 0;

        return good && amount > 1
            ? Color.Lerp(Color.green, Color.white, amount - 1)
            : Color.Lerp(baseColor, good ? Color.green : Color.red, amount);
    }
}

public class OverlayWorker_Lighting : OverlayWorker
{
    public bool UseGlowColor;
    public override float SettingsHeight => base.SettingsHeight + Text.LineHeight + 2f;

    public override bool ShouldAutoShow() =>
        Find.DesignatorManager.SelectedDesignator is Designator_Place { PlacingDef: ThingDef td } && td.HasComp(typeof(CompGlower));

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref UseGlowColor, "useGlowColor");
    }

    public override void DoSettings(Listing_Standard listing)
    {
        base.DoSettings(listing);
        listing.CheckboxLabeled("VUIE.Overlays.UseColor".Translate(), ref UseGlowColor, "VUIE.Overlays.UseColor.Desc".Translate());
    }

    public override string ValueToString(float value) => value.ToStringPercent();
    public override float? ValueForCell(IntVec3 c) => Map.glowGrid.GameGlowAt(c);

    public override Color? ColorForCell(IntVec3 c) => UseGlowColor
        ? Map.glowGrid.VisualGlowAt(c)
        : Color.Lerp(Color.red, Color.green, Map.glowGrid.GameGlowAt(c));
}

public class OverlayWorker_TableSearch : OverlayWorker
{
    public override bool ShouldAutoShow()
    {
        return Find.Selector.SelectedObjects.OfType<Thing>().Any(t => ThingRequestGroup.FoodSource.Includes(t.def));
    }

    public override void OverlayUpdate()
    {
        base.OverlayUpdate();
        if (Visible)
            foreach (var source in Find.Selector.SelectedObjects.OfType<Thing>().Where(t => ThingRequestGroup.FoodSource.Includes(t.def)))
                GenDraw.DrawFieldEdges(GenRadial.RadialCellsAround(source.Position, source.def.ingestible?.chairSearchRadius ?? 32f, true).ToList(),
                    Color.blue);
    }
}

public class OverlayWorker_WindBlocker : OverlayWorker
{
    public override bool ShouldAutoShow() => Find.DesignatorManager.SelectedDesignator is Designator_Place { PlacingDef: ThingDef td } &&
                                             td.HasComp(typeof(CompPowerPlantWind));

    public override Color? ColorForCell(IntVec3 c) => Map.thingGrid.ThingsListAtFast(c).Any(t => t.def.blockWind) ? Color.blue : null;
}

public class OverlayWorker_Usage : OverlayWorker
{
    public static ByteGrid Grid = new();
    private MapMeshFlagExtDirtier dirtier;
    private byte max;

    private byte min;
    public static MapMeshFlag UsageFlag => (MapMeshFlag)MapMeshFlagExt.Usage;

    public override void InitInner()
    {
        base.InitInner();
        def.relevantChangeTypes = UsageFlag;
        dirtier = new MapMeshFlagExtDirtier();
    }

    public override void Notify_Map(Map map)
    {
        base.Notify_Map(map);
        dirtier.DoPatches(UIMod.Harm);
        Grid = new ByteGrid(map);
        min = Grid.grid.Min();
        max = Grid.grid.Max();
    }

    public override void Notify_Disabled()
    {
        base.Notify_Disabled();
        dirtier.UndoPatches(UIMod.Harm);
    }

    public override float? ValueForCell(IntVec3 c) => Grid[c];

    public override Color? ColorForCell(IntVec3 c) => Color.Lerp(Color.red, Color.green, Mathf.InverseLerp(min, max, Grid[c]));
}

public class OverlayWorker_Wealth : OverlayWorker
{
    private float max;
    private float[] wealthGrid;

    private static float ThingWealth(Thing t)
    {
        var num = 0f;
        if (ThingRequestGroup.HaulableEver.Includes(t.def)) num += t.MarketValue * t.stackCount;
        if (ThingRequestGroup.BuildingArtificial.Includes(t.def)) num += t.GetStatValue(StatDefOf.MarketValueIgnoreHp);
        if (t is IThingHolder x and not (PassingShip or MapComponent or Pawn)) num += x.GetDirectlyHeldThings()?.Sum(ThingWealth) ?? 0;
        return num;
    }

    protected override void RecacheCell(IntVec3 loc)
    {
        if (max == 0f)
            wealthGrid[Map.cellIndices.CellToIndex(loc)] = ValueForCellInt(loc);
        else
            base.RecacheCell(loc);
    }

    public override void OverlayUpdate()
    {
        base.OverlayUpdate();
        if (!initing && max == 0f)
        {
            max = wealthGrid.Max();
            initing = true;
            curIdx = -1;
        }
    }

    public override void Notify_Disabled()
    {
        base.Notify_Disabled();
        wealthGrid = null;
        max = 0f;
    }

    public override void Notify_Map(Map map)
    {
        base.Notify_Map(map);
        wealthGrid = new float[map.cellIndices.NumGridCells];
        max = 0f;
    }

    private float ValueForCellInt(IntVec3 c) =>
        WealthWatcher.cachedTerrainMarketValue[Map.terrainGrid.TerrainAt(c).index] + Map.thingGrid.ThingsListAtFast(c).Sum(ThingWealth);

    public override float? ValueForCell(IntVec3 c)
    {
        var val = wealthGrid[Map.cellIndices.CellToIndex(c)];
        if (val == 0) return null;
        return val;
    }

    public override Color? ColorForCell(IntVec3 c) =>
        ValueForCell(c) is float val ? Color.Lerp(Color.white, Color.green, Mathf.InverseLerp(0f, max, val)) : null;
}