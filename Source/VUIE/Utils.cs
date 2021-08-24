using System.Collections.Generic;
using RimWorld;
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
    }
}