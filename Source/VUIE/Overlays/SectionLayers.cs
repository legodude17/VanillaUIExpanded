using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public abstract class SectionLayer_Overlay : SectionLayer
    {
        protected SectionLayer_Overlay(Section section) : base(section)
        {
        }

        public override bool Visible => OverlayDef.Worker.DrawToggle;
        public abstract OverlayDef OverlayDef { get; }

        public override void Regenerate()
        {
            ClearSubMeshes(MeshParts.All);
            var y = AltitudeLayer.MetaOverlays.AltitudeFor();
            foreach (var cell in section.CellRect)
                if (ShowOnCell(cell))
                {
                    var color = ColorForCell(cell);
                    color.a = 0.33f;
                    var subMesh = GetSubMesh(SolidColorMaterials.SimpleSolidColorMaterial(color, true));
                    var count = subMesh.verts.Count;
                    subMesh.verts.Add(new Vector3(cell.x, y, cell.z));
                    subMesh.verts.Add(new Vector3(cell.x, y, cell.z + 1));
                    subMesh.verts.Add(new Vector3(cell.x + 1, y, cell.z));
                    subMesh.verts.Add(new Vector3(cell.x + 1, y, cell.z + 1));
                    subMesh.uvs.Add(new Vector2(0f, 0f));
                    subMesh.uvs.Add(new Vector2(0f, 1f));
                    subMesh.uvs.Add(new Vector2(1f, 1f));
                    subMesh.uvs.Add(new Vector2(1f, 0f));
                    subMesh.tris.Add(count);
                    subMesh.tris.Add(count + 1);
                    subMesh.tris.Add(count + 2);
                    subMesh.tris.Add(count + 3);
                    subMesh.tris.Add(count + 2);
                    subMesh.tris.Add(count + 1);
                }

            FinalizeMesh(MeshParts.All);
        }

        public override void DrawLayer()
        {
            if (OverlayDef.Worker.Visible && (OverlayDef.Worker is not OverlayWorker_Numbers numbers || numbers.ShowSectionLayer))
                base.DrawLayer();
        }

        public abstract float ValueForCell(IntVec3 c);

        public abstract Color ColorForCell(IntVec3 c);
        public abstract bool ShowOnCell(IntVec3 c);
    }

    public class SectionLayer_Beauty : SectionLayer_Overlay
    {
        public SectionLayer_Beauty(Section section) : base(section) => relevantChangeTypes = MapMeshFlag.Buildings | MapMeshFlag.Terrain;

        public override OverlayDef OverlayDef => OverlayDefOf.Beauty;

        public override float ValueForCell(IntVec3 c) => BeautyUtility.CellBeauty(c, Map);

        public override Color ColorForCell(IntVec3 c)
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

        public override bool ShowOnCell(IntVec3 c) => Mathf.Abs(BeautyUtility.CellBeauty(c, Map)) > 2f;
    }

    public class SectionLayer_LightingOverlay : SectionLayer_Overlay
    {
        public SectionLayer_LightingOverlay(Section section) : base(section) => relevantChangeTypes = MapMeshFlag.GroundGlow;

        public override OverlayDef OverlayDef => OverlayDefOf.Lighting;
        public override float ValueForCell(IntVec3 c) => Map.glowGrid.GameGlowAt(c);

        public override Color ColorForCell(IntVec3 c) => OverlayDef.Worker is OverlayWorker_Lighting {UseGlowColor: true}
            ? Map.glowGrid.VisualGlowAt(c)
            : Color.Lerp(Color.red, Color.green, ValueForCell(c));

        public override bool ShowOnCell(IntVec3 c) => Map.edificeGrid[c] is not {def: {passability: Traversability.Impassable}} && (Map.roofGrid.Roofed(c) ||
            !Mathf.Approximately(ValueForCell(c), Map.skyManager.CurSkyGlow));
    }

    public class SectionLayer_WalkSpeed : SectionLayer_Overlay
    {
        public SectionLayer_WalkSpeed(Section section) : base(section) => relevantChangeTypes = MapMeshFlag.Buildings | MapMeshFlag.Terrain | MapMeshFlag.Snow | MapMeshFlag.Things;
        public override OverlayDef OverlayDef => OverlayDefOf.WalkSpeed;
        public override float ValueForCell(IntVec3 c) => Map.pathing.Normal.pathGrid.PerceivedPathCostAt(c);

        public override Color ColorForCell(IntVec3 c) => Color.Lerp(Color.green, Color.red, Mathf.InverseLerp(0f, 100f, ValueForCell(c)));

        public override bool ShowOnCell(IntVec3 c) => Map.edificeGrid[c] is not {def: {passability: Traversability.Impassable}};
    }

    public class SectionLayer_Usage : SectionLayer_Overlay
    {
        public static Dictionary<Map, ByteGrid> Grids = new();
        private byte max;

        private byte min;

        static SectionLayer_Usage()
        {
            MapMeshFlagUtility.allFlags.Add(UsageFlag);
        }

        public SectionLayer_Usage(Section section) : base(section)
        {
            relevantChangeTypes = UsageFlag;
            if (!Grids.ContainsKey(Map)) Grids[Map] = new ByteGrid(Map);
        }

        public static MapMeshFlag UsageFlag => (MapMeshFlag) MapMeshFlagExt.Usage;
        public override OverlayDef OverlayDef => OverlayDefOf.Usage;

        public override void Regenerate()
        {
            min = Grids[Map].grid.Min();
            max = Grids[Map].grid.Max();
            base.Regenerate();
        }

        public override float ValueForCell(IntVec3 c) => Grids[Map][c];

        public override Color ColorForCell(IntVec3 c) => Color.Lerp(Color.red, Color.green, Mathf.InverseLerp(min, max, ValueForCell(c)));

        public override bool ShowOnCell(IntVec3 c) => Map.edificeGrid[c] is not {def: {passability: Traversability.Impassable}};
    }

    public class SectionLayer_Cleanliness : SectionLayer_Overlay
    {
        public SectionLayer_Cleanliness(Section section) : base(section) => relevantChangeTypes = MapMeshFlag.Things | MapMeshFlag.Terrain | MapMeshFlag.Snow;

        public override OverlayDef OverlayDef => OverlayDefOf.Cleanliness;

        public override float ValueForCell(IntVec3 c) => c.GetTerrain(Map).GetStatValueAbstract(StatDefOf.Cleanliness) + Map.thingGrid.ThingsListAtFast(c).Where(thing =>
                thing.def.category is ThingCategory.Building or ThingCategory.Item or ThingCategory.Filth or ThingCategory.Plant)
            .Sum(thing => thing.stackCount * thing.GetStatValue(StatDefOf.Cleanliness));

        public override Color ColorForCell(IntVec3 c)
        {
            var clean = ValueForCell(c);
            return clean == 0f ? Color.white :
                clean > 0f ? Color.Lerp(Color.white, Color.green, Mathf.InverseLerp(0f, 1.5f, clean)) : Color.Lerp(Color.red, Color.white, Mathf.InverseLerp(-10f, 3f, clean));
        }

        public override bool ShowOnCell(IntVec3 c) => Map.edificeGrid[c] is not {def: {passability: Traversability.Impassable}};
    }

    public class SectionLayer_PlantGrowth : SectionLayer_Overlay
    {
        public SectionLayer_PlantGrowth(Section section) : base(section) => relevantChangeTypes = MapMeshFlag.Things;

        public override OverlayDef OverlayDef => OverlayDefOf.PlantGrowth;
        public override float ValueForCell(IntVec3 c) => Map.thingGrid.ThingAt<Plant>(c).Growth;

        public override Color ColorForCell(IntVec3 c) => Color.Lerp(Color.red, Color.green, ValueForCell(c));

        public override bool ShowOnCell(IntVec3 c) => Map.thingGrid.ThingsListAtFast(c).OfType<Plant>().Any();
    }

    public class SectionLayer_Smoothable : SectionLayer_Overlay
    {
        public SectionLayer_Smoothable(Section section) : base(section) => relevantChangeTypes = MapMeshFlag.Buildings | MapMeshFlag.Terrain;

        public override OverlayDef OverlayDef => OverlayDefOf.Smoothable;
        public override float ValueForCell(IntVec3 c) => ShowOnCell(c) ? 1f : 0f;

        public override Color ColorForCell(IntVec3 c) => Color.green;

        public override bool ShowOnCell(IntVec3 c)
        {
            if (Map.edificeGrid[c] is Thing wall)
                return wall.def.IsSmoothable;
            return Map.terrainGrid.TerrainAt(c).affordances.Contains(TerrainAffordanceDefOf.SmoothableStone);
        }
    }

    public class SectionLayer_BlocksWind : SectionLayer_Overlay
    {
        public SectionLayer_BlocksWind(Section section) : base(section) => relevantChangeTypes = MapMeshFlag.Buildings;

        public override OverlayDef OverlayDef => OverlayDefOf.WindBlockers;
        public override float ValueForCell(IntVec3 c) => ShowOnCell(c) ? 1f : 0f;

        public override Color ColorForCell(IntVec3 c) => Color.blue;

        public override bool ShowOnCell(IntVec3 c) => Map.thingGrid.ThingsListAtFast(c).Any(t => t.def.blockWind);
    }

    public class SectionLayer_Wealth : SectionLayer_Overlay
    {
        public static Dictionary<Map, float[]> Grids = new();

        private float max;

        public SectionLayer_Wealth(Section section) : base(section)
        {
            relevantChangeTypes = MapMeshFlag.Buildings | MapMeshFlag.Terrain;
            if (!Grids.ContainsKey(section.map)) Grids.Add(section.map, new float[section.map.cellIndices.NumGridCells]);
        }

        public override OverlayDef OverlayDef => OverlayDefOf.Wealth;

        private static float ThingWealth(Thing t)
        {
            var num = 0f;
            if (ThingRequestGroup.HaulableEver.Includes(t.def)) num += t.MarketValue * t.stackCount;
            if (ThingRequestGroup.BuildingArtificial.Includes(t.def)) num += t.GetStatValue(StatDefOf.MarketValueIgnoreHp);
            if (t is IThingHolder x and not (PassingShip or MapComponent or Pawn)) num += x.GetDirectlyHeldThings().Sum(ThingWealth);
            return num;
        }

        public override void Regenerate()
        {
            foreach (var cell in section.CellRect) Grids[Map][Map.cellIndices.CellToIndex(cell)] = ValueForCellInt(cell);

            max = Grids[Map].Max();

            base.Regenerate();
        }

        private float ValueForCellInt(IntVec3 c) =>
            WealthWatcher.cachedTerrainMarketValue[Map.terrainGrid.TerrainAt(c).index] + Map.thingGrid.ThingsListAtFast(c).Sum(ThingWealth);

        public override float ValueForCell(IntVec3 c) => Grids[Map][Map.cellIndices.CellToIndex(c)];

        public override Color ColorForCell(IntVec3 c) => Color.Lerp(Color.white, Color.green, Mathf.InverseLerp(0f, max, ValueForCell(c)));

        public override bool ShowOnCell(IntVec3 c) => ValueForCell(c) > 0;
    }

    public class SectionLayer_Health : SectionLayer_Overlay
    {
        public SectionLayer_Health(Section section) : base(section) => relevantChangeTypes = MapMeshFlag.Buildings | MapMeshFlag.BuildingsDamage;

        public override OverlayDef OverlayDef => OverlayDefOf.Health;

        public override float ValueForCell(IntVec3 c)
        {
            var things = Map.thingGrid.ThingsListAtFast(c).Where(t => t.def.useHitPoints).ToList();
            return things.Sum(t => t.HitPoints) / (float) things.Sum(t => t.MaxHitPoints);
        }

        public override Color ColorForCell(IntVec3 c) => Color.Lerp(Color.red, Color.green, ValueForCell(c));

        public override bool ShowOnCell(IntVec3 c) => Map.edificeGrid[c] is not null || Map.thingGrid.ThingsListAtFast(c).Any(t => t.def.useHitPoints);
    }
}