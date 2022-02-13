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
        private readonly ArchitectModule module = UIMod.GetModule<ArchitectModule>();
        public int Columns;
        private List<(Designator, Rect)> display;
        public List<Designator> Elements;
        private Vector2 lastTopLeft;

        public Designator_Group(List<Designator> inGroup, string str)
        {
            Elements = inGroup;
            label = str;
            var des = inGroup.FirstOrDefault();
            if (module.GroupDisplay == ArchitectModule.GroupDisplayType.Vanilla) Active = des;
            if (des == null) return;
            icon = des.icon;
            iconDrawScale = des.iconDrawScale;
            iconProportions = des.iconProportions;
            iconTexCoords = des.iconTexCoords;
            iconAngle = des.iconAngle;
            iconOffset = des.iconOffset;
        }

        public Designator Active { get; private set; }

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get { yield break; }
        }

        public override float PanelReadoutTitleExtraRightMargin => Active?.PanelReadoutTitleExtraRightMargin ?? 0f;

        public override bool Visible => VisibleElements.Any();
        public override string Label => Active?.Label ?? label.CapitalizeFirst();
        public override string Desc => Active?.Desc ?? Elements.Where(elm => elm.Visible).Select(elm => elm.Label).ToLineList("  - ", true);

        public IEnumerable<Designator> VisibleElements => Elements.Where(elm => elm.Visible);

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
                    Elements, new Rect(Vector2.zero, size).ContractedBy(GizmoGridDrawer.GizmoSpacing.x), true,
                    (gizmo, _) =>
                    {
                        if (module.GroupDisplay == ArchitectModule.GroupDisplayType.Vanilla && gizmo is Designator des)
                        {
                            Active = des;
                            icon = des.icon;
                            iconDrawScale = des.iconDrawScale;
                            iconProportions = des.iconProportions;
                            iconTexCoords = des.iconTexCoords;
                            iconAngle = des.iconAngle;
                            iconOffset = des.iconOffset;
                        }

                        return false;
                    }, command => ArchitectModule.overrideMouseOverGizmo = command,
                    ArchitectModule.CurrentTab.shouldHighLightGizmoFunc,
                    ArchitectModule.CurrentTab.shouldLowLightGizmoFunc, false));
            }

            Designator_Dropdown.DrawExtraOptionsIcon(topLeft, GetWidth(maxWidth));
            return result;
        }

        public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
        {
            if (module.GroupDisplay == ArchitectModule.GroupDisplayType.Vanilla)
            {
                if (Active is not null) Active.DrawIcon(rect, buttonMat, parms);
                else base.DrawIcon(rect, buttonMat, parms);
                return;
            }

            var gizmos = GizmoDrawer.State.GroupGizmos(VisibleElements);
            rect.position += new Vector2(iconOffset.x * rect.size.x, iconOffset.y * rect.size.y);
            GUI.color = !disabled || parms.lowLight ? IconDrawColor : IconDrawColor.SaturationChanged(0f);
            var grid = (module.GroupDisplay == ArchitectModule.GroupDisplayType.SquareGrid
                ? GizmoDrawer.DivideIntoGrid(rect, Math.Min(gizmos.Count, (int) Math.Pow(module.MaxSize, 2)))
                : GizmoDrawer.DivideIntoGrid(rect, gizmos.Count, Mathf.Min(module.MaxSize, Mathf.CeilToInt(gizmos.Count / 2f)), 2)).ToList();
            display = gizmos.Select(group => group[0] as Designator).Zip(grid, (designator, rect1) => (designator, rect1)).ToList();
            Active = display?.FirstOrDefault(tuple => Mouse.IsOver(tuple.Item2)).Item1;

            if (parms.lowLight) GUI.color = GUI.color.ToTransparent(0.6f);
            foreach (var (designator, rect1) in display)
                designator.DrawIcon(rect1, buttonMat, parms);

            GUI.color = Color.white;
        }

        public override float GetWidth(float maxWidth)
        {
            if (module.GroupDisplay == ArchitectModule.GroupDisplayType.ExpandGrid)
                return Mathf.Min(module.MaxSize, Mathf.CeilToInt(VisibleElements.Count() / 2f)) * (Height - 1f) / 2f;
            return base.GetWidth(maxWidth);
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc) => Active?.CanDesignateCell(loc) ?? false;

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
            if (ev is {button: 0} && Active != null && !module.GroupOpenLeft) Active.ProcessInput(ev);
            else OpenMenu();
        }

        private void OpenMenu(bool vanish = true, Func<bool> shouldClose = null, bool closeOthers = false)
        {
            if (Find.WindowStack.windows.Any(window => window is CommandGroup group && group.Anchor == lastTopLeft) && !closeOthers) return;
            if (closeOthers) Find.WindowStack.windows.Where(window => window is CommandGroup group && group.Anchor == lastTopLeft).Do(window => window.Close(false));
            Find.WindowStack.Add(new CommandGroup(Elements.Cast<Command>().ToList(), lastTopLeft,
                command => ArchitectModule.overrideMouseOverGizmo = command, command =>
                {
                    if (module.GroupDisplay == ArchitectModule.GroupDisplayType.Vanilla)
                    {
                        Active = command as Designator;
                        icon = command.icon;
                        iconDrawScale = command.iconDrawScale;
                        iconProportions = command.iconProportions;
                        iconTexCoords = command.iconTexCoords;
                        iconAngle = command.iconAngle;
                        iconOffset = command.iconOffset;
                    }
                },
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