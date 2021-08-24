using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class Dialog_ConfigureArchitect : Window
    {
        private readonly DragDropManager<Designator> dragDropManager;

        private readonly List<Designator> unassigned = new List<Designator>();
        private ArchitectCategoryTab selectedCategoryTab;
        private Vector2 tabListScrollPos = Vector2.zero;
        private Vector2 unassignedScrollPos = Vector2.zero;

        public Dialog_ConfigureArchitect()
        {
            doCloseButton = true;
            doCloseX = false;
            dragDropManager = new DragDropManager<Designator>(DrawDragged);
        }

        public override Vector2 InitialSize => new Vector2(UI.screenWidth - 100f, UI.screenHeight - 100f);

        public static List<ArchitectCategoryTab> ArchitectCategoryTabs => ((MainTabWindow_Architect) MainButtonDefOf.Architect.TabWindow).desPanelsCached;

        private void DrawDragged(Designator dragee, Vector2 topLeft)
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
            Widgets.DrawMenuSection(gizmoRect);
            gizmoRect = gizmoRect.ContractedBy(12f);
            var flag = false;
            if (selectedCategoryTab != null)
            {
                GizmoDrawer.DrawGizmos(selectedCategoryTab.def.AllResolvedDesignators, gizmoRect, false, (giz, topLeft) => true, useHotkeys: false, drawExtras: (gizmo, rect) =>
                {
                    if (Input.GetMouseButton(0) && !dragDropManager.DraggingNow && Mouse.IsOver(rect))
                    {
                        dragDropManager.StartDrag(gizmo as Designator, rect.position);
                        selectedCategoryTab.def.AllResolvedDesignators.Remove(gizmo as Designator);
                    }
                    else
                    {
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
                    }
                });
                if (!flag)
                    dragDropManager.DropLocation(gizmoRect, des => Widgets.DrawHighlight(gizmoRect), des =>
                    {
                        selectedCategoryTab.def.AllResolvedDesignators.Add(des);
                        return true;
                    });
            }

            DoUnassignedList(inRect.ContractedBy(7f));
            dragDropManager.DragDropOnGUI(des => unassigned.Add(des));
        }

        private void DoUnassignedList(Rect inRect)
        {
            GizmoDrawer.DrawGizmos(unassigned, inRect, false, (giz, topLeft) => true, drawExtras: (giz, rect) =>
            {
                if (Input.GetMouseButton(0) && !dragDropManager.DraggingNow && Mouse.IsOver(rect))
                {
                    dragDropManager.StartDrag(giz as Designator, rect.position);
                    unassigned.Remove(giz as Designator);
                }
            }, useHotkeys: false);
            dragDropManager.DropLocation(inRect, des => Widgets.DrawHighlight(inRect), des =>
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