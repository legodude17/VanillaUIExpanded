using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VUIE
{
    public static class GizmoDrawer
    {
        public static Stack<State> StateStack = new Stack<State>();

        public static void DrawGizmos(IEnumerable<Gizmo> gizmos, Rect inRect, bool forceShrunk = false, Func<Gizmo, Vector2, bool> onClicked = null,
            Action<Gizmo> onMouseOver = null, Func<Gizmo, bool> highlightFunc = null, Func<Gizmo, bool> lowlightFunc = null, bool useHotkeys = true,
            Action<Gizmo, Rect> drawExtras = null)
        {
            StateStack.Push(new State(gizmos));
            var state = StateStack.Peek();
            foreach (var gizmo in state.tmpAllGizmos)
            {
                var group = state.gizmoGroups.FirstOrDefault(g => g[0].GroupsWith(gizmo));
                if (group != null)
                {
                    group.Add(gizmo);
                    group[0].MergeWith(gizmo);
                }
                else
                {
                    var list = SimplePool<List<Gizmo>>.Get();
                    list.Add(gizmo);
                    state.gizmoGroups.Add(list);
                }
            }

            state.firstGizmos.Clear();
            state.shrinkableCommands.Clear();
            var curLoc = new Vector2(inRect.xMin, inRect.yMin);
            var num2 = 0;
            foreach (var group in state.gizmoGroups)
            {
                var gizmo2 = group.FirstOrDefault(g => !g.disabled);
                if (gizmo2 == null)
                {
                    gizmo2 = group.FirstOrDefault();
                }
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
                        curLoc.y += Gizmo.Height + GizmoGridDrawer.GizmoSpacing.x;
                        num2++;
                    }

                    curLoc.x += gizmo2.GetWidth(inRect.width) + GizmoGridDrawer.GizmoSpacing.x;
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
                        curLoc.y += Gizmo.Height + GizmoGridDrawer.GizmoSpacing.x;
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
                    curLoc.x += gizmo.GetWidth(inRect.width) + GizmoGridDrawer.GizmoSpacing.x;
                }

            var x = curLoc.x;
            var rows = 0;
            foreach (var command in state.shrinkableCommands)
            {
                var shrunkSize = command.GetShrunkSize;
                if (curLoc.x + shrunkSize > inRect.xMax)
                {
                    rows++;
                    if (rows > 1) x = inRect.x;

                    curLoc.x = x;
                    curLoc.y += shrunkSize + 3f;
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
                curLoc.x += shrunkSize + 3f;
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
                    {
                        opts.Add(option);
                    }
                    else if (!option.Disabled)
                    {
                        if (matching.Disabled)
                        {
                            opts[opts.IndexOf(matching)] = option;
                        }
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

            foreach (var gizmoGroup in state.gizmoGroups)
            {
                gizmoGroup.Clear();
                SimplePool<List<Gizmo>>.Return(gizmoGroup);
            }

            GizmoGridDrawer.drawnHotKeys.Clear();
            GizmoGridDrawer.drawnHotKeys.AddRange(cachedHotkeys);

            StateStack.Pop();
        }

        private static void ProcessGizmoState(Gizmo giz, GizmoResult result, Vector2 topLeft, ref Gizmo mouseoverGiz, ref Gizmo interactedGiz, ref Gizmo floatMenuGiz,
            ref Event interactedEvent,
            Func<Gizmo, Vector2, bool> onClicked = null)
        {
            if (result.State >= GizmoState.Mouseover) mouseoverGiz = giz;
            if ((result.State == GizmoState.Interacted || result.State == GizmoState.OpenedFloatMenu) && onClicked != null && onClicked(giz, topLeft)) return;
            if (result.State == GizmoState.Interacted || result.State == GizmoState.OpenedFloatMenu && giz.RightClickFloatMenuOptions.FirstOrDefault() == null)
            {
                interactedEvent = result.InteractEvent;
                interactedGiz = giz;
            }
            else if (result.State == GizmoState.OpenedFloatMenu)
            {
                floatMenuGiz = giz;
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
            Log.Message($"GizmoAreaSize: gizmos.Count={gizmos.Count},shrunk={shrunk},maxColumns={maxColumns},maxRows={maxRows}");
            var sizeX = shrunk ? gizmos.Cast<Command>().Max(c => c.GetShrunkSize) : gizmos.Max(g => g.GetWidth(maxRows * Gizmo.Height));
            var sizeY = shrunk ? gizmos.Cast<Command>().Max(c => c.GetShrunkSize) : Gizmo.Height;
            return new Vector2(maxColumns * sizeX + (2 + maxColumns) * (shrunk ? 4f : GizmoGridDrawer.GizmoSpacing.x), maxRows * sizeY + (2 + maxRows) *
                (shrunk ? 4f : GizmoGridDrawer.GizmoSpacing.y));
        }

        public static IEnumerable<Rect> DivideIntoGrid(Rect rect, int items)
        {
            if (!Mathf.Approximately(rect.width, rect.height)) throw new ArgumentException("Provided rect is not square!");
            var perSide = (int) Math.Ceiling(Math.Sqrt(items));
            var sideLengthEach = rect.width / perSide;
            var curLoc = new Vector2(rect.xMin, rect.yMin);
            var size = Vector2.one * sideLengthEach;
            var color = Color.gray;
            for (var i = 0; i < perSide; i++)
            {
                for (var j = 0; j < perSide; j++)
                {
                    yield return new Rect(curLoc, size).ContractedBy(1f);
                    curLoc.y += sideLengthEach;
                    if (i == 0 && j < perSide - 1) Widgets.DrawLine(curLoc, new Vector2(rect.xMax, curLoc.y), color, 1f);
                }

                curLoc.x += sideLengthEach;
                curLoc.y = rect.yMin;
                if (i < perSide - 1) Widgets.DrawLine(new Vector2(curLoc.x, curLoc.y + 2f), new Vector2(curLoc.x, rect.yMax), color, 1f);
            }
        }

        public struct State
        {
            public readonly List<List<Gizmo>> gizmoGroups;

            public readonly List<Gizmo> firstGizmos;

            public readonly List<Command> shrinkableCommands;

            public readonly List<Gizmo> tmpAllGizmos;

            public State(IEnumerable<Gizmo> gizmos)
            {
                tmpAllGizmos = new List<Gizmo>();
                tmpAllGizmos.AddRange(gizmos);
                tmpAllGizmos.SortStable(GizmoGridDrawer.SortByOrder);
                gizmoGroups = new List<List<Gizmo>>();
                firstGizmos = new List<Gizmo>();
                shrinkableCommands = new List<Command>();
            }
        }
    }
}