using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VFECore.UItils;

namespace VUIE
{
    public class Dialog_ConfigureArchitect : Window
    {
        public static HashSet<Type> IgnoreDesignatorTypes = new()
        {
            typeof(Designator_Dropdown),
            typeof(Designator_Group),
            AccessTools.TypeByName("RFF_Code.Designator_Terraform")
        };

        public static Dictionary<Type, DesignatorTypeHandling> SpecialHandling = new();

        private static readonly List<Designator> unassigned = new();

        private readonly List<Designator> available = new();
        private readonly QuickSearchWidget availableSearch = new();
        private readonly DragDropManager<Designator> desDragDropManager = new((des, topLeft) => des.GizmoOnGUI(topLeft, Gizmo.Height, new GizmoRenderParms()));

        private readonly GizmoDrawer.BoolHolder hideAvailableSearch = new();

        private readonly DragDropManager<ArchitectCategoryTab> tabDragDropManager = new((tab, topLeft) =>
            Widgets.ButtonTextSubtle(new Rect(topLeft, new Vector2(100f, 33f)), tab.def.LabelCap, 0f, 8f, null, new Vector2(-1f, -1f)));

        private int curAvailablePage;
        private int curPageGroup;

        private int curPageMain;

        private int curUnassignedPage;

        private readonly bool godMode;

        private bool jump;

        private Designator selected;
        private ArchitectCategoryTab selectedCategoryTab;

        private Vector2 tabListScrollPos = Vector2.zero;
        private int tabMouseoverIdx = -1;

        static Dialog_ConfigureArchitect()
        {
            SpecialHandling.Add(typeof(Designator_Build), DesignatorTypeHandling.Create(
                _ => DefDatabase<ThingDef>.AllDefs
                    .Concat<BuildableDef>(DefDatabase<TerrainDef>.AllDefs)
                    .Where(d => d.canGenerateDefaultDesignator && d.designationCategory != null)
                    .Select(def => new Designator_Build(def)),
                des => (des as Designator_Build)?.PlacingDef.defName,
                (data, _) =>
                {
                    if (data is null) return null;
                    var def = (BuildableDef) DefDatabase<ThingDef>.GetNamedSilentFail(data) ?? DefDatabase<TerrainDef>.GetNamedSilentFail(data);
                    return def is null ? null : new Designator_Build(def);
                }));
            if (ModLister.HasActiveModWithName("More Planning 1.3"))
                SpecialHandling.Add(AccessTools.TypeByName("MorePlanning.Designators.SelectColorDesignator"),
                    DesignatorTypeHandling.Create(
                        type => Enumerable.Range(0, 10).Select(i => (Designator) Activator.CreateInstance(type, i)),
                        des => Traverse.Create(des).Field("Color").GetValue<int>().ToString(),
                        (data, type) => (Designator) Activator.CreateInstance(type, int.Parse(data))));
            if (ModLister.HasActiveModWithName("Blueprints"))
                SpecialHandling.Add(AccessTools.TypeByName("Blueprints.Designator_Blueprint"), DesignatorTypeHandling.Create(
                    type => Traverse
                        .Create(AccessTools.TypeByName("Blueprints.BlueprintController"))
                        .Field("_instance")
                        .Field("_blueprints")
                        .GetValue<IList>()?.Cast<object>()
                        .Select(obj => (Designator) Activator.CreateInstance(type, obj)),
                    des => Traverse.Create(des).Field("Blueprint").Field("name").GetValue<string>(),
                    (data, type) => (Designator) Activator.CreateInstance(type, Traverse.Create(AccessTools.TypeByName("Blueprints.BlueprintController"))
                        .Method("FindBlueprint", data)
                        .GetValue(data))));
        }

        public Dialog_ConfigureArchitect()
        {
            doCloseButton = true;
            doCloseX = false;
            godMode = DebugSettings.godMode;
            DebugSettings.godMode = true;
            foreach (var type in typeof(Designator).AllSubclassesNonAbstract()
                .Where(type => !typeof(Designator_Install).IsAssignableFrom(type) && !IgnoreDesignatorTypes.Contains(type) && (SpecialHandling.ContainsKey(type) ||
                    type.GetConstructors().Any(m => m.GetParameters().Length == 0))))
                if (SpecialHandling.ContainsKey(type))
                    available.AddRange(SpecialHandling[type].AllOptions(type) ?? Enumerable.Empty<Designator>());
                else
                    try
                    {
                        available.Add((Designator) Activator.CreateInstance(type));
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[VUIE] Got error while attempting to create a Designator of type {type}: {e}");
                    }

            var not = typeof(Designator).AllSubclassesNonAbstract().Except(IgnoreDesignatorTypes)
                .Where(type => !SpecialHandling.ContainsKey(type) && type.GetConstructors().All(m => m.GetParameters().Length != 0)).ToList();
            if (not.Any())
            {
                Log.Warning("[VUIE] Found unhandled Designator types:");
                GenDebug.LogList(not);
            }
        }

        public static Vector2 AdditionalSpacing => new(10f, 0);

        public override Vector2 InitialSize => new(UI.screenWidth - 100f, UI.screenHeight - 100f);

        public static List<ArchitectCategoryTab> ArchitectCategoryTabs => ((MainTabWindow_Architect) MainButtonDefOf.Architect.TabWindow).desPanelsCached;

        public override void PostClose()
        {
            base.PostClose();
            var module = UIMod.GetModule<ArchitectModule>();
            module.SavedStates[module.ActiveIndex] = ArchitectLoadSaver.SaveState(module.SavedStates[module.ActiveIndex].Name);

            ModCompatModule.Notify_ArchitectChanged();
            DebugSettings.godMode = godMode;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var architectWidth = 33f + ArchitectModule.ArchitectWidth + 25f;
            var tabListRect = inRect.LeftPartPixels(architectWidth);
            DoArchitectTabList(tabListRect);
            inRect.xMin += architectWidth + 5f;
            var gizmoRect = inRect.LeftPart(0.7f);
            inRect.xMin += gizmoRect.width;
            var selectedRect = gizmoRect.BottomHalf().ContractedBy(12f);
            gizmoRect = gizmoRect.TopHalf().ContractedBy(12f);
            selectedRect.yMax -= 50f;
            selectedRect.yMin += 12f;
            var controlsRect = gizmoRect;
            controlsRect.height = 40f;
            controlsRect.y += gizmoRect.height + 5f;
            var labelRect = controlsRect.RightHalf();
            controlsRect = controlsRect.LeftHalf();

            if (selectedCategoryTab != null)
            {
                var flag = false;
                Widgets.Label(controlsRect.RightHalf(), selectedCategoryTab.def.LabelCap + " ^");
                Widgets.DrawMenuSection(gizmoRect);
                var gizmos = selectedCategoryTab.def.AllResolvedDesignators.Cast<Gizmo>().ToList();
                Placeholder placeholder = null;
                if (desDragDropManager.DraggingNow && Mouse.IsOver(gizmoRect))
                {
                    var insertBefore = GizmoDrawer.Format(gizmos, gizmoRect.ContractedBy(GizmoGridDrawer.GizmoSpacing.x * 3), false, AdditionalSpacing)
                        .FirstOrFallback(
                            i => Mouse.IsOver(new Rect(i.Item1.x - GizmoGridDrawer.GizmoSpacing.x - AdditionalSpacing.x, i.Item1.y,
                                GizmoGridDrawer.GizmoSpacing.x + AdditionalSpacing.x, i.Item1.height)),
                            (Rect.zero, null)).Item2;
                    if (insertBefore is not null)
                    {
                        var idx = gizmos.IndexOf(insertBefore) + 1;
                        gizmos.Place(idx,
                            placeholder = new Placeholder((insertBefore.order + (idx >= gizmos.Count - 1 ? insertBefore.order + 2 : gizmos[idx + 1].order)) / 2,
                                desDragDropManager.Dragging.GetWidth(gizmoRect.width)));
                    }
                }

                GizmoDrawer.DrawGizmosWithPages(gizmos, ref curPageMain, gizmoRect.ContractedBy(GizmoGridDrawer.GizmoSpacing.x * 3), controlsRect.LeftHalf(), false, (giz, _) =>
                {
                    if (Event.current.shift)
                    {
                        selectedCategoryTab.def.AllResolvedDesignators.Remove(giz as Designator);
                        unassigned.Add(giz as Designator);
                    }
                    else if (giz is Designator_Dropdown or Designator_Group) selected = (Designator) giz;

                    return true;
                }, useHotkeys: false, additionalSpacing: AdditionalSpacing, drawExtras: (gizmo, rect) =>
                {
                    if (desDragDropManager.TryStartDrag(gizmo as Designator, rect))
                        selectedCategoryTab.def.AllResolvedDesignators.Remove(gizmo as Designator);
                    else
                        desDragDropManager.DropLocation(rect, des =>
                        {
                            Widgets.DrawHighlight(rect);
                            flag = true;
                        }, des =>
                        {
                            if (des is Designator_Group) return false;
                            if (gizmo is Designator_Group group)
                                group.Add(des);
                            else if (gizmo is Placeholder)
                            {
                                des.order = gizmo.order;
                                selectedCategoryTab.def.AllResolvedDesignators.Place(gizmos.IndexOf(gizmo), des);
                            }
                            else
                                Dialog_TextEntry.GetString(str =>
                                {
                                    selectedCategoryTab.def.AllResolvedDesignators.Remove(gizmo as Designator);
                                    selectedCategoryTab.def.AllResolvedDesignators.Add(new Designator_Group(new List<Designator> {des, gizmo as Designator}, str));
                                });

                            return true;
                        });
                });
                if (!flag)
                    desDragDropManager.DropLocation(gizmoRect, des => Widgets.DrawHighlight(gizmoRect), des =>
                    {
                        if (placeholder != null)
                        {
                            des.order = placeholder.order;
                            selectedCategoryTab.def.AllResolvedDesignators.Place(gizmos.IndexOf(placeholder), des);
                        }
                        else selectedCategoryTab.def.AllResolvedDesignators.Add(des);

                        return true;
                    });
            }

            Widgets.DrawMenuSection(selectedRect);

            if (selected != null)
            {
                Widgets.Label(labelRect.LeftHalf(), selected.Label + " V");
                var children = selected switch
                {
                    Designator_Group group => group.Elements,
                    Designator_Dropdown dropdown => dropdown.Elements,
                    _ => null
                };
                if (children != null)
                {
                    GizmoDrawer.DrawGizmosWithPages(children, ref curPageGroup, selectedRect.ContractedBy(GizmoGridDrawer.GizmoSpacing.x * 3), labelRect.RightHalf(), false,
                        (giz, topLeft) => true, useHotkeys: false,
                        drawExtras: (gizmo, rect) =>
                        {
                            if (desDragDropManager.TryStartDrag(gizmo as Designator, rect)) children.Remove(gizmo as Designator);
                        }, additionalSpacing: AdditionalSpacing);
                    desDragDropManager.DropLocation(selectedRect, _ => Widgets.DrawHighlight(selectedRect), des =>
                    {
                        children.Add(des);
                        return true;
                    });
                }
            }

            DoUnassignedList(inRect.TopHalf().ContractedBy(7f));
            DoAvailable(inRect.BottomHalf().ContractedBy(7f));
            desDragDropManager.DragDropOnGUI(des => unassigned.Add(des));
        }

        private void DoAvailable(Rect inRect)
        {
            var controlsRect = inRect.TopPartPixels(35f);
            inRect.yMin += 40f;
            Widgets.DrawMenuSection(inRect);
            if (availableSearch.CurrentlyFocused()) jump = true;
            var oldPage = curAvailablePage;
            GizmoDrawer.DrawGizmosWithPages(available, ref curAvailablePage, inRect.ContractedBy(GizmoGridDrawer.GizmoSpacing.x), controlsRect, false, (giz, topLeft) =>
                {
                    if (Event.current.shift) selectedCategoryTab.def.AllResolvedDesignators.Add(Clone((Designator) giz));
                    return true;
                },
                drawExtras: (giz, rect) =>
                {
                    if (giz is Designator des && desDragDropManager.TryStartDrag(des, rect)) available[available.IndexOf(des)] = Clone(des);
                }, useHotkeys: false, searchWidget: availableSearch, jump: jump, hideSearch: hideAvailableSearch);
            if (oldPage != curAvailablePage && availableSearch.CurrentlyFocused()) jump = false;
            if (desDragDropManager.DraggingNow) TooltipHandler.TipRegionByKey(inRect, "VUIE.Architect.DropDelete");
            desDragDropManager.DropLocation(inRect, _ => Widgets.DrawHighlight(inRect), _ => true);
        }

        public static Designator Clone(Designator des) => DesignatorSaved.Load(DesignatorSaved.Save(des));

        private static IEnumerable<Designator> ExpandGroups(Designator des) => des switch
        {
            Designator_Dropdown dropdown => dropdown.Elements.SelectMany(ExpandGroups),
            Designator_Group group => group.Elements.SelectMany(ExpandGroups),
            _ => Gen.YieldSingle(des)
        };

        private void DoUnassignedList(Rect inRect)
        {
            var controlsRect = inRect.TakeTopPart(35f);
            inRect.yMin += 5f;
            var gatherRect = controlsRect.TakeRightPart(150f);
            TooltipHandler.TipRegionByKey(gatherRect, "VUIE.Architect.Gather.Desc");
            if (Widgets.ButtonText(gatherRect, "VUIE.Architect.Gather".Translate()))
                unassigned.AddRange(available.Select(DesignatorSaved.Save)
                    .Except(ArchitectCategoryTabs.SelectMany(tab => tab.def.resolvedDesignators.SelectMany(ExpandGroups)).Select(DesignatorSaved.Save))
                    .Select(DesignatorSaved.Load));

            Widgets.DrawMenuSection(inRect);
            GizmoDrawer.DrawGizmosWithPages(unassigned, ref curUnassignedPage, inRect.ContractedBy(GizmoGridDrawer.GizmoSpacing.x), controlsRect, false, (giz, topLeft) =>
                {
                    if (Event.current.shift)
                    {
                        unassigned.Remove(giz as Designator);
                        selectedCategoryTab.def.AllResolvedDesignators.Add((Designator) giz);
                    }

                    return true;
                },
                drawExtras: (giz, rect) =>
                {
                    if (desDragDropManager.TryStartDrag(giz as Designator, rect)) unassigned.Remove(giz as Designator);
                }, useHotkeys: false);
            desDragDropManager.DropLocation(inRect, des => Widgets.DrawHighlight(inRect), des =>
            {
                unassigned.Add(des);
                return true;
            });
        }

        private void DoArchitectTabList(Rect inRect)
        {
            var viewRect = new Rect(0, 0, inRect.width - 25f, ArchitectCategoryTabs.Count * 33f);
            var addRect = inRect.BottomPartPixels(30f);
            inRect.yMax -= 50f;
            Widgets.DrawMenuSection(inRect);
            Widgets.BeginScrollView(inRect, ref tabListScrollPos, viewRect);
            var curY = viewRect.y;
            for (var i = 0; i < ArchitectCategoryTabs.Count; i++)
            {
                var tab = ArchitectCategoryTabs[i];
                var rect = new Rect(viewRect.x, curY, viewRect.width, 32f);
                curY += 32f;
                if (tabDragDropManager.DraggingNow && Mouse.IsOver(rect))
                {
                    tabMouseoverIdx = ArchitectCategoryTabs.IndexOf(tab);
                    rect.y += 32f;
                    curY += 32f;
                }


                desDragDropManager.DropLocation(rect, des => selectedCategoryTab = tab, des =>
                {
                    selectedCategoryTab.def.AllResolvedDesignators.Add(des);
                    return true;
                });
                string label = tab.def.LabelCap;
                var buttonRect = rect.LeftPartPixels(rect.width - rect.height);
                if (!desDragDropManager.DraggingNow && tabDragDropManager.TryStartDrag(tab, buttonRect))
                    ArchitectCategoryTabs.Remove(tab);
                else if (ArchitectModule.IconsActive && Mouse.IsOver(buttonRect) && Input.GetMouseButtonDown(1))
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                    {
                        new("VUIE.Architect.EditIcon".Translate(), () => Find.WindowStack.Add(new Dialog_ChooseIcon(path => ArchitectModule.SetIcon(tab.def.defName, path))))
                    }));
                else if (ArchitectModule.DoArchitectButton(buttonRect, label, 0f, 8f, SoundDefOf.Mouseover_Category, new Vector2(-1f, -1f)))
                    selectedCategoryTab = tab;


                if (Widgets.ButtonImage(rect.RightPartPixels(rect.height), Widgets.CheckboxOffTex))
                {
                    unassigned.AddRange(tab.def.AllResolvedDesignators);
                    ArchitectCategoryTabs.Remove(tab);
                }

                if (selectedCategoryTab != tab) UIHighlighter.HighlightOpportunity(rect, tab.def.cachedHighlightClosedTag);
            }

            tabDragDropManager.DropLocation(viewRect, null, tab =>
            {
                ArchitectCategoryTabs.Place(tabMouseoverIdx, tab);
                tabMouseoverIdx = -1;
                return true;
            });

            tabDragDropManager.DragDropOnGUI(tab => ArchitectCategoryTabs.Add(tab));
            Widgets.EndScrollView();

            if (Widgets.ButtonText(addRect, "VUIE.Add".Translate()))
                Dialog_TextEntry.GetString(str =>
                {
                    var catDef = new DesignationCategoryDef
                    {
                        defName = str.Replace(" ", ""),
                        label = str
                    };
                    catDef.ResolveDesignators();
                    if (ArchitectModule.IconsActive) ArchitectModule.SetIcon(catDef.defName, ArchitectModule.AllPossibleIcons().RandomElement());
                    DefGenerator.AddImpliedDef(catDef);
                    ArchitectCategoryTabs.Add(new ArchitectCategoryTab(catDef, ((MainTabWindow_Architect) MainButtonDefOf.Architect.TabWindow).quickSearchWidget.filter));
                });
        }

        public struct DesignatorTypeHandling
        {
            public Func<Type, IEnumerable<Designator>> AllOptions;
            public Func<Designator, string> Save;
            public Func<string, Type, Designator> Load;

            public static DesignatorTypeHandling Create(Func<Type, IEnumerable<Designator>> options, Func<Designator, string> save, Func<string, Type, Designator> load) =>
                new()
                {
                    AllOptions = options,
                    Save = save,
                    Load = load
                };
        }

        public class Placeholder : Gizmo
        {
            private readonly float width;

            public Placeholder(float order, float width)
            {
                this.order = order;
                this.width = width;
            }

            public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms) => new(GizmoState.Clear);

            public override float GetWidth(float maxWidth) => Mathf.Min(width, maxWidth);
        }
    }
}