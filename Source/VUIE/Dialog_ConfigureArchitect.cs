using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class Dialog_ConfigureArchitect : Window
    {
        private readonly List<Designator> available = new();
        private readonly QuickSearchWidget availableSearch = new();
        private readonly DragDropManager<Designator> desDragDropManager = new((des, topLeft) => des.GizmoOnGUI(topLeft, Gizmo.Height, new GizmoRenderParms()));

        private readonly DragDropManager<ArchitectCategoryTab> tabDragDropManager = new((tab, topLeft) =>
            Widgets.ButtonTextSubtle(new Rect(topLeft, new Vector2(100f, 33f)), tab.def.LabelCap, 0f, 8f, null, new Vector2(-1f, -1f)));

        private readonly List<Designator> unassigned = new();
        private int curAvailablePage;

        private int curUnassignedPage;

        private Designator selected;
        private ArchitectCategoryTab selectedCategoryTab;

        private bool setPageWhileSearching;

        private Vector2 tabListScrollPos = Vector2.zero;
        private int tabMouseoverIdx = -1;

        public Dialog_ConfigureArchitect()
        {
            doCloseButton = true;
            doCloseX = false;
            foreach (var type in typeof(Designator).AllSubclassesNonAbstract()
                .Where(type => type != typeof(Designator_Dropdown) && type != typeof(Designator_Group) && !typeof(Designator_Install).IsAssignableFrom(type)))
                if (type == typeof(Designator_Build))
                    foreach (var def in DefDatabase<ThingDef>.AllDefs
                        .Concat<BuildableDef>(DefDatabase<TerrainDef>.AllDefs)
                        .Where(d => d.canGenerateDefaultDesignator && d.designationCategory != null))
                        available.Add(new Designator_Build(def));
                else
                    available.Add((Designator) Activator.CreateInstance(type));
        }

        public static Vector2 AdditionalSpacing => new(10f, 0);

        public override Vector2 InitialSize => new(UI.screenWidth - 100f, UI.screenHeight - 100f);

        public static List<ArchitectCategoryTab> ArchitectCategoryTabs => ((MainTabWindow_Architect) MainButtonDefOf.Architect.TabWindow).desPanelsCached;

        public override void PostClose()
        {
            base.PostClose();
            var module = UIMod.GetModule<ArchitectModule>();
            if (module.ActiveIndex == module.VanillaIndex)
            {
                var state = ArchitectLoadSaver.SaveState("VUIE.Main".Translate());
                module.ActiveIndex = module.SavedStates.Count;
                module.SavedStates.Add(state);
            }
            else
                module.SavedStates[module.ActiveIndex] = ArchitectLoadSaver.SaveState(module.SavedStates[module.ActiveIndex].Name);
        }

        public override void DoWindowContents(Rect inRect)
        {
            var tabListRect = inRect.LeftPartPixels(120f);
            inRect.xMin += 125f;
            DoArchitectTabList(tabListRect);
            var gizmoRect = inRect.LeftPart(0.7f);
            inRect.xMin += gizmoRect.width;
            var selectedRect = gizmoRect.BottomHalf().ContractedBy(12f);
            gizmoRect = gizmoRect.TopHalf().ContractedBy(12f);
            Widgets.DrawMenuSection(gizmoRect);
            Widgets.DrawMenuSection(selectedRect);

            if (selectedCategoryTab != null)
            {
                var flag = false;
                gizmoRect = gizmoRect.ContractedBy(GizmoGridDrawer.GizmoSpacing.x);
                var gizmos = selectedCategoryTab.def.AllResolvedDesignators.Cast<Gizmo>().ToList();
                gizmos.SortStable(GizmoGridDrawer.SortByOrder);
                Placeholder placeholder = null;
                if (desDragDropManager.DraggingNow && Mouse.IsOver(gizmoRect))
                {
                    var insertBefore = GizmoDrawer.Format(gizmos, gizmoRect, false, AdditionalSpacing)
                        .FirstOrFallback(
                            i => Mouse.IsOver(new Rect(i.Item1.x - GizmoGridDrawer.GizmoSpacing.x - AdditionalSpacing.x, i.Item1.y,
                                GizmoGridDrawer.GizmoSpacing.x + AdditionalSpacing.x, i.Item1.height)),
                            (Rect.zero, null)).Item2;
                    if (insertBefore is not null)
                    {
                        var idx = gizmos.IndexOf(insertBefore) + 1;
                        gizmos.Insert(idx,
                            placeholder = new Placeholder((insertBefore.order + (idx == gizmos.Count - 1 ? insertBefore.order + 2 : gizmos[idx + 1].order)) / 2,
                                desDragDropManager.Dragging.GetWidth(gizmoRect.width)));
                    }
                }

                GizmoDrawer.DrawGizmos(gizmos, gizmoRect, false, (giz, _) =>
                {
                    if (giz is Designator_Dropdown or Designator_Group) selected = (Designator) giz;
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
                                selectedCategoryTab.def.AllResolvedDesignators.Insert(gizmos.IndexOf(gizmo), des);
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
                            selectedCategoryTab.def.AllResolvedDesignators.Insert(gizmos.IndexOf(placeholder), des);
                        }
                        else selectedCategoryTab.def.AllResolvedDesignators.Add(des);

                        return true;
                    });
            }

            if (selected != null)
            {
                var labelRect = selectedRect.TopPartPixels(20f);
                Widgets.Label(labelRect, selected.Label);
                selectedRect.yMin += 30f;
                var children = selected switch
                {
                    Designator_Group group => group.Elements,
                    Designator_Dropdown dropdown => dropdown.Elements,
                    _ => null
                };
                if (children != null)
                {
                    GizmoDrawer.DrawGizmos(children, selectedRect.ContractedBy(GizmoGridDrawer.GizmoSpacing.x), false, (giz, topLeft) => true, useHotkeys: false,
                        drawExtras: (gizmo, rect) =>
                        {
                            if (desDragDropManager.TryStartDrag(gizmo as Designator, rect)) children.Remove(gizmo as Designator);
                        });
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
            if (!availableSearch.filter.Active) setPageWhileSearching = false;
            var oldPage = curAvailablePage;
            GizmoDrawer.DrawGizmosWithPages(available, ref curAvailablePage, inRect.ContractedBy(GizmoGridDrawer.GizmoSpacing.x), controlsRect, false, (giz, topLeft) => true,
                drawExtras: (giz, rect) =>
                {
                    if (giz is Designator des && desDragDropManager.TryStartDrag(des, rect))
                    {
                        if (giz is Designator_Build build)
                            available[available.IndexOf(des)] = new Designator_Build(build.entDef);
                        else available[available.IndexOf(des)] = (Designator) Activator.CreateInstance(des.GetType());
                    }
                }, useHotkeys: false, searchWidget: availableSearch, jump: !setPageWhileSearching);
            if (oldPage != curAvailablePage && availableSearch.filter.Active) setPageWhileSearching = true;
            if (desDragDropManager.DraggingNow) TooltipHandler.TipRegionByKey(inRect, "VUIE.Architect.DropDelete");
            desDragDropManager.DropLocation(inRect, _ => Widgets.DrawHighlight(inRect), _ => true);
        }

        private void DoUnassignedList(Rect inRect)
        {
            var controlsRect = inRect.TopPartPixels(35f);
            inRect.yMin += 40f;
            Widgets.DrawMenuSection(inRect);
            GizmoDrawer.DrawGizmosWithPages(unassigned, ref curUnassignedPage, inRect.ContractedBy(GizmoGridDrawer.GizmoSpacing.x), controlsRect, false, (giz, topLeft) => true,
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
            var viewRect = new Rect(0, 0, inRect.width, ArchitectCategoryTabs.Count * 33f);
            var addRect = inRect.BottomPartPixels(30f);
            inRect.yMax += 50f;
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
                else if (Widgets.ButtonTextSubtle(buttonRect, label, 0f, 8f, SoundDefOf.Mouseover_Category, new Vector2(-1f, -1f)))
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
                ArchitectCategoryTabs.Insert(tabMouseoverIdx, tab);
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
                    ArchitectCategoryTabs.Add(new ArchitectCategoryTab(catDef, ((MainTabWindow_Architect) MainButtonDefOf.Architect.TabWindow).quickSearchWidget.filter));
                });
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