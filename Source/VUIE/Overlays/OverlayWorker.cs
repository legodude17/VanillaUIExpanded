using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE;

public class OverlayWorker : IExposable
{
    public const int CELLS_PER_UPDATE = 32;
    private readonly List<IntVec3> cachedRelevantCells = new();
    private bool autoShow;
    private bool autoShowing;
    private IntVec3 cachedCell;
    protected int curIdx;

    // ReSharper disable once InconsistentNaming
    public OverlayDef def;
    protected OverlayDrawer drawer;
    private bool enabled = true;
    private string failedMod;
    public Color?[] Grid;
    protected bool initing;
    public Map Map;
    public HashSet<Type> showOnDesTypes;
    public HashSet<BuildableDef> showWhenBuilding;
    private bool visibleInt;

    public virtual bool AutoShowEnabled
    {
        get => autoShow;
        set
        {
            if (autoShow && !value) OverlayController.RemoveUpdate(this);
            if (!autoShow && value) OverlayController.RegisterUpdateInvisible(this);
            autoShow = value;
        }
    }

    public virtual bool Visible
    {
        get => visibleInt || autoShowing;
        set
        {
            var old = visibleInt;
            if (value && !old) Notify_Map(Find.CurrentMap);
            if (!value && old) Notify_Disabled();
            visibleInt = value;
        }
    }

    public bool RequireModsLoaded => failedMod == null;
    public virtual bool Enabled => failedMod == null && enabled;
    public virtual Texture2D Icon => failedMod == null ? def.Icon : TexButton.Add;
    public virtual string Label => def.LabelCap;
    public virtual string Description => def.description == "." ? "" : def.description;

    public virtual float SettingsHeight
    {
        get
        {
            var lineHeight = Text.LineHeight + 2f;
            var height = lineHeight;
            if (failedMod != null) return height + lineHeight;
            if (CanDisabled) height += lineHeight;
            if (def.canShowNumbers) height += lineHeight;
            if (def.canShowGrid) height += lineHeight;
            if (def.canAutoShow) height += lineHeight;
            return height;
        }
    }

    public virtual bool CanDisabled => true;
    public virtual float NumberRange => 8.9f;
    private int NumCells => GenRadial.NumCellsInRadius(NumberRange);
    public virtual bool ShowNumbers { get; set; }
    public virtual bool ShowGrid { get; set; }

    public virtual void ExposeData()
    {
        Scribe_Values.Look(ref enabled, "enabled", def.enableByDefault);
        if (def.canShowNumbers)
        {
            var showNumbers = ShowNumbers;
            Scribe_Values.Look(ref showNumbers, "showNumbers", true);
            ShowNumbers = showNumbers;
        }

        if (def.canShowGrid)
        {
            var showGrid = ShowGrid;
            Scribe_Values.Look(ref showGrid, "showGrid", true);
            ShowGrid = showGrid;
        }

        if (def.canAutoShow)
        {
            var ase = AutoShowEnabled;
            Scribe_Values.Look(ref ase, "autoShowEnabled", true);
            AutoShowEnabled = ase;
        }
    }

    public virtual void Notify_Map(Map map)
    {
        Map = map;
        Log.Message("Initializing...");
        if (def.canShowGrid)
        {
            initing = true;
            Grid = new Color?[map.cellIndices.NumGridCells];
            curIdx = -1;
            drawer.Init(map);
        }

        OverlayController.Notify_Visible(this);
    }

    public virtual void OverlayUpdate()
    {
        if (initing)
            for (var i = 0; i < UIMod.GetModule<OverlayModule>().ConstructRate; i++)
            {
                var cell = Map.cellIndices.IndexToCell(++curIdx);
                Log.Message($"Recalculating: {cell}");
                RecacheCell(cell);
                if (curIdx >= Map.cellIndices.NumGridCells - 1)
                {
                    initing = false;
                    break;
                }
            }

        if (AutoShowEnabled)
        {
            var shouldAutoShow = ShouldAutoShow();
            if (autoShowing && !shouldAutoShow) autoShowing = false;
            if (!autoShowing && shouldAutoShow) autoShowing = true;
        }
    }

    public virtual void OverlayDraw()
    {
        if (ShowGrid) drawer.Draw();
    }

    public virtual bool ShouldAutoShow()
    {
        if (!AutoShowEnabled) return false;
        if (showOnDesTypes is not null && Find.DesignatorManager.SelectedDesignator is not null &&
            showOnDesTypes.Contains(Find.DesignatorManager.SelectedDesignator.GetType())) return true;
        if (showWhenBuilding is not null &&
            Find.DesignatorManager.SelectedDesignator is Designator_Place place &&
            showWhenBuilding.Contains(place.PlacingDef)) return true;
        if (showWhenBuilding is not null &&
            Find.Selector.SelectedObjects.OfType<Thing>()
                .Any(t => showWhenBuilding.Contains(GenConstruct.BuiltDefOf(t.GetInnerIfMinified().def)))) return true;
        return false;
    }

    public void Notify_MapMeshChanged(IntVec3 loc, MapMeshFlag dirtyFlags, bool regenAdjacentCells, bool regenAdjacentSections)
    {
        if ((def.relevantChangeTypes & dirtyFlags) == MapMeshFlag.None) return;
        var sections = new HashSet<Section> { Map.mapDrawer.SectionAt(loc) };
        if (regenAdjacentCells)
            for (var i = 0; i < 8; i++)
            {
                var intVec = loc + GenAdj.AdjacentCells[i];
                if (intVec.InBounds(Map)) sections.Add(Map.mapDrawer.SectionAt(intVec));
            }

        if (regenAdjacentSections)
        {
            var a = Map.mapDrawer.SectionCoordsAt(loc);
            for (var j = 0; j < 8; j++)
            {
                var coords = a + GenAdj.AdjacentCells[j].ToIntVec2;
                var sectionCount = Map.mapDrawer.SectionCount;
                if (coords.x >= 0 && coords.z >= 0 && coords.x <= sectionCount.x - 1 && coords.z <= sectionCount.z - 1)
                    sections.Add(Map.mapDrawer.sections[coords.x, coords.z]);
            }
        }

        foreach (var section in sections)
        foreach (var c in section.CellRect)
            RecacheCell(c);
    }

    public virtual void Notify_BuildingChanged(Thing t)
    {
        foreach (var c in t.OccupiedRect()) RecacheCell(c);
    }

    public virtual void OverlayOnGUI()
    {
        if (ShowNumbers)
        {
            var c = UI.MouseCell();
            var map = Find.CurrentMap;
            if (!c.InBounds(map) || c.Fogged(map)) return;
            if (c != cachedCell)
            {
                cachedCell = c;
                CacheRelevantCells(map);
            }

            foreach (var cell in cachedRelevantCells)
                if (ValueForCell(cell) is float val && ColorForCell(cell) is Color color)
                    GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(cell), ValueToString(val), color);
        }
    }

    public virtual string ValueToString(float value) => Mathf.RoundToInt(value).ToStringCached();

    public void CacheRelevantCells(Map map)
    {
        cachedRelevantCells.Clear();
        for (var i = 0; i < NumCells; i++)
        {
            var intVec = cachedCell + GenRadial.RadialPattern[i];
            if (!intVec.InBounds(map) || intVec.Fogged(map)) continue;
            cachedRelevantCells.Add(intVec);
        }
    }

    public virtual float? ValueForCell(IntVec3 c) => def.valueGetter?.Call(c, Map);
    public virtual void Enable() => enabled = true;
    public virtual void Disable() => enabled = false;

    public virtual void DoSettings(Listing_Standard listing)
    {
        if (failedMod != null)
        {
            listing.Label("ModDependsOn".Translate(failedMod));
            return;
        }

        if (CanDisabled) listing.CheckboxLabeled("VUIE.Enabled".Translate(), ref enabled, "VUIE.Enabled.Desc".Translate());
        if (def.canShowNumbers)
        {
            var showNumbers = ShowNumbers;
            listing.CheckboxLabeled("VUIE.Overlays.ShowNumbers".Translate(), ref showNumbers, "VUIE.Overlays.ShowNumbers.Desc".Translate());
            if (ShowNumbers != showNumbers) ShowNumbers = showNumbers;
        }

        if (def.canShowGrid)
        {
            var showGrid = ShowGrid;
            listing.CheckboxLabeled("VUIE.Overlays.ShowGrid".Translate(), ref showGrid, "VUIE.Overlays.ShowGrid.Desc".Translate());
            if (ShowGrid != showGrid) ShowGrid = showGrid;
        }

        if (def.canAutoShow)
        {
            var ase = AutoShowEnabled;
            listing.CheckboxLabeled("VUIE.Overlays.Autoshow".Translate(), ref ase, "VUIE.Overlays.Autoshow.Desc".Translate());
            AutoShowEnabled = ase;
        }
    }

    public virtual void DoInterface(WidgetRow row)
    {
        var visible = Visible;
        row.ToggleableIcon(ref visible, Icon, $"{Label}{(Description.NullOrEmpty() ? "" : $"\n\n{Description}")}", SoundDefOf.Mouseover_ButtonToggle);
        if (visible != Visible) Visible = visible;
    }

    public OverlayWorker Init(OverlayDef def)
    {
        this.def = def;
        enabled = def.enableByDefault;

        if (def.requiredMods is not null)
        {
            foreach (var mod in def.requiredMods)
                if (!ModLister.HasActiveModWithName(mod))
                {
                    failedMod = mod;
                    return this;
                }

            failedMod = null;
        }

        if (def.canShowGrid)
        {
            drawer = UIMod.GetModule<OverlayModule>().Instancing ? new OverlayDrawer_Instanced() : new OverlayDrawer_NotInstanced();
            drawer.Init(this);
        }

        if (def.canAutoShow && def.autoshowOn is not null)
            foreach (var showOnDes in def.autoshowOn)
            {
                var ent = DefDatabase<BuildableDef>.GetNamedSilentFail(showOnDes);
                if (ent is not null)
                {
                    showWhenBuilding ??= new HashSet<BuildableDef>();
                    showWhenBuilding.Add(ent);
                }

                var type = GenTypes.GetTypeInAnyAssembly(showOnDes);
                if (type is not null)
                {
                    showOnDesTypes ??= new HashSet<Type>();
                    showOnDesTypes.Add(type);
                }
            }

        if (RequireModsLoaded) InitInner();

        return this;
    }

    public virtual void InitInner()
    {
    }

    public virtual void Notify_Disabled()
    {
        Grid = null;
        Map = null;
        initing = false;
        drawer?.Deinit();
        OverlayController.Notify_Invisible(this);
    }

    public virtual Color? ColorForCell(IntVec3 c) => def.colorGetter?.Call(c, Map);

    protected virtual void RecacheCell(IntVec3 loc)
    {
        Grid[Map.cellIndices.CellToIndex(loc)] = ColorForCell(loc);
        drawer.OnCellChange(loc);
    }
}