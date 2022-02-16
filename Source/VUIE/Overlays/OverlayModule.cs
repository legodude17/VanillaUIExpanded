using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class OverlayModule : Module
    {
        private readonly Dictionary<OverlayDef, string> labelCache = new();
        public bool MoveOverlays;
        private Vector2 settingsScrollPos = Vector2.zero;
        public override string LabelKey => "VUIE.Overlays";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            var listing = new Listing_Standard();
            var viewRect = new Rect(0, 0, inRect.width - 20f, 33f + 12f + DefDatabase<OverlayDef>.AllDefs.Sum(def => def.Worker.SettingsHeight));
            Widgets.BeginScrollView(inRect, ref settingsScrollPos, viewRect);
            listing.Begin(viewRect);
            listing.CheckboxLabeled("VUIE.Overlays.Move".Translate(), ref MoveOverlays);
            listing.GapLine();
            var rect = listing.GetRect(30f);
            if (Widgets.ButtonText(rect.LeftHalf(), "VUIE.Overlays.EnableAll".Translate()))
                foreach (var def in DefDatabase<OverlayDef>.AllDefs)
                    def.Worker.Enable();
            if (Widgets.ButtonText(rect.RightHalf(), "VUIE.Overlays.DisabledAll".Translate()))
                foreach (var def in DefDatabase<OverlayDef>.AllDefs)
                    def.Worker.Disable();
            foreach (var def in DefDatabase<OverlayDef>.AllDefs)
            {
                if (!labelCache.TryGetValue(def, out var label))
                {
                    label = def.Worker.Label.Replace("Toggle ", "").CapitalizeFirst().Split('\n')[0] + ":";
                    labelCache[def] = label;
                }

                listing.Label(label);
                listing.Indent();
                listing.ColumnWidth -= 12f;
                def.Worker.DoSettings(listing);
                listing.ColumnWidth += 12f;
                listing.Outdent();
            }

            listing.End();
            Widgets.EndScrollView();
        }

        public override void SaveSettings()
        {
            base.SaveSettings();
            Scribe_Values.Look(ref MoveOverlays, "moveOverlays");
            if (LongEventHandler.currentEvent != null)
                LongEventHandler.ExecuteWhenFinished(() => UIDefOf.VUIE_Overlays.buttonVisible = MoveOverlays);
            else UIDefOf.VUIE_Overlays.buttonVisible = MoveOverlays;
            if (Scribe.EnterNode("overlays"))
            {
                foreach (var def in DefDatabase<OverlayDef>.AllDefs)
                {
                    Scribe.EnterNode(def.defName);
                    def.Worker.ExposeData();
                    Scribe.ExitNode();
                }

                Scribe.ExitNode();
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
                foreach (var def in DefDatabase<OverlayDef>.AllDefs)
                {
                    Scribe.EnterNode("overlayWorker_" + def.defName);
                    def.Worker.ExposeData();
                    Scribe.ExitNode();
                }
        }

        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls)),
                transpiler: new HarmonyMethod(AccessTools.Method(GetType(), nameof(MaybeShowOverlays)), Priority.Last));
            harm.Patch(AccessTools.Method(typeof(MainButtonsRoot), nameof(MainButtonsRoot.HandleLowPriorityShortcuts)),
                postfix: new HarmonyMethod(GetType(), nameof(HandleShortcuts)));
            harm.Patch(AccessTools.Method(typeof(MapInterface), nameof(MapInterface.MapInterfaceOnGUI_BeforeMainTabs)),
                postfix: new HarmonyMethod(GetType(), nameof(OverlaysOnGUI)));
            harm.Patch(AccessTools.Method(typeof(MapInterface), nameof(MapInterface.MapInterfaceUpdate)),
                postfix: new HarmonyMethod(GetType(), nameof(OverlaysUpdate)));
        }

        public static void OverlaysOnGUI()
        {
            if (WorldRendererUtility.WorldRenderedNow || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;
            foreach (var def in DefDatabase<OverlayDef>.AllDefs.Where(def => def.Worker.Visible).ToList()) def.Worker.OverlayOnGUI();
        }

        public static void OverlaysUpdate()
        {
            if (WorldRendererUtility.WorldRenderedNow || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;
            foreach (var def in DefDatabase<OverlayDef>.AllDefs.Where(def => def.Worker.DrawToggle).ToList()) def.Worker.OverlayUpdate();
        }

        public static void HandleShortcuts()
        {
            if (WorldRendererUtility.WorldRenderedNow || Current.ProgramState != ProgramState.Playing || !UIDefOf.VUIE_CycleOverlay.KeyDownEvent) return;
            var window = (MainTabWindow_Overlays) UIDefOf.VUIE_Overlays.TabWindow;
            if (window.CurrentOverlay == null) window.CurrentOverlay = DefDatabase<OverlayDef>.AllDefs.FirstOrDefault(d => d.Worker.DrawToggle);
            else
            {
                var list = DefDatabase<OverlayDef>.AllDefsListForReading;
                var index = list.IndexOf(window.CurrentOverlay) + 1;
                var flag = false;
                if (index >= list.Count) window.CurrentOverlay = null;
                else
                {
                    while (!list[index].Worker.DrawToggle)
                    {
                        index++;
                        if (index < list.Count) continue;

                        if (flag)
                        {
                            index = int.MaxValue;
                            break;
                        }

                        index = 0;
                        flag = true;
                    }

                    window.CurrentOverlay = index >= list.Count ? null : list[index];
                }
            }
        }

        public static IEnumerable<CodeInstruction> MaybeShowOverlays(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var list = instructions.ToList();
            var info1 = AccessTools.Field(typeof(PlaySettings), nameof(PlaySettings.showRoofOverlay));
            var idx1 = list.FindIndex(ins => ins.opcode == OpCodes.Ldflda && info1 == (FieldInfo) ins.operand) - 2;
            list.RemoveRange(idx1, 30);
            var label1 = generator.DefineLabel();
            list[idx1].labels.Add(label1);
            list.InsertRange(idx1, new[]
            {
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UIMod), nameof(UIMod.GetModule), generics: new[] {typeof(OverlayModule)})),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(OverlayModule), nameof(MoveOverlays))),
                new CodeInstruction(OpCodes.Brtrue, label1),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MainTabWindow_Overlays), nameof(MainTabWindow_Overlays.DoOverlayToggles)))
            });
            var info3 = AccessTools.Field(typeof(PlaySettings), nameof(PlaySettings.showBeauty));
            var idx3 = list.FindIndex(ins => ins.opcode == OpCodes.Ldflda && info3 == (FieldInfo) ins.operand) - 2;
            list.RemoveRange(idx3, 10);
            return list;
        }
    }

    public class OverlayDef : Def
    {
        public List<string> autoshowOn;
        [Unsaved] private Texture2D iconInt;

        public string iconPath;
        public Type workerClass;

        [Unsaved] private OverlayWorker workerInt;

        public Texture2D Icon
        {
            get => iconInt ??= iconPath.NullOrEmpty() ? TexButton.Add : ContentFinder<Texture2D>.Get(iconPath);
            set => iconInt = value;
        }

        public OverlayWorker Worker
        {
            get => workerInt ??= ((OverlayWorker) Activator.CreateInstance(workerClass ?? typeof(OverlayWorker))).Init(this);
            set => workerInt = value;
        }
    }
}