using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE;

public static class OverlayGetters
{
    public static bool NotImpassable(IntVec3 c, Map map) => map.edificeGrid[c] is not { def: { passability: Traversability.Impassable } };
    public static float? WalkSpeed(IntVec3 c, Map map) => NotImpassable(c, map) ? map.pathing.Normal.pathGrid.PerceivedPathCostAt(c) : null;

    public static Color? WalkSpeedColor(IntVec3 c, Map map) => NotImpassable(c, map)
        ? Color.Lerp(Color.green, Color.red, Mathf.InverseLerp(0f, 100f, WalkSpeed(c, map).GetValueOrDefault()))
        : null;

    public static float? Cleanliness(IntVec3 c, Map map) => NotImpassable(c, map)
        ? c.GetTerrain(map).GetStatValueAbstract(StatDefOf.Cleanliness) + map
            .thingGrid.ThingsListAtFast(c)
            .Where(thing =>
                thing.def.category is ThingCategory.Building or ThingCategory.Item or ThingCategory.Filth or ThingCategory.Plant)
            .Sum(thing => thing.stackCount * thing.GetStatValue(StatDefOf.Cleanliness))
        : null;

    public static Color? CleanlinessColor(IntVec3 c, Map map)
    {
        if (!NotImpassable(c, map)) return null;
        var clean = Cleanliness(c, map).GetValueOrDefault();
        return clean == 0f ? Color.white :
            clean > 0f ? Color.Lerp(Color.white, Color.green, Mathf.InverseLerp(0f, 1.5f, clean)) :
            Color.Lerp(Color.red, Color.white, Mathf.InverseLerp(-10f, 3f, clean));
    }

    public static float? PlantGrowth(IntVec3 c, Map map) => map.thingGrid.ThingAt<Plant>(c)?.Growth;

    public static Color? PlantGrowthColor(IntVec3 c, Map map) => PlantGrowth(c, map) is float val ? Color.Lerp(Color.red, Color.green, val) : null;

    public static float? Smoothable(IntVec3 c, Map map)
    {
        if (map.edificeGrid[c] is Thing wall && wall.def.IsSmoothable)
            return 1f;
        if (map.terrainGrid.TerrainAt(c).affordances.Contains(TerrainAffordanceDefOf.SmoothableStone)) return 1f;
        return null;
    }

    public static Color? SmoothableColor(IntVec3 c, Map map)
    {
        if (map.edificeGrid[c] is Thing wall && wall.def.IsSmoothable)
            return Color.blue;
        if (map.terrainGrid.TerrainAt(c).affordances.Contains(TerrainAffordanceDefOf.SmoothableStone)) return Color.blue;
        return null;
    }

    public static float? Health(IntVec3 c, Map map)
    {
        var things = map.thingGrid.ThingsListAtFast(c).Where(t => t.def.useHitPoints).ToList();
        if (!things.Any()) return null;
        return things.Sum(t => t.HitPoints) / (float)things.Sum(t => t.MaxHitPoints);
    }

    public static Color? HealthColor(IntVec3 c, Map map) => Health(c, map) is float val ? Color.Lerp(Color.red, Color.green, val) : null;
}