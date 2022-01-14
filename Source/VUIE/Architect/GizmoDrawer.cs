using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public static class GizmoDrawer
    {
        public static Stack<State> StateStack = new();

        public static void DrawGizmosWithPages(IEnumerable<Gizmo> gizmos, ref int curPage, Rect gizmosRect, Rect controlsRect, bool forceShrunk = false,
            Func<Gizmo, Vector2, bool> onClicked = null,
            Action<Gizmo> onMouseOver = null, bool useHotkeys = true, Action<Gizmo, Rect> drawExtras = null, QuickSearchWidget searchWidget = null, bool jump = false)
        {
            var groups = SplitIntoGroups(gizmos.ToList(), gizmosRect, forceShrunk);
            curPage = Mathf.Clamp(curPage, 0, groups.Count - 1);
            DoPageControls(ref curPage, groups.Count, controlsRect, searchWidget, gizmosRect);
            if (searchWidget?.filter is {Active: true} && !groups[curPage].Any(gizmo => Matches(searchWidget.filter, gizmo)) && jump)
            {
                var idx = groups.FindIndex(group => group.Any(gizmo => Matches(searchWidget.filter, gizmo)));
                if (idx > 0) curPage = idx;
            }

            DrawGizmos(groups[curPage], gizmosRect, forceShrunk, onClicked, onMouseOver,
                gizmo => Matches(searchWidget?.filter, gizmo),
                gizmo => gizmo is Command c && searchWidget != null && searchWidget.filter.Active && !searchWidget.filter.Matches(c.Label), useHotkeys, drawExtras);
        }

        private static bool Matches(QuickSearchFilter filter, Gizmo gizmo) => gizmo is Command c && filter is {Active: true} && filter.Matches(c.Label);

        private static void DoPageControls(ref int curPage, int pages, Rect inRect, QuickSearchWidget searchWidget, Rect? checkForMouseOver = null)
        {
            var row = new WidgetRow(inRect.x, inRect.y, UIDirection.RightThenDown);
            if (row.ButtonText("<", fixedWidth: 50f, active: curPage > 0)) curPage--;
            row.Gap(12f);
            row.Label("VUIE.PageOf".Translate(curPage + 1, pages));
            if (searchWidget != null)
            {
                row.Gap(12f);
                var rect = new Rect(row.LeftX(200f), row.curY, 200f, inRect.height);
                row.IncrementPosition(200f);
                searchWidget.OnGUI(rect);
            }

            row.Gap(12f);
            if (row.ButtonText(">", fixedWidth: 50f, active: curPage + 1 < pages)) curPage++;

            if (Event.current.type == EventType.ScrollWheel && (Mouse.IsOver(inRect) || checkForMouseOver is not null && Mouse.IsOver(checkForMouseOver.Value)))
            {
                curPage = Mathf.Clamp(curPage + (int) Mathf.Sign(Event.current.delta.y), 0, pages - 1);
                Event.current.Use();
            }
        }

        private static List<List<Gizmo>> SplitIntoGroups(IReadOnlyCollection<Gizmo> gizmos, Rect inRect, bool shrunk = false)
        {
            if (shrunk && !gizmos.All(g => g is Command)) throw new ArgumentException("If shrunk, all gizmos must be Commands");
            var result = new List<List<Gizmo>>();
            var curGroup = new List<Gizmo>();
            var curLoc = new Vector2(inRect.xMin, inRect.yMin);
            if (shrunk)
                foreach (var command in gizmos.OfType<Command>().Where(c => c.Visible))
                {
                    var shrunkSize = command.GetShrunkSize;
                    if (curLoc.x + shrunkSize > inRect.xMax)
                    {
                        curLoc.x = inRect.xMin;
                        curLoc.y += shrunkSize + 3f;
                        if (curLoc.y + shrunkSize * 2 > inRect.yMax)
                        {
                            result.Add(curGroup);
                            curGroup = new List<Gizmo>();
                            curLoc.x = inRect.xMin;
                            curLoc.y = inRect.yMin;
                        }
                    }

                    curLoc.x += shrunkSize + 3f;
                    curGroup.Add(command);
                }
            else
                foreach (var gizmo in gizmos.Where(gizmo => gizmo.Visible))
                {
                    if (curLoc.x + gizmo.GetWidth(inRect.width) > inRect.xMax)
                    {
                        curLoc.x = inRect.xMin;
                        curLoc.y += Gizmo.Height + GizmoGridDrawer.GizmoSpacing.y;
                        if (curLoc.y + Gizmo.Height * 2 > inRect.yMax)
                        {
                            result.Add(curGroup);
                            curGroup = new List<Gizmo>();
                            curLoc.x = inRect.xMin;
                            curLoc.y = inRect.yMin;
                        }
                    }

                    curLoc.x += gizmo.GetWidth(inRect.width) + GizmoGridDrawer.GizmoSpacing.x;
                    curGroup.Add(gizmo);
                }

            if (curGroup.Any() || !result.Any()) result.Add(curGroup);
            return result;
        }

        public static IEnumerable<(Rect, Gizmo)> Format(IReadOnlyCollection<Gizmo> gizmos, Rect inRect, bool shrunk = false, Vector2 additionalSpacing = default)
        {
            if (shrunk && !gizmos.All(g => g is Command)) throw new ArgumentException("If shrunk, all gizmos must be Commands");
            var curLoc = new Vector2(inRect.xMin, inRect.yMin);
            if (shrunk)
                foreach (var command in gizmos.OfType<Command>().Where(c => c.Visible))
                {
                    var shrunkSize = command.GetShrunkSize;
                    if (curLoc.x + shrunkSize > inRect.xMax)
                    {
                        curLoc.x = inRect.xMin;
                        curLoc.y += shrunkSize + 3f + additionalSpacing.y;
                        if (curLoc.y + Gizmo.Height > inRect.yMax)
                        {
                            curLoc.x = inRect.xMin;
                            curLoc.y = inRect.yMin;
                        }
                    }

                    yield return (new Rect(curLoc, new Vector2(shrunkSize, shrunkSize)), command);
                    curLoc.x += shrunkSize + 3f + additionalSpacing.x;
                }
            else
                foreach (var gizmo in gizmos.Where(gizmo => gizmo.Visible))
                {
                    if (curLoc.x + gizmo.GetWidth(inRect.width) > inRect.xMax)
                    {
                        curLoc.x = inRect.xMin;
                        curLoc.y += Gizmo.Height + GizmoGridDrawer.GizmoSpacing.x + additionalSpacing.y;
                        if (curLoc.y + Gizmo.Height > inRect.yMax)
                        {
                            curLoc.x = inRect.xMin;
                            curLoc.y = inRect.yMin;
                        }
                    }

                    curLoc.x += gizmo.GetWidth(inRect.width) + GizmoGridDrawer.GizmoSpacing.x + additionalSpacing.x;
                    yield return (new Rect(curLoc, new Vector2(gizmo.GetWidth(inRect.width), Gizmo.Height)), gizmo);
                }
        }

        public static void DrawGizmos(IEnumerable<Gizmo> gizmos, Rect inRect, bool forceShrunk = false, Func<Gizmo, Vector2, bool> onClicked = null,
            Action<Gizmo> onMouseOver = null, Func<Gizmo, bool> highlightFunc = null, Func<Gizmo, bool> lowlightFunc = null, bool useHotkeys = true,
            Action<Gizmo, Rect> drawExtras = null, Vector2 additionalSpacing = default, bool sort = true)
        {
            StateStack.Push(SimplePool<State>.Get());
            var state = StateStack.Peek();
            state.Init(gizmos, sort);
            var curLoc = new Vector2(inRect.xMin, inRect.yMin);
            var num2 = 0;
            foreach (var group in state.gizmoGroups)
            {
                var gizmo2 = group.FirstOrDefault(g => !g.disabled);
                if (gizmo2 == null)
                    gizmo2 = group.FirstOrDefault();
                else
                {
                    if (gizmo2 is Command_Toggle toggle)
                    {
                        if (!toggle.activateIfAmbiguous && !toggle.isActive())
                            gizmo2 = (from gizmo3 in @group
                                let toggle2 = gizmo3 as Command_Toggle
                                where toggle2 != null && !toggle2.disabled && toggle2.isActive()
                                select gizmo3).FirstOrDefault() ?? gizmo2;

                        if (toggle.activateIfAmbiguous && toggle.isActive())
                            gizmo2 = (from t in @group
                                let toggle3 = t as Command_Toggle
                                where toggle3 != null && !toggle3.disabled && !toggle3.isActive()
                                select t).FirstOrDefault() ?? gizmo2;
                    }
                }

                if (gizmo2 != null)
                {
                    if (gizmo2 is Command command && (command.shrinkable && command.Visible || forceShrunk)) state.shrinkableCommands.Add(command);

                    if (curLoc.x + gizmo2.GetWidth(inRect.width) > inRect.xMax)
                    {
                        curLoc.x = inRect.xMin;
                        curLoc.y += Gizmo.Height + GizmoGridDrawer.GizmoSpacing.y + additionalSpacing.y;
                        num2++;
                    }

                    curLoc.x += gizmo2.GetWidth(inRect.width) + GizmoGridDrawer.GizmoSpacing.x + additionalSpacing.x;
                    state.firstGizmos.Add(gizmo2);
                }
            }

            if (num2 > 1 && state.shrinkableCommands.Count > 1 || forceShrunk)
                foreach (var command in state.shrinkableCommands)
                    state.firstGizmos.Remove(command);
            else
                state.shrinkableCommands.Clear();
            if (forceShrunk && state.firstGizmos.Any()) Log.Warning("Using force shrunk, but still have nonshrunk gizmos");
            var cachedHotkeys = new HashSet<KeyCode>(GizmoGridDrawer.drawnHotKeys);
            GizmoGridDrawer.drawnHotKeys.Clear();
            if (!useHotkeys) GizmoGridDrawer.drawnHotKeys.AddRange(typeof(KeyCode).GetEnumValues().OfType<KeyCode>());
            Text.Font = GameFont.Tiny;
            curLoc = new Vector2(inRect.xMin, inRect.yMin);
            Gizmo interactedGiz = null;
            Event interactedEvent = null;
            Gizmo floatMenuGiz = null;
            Gizmo mouseoverGiz = null;
            if (!forceShrunk)
                foreach (var gizmo in state.firstGizmos.Where(gizmo => gizmo.Visible))
                {
                    if (curLoc.x + gizmo.GetWidth(inRect.width) > inRect.xMax)
                    {
                        curLoc.x = inRect.xMin;
                        curLoc.y += Gizmo.Height + GizmoGridDrawer.GizmoSpacing.y + additionalSpacing.y;
                    }

                    var parms = new GizmoRenderParms
                    {
                        highLight = highlightFunc != null && highlightFunc(gizmo),
                        lowLight = lowlightFunc != null && lowlightFunc(gizmo)
                    };

                    var result = gizmo.GizmoOnGUI(curLoc, inRect.width, parms);
                    drawExtras?.Invoke(gizmo, new Rect(curLoc, new Vector2(gizmo.GetWidth(inRect.width), Gizmo.Height)));
                    ProcessGizmoState(gizmo, result, curLoc, ref mouseoverGiz, ref interactedGiz, ref floatMenuGiz, ref interactedEvent, onClicked);
                    GenUI.AbsorbClicksInRect(new Rect(curLoc.x, curLoc.y, gizmo.GetWidth(inRect.width), Gizmo.Height + GizmoGridDrawer.GizmoSpacing.y).ContractedBy(-12f));
                    curLoc.x += gizmo.GetWidth(inRect.width) + GizmoGridDrawer.GizmoSpacing.x + additionalSpacing.x;
                }

            var x = curLoc.x;
            var rows = 0;
            foreach (var command in state.shrinkableCommands.Where(c => c.Visible))
            {
                var shrunkSize = command.GetShrunkSize;
                if (curLoc.x + shrunkSize > inRect.xMax)
                {
                    rows++;
                    if (rows > 1) x = inRect.x;

                    curLoc.x = x;
                    curLoc.y += shrunkSize + 3f + additionalSpacing.y;
                }

                var parms = new GizmoRenderParms
                {
                    highLight = highlightFunc != null && highlightFunc(command),
                    lowLight = lowlightFunc != null && lowlightFunc(command)
                };
                var result = command.GizmoOnGUIShrunk(curLoc, shrunkSize, parms);
                drawExtras?.Invoke(command, new Rect(curLoc, Vector2.one * shrunkSize));
                ProcessGizmoState(command, result, curLoc, ref mouseoverGiz, ref interactedGiz, ref floatMenuGiz, ref interactedEvent, onClicked);
                GenUI.AbsorbClicksInRect(new Rect(curLoc.x, curLoc.y, shrunkSize, shrunkSize + 3f).ExpandedBy(3f));
                curLoc.x += shrunkSize + 3f + additionalSpacing.x;
            }

            onMouseOver?.Invoke(mouseoverGiz);

            if (interactedGiz != null)
            {
                foreach (var gizmo in FindMatchingGroup(interactedGiz).Where(gizmo => gizmo != interactedGiz && !gizmo.disabled && interactedGiz.InheritInteractionsFrom(gizmo)))
                    gizmo.ProcessInput(interactedEvent);
                interactedGiz.ProcessInput(interactedEvent);
                Event.current.Use();
            }
            else if (floatMenuGiz != null)
            {
                var opts = new List<FloatMenuOption>();
                opts.AddRange(floatMenuGiz.RightClickFloatMenuOptions);
                foreach (var option in from gizmo in FindMatchingGroup(floatMenuGiz)
                    where gizmo != floatMenuGiz && !gizmo.disabled &&
                          floatMenuGiz.InheritFloatMenuInteractionsFrom(gizmo)
                    from option in gizmo.RightClickFloatMenuOptions
                    select option)
                {
                    var matching = opts.FirstOrDefault(p => p.Label == option.Label);
                    if (matching == null)
                        opts.Add(option);
                    else if (!option.Disabled)
                    {
                        if (matching.Disabled)
                            opts[opts.IndexOf(matching)] = option;
                        else
                        {
                            var prevAction = matching.action;
                            var localOptionAction = option.action;
                            matching.action = delegate
                            {
                                prevAction();
                                localOptionAction();
                            };
                        }
                    }
                }

                Event.current.Use();
                if (opts.Any()) Find.WindowStack.Add(new FloatMenu(opts));
            }

            GizmoGridDrawer.drawnHotKeys.Clear();
            GizmoGridDrawer.drawnHotKeys.AddRange(cachedHotkeys);
            state.Reset();
            SimplePool<State>.Return(state);
            StateStack.Pop();
        }

        private static void ProcessGizmoState(Gizmo giz, GizmoResult result, Vector2 topLeft, ref Gizmo mouseoverGiz, ref Gizmo interactedGiz, ref Gizmo floatMenuGiz,
            ref Event interactedEvent,
            Func<Gizmo, Vector2, bool> onClicked = null)
        {
            if (result.State >= GizmoState.Mouseover) mouseoverGiz = giz;
            switch (result.State)
            {
                case GizmoState.Interacted or GizmoState.OpenedFloatMenu when onClicked != null && onClicked(giz, topLeft):
                    return;
                case GizmoState.Interacted:
                case GizmoState.OpenedFloatMenu when giz.RightClickFloatMenuOptions.FirstOrDefault() == null:
                    interactedEvent = result.InteractEvent;
                    interactedGiz = giz;
                    break;
                case GizmoState.OpenedFloatMenu:
                    floatMenuGiz = giz;
                    break;
            }
        }

        private static List<Gizmo> FindMatchingGroup(Gizmo toMatch)
        {
            return StateStack.Peek().gizmoGroups.FirstOrDefault(group => group.Contains(toMatch));
        }

        public static Vector2 GizmoAreaSize(List<Gizmo> gizmos, bool shrunk, int maxColumns = 0, int maxRows = 0)
        {
            if (maxColumns == 0 && maxRows == 0) maxColumns = maxRows = (int) Math.Ceiling(Math.Sqrt(gizmos.Count));
            if (maxColumns == 0) maxColumns = Mathf.CeilToInt(gizmos.Count / (float) maxRows);
            if (maxRows == 0) maxRows = Mathf.CeilToInt(gizmos.Count / (float) maxColumns);
            var sizeX = shrunk ? gizmos.Cast<Command>().Max(c => c.GetShrunkSize) : gizmos.Max(g => g.GetWidth(maxRows * Gizmo.Height));
            var sizeY = shrunk ? gizmos.Cast<Command>().Max(c => c.GetShrunkSize) : Gizmo.Height;
            return new Vector2(maxColumns * sizeX + (2 + maxColumns) * (shrunk ? 4f : GizmoGridDrawer.GizmoSpacing.x), maxRows * sizeY + (2 + maxRows) *
                (shrunk ? 4f : GizmoGridDrawer.GizmoSpacing.y));
        }

        public static IEnumerable<Rect> DivideIntoGrid(Rect rect, int items, int columns = 0, int rows = 0)
        {
            if (columns == 0 && rows == 0)
            {
                if (!Mathf.Approximately(rect.width, rect.height)) throw new ArgumentException("Provided rect is not square!");
                var perSide = (int) Math.Ceiling(Math.Sqrt(items));
                rows = perSide;
                columns = perSide;
            }

            if (rows == 0) rows = (int) Math.Ceiling((double) items / columns);
            else if (columns == 0) columns = (int) Math.Ceiling((double) items / rows);
            var curLoc = new Vector2(rect.xMin, rect.yMin);
            var size = new Vector2(rect.width / columns, rect.height / rows);
            var color = Color.gray;
            for (var i = 0; i < columns; i++)
            {
                for (var j = 0; j < rows; j++)
                {
                    yield return new Rect(curLoc, size).ContractedBy(1f);
                    curLoc.y += size.y;
                    if (i == 0 && j < rows - 1) Widgets.DrawLine(curLoc, new Vector2(rect.xMax, curLoc.y), color, 1f);
                }

                curLoc.x += size.x;
                curLoc.y = rect.yMin;
                if (i < columns - 1) Widgets.DrawLine(new Vector2(curLoc.x, curLoc.y + 2f), new Vector2(curLoc.x, rect.yMax), color, 1f);
            }
        }

        public class State
        {
            public readonly List<Gizmo> firstGizmos = new();
            public readonly List<List<Gizmo>> gizmoGroups = new();

            public readonly List<Command> shrinkableCommands = new();

            public readonly List<Gizmo> tmpAllGizmos = new();

            public void Init(IEnumerable<Gizmo> gizmos, bool sort = true)
            {
                tmpAllGizmos.AddRange(gizmos);
                if (sort) tmpAllGizmos.SortStable(GizmoGridDrawer.SortByOrder);
                foreach (var gizmo in tmpAllGizmos)
                {
                    var group = gizmoGroups.FirstOrDefault(g => g[0].GroupsWith(gizmo));
                    if (group != null)
                    {
                        group.Add(gizmo);
                        group[0].MergeWith(gizmo);
                    }
                    else
                    {
                        var list = SimplePool<List<Gizmo>>.Get();
                        list.Add(gizmo);
                        gizmoGroups.Add(list);
                    }
                }
            }

            public void Reset()
            {
                tmpAllGizmos.Clear();

                foreach (var gizmoGroup in gizmoGroups)
                {
                    gizmoGroup.Clear();
                    SimplePool<List<Gizmo>>.Return(gizmoGroup);
                }

                gizmoGroups.Clear();
                shrinkableCommands.Clear();
                firstGizmos.Clear();
            }
        }
    }
}