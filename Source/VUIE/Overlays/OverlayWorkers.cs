using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class OverlayWorker : IExposable
    {
        public OverlayDef def;
        private bool enabled = true;
        public virtual bool Visible { get; set; }
        public virtual bool DrawToggle => enabled;
        public virtual Texture2D Icon => def.Icon;
        public virtual string Label => def.LabelCap;
        public virtual string Description => def.description == "." ? "" : def.description;
        public virtual float SettingsHeight => 66f;
        public virtual bool EnabledByDefault => true;

        public virtual void ExposeData()
        {
            var visible = Visible;
            Scribe_Values.Look(ref visible, "visible");
            Visible = visible;
            Scribe_Values.Look(ref enabled, "enabled", true);
        }

        public virtual void DoSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled("VUIE.Enabled".Translate(), ref enabled);
        }

        public virtual void DoInterface(WidgetRow row)
        {
            var visible = Visible;
            row.ToggleableIcon(ref visible, Icon, $"{Label}{(Description.NullOrEmpty() ? "" : $"\n\n{Description}")}", SoundDefOf.Mouseover_ButtonToggle);
            if (visible != Visible) Visible = visible;
        }

        public virtual OverlayWorker Init(OverlayDef def)
        {
            this.def = def;
            enabled = EnabledByDefault;
            return this;
        }

        public virtual void OverlayOnGUI()
        {
        }

        public virtual void OverlayUpdate()
        {
        }
    }

    public class OverlayWorker_Power : OverlayWorker_Mod
    {
        protected override string ModName => "TD_EnhancementPack-1.3_Fork";

        public override OverlayWorker Init(OverlayDef def)
        {
            base.Init(def);
            Active = !Active;
            return this;
        }

        public override void OverlayOnGUI()
        {
            base.OverlayOnGUI();
            OverlayDrawHandler.DrawPowerGridOverlayThisFrame();
        }
    }

    public class OverlayWorker_Roof : OverlayWorker
    {
        public override bool Visible
        {
            get => Current.Game.playSettings.showRoofOverlay;
            set => Current.Game.playSettings.showRoofOverlay = value;
        }

        public override string Label => "ShowRoofOverlayToggleButton".Translate();

        public override void ExposeData()
        {
            if (Current.Game != null)
                base.ExposeData();
        }
    }

    public class OverlayWorker_Fertility : OverlayWorker_Auto
    {
        public override bool Visible
        {
            get => Current.Game.playSettings.showFertilityOverlay;
            set => Current.Game.playSettings.showFertilityOverlay = value;
        }

        public override bool CanShowNumbers => false;
        public override string Label => "ShowFertilityOverlayToggleButton".Translate();

        public override void ExposeData()
        {
            if (Current.Game != null)
                base.ExposeData();
        }
    }

    public class OverlayWorker_Terrain : OverlayWorker
    {
        public override bool Visible
        {
            get => Current.Game.playSettings.showTerrainAffordanceOverlay;
            set => Current.Game.playSettings.showTerrainAffordanceOverlay = value;
        }

        public override string Label => "ShowTerrainAffordanceOverlayToggleButton".Translate();

        public override void ExposeData()
        {
            if (Current.Game != null)
                base.ExposeData();
        }
    }

    public abstract class OverlayWorker_Mod : OverlayWorker
    {
        protected bool Active;
        protected abstract string ModName { get; }
        public override bool DrawToggle => Active && base.DrawToggle;
        public override Texture2D Icon => Active ? base.Icon : TexButton.Add;

        public override OverlayWorker Init(OverlayDef def)
        {
            if (ModLister.HasActiveModWithName(ModName))
            {
                Active = true;
                ModInit(def);
            }

            return base.Init(def);
        }

        public override void DoSettings(Listing_Standard listing)
        {
            if (Active) base.DoSettings(listing);
            else listing.Label("ModDependsOn".Translate(ModName));
        }

        protected virtual void ModInit(OverlayDef def)
        {
        }
    }

    public class OverlayWorker_Gas : OverlayWorker_Mod
    {
        private Action showGasGrid;

        protected override string ModName => "Vanilla Furniture Expanded - Power";

        protected override void ModInit(OverlayDef def)
        {
            base.ModInit(def);
            showGasGrid = AccessTools.MethodDelegate<Action>(
                AccessTools.Method(AccessTools.TypeByName("GasNetwork.Overlay.SectionLayer_GasNetwork"), "DrawGasGridOverlayThisFrame"));
        }

        public override void OverlayOnGUI()
        {
            base.OverlayOnGUI();
            showGasGrid();
        }
    }

    public class OverlayWorker_Numbers : OverlayWorker
    {
        private readonly List<IntVec3> cachedRelevantCells = new();
        private IntVec3 cachedCell;
        private Section cachedSection;
        private SectionLayer_Overlay sectionLayer;
        public virtual bool CanShowNumbers => true;

        public virtual bool ShowSectionLayer => !ShowNumberDisplay;
        public virtual float NumberRange => 8.9f;
        private int NumCells => GenRadial.NumCellsInRadius(NumberRange);
        public virtual bool ShowNumberDisplay { get; set; }

        public override float SettingsHeight => base.SettingsHeight + (CanShowNumbers ? 33f : 0f);

        public override void ExposeData()
        {
            base.ExposeData();
            if (!CanShowNumbers) return;
            var showNumbers = ShowNumberDisplay;
            Scribe_Values.Look(ref showNumbers, "showNumbers");
            ShowNumberDisplay = showNumbers;
        }

        public override void DoSettings(Listing_Standard listing)
        {
            base.DoSettings(listing);
            if (!CanShowNumbers) return;
            var showNumbers = ShowNumberDisplay;
            listing.CheckboxLabeled("VUIE.Overlays.ShowNumbers".Translate(), ref showNumbers, "VUIE.Overlays.ShowNumbers.Desc".Translate());
            if (ShowNumberDisplay != showNumbers) ShowNumberDisplay = showNumbers;
        }

        public override void OverlayOnGUI()
        {
            base.OverlayOnGUI();
            if (!ShowNumberDisplay || !CanShowNumbers) return;
            var c = UI.MouseCell();
            var map = Find.CurrentMap;
            if (!c.InBounds(map) || c.Fogged(map)) return;
            if (cachedSection is null || map.mapDrawer.SectionAt(c) != cachedSection)
            {
                cachedSection = map.mapDrawer.SectionAt(c);
                sectionLayer = cachedSection.layers.OfType<SectionLayer_Overlay>().First(layer => layer.OverlayDef == def);
            }

            if (c != cachedCell)
            {
                cachedCell = c;
                CacheRelevantCells(map);
            }

            foreach (var cell in cachedRelevantCells)
                GenMapUI.DrawThingLabel(GenMapUI.LabelDrawPosFor(cell), ValueToString(sectionLayer.ValueForCell(cell)), sectionLayer.ColorForCell(cell));
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
    }

    public class OverlayWorker_Beauty : OverlayWorker_Numbers
    {
        private State state = State.VanillaHidden;

        public override bool ShowNumberDisplay
        {
            get => state is State.Vanilla or State.VanillaHidden;
            set
            {
                if (value) state = Visible ? State.Vanilla : State.VanillaHidden;
                else state = Visible ? State.Overlay : State.OverlayHidden;
                Current.Game.playSettings.showBeauty = state == State.Vanilla;
            }
        }

        public override bool Visible
        {
            get => state is State.Vanilla or State.Overlay;
            set
            {
                if (value) state = ShowNumberDisplay ? State.Vanilla : State.Overlay;
                else state = ShowNumberDisplay ? State.VanillaHidden : State.OverlayHidden;
                Current.Game.playSettings.showBeauty = state == State.Vanilla;
            }
        }

        public override bool ShowSectionLayer => state == State.Overlay;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref state, "state");
        }

        public override void OverlayOnGUI()
        {
        }

        private enum State
        {
            VanillaHidden,
            OverlayHidden,
            Vanilla,
            Overlay
        }
    }

    public class OverlayWorker_Auto : OverlayWorker_Numbers
    {
        private bool autoShowing;
        private bool showing;
        public HashSet<Type> showOnDesTypes;
        public HashSet<BuildableDef> showWhenBuilding;
        public virtual bool AutoShowEnabled { get; set; }

        public override bool Visible
        {
            get => showing || autoShowing;
            set => showing = value;
        }

        public override float SettingsHeight => base.SettingsHeight + 33f;

        public virtual bool ShouldAutoShow()
        {
            if (!AutoShowEnabled) return false;
            if (showOnDesTypes is not null && Find.DesignatorManager.SelectedDesignator is not null &&
                showOnDesTypes.Contains(Find.DesignatorManager.SelectedDesignator.GetType())) return true;
            if (showWhenBuilding is not null &&
                Find.DesignatorManager.SelectedDesignator is Designator_Place place &&
                showWhenBuilding.Contains(place.PlacingDef)) return true;
            if (showWhenBuilding is not null &&
                Find.Selector.SelectedObjects.OfType<Thing>().Any(t => showWhenBuilding.Contains(GenConstruct.BuiltDefOf(t.GetInnerIfMinified().def)))) return true;
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            var ase = AutoShowEnabled;
            Scribe_Values.Look(ref ase, "autoShowEnabled", true);
            AutoShowEnabled = ase;
        }

        public override void DoSettings(Listing_Standard listing)
        {
            base.DoSettings(listing);
            var ase = AutoShowEnabled;
            listing.CheckboxLabeled("VUIE.Overlays.Autoshow".Translate(), ref ase);
            AutoShowEnabled = ase;
        }

        public override void OverlayUpdate()
        {
            base.OverlayUpdate();
            if (!AutoShowEnabled) return;
            if (autoShowing && !ShouldAutoShow()) autoShowing = false;
            if (!autoShowing && ShouldAutoShow()) autoShowing = true;
        }

        public override OverlayWorker Init(OverlayDef def)
        {
            if (def.autoshowOn is not null)
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

            return base.Init(def);
        }
    }

    public class OverlayWorker_Lighting : OverlayWorker_Auto
    {
        public bool UseGlowColor;
        public override float SettingsHeight => base.SettingsHeight + 33f;
        public override bool ShouldAutoShow() => Find.DesignatorManager.SelectedDesignator is Designator_Place {PlacingDef: ThingDef td} && td.HasComp(typeof(CompGlower));

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
    }

    public class OverlayWorker_TableSearch : OverlayWorker_Auto
    {
        public override bool CanShowNumbers => false;

        public override bool ShouldAutoShow()
        {
            return Find.Selector.SelectedObjects.OfType<Thing>().Any(t => ThingRequestGroup.FoodSource.Includes(t.def));
        }

        public override void OverlayUpdate()
        {
            base.OverlayUpdate();
            if (Visible)
                foreach (var source in Find.Selector.SelectedObjects.OfType<Thing>().Where(t => ThingRequestGroup.FoodSource.Includes(t.def)))
                    GenDraw.DrawFieldEdges(GenRadial.RadialCellsAround(source.Position, source.def.ingestible?.chairSearchRadius ?? 32f, true).ToList(), Color.blue);
        }
    }

    public class OverlayWorker_WindBlocker : OverlayWorker_Auto
    {
        public override bool CanShowNumbers => false;
        public override bool ShouldAutoShow() => Find.DesignatorManager.SelectedDesignator is Designator_Place {PlacingDef: ThingDef td} && td.HasComp(typeof(CompPowerPlantWind));
    }

    public class OverlayWorker_Usage : OverlayWorker_Numbers
    {
        private MapMeshFlagExtDirtier dirtier;
        public override bool EnabledByDefault => false;

        public override OverlayWorker Init(OverlayDef def)
        {
            base.Init(def);
            dirtier = new MapMeshFlagExtDirtier();
            return this;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (DrawToggle) dirtier.DoPatches(UIMod.Harm);
            else dirtier.UndoPatches(UIMod.Harm);
        }
    }
}