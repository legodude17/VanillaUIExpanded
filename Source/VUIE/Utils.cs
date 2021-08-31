using System.Collections.Generic;
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
    }
}