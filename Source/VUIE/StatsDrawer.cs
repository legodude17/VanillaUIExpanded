using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VUIE
{
    public static class StatsDrawer
    {
        public static void DrawComparison(List<List<StatDrawEntry>> stats, Rect inRect, Action<string> onClick, string selected, ref Vector2 scrollPos)
        {
            var groups = GroupStats(stats).ToList();
            var viewRect = new Rect(0, 0, inRect.width - 22f, groups.Count * 33f);
            var width = viewRect.width / (groups[0].Count() + 1);
            Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);
            var rect = new Rect(8f, 0f, width, 33f);
            foreach (var group in groups)
            {
                foreach (var entry in group)
                {
                    if (Mathf.Approximately(rect.x, 8f))
                    {
                        Widgets.Label(rect, entry.LabelCap);
                        rect.x += width;
                    }

                    Widgets.Label(rect, entry.ValueString);
                    rect.x += width;
                }

                rect.y += 33f;
            }

            Widgets.EndScrollView();
        }

        public static IEnumerable<IEnumerable<StatDrawEntry>> GroupStats(List<List<StatDrawEntry>> source)
        {
            return source.Aggregate(Enumerable.Empty<string>(), (cur, add) => cur.Union(add.Select(sd => sd.labelInt)))
                .Select(label => source.Select(stats => stats.FirstOrDefault(stat => stat.labelInt == label)).ToList()).OrderBy(stats => stats[0].category.displayOrder)
                .ThenByDescending(stats => stats[0].DisplayPriorityWithinCategory).ThenByDescending(stats => stats[0].LabelCap);
        }

        public static IEnumerable<StatDrawEntry> StatsToDraw(Def def, ThingDef stuff) =>
            FinalizeStatsToDraw(StatsReportUtility.StatsToDraw(def, stuff).Where(r => r.ShouldDisplay).Concat(
                def.SpecialDisplayStats(def is BuildableDef buildableDef ? StatRequest.For(buildableDef, stuff) : StatRequest.ForEmpty())));

        public static IEnumerable<StatDrawEntry> StatsToDraw(AbilityDef def) =>
            FinalizeStatsToDraw(StatsReportUtility.StatsToDraw(def).Where(r => r.ShouldDisplay).Concat(def.SpecialDisplayStats(StatRequest.ForEmpty())));

        public static IEnumerable<StatDrawEntry> StatsToDraw(Thing thing) =>
            FinalizeStatsToDraw(StatsReportUtility.StatsToDraw(thing).Where(r => r.ShouldDisplay).Concat(thing.def.SpecialDisplayStats(StatRequest.For(thing)))
                .Where(sd => sd.stat == null || sd.stat.showNonAbstract));

        public static IEnumerable<StatDrawEntry> StatsToDraw(WorldObject worldObject) =>
            FinalizeStatsToDraw(StatsReportUtility.StatsToDraw(worldObject).Where(r => r.ShouldDisplay).Concat(worldObject.def.SpecialDisplayStats(StatRequest.ForEmpty()))
                .Where(sd => sd.stat == null || sd.stat.showNonAbstract));

        public static IEnumerable<StatDrawEntry> StatsToDraw(RoyalTitleDef title, Faction faction, Pawn pawn = null) =>
            FinalizeStatsToDraw(StatsReportUtility.StatsToDraw(title, faction).Where(r => r.ShouldDisplay)
                .Concat(title.SpecialDisplayStats(StatRequest.For(title, faction, pawn))));

        public static IEnumerable<StatDrawEntry> StatsToDraw(Faction faction) =>
            FinalizeStatsToDraw(StatsReportUtility.StatsToDraw(faction).Where(r => r.ShouldDisplay)
                .Concat(faction.def.SpecialDisplayStats(StatRequest.ForEmpty())));

        private static IEnumerable<StatDrawEntry> FinalizeStatsToDraw(IEnumerable<StatDrawEntry> source) =>
            from sd in source orderby sd.category.displayOrder, sd.DisplayPriorityWithinCategory descending, sd.LabelCap select sd;
    }
}