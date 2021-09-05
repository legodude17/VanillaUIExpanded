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

        static CoverageOverlays()
        {
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

            UIMod.Harm.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.SpawnSetup)), postfix: new HarmonyMethod(typeof(CoverageOverlays), nameof(BuildingCheck)));
            UIMod.Harm.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.DeSpawn)), postfix: new HarmonyMethod(typeof(CoverageOverlays), nameof(BuildingCheck)));
            UIMod.Harm.Patch(AccessTools.PropertySetter(typeof(Game), nameof(Game.CurrentMap)), postfix: new HarmonyMethod(typeof(CoverageOverlays), nameof(PostMapChange)));
        }

        public static void BuildingCheck(Thing __instance)
        {
            if (Find.CurrentMap == __instance.Map && CoverageOverlayDefs.TryGetValue(__instance.def, out var overlay))
                (overlay.Worker as OverlayWorker_Coverage)?.Notify_BuildingChanged();
        }

        public static void PostMapChange()
        {
            foreach (var def in CoverageOverlayDefs.Values) (def.Worker as OverlayWorker_Coverage)?.Notify_ChangedMap();
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
        private readonly List<IntVec3> centers = new();
        private readonly List<IntVec3> covered = new();
        private readonly HashSet<int> coveredIndices = new();
        public Color CoverageColor;
        public ThingDef CoverageDef;
        public float CoverageRange;
        private CellBoolDrawer drawer;

        public override bool CanShowNumbers => false;

        public bool GetCellBool(int index) => coveredIndices.Contains(index);

        public Color GetCellExtraColor(int index) => Color.white;

        public Color Color => CoverageColor;

        public override OverlayWorker Init(OverlayDef def)
        {
            Notify_ChangedMap();
            return base.Init(def);
        }

        public void Notify_ChangedMap()
        {
            if (Find.CurrentMap is null)
            {
                drawer = null;
                covered.Clear();
                return;
            }

            drawer = new CellBoolDrawer(this, Find.CurrentMap.Size.x, Find.CurrentMap.Size.z);
            CacheCovered();
        }

        public void Notify_BuildingChanged()
        {
            CacheCovered();
        }

        public override void OverlayUpdate()
        {
            base.OverlayUpdate();
            if (Visible && drawer is not null)
            {
                drawer.MarkForDraw();
                if (covered.Count > 0) GenDraw.DrawFieldEdges(covered.ToList(), CoverageColor);
            }

            drawer?.CellBoolDrawerUpdate();
        }

        public void CacheCovered()
        {
            centers.Clear();

            centers.AddRange(Find.CurrentMap.listerThings.ThingsOfDef(CoverageDef).Select(t => t.Position));

            centers.AddRange(Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint)
                .Where(bp => GenConstruct.BuiltDefOf(bp.def) == CoverageDef).Select(t => t.Position).ToList());

            centers.AddRange(Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame)
                .Where(frame => GenConstruct.BuiltDefOf(frame.def) == CoverageDef).Select(t => t.Position).ToList());

            covered.Clear();

            covered.AddRange(centers.SelectMany(center => GenRadial.RadialCellsAround(center, CoverageRange, true)));

            coveredIndices.AddRange(covered.Select(Find.CurrentMap.cellIndices.CellToIndex));

            drawer.SetDirty();
        }
    }
}