using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class ArchitectModule : Module
    {
        public static ArchitectCategoryTab CurrentTab;

        public static Gizmo overrideMouseOverGizmo;
        public override string Label => "Architect Menu";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            if (Widgets.ButtonText(inRect.ContractedBy(12f).TopPartPixels(30f), "Open Configuration Menu")) Find.WindowStack.Add(new Dialog_ConfigureArchitect());
        }

        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(DesignationCategoryDef), "ResolveDesignators"), postfix: new HarmonyMethod(typeof(ArchitectModule), nameof(FixDesig)));
            harm.Patch(AccessTools.Method(typeof(ArchitectCategoryTab), nameof(ArchitectCategoryTab.DesignationTabOnGUI)),
                new HarmonyMethod(typeof(ArchitectModule), nameof(UpdateCurrentTab)));
            harm.Patch(AccessTools.Method(typeof(ArchitectCategoryTab), "Matches"), new HarmonyMethod(typeof(ArchitectModule), nameof(CustomMatch)));
            harm.Patch(AccessTools.Method(typeof(ArchitectCategoryTab), "CacheSearchState"), postfix: new HarmonyMethod(typeof(ArchitectModule), nameof(FixUnique)));
            harm.Patch(AccessTools.Method(typeof(GizmoGridDrawer), nameof(GizmoGridDrawer.DrawGizmoGrid)),
                postfix: new HarmonyMethod(typeof(ArchitectModule), nameof(OverrideMouseOverGizmo)));
        }

        public static void OverrideMouseOverGizmo(ref Gizmo mouseoverGizmo)
        {
            if (overrideMouseOverGizmo != null) mouseoverGizmo = overrideMouseOverGizmo;
        }

        public static void FixUnique(ref Designator ___uniqueSearchMatch, QuickSearchFilter ___quickSearchFilter)
        {
            if (___uniqueSearchMatch is ICustomCommandMatch matcher) ___uniqueSearchMatch = matcher.UniqueSearchMatch(___quickSearchFilter) as Designator;
        }

        public static bool CustomMatch(Command c, QuickSearchFilter ___quickSearchFilter, ref bool __result)
        {
            if (c is ICustomCommandMatch matcher)
            {
                __result = matcher.Matches(___quickSearchFilter);
                return false;
            }

            return true;
        }

        public static void UpdateCurrentTab(ArchitectCategoryTab __instance)
        {
            CurrentTab = __instance;
        }

        public static void FixDesig(DesignationCategoryDef __instance)
        {
            foreach (var def in DefDatabase<BuildableGroupDef>.AllDefs) def.RemoveChildren(__instance);
            // var def = new BuildableGroupDef
            // {
            //     defName = __instance.defName,
            //     label = __instance.label,
            //     defs = __instance.AllResolvedDesignators.OfType<Designator_Build>().Select(d => d.entDef).ToList(),
            //     category = __instance
            // };
            // def.RemoveChildren(__instance);
        }
    }

    public class BuildableGroupDef : Def
    {
        public DesignationCategoryDef category;
        public int columns;
        public List<BuildableDef> defs;
        private Designator_Group designatorGroup;
        public int rows;
        public bool scroll;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var er in base.ConfigErrors()) yield return er;

            if (defs.NullOrEmpty()) yield return "Must provide defs";
            if (category == null) yield return "Must provide category";
            if (!scroll && rows > 0) yield return "Only provide rows when scrollable";
        }

        public void RemoveChildren(DesignationCategoryDef def)
        {
            var inGroup = new List<Designator>();
            foreach (var designator in def.AllResolvedDesignators.ToList())
                switch (designator)
                {
                    case Designator_Build build when defs.Contains(build.entDef) || defName == "Everything":
                    {
                        inGroup.Add(designator);
                        def.AllResolvedDesignators.Remove(designator);
                        break;
                    }
                    case Designator_Dropdown dropdown:
                    {
                        foreach (var element in dropdown.Elements.OfType<Designator_Build>().Where(build2 => defs.Contains(build2.entDef) || defName == "Everything").ToList())
                        {
                            inGroup.Add(element);
                            dropdown.Elements.Remove(element);
                        }

                        if (dropdown.Elements.Count == 0) def.AllResolvedDesignators.Remove(designator);


                        break;
                    }
                }

            if (!inGroup.Any()) return;

            if (designatorGroup == null)
            {
                designatorGroup = new Designator_Group(inGroup, label)
                {
                    Columns = columns
                };
                category.resolvedDesignators.Add(designatorGroup);
            }
            else
            {
                designatorGroup.Elements.AddRange(inGroup);
            }
        }
    }

    public interface ICustomCommandMatch
    {
        bool Matches(QuickSearchFilter filter);
        Command UniqueSearchMatch(QuickSearchFilter filter);
    }

    public class Designator_Group : Designator, ICustomCommandMatch
    {
        private readonly string label;
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

    public class CommandGroup : Window
    {
        public readonly Vector2 Anchor;
        private readonly Func<bool> closeFunc;

        private readonly List<Command> elements;
        private readonly Func<Command, bool> highlightFunc;
        private readonly Func<Command, bool> lowlightFunc;
        private readonly Action<Command> onChosen;
        private readonly Action<Command> onMouseOver;
        public int Columns;
        public bool vanishIfMouseDistant;

        public CommandGroup(List<Command> gizmos, Vector2 anchor, Action<Command> onMouseOver = null, Action<Command> onChosen = null, Func<Command, bool> highlightFunc = null,
            Func<Command, bool> lowlightFunc = null, Func<bool> shouldClose = null)
        {
            elements = gizmos;
            Anchor = anchor;
            doCloseButton = false;
            doCloseX = false;
            drawShadow = false;
            preventCameraMotion = false;
            layer = WindowLayer.Super;
            this.onMouseOver = onMouseOver ?? (c => { });
            this.onChosen = onChosen ?? (c => { });
            this.highlightFunc = highlightFunc ?? (c => false);
            this.lowlightFunc = lowlightFunc ?? (c => false);
            closeFunc = shouldClose ?? (() => false);
        }

        public override Vector2 InitialSize => GizmoDrawer.GizmoAreaSize(elements.Cast<Gizmo>().ToList(), true, Columns);

        public override float Margin => GizmoGridDrawer.GizmoSpacing.x;

        public override void DoWindowContents(Rect inRect)
        {
            if (vanishIfMouseDistant &&
                GenUI.DistFromRect(new Rect(0, 0, InitialSize.x, InitialSize.y).ExpandedBy(Gizmo.Height * 2), Event.current.mousePosition) > 95f) Close();
            if (closeFunc()) Close(false);
            GizmoDrawer.DrawGizmos(elements, inRect, true, (gizmo, topLeft) =>
            {
                if (!Event.current.control) return false;
                onChosen((Command) gizmo);
                return true;
            }, gizmo => onMouseOver((Command) gizmo), gizmo => highlightFunc((Command) gizmo), gizmo => lowlightFunc((Command) gizmo), false);
        }

        public override void SetInitialSizeAndPosition()
        {
            windowRect = new Rect(Anchor.x, Anchor.y - InitialSize.y, InitialSize.x, InitialSize.y);
        }
    }
}