using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VUIE
{
    public static class Utils
    {
        public static List<T> LogInline<T>(this List<T> source, string heading)
        {
            Log.Message(heading);
            GenDebug.LogList(source);
            return source;
        }

        public static bool Matches(this Command command, QuickSearchFilter filter)
        {
            if (command is ICustomCommandMatch mathcer) return mathcer.Matches(filter);
            return filter.Matches(command.Label);
        }

        public static string ToStringTicksToTime(this int ticks)
        {
            Vector2 vector;
            switch (WorldRendererUtility.WorldRenderedNow)
            {
                case true when Find.WorldSelector.selectedTile >= 0:
                    vector = Find.WorldGrid.LongLatOf(Find.WorldSelector.selectedTile);
                    break;
                case true when Find.WorldSelector.NumSelectedObjects > 0:
                    vector = Find.WorldGrid.LongLatOf(Find.WorldSelector.FirstSelectedObject.Tile);
                    break;
                case false when Find.CurrentMap == null: return null;
                default:
                    vector = Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile);
                    break;
            }

            return GenDate.HourOfDay(ticks, vector.x) + "LetterHour".Translate() + " " + GenDate.DateShortStringAt(ticks, vector);
        }

        public static WidgetRow StartRow(this Listing listing) => new(listing.curX, listing.curY, UIDirection.RightThenDown, listing.ColumnWidth);

        public static void EndRow(this Listing listing, WidgetRow row)
        {
            listing.curY = row.FinalY + 24f + row.CellGap;
        }

        public static Dictionary<TKey, TValue> Clone<TKey, TValue>(this IDictionary<TKey, TValue> source)
        {
            var arr = new KeyValuePair<TKey, TValue>[source.Count];
            source.CopyTo(arr, 0);
            return arr.ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        public static void CopyFrom<TKey, TValue>(this IDictionary<TKey, TValue> dest, IDictionary<TKey, TValue> source)
        {
            dest.Clear();
            foreach (var pair in source) dest.Add(pair.Key, pair.Value);
        }
    }
}