using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    [StaticConstructorOnStartup]
    public static class CoverageOverlays
    {
        public static Dictionary<ThingDef, (float, Color)> Coverers = new();
        public static Dictionary<ThingDef, OverlayDef> CoverageOverlayDefs = new();
        public static Dictionary<Type, List<OverlayDef>> SpecialCoverageOverlays = new();

        static CoverageOverlays()
        {
            Log.Message("[VUIE] Creating implied overlays from coverage...");
            AddCoverer(ThingDefOf.OrbitalTradeBeacon, Building_OrbitalTradeBeacon.TradeRadius);
            AddCoverer(ThingDef.Named("SunLamp"), color: Color.green);
            AddCoverer(ThingDefOf.FirefoamPopper);
            AddCoverer(ThingDefOf.PsychicEmanator);
            AddCoverer(ThingDef.Named("TrapIED_HighExplosive"), color: Color.red);
            AddCoverer(ThingDef.Named("TrapIED_Incendiary"), color: Color.red);
            AddCoverer(ThingDef.Named("TrapIED_EMP"), color: Color.red);
            AddCoverer(ThingDef.Named("TrapIED_Firefoam"), color: Color.red);
            AddCoverer(ThingDef.Named("TrapIED_AntigrainWarhead"), color: Color.red);

            foreach (var coverer in Coverers)
            {
                var def = new OverlayDef
                {
                    defName = "Coverage_" + coverer.Key.defName,
                    label = "Toggle " + coverer.Key.label + " coverage",
                    description = ".",
                    autoshowOn = new List<string> {coverer.Key.defName},
                    workerClass = typeof(OverlayWorker_Coverage),
                    Worker = new OverlayWorker_Coverage
                    {
                        CoverageDef = coverer.Key,
                        CoverageRange = coverer.Value.Item1,
                        CoverageColor = coverer.Value.Item2,
                        AutoShowEnabled = true
                    },
                    Icon = coverer.Key.uiIcon
                };
                def.Worker.Init(def);
                CoverageOverlayDefs.Add(coverer.Key, def);
                DefGenerator.AddImpliedDef(def);
            }

            foreach (var type in typeof(OverlayWorker_Coverage).AllSubclassesNonAbstract())
                SpecialCoverageOverlays.Add(type, DefDatabase<OverlayDef>.AllDefs.Where(def => type.IsInstanceOfType(def.Worker)).ToList());

            UIMod.Harm.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.SpawnSetup)), postfix: new HarmonyMethod(typeof(CoverageOverlays), nameof(BuildingCheck)));
            UIMod.Harm.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.DeSpawn)), postfix: new HarmonyMethod(typeof(CoverageOverlays), nameof(BuildingCheck)));
            UIMod.Harm.Patch(AccessTools.PropertySetter(typeof(Game), nameof(Game.CurrentMap)), postfix: new HarmonyMethod(typeof(CoverageOverlays), nameof(PostMapChange)));

            Log.Message("[VUIE] Reloading settings...");
            UIMod.Settings.Read();
        }

        public static void BuildingCheck(Thing __instance)
        {
            if (Find.CurrentMap == __instance.Map && CoverageOverlayDefs.TryGetValue(__instance.def, out var overlay) && overlay.Worker.DrawToggle)
                (overlay.Worker as OverlayWorker_Coverage)?.Notify_BuildingChanged(__instance);
            foreach (var def in SpecialCoverageOverlays.Values.SelectMany(a => a).Where(def => def.Worker.DrawToggle))
                (def.Worker as OverlayWorker_Coverage)?.Notify_BuildingChanged(__instance);
        }

        public static void PostMapChange()
        {
            foreach (var def in CoverageOverlayDefs.Values.Where(def => def.Worker.DrawToggle)) (def.Worker as OverlayWorker_Coverage)?.Notify_ChangedMap();
            foreach (var def in SpecialCoverageOverlays.Values.SelectMany(a => a).Where(def => def.Worker.DrawToggle)) (def.Worker as OverlayWorker_Coverage)?.Notify_ChangedMap();
        }

        public static void AddCoverer(ThingDef def, float? radius = null, Color? color = null)
        {
            var r = radius ?? def.specialDisplayRadius;
            var c = color ?? Color.blue;
            Coverers.Add(def, (r, c));
        }
    }

    public class OverlayWorker_Coverage : OverlayWorker_Auto, ICellBoolGiver
    {
        protected readonly List<IntVec3> Centers = new();
        protected readonly List<IntVec3> Covered = new();
        protected readonly HashSet<int> CoveredIndices = new();
        public Color CoverageColor;
        public ThingDef CoverageDef;
        public float CoverageRange;
        protected CellBoolDrawer Drawer;

        public override bool CanShowNumbers => false;

        public bool GetCellBool(int index) => CoveredIndices.Contains(index);

        public Color GetCellExtraColor(int index) => Color.white;

        public Color Color => CoverageColor;

        public override OverlayWorker Init(OverlayDef def)
        {
            Notify_ChangedMap();
            return base.Init(def);
        }

        public virtual void Notify_ChangedMap()
        {
            if (Find.CurrentMap is null)
            {
                Drawer = null;
                Covered.Clear();
                return;
            }

            Drawer = new CellBoolDrawer(this, Find.CurrentMap.Size.x, Find.CurrentMap.Size.z);
            CacheCovered();
        }

        public virtual void Notify_BuildingChanged(Thing t)
        {
            CacheCovered();
        }

        public override void OverlayUpdate()
        {
            base.OverlayUpdate();
            if (Visible && Drawer is not null)
            {
                Drawer.MarkForDraw();
                if (Covered.Count > 0) GenDraw.DrawFieldEdges(Covered.ToList(), CoverageColor);
            }

            Drawer?.CellBoolDrawerUpdate();
        }

        public virtual void CacheCovered()
        {
            Centers.Clear();

            Centers.AddRange(Find.CurrentMap.listerThings.ThingsOfDef(CoverageDef).Select(t => t.Position));

            Centers.AddRange(Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint)
                .Where(bp => GenConstruct.BuiltDefOf(bp.def) == CoverageDef).Select(t => t.Position));

            Centers.AddRange(Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame)
                .Where(frame => GenConstruct.BuiltDefOf(frame.def) == CoverageDef).Select(t => t.Position));

            FromCenters();
        }

        protected void FromCenters(float? r = null)
        {
            var range = r ?? CoverageRange;

            Covered.Clear();

            Covered.AddRange(Centers.SelectMany(center => GenRadial.RadialCellsAround(center, range, true)).Where(c => c.InBounds(Find.CurrentMap) && !c.Fogged(Find.CurrentMap)));

            CoveredIndices.Clear();

            CoveredIndices.AddRange(Covered.Select(Find.CurrentMap.cellIndices.CellToIndex));

            Drawer.SetDirty();
        }
    }

    public class OverlayWorker_Chairs : OverlayWorker_Coverage
    {
        private readonly Func<ThingDef, bool> validator = td => td is {surfaceType: SurfaceType.Eat} or {building: {isSittable: true}};

        private readonly Func<Thing, bool> validator2 = t =>
            t.def.surfaceType == SurfaceType.Eat
                ? GenAdj.CellsAdjacentCardinal(t).Any(c => Find.CurrentMap.thingGrid.ThingsListAt(c).Any(t => t is {def: {building: {isSittable: true}}}))
                : GenAdj.CellsAdjacentCardinal(t).Any(c => c.HasEatSurface(Find.CurrentMap));

        public OverlayWorker_Chairs()
        {
            CoverageColor = Color.blue;
            CoverageRange = 32f;
            CoverageDef = ThingDefOf.DiningChair;
        }

        public override bool ShouldAutoShow()
        {
            if (base.ShouldAutoShow()) return true;
            if (Find.DesignatorManager.SelectedDesignator is Designator_Place {PlacingDef: ThingDef td} && validator(td)) return true;
            return Find.Selector.SelectedObjects.OfType<Thing>().Any(t => validator(GenConstruct.BuiltDefOf(t.def) as ThingDef) && validator2(t));
        }

        public override void Notify_BuildingChanged(Thing t)
        {
            if (Find.CurrentMap is null) return;
            if (validator(t.def) && (!t.Spawned || validator2(t)))
                base.Notify_BuildingChanged(t);
        }

        public override void CacheCovered()
        {
            Centers.Clear();

            Centers.AddRange(Find.CurrentMap.listerBuildings.allBuildingsColonist.Where(t => validator(t.def) && validator2(t)).Select(t => t.Position));

            Centers.AddRange(Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint)
                .Where(bp => GenConstruct.BuiltDefOf(bp.def) is ThingDef td && validator(td) && validator2(bp)).Select(t => t.Position));

            Centers.AddRange(Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame)
                .Where(frame => GenConstruct.BuiltDefOf(frame.def) is ThingDef td && validator(td) && validator2(frame)).Select(t => t.Position));

            FromCenters();
        }
    }

    public class OverlayWorker_Terror : OverlayWorker_Coverage
    {
        public OverlayWorker_Terror()
        {
            CoverageColor = Color.red;
            CoverageRange = 5f;
            CoverageDef = ThingDefOf.Skullspike;
        }

        public override bool DrawToggle => ModsConfig.IdeologyActive && base.DrawToggle;

        public override void DoSettings(Listing_Standard listing)
        {
            if (ModsConfig.IdeologyActive) base.DoSettings(listing);
            else listing.Label("ModDependsOn".Translate("Ideology"));
        }

        public override void Notify_BuildingChanged(Thing t)
        {
            if (ThingRelevant(t)) base.Notify_BuildingChanged(t);
        }

        public override void Enable()
        {
            if (ModsConfig.IdeologyActive) base.Enable();
        }

        private static bool ThingRelevant(Thing t) => (ThingRequestGroup.Corpse.Includes(t.def) || ThingRequestGroup.BuildingArtificial.Includes(t.def)) &&
                                                      t.GetStatValue(StatDefOf.TerrorSource) > 0;

        public override bool ShouldAutoShow()
        {
            return Find.Selector.SelectedObjects.OfType<Thing>().Any(ThingRelevant) ||
                   Find.DesignatorManager.SelectedDesignator is Designator_Place {PlacingDef: {statBases: { } stats}} &&
                   stats.Any(s => s.stat == StatDefOf.TerrorSource);
        }

        public override void CacheCovered()
        {
            Centers.Clear();
            Centers.AddRange(Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).Where(t => t.GetStatValue(StatDefOf.TerrorSource) > 0).Select(t => t.Position));
            Centers.AddRange(Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).Where(t => t.GetStatValue(StatDefOf.TerrorSource) > 0)
                .Select(t => t.Position));
            FromCenters();
        }
    }
}