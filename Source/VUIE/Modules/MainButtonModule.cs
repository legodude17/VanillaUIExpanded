using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VFECore.UItils;

namespace VUIE
{
    public class MainButtonModule : Module
    {
        private static int lastWidthFull;
        private static int lastWidthMin;

        private static readonly DragDropManager<MainButtonDef> dragDropManager =
            new((def, topLeft) => def.Worker.DoButton(new Rect(topLeft, new Vector2(def.minimized ? lastWidthMin : lastWidthFull, 35f))));

        private static int mouseoverIdx;
        private static bool lastEditMode;
        private static List<string> order;
        private static Dictionary<string, bool> minimized;
        private static Dictionary<string, bool> hidden;

        public override void SaveSettings()
        {
            base.SaveSettings();
            Scribe_Collections.Look(ref order, "mainButtonOrder");
            Scribe_Collections.Look(ref minimized, "mainButtonMinimization");
            Scribe_Collections.Look(ref hidden, "mainButtonsHidden");
        }

        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(MainButtonsRoot), "DoButtons"), new HarmonyMethod(typeof(MainButtonModule), nameof(DoButtons)));
            harm.Patch(AccessTools.Constructor(typeof(MainButtonsRoot)), postfix: new HarmonyMethod(typeof(MainButtonModule), nameof(ApplySettings)));
        }

        public static void ApplySettings(MainButtonsRoot __instance)
        {
            if (order is not null)
            {
                var buttons = __instance.allButtonsInOrder.LogInline("Buttons:").ListFullCopy();
                __instance.allButtonsInOrder.Clear();
                foreach (var defName in order)
                    if (buttons.FirstOrDefault(def => def.defName == defName) is { } buttonDef)
                        __instance.allButtonsInOrder.Add(buttonDef);
                foreach (var def in buttons.Except(__instance.allButtonsInOrder)) __instance.allButtonsInOrder.Add(def);
            }

            if (minimized is not null)
                foreach (var kv in minimized)
                    if (__instance.allButtonsInOrder.FirstOrDefault(def => def.defName == kv.Key) is { } buttonDef)
                        buttonDef.minimized = kv.Value;

            if (hidden is not null)
                foreach (var kv in hidden)
                    if (__instance.allButtonsInOrder.FirstOrDefault(def => def.defName == kv.Key) is { } buttonDef)
                        buttonDef.buttonVisible = !kv.Value;
        }

        public static bool DoButtons(MainButtonsRoot __instance)
        {
            if (UIDefOf.UI_EditMode.Worker.Active)
            {
                lastEditMode = true;
                var totalWidth = __instance.allButtonsInOrder.Append(dragDropManager.Dragging).Where(t => t is not null && t.buttonVisible).Sum(t => t.minimized ? 0.5f : 1f);
                GUI.color = Color.white;
                var widthFull = lastWidthFull = (int) (UI.screenWidth / totalWidth);
                var widthMin = lastWidthMin = widthFull / 2;
                var lastVisible = __instance.allButtonsInOrder.FindLast(x => x.buttonVisible);
                var curX = 0;
                foreach (var button in __instance.allButtonsInOrder.ListFullCopy())
                    if (button.buttonVisible)
                    {
                        var num6 = button.minimized ? widthMin : widthFull;
                        if (button == lastVisible) num6 = UI.screenWidth - curX;

                        var rect = new Rect(curX, UI.screenHeight - 35, num6, 35f);

                        if (Mouse.IsOver(rect) && dragDropManager.DraggingNow)
                        {
                            mouseoverIdx = __instance.allButtonsInOrder.IndexOf(button);
                            rect.x += dragDropManager.Dragging.minimized ? widthMin : widthFull;
                            curX += dragDropManager.Dragging.minimized ? widthMin : widthFull;
                        }

                        if (Mouse.IsOver(rect) && Input.GetMouseButtonDown(1))
                            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                            {
                                new("VUIE.Minimize".Translate(), button.minimized ? null : () => button.minimized = true),
                                new("VUIE.Maximizie".Translate(), button.minimized ? () => button.minimized = false : null)
                            }));

                        button.Worker.DoButton(rect);

                        if (dragDropManager.TryStartDrag(button, rect)) __instance.allButtonsInOrder.Remove(button);

                        curX += num6;
                    }

                dragDropManager.DropLocation(new Rect(0, UI.screenHeight - 35f, UI.screenWidth, 35f), null, def =>
                {
                    __instance.allButtonsInOrder.Insert(mouseoverIdx, def);
                    return true;
                });

                dragDropManager.DragDropOnGUI(def =>
                {
                    def.buttonVisible = false;
                    __instance.allButtonsInOrder.Add(def);
                });
                return false;
            }

            if (lastEditMode)
            {
                order = __instance.allButtonsInOrder.Select(def => def.defName).ToList();
                minimized = __instance.allButtonsInOrder.ToDictionary(def => def.defName, def => def.minimized);
                hidden = __instance.allButtonsInOrder.ToDictionary(def => def.defName, def => !def.buttonVisible);
                UIMod.Settings.Write();
                lastEditMode = false;
            }

            return true;
        }
    }
}