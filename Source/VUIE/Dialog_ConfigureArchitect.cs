﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    /* TODO:
    *   - Make changes persist across game loads
    *   - Allow moving of designations out of folders and into and out of dropdowns DONE!
    *   - Allow the user to add new designations, both of the special type, and for building buildings
    */
    public class Dialog_ConfigureArchitect : Window
    {
        private readonly DragDropManager<Designator> dragDropManager;

        private readonly List<Designator> unassigned = new List<Designator>();

        private Designator selected;
        private ArchitectCategoryTab selectedCategoryTab;

        private Vector2 tabListScrollPos = Vector2.zero;
        // private Vector2 unassignedScrollPos = Vector2.zero;

        public Dialog_ConfigureArchitect()
        {
            doCloseButton = true;
            doCloseX = false;
            dragDropManager = new DragDropManager<Designator>(DrawDragged);
        }

        public override Vector2 InitialSize => new Vector2(UI.screenWidth - 100f, UI.screenHeight - 100f);

        public static List<ArchitectCategoryTab> ArchitectCategoryTabs => ((MainTabWindow_Architect) MainButtonDefOf.Architect.TabWindow).desPanelsCached;

        public override void PostClose()
        {
            base.PostClose();
            var module = UIMod.GetModule<ArchitectModule>();
            if (module.ActiveIndex == module.VanillaIndex)
            {
                var state = ArchitectLoadSaver.SaveState("Main");
                module.ActiveIndex = module.SavedStates.Count;
                module.SavedStates.Add(state);
            }
            else
            {
                module.SavedStates[module.ActiveIndex] = ArchitectLoadSaver.SaveState(module.SavedStates[module.ActiveIndex].Name);
            }
        }

        private static void DrawDragged(Designator dragee, Vector2 topLeft)
        {
            dragee.GizmoOnGUI(topLeft, Gizmo.Height, new GizmoRenderParms());
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
                GizmoDrawer.DrawGizmos(selectedCategoryTab.def.AllResolvedDesignators, gizmoRect.ContractedBy(GizmoGridDrawer.GizmoSpacing.x), false, (giz, topLeft) =>
                {
                    if (giz is Designator_Dropdown || giz is Designator_Group) selected = (Designator) giz;
                    return true;
                }, useHotkeys: false, drawExtras: (gizmo, rect) =>
                {
                    if (dragDropManager.TryStartDrag(gizmo as Designator, rect))
                        selectedCategoryTab.def.AllResolvedDesignators.Remove(gizmo as Designator);
                    else
                        dragDropManager.DropLocation(rect, des =>
                        {
                            Widgets.DrawHighlight(rect);
                            flag = true;
                        }, des =>
                        {
                            if (des is Designator_Group) return false;
                            if (gizmo is Designator_Group group)
                                group.Add(des);
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
                    dragDropManager.DropLocation(gizmoRect, des => Widgets.DrawHighlight(gizmoRect), des =>
                    {
                        selectedCategoryTab.def.AllResolvedDesignators.Add(des);
                        return true;
                    });
            }

            if (selected != null)
            {
                var labelRect = selectedRect.TopPartPixels(20f);
                Widgets.Label(labelRect, selected.Label);
                selectedRect.yMin += 30f;
                var children = selected is Designator_Group group ? group.Elements : selected is Designator_Dropdown dropdown ? dropdown.Elements : null;
                if (children != null)
                {
                    GizmoDrawer.DrawGizmos(children, selectedRect.ContractedBy(GizmoGridDrawer.GizmoSpacing.x), false, (giz, topLeft) => true, useHotkeys: false,
                        drawExtras: (gizmo, rect) =>
                        {
                            if (dragDropManager.TryStartDrag(gizmo as Designator, rect)) children.Remove(gizmo as Designator);
                        });
                    dragDropManager.DropLocation(selectedRect, des => Widgets.DrawHighlight(selectedRect), des =>
                    {
                        children.Add(des);
                        return true;
                    });
                }
            }

            DoUnassignedList(inRect.ContractedBy(7f));
            dragDropManager.DragDropOnGUI(des => unassigned.Add(des));
        }

        private void DoUnassignedList(Rect inRect)
        {
            var addRect = inRect.BottomPartPixels(30f);
            inRect.yMax += 50f;
            // var viewRect = new Rect(0, 0, inRect.width - 20f, unassigned.Count * (Gizmo.Height + GizmoGridDrawer.GizmoSpacing.y));
            // Widgets.BeginScrollView(inRect, ref unassignedScrollPos, viewRect);
            GizmoDrawer.DrawGizmos(unassigned, inRect.ContractedBy(GizmoGridDrawer.GizmoSpacing.x), false, (giz, topLeft) => true, drawExtras: (giz, rect) =>
            {
                if (dragDropManager.TryStartDrag(giz as Designator, rect)) unassigned.Remove(giz as Designator);
            }, useHotkeys: false);
            // Widgets.EndScrollView();
            dragDropManager.DropLocation(inRect, des => Widgets.DrawHighlight(inRect), des =>
            {
                unassigned.Add(des);
                return true;
            });

            if (Widgets.ButtonText(addRect, "Add"))
            {
                var opts = new List<FloatMenuOption>();
                foreach (var type in typeof(Designator).AllSubclassesNonAbstract()
                    .Where(type => type != typeof(Designator_Dropdown) && type != typeof(Designator_Group) && !typeof(Designator_Install).IsAssignableFrom(type)))
                    if (type == typeof(Designator_Build))
                        opts.Add(new FloatMenuOption("Build", () => Find.WindowStack.Add(new FloatMenu(DefDatabase<ThingDef>.AllDefs
                            .Concat<BuildableDef>(DefDatabase<TerrainDef>.AllDefs)
                            .Where(d => d.canGenerateDefaultDesignator && (d.designationCategory != null || !d.CostList.NullOrEmpty()))
                            .Select(def => new FloatMenuOption(def.label, () => unassigned.Add(new Designator_Build(def)), def as ThingDef)).ToList()))));
                    else
                        opts.Add(new FloatMenuOption(type.Name, () => unassigned.Add((Designator) Activator.CreateInstance(type))));

                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }

        private void DoArchitectTabList(Rect inRect)
        {
            var viewRect = new Rect(0, 0, inRect.width, ArchitectCategoryTabs.Count * 33f);
            var addRect = inRect.BottomPartPixels(30f);
            inRect.yMax += 50f;
            Widgets.DrawMenuSection(inRect);
            Widgets.BeginScrollView(inRect, ref tabListScrollPos, viewRect);
            for (var i = 0; i < ArchitectCategoryTabs.Count; i++)
            {
                var tab = ArchitectCategoryTabs[i];
                var rect = new Rect(viewRect.x, viewRect.y + i * 32f, viewRect.width, 33f);
                dragDropManager.DropLocation(rect, des => selectedCategoryTab = tab, des =>
                {
                    selectedCategoryTab.def.AllResolvedDesignators.Add(des);
                    return true;
                });
                string label = tab.def.LabelCap;
                if (Widgets.ButtonTextSubtle(rect.LeftPartPixels(rect.width - rect.height), label, 0f, 8f, SoundDefOf.Mouseover_Category, new Vector2(-1f, -1f)))
                    selectedCategoryTab = tab;

                if (Widgets.ButtonImage(rect.RightPartPixels(rect.height), Widgets.CheckboxOffTex))
                {
                    unassigned.AddRange(tab.def.AllResolvedDesignators);
                    ArchitectCategoryTabs.Remove(tab);
                }

                if (selectedCategoryTab != tab) UIHighlighter.HighlightOpportunity(rect, tab.def.cachedHighlightClosedTag);
            }

            if (Widgets.ButtonText(addRect, "Add"))
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

            Widgets.EndScrollView();
        }
    }
}