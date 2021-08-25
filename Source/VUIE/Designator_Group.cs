using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class Designator_Group : Designator, ICustomCommandMatch
    {
        internal readonly string label;
        public int Columns;
        private List<(Designator, Rect)> display;
        public List<Designator> Elements;
        private Vector2 lastTopLeft;

        public Designator_Group(List<Designator> inGroup, string str)
        {
            Elements = inGroup;
            label = str;
        }

        public Designator Active { get; private set; }

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get { yield break; }
        }

        public override float PanelReadoutTitleExtraRightMargin => Active?.PanelReadoutTitleExtraRightMargin ?? 0f;

        public override bool Visible => Elements.Any(d => d.Visible);
        public override string Label => Active?.Label ?? label.CapitalizeFirst();
        public override string Desc => Active?.Desc ?? Elements.Where(elm => elm.Visible).Select(elm => elm.Label).ToLineList("  - ", true);

        public bool Matches(QuickSearchFilter filter)
        {
            return Elements.Where(elm => elm.Visible).Any(c => c.Matches(filter));
        }

        public Command UniqueSearchMatch(QuickSearchFilter filter)
        {
            var matches = Elements.Where(elm => elm.Visible).Where(elm => elm.Matches(filter)).Take(2).ToList();
            return matches.Count == 1 ? matches.First() : null;
        }

        public void Add(Designator des)
        {
            Elements.Add(des);
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var result = base.GizmoOnGUI(topLeft, maxWidth, parms);
            lastTopLeft = topLeft;
            if (ArchitectModule.CurrentTab != null && ArchitectModule.CurrentTab.quickSearchFilter.Active && Matches(ArchitectModule.CurrentTab.quickSearchFilter))
            {
                var size = GizmoDrawer.GizmoAreaSize(Elements.Where(elm => elm.Visible).Cast<Gizmo>().ToList(), true, Columns);
                var rect = new Rect(new Vector2(lastTopLeft.x, lastTopLeft.y - size.y), size);
                Find.WindowStack.ImmediateWindow((int) Elements.Average(elm => elm.GetHashCode()), rect, WindowLayer.Super, () => GizmoDrawer.DrawGizmos(
                    Elements.Where(elm => elm.Visible),
                    new Rect(Vector2.zero, size).ContractedBy(GizmoGridDrawer.GizmoSpacing.x), true,
                    null, command => ArchitectModule.overrideMouseOverGizmo = command,
                    ArchitectModule.CurrentTab.shouldHighLightGizmoFunc,
                    ArchitectModule.CurrentTab.shouldLowLightGizmoFunc, false));
            }

            Designator_Dropdown.DrawExtraOptionsIcon(topLeft, GetWidth(maxWidth));
            return result;
        }

        public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
        {
            rect.position += new Vector2(iconOffset.x * rect.size.x, iconOffset.y * rect.size.y);
            if (!disabled || parms.lowLight)
                GUI.color = IconDrawColor;
            else
                GUI.color = IconDrawColor.SaturationChanged(0f);

            display = Elements.Where(elm => elm.Visible).Take(16)
                .Zip(GizmoDrawer.DivideIntoGrid(rect, Math.Min(Elements.Count, 16)).ToList(), (designator, rect1) => (designator, rect1)).ToList();
            Active = display?.FirstOrDefault(tuple => Mouse.IsOver(tuple.Item2)).Item1;

            if (parms.lowLight) GUI.color = GUI.color.ToTransparent(0.6f);
            foreach (var (designator, rect1) in display)
                designator.DrawIcon(rect1, buttonMat, parms);

            GUI.color = Color.white;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            return Active?.CanDesignateCell(loc) ?? false;
        }

        public override void DrawMouseAttachments()
        {
            Active?.DrawMouseAttachments();
        }

        public override void DrawPanelReadout(ref float curY, float width)
        {
            Active?.DrawPanelReadout(ref curY, width);
        }

        public override void ProcessInput(Event ev)
        {
            if (ev.button == 0 && Active != null) Active.ProcessInput(ev);
            else OpenMenu();
        }

        private void OpenMenu(bool vanish = true, Func<bool> shouldClose = null, bool closeOthers = false)
        {
            if (Find.WindowStack.windows.Any(window => window is CommandGroup group && group.Anchor == lastTopLeft) && !closeOthers) return;
            if (closeOthers) Find.WindowStack.windows.Where(window => window is CommandGroup group && group.Anchor == lastTopLeft).Do(window => window.Close(false));
            Find.WindowStack.Add(new CommandGroup(Elements.Where(elm => elm.Visible).Cast<Command>().ToList(), lastTopLeft,
                command => ArchitectModule.overrideMouseOverGizmo = command, null,
                ArchitectModule.CurrentTab.shouldHighLightGizmoFunc, ArchitectModule.CurrentTab.shouldLowLightGizmoFunc, shouldClose)
            {
                vanishIfMouseDistant = vanish,
                closeOnClickedOutside = vanish,
                focusWhenOpened = !vanish,
                Columns = Columns
            });
        }
    }
}