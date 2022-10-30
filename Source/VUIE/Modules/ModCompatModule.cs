﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MonoMod.Utils;
using RimWorld;
using UnityEngine;
using Verse;
using VFECore.UItils;

namespace VUIE
{
    public class ModCompatModule : Module
    {
        private const string TD_ID = "Uuugggg.rimworld.TD_Enhancement_Pack.main";

        private static string searchTerm = "";
        private static bool tdIntegration = true;
        private static bool oldTdIntegration = true;

        private static Action rebindContextMenus;


        private static AccessTools.FieldRef<object, List<ArchitectCategoryTab>> mintDesPanelsCached;

        public override string LabelKey => "VUIE.ModCompat";

        private const string FSM_ID = "kathanon.floatsubmenu";

        private static bool fsmActive = false;
        private static Type fsmMenuOption;
        private static Type fsmSubMenu;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("VUIE.ModCompat.TD".Translate(), ref tdIntegration, "VUIE.ModCompat.TD.Desc".Translate());
            listing.End();
        }

        public override void SaveSettings()
        {
            base.SaveSettings();
            Scribe_Values.Look(ref tdIntegration, "tdIntegration", true);
            if (Scribe.mode == LoadSaveMode.Saving && tdIntegration != oldTdIntegration) Utils.ConfirmAndRestart();

            oldTdIntegration = tdIntegration;
        }

        public override void DoPatches(Harmony harm)
        {
            if (ModLister.HasActiveModWithName("Blueprints"))
            {
                Log.Message("[VUIE] Blueprints detected, activating compatibility patch...");
                harm.Patch(AccessTools.Method(AccessTools.TypeByName("Blueprints.Designator_Blueprint"), "GroupsWith"),
                    finalizer: new HarmonyMethod(GetType(), nameof(GroupsWithFinalizer)));
            }

            if (ModLister.GetModWithIdentifier(FSM_ID).Active)
            {
                Log.Message("[VUIE] Float Sub-Menus detected, compatibility active");
                fsmActive = true;
                fsmMenuOption = AccessTools.TypeByName("FloatSubMenus.FloatSubMenu");
                fsmSubMenu = AccessTools.TypeByName("FloatSubMenus.FloatSubMenu+FloatSubMenuInner");
            }

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (Harmony.HasAnyPatches(TD_ID))
                {
                    if (tdIntegration)
                    {
                        var numUnpatched = 0;
                        Log.Message("[VUIE] TD Enhancement Pack detected, activating integration...");

                        var unpatch = new HashSet<MethodInfo>
                        {
                            AccessTools.Method(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls)),
                            AccessTools.Method(typeof(MapInterface), nameof(MapInterface.MapInterfaceUpdate)),
                            AccessTools.Method(typeof(ThingGrid), nameof(ThingGrid.Deregister))
                        };
                        var setDirty = AccessTools.Method(AccessTools.TypeByName("TD_Enhancement_Pack.BaseOverlay"), "SetDirty", new[] {typeof(Type)});
                        foreach (var method in from method in Harmony.GetAllPatchedMethods().ToList()
                            let info = Harmony.GetPatchInfo(method)
                            from patch in info.Postfixes
                            where patch.owner == TD_ID
                            where unpatch.Contains(method) ||
                                  PatchProcessor.GetCurrentInstructions(patch.PatchMethod).Any(ins => ins.opcode == OpCodes.Call && ins.OperandIs(setDirty))
                            select method)
                        {
                            harm.Unpatch(method, HarmonyPatchType.Postfix, TD_ID);
                            numUnpatched++;
                        }

                        Log.Message($"[VUIE] Unpatched <color=\"green\">{numUnpatched}</color> TD Enhancement Pack patches. This has disabled its the overlays system.");
                    }
                    else
                        Log.Message("[VUIE] TD Enhancement Pack Integration disabled.");
                }
            });

            if (ArchitectModule.IconsActive) Log.Message("[VUIE] Architect icons detected, activating compatibility patch...");

            if (ModLister.HasActiveModWithName("HugsLib"))
            {
                Log.Message("[VUIE] HugsLib detected, activating compatibility patch...");
                harm.Patch(AccessTools.Method(AccessTools.TypeByName("HugsLib.HugsLibController"), "LoadReloadInitialize"),
                    postfix: new HarmonyMethod(GetType(), nameof(HugsLibInit)));
                UIMod.DoLateInit = false;
                harm.Patch(AccessTools.Method(AccessTools.TypeByName("HugsLib.Settings.Dialog_ModSettings"), "DoWindowContents"),
                    transpiler: new HarmonyMethod(GetType(), nameof(AddSearchBoxToModSettings)));
            }

            var type = AccessTools.TypeByName("DubsMintMenus.MainTabWindow_MintArchitect");
            if (type is not null)
            {
                mintDesPanelsCached = AccessTools.FieldRefAccess<List<ArchitectCategoryTab>>(type, "desPanelsCached");
                Log.Message("[VUIE] Dubs Mint Menus detected, activating compatibility patch...");
            }

            type = AccessTools.TypeByName("AllowTool.Context.DesignatorContextMenuController");
            if (type is not null)
            {
                rebindContextMenus = AccessTools.Method(type, "RebindAllContextMenus").CreateDelegate<Action>();
                Log.Message("[VUIE] Allow Tool detected, activating compatibility patch...");
            }
        }

        public static void HugsLibInit()
        {
            UIMod.Instance.LateInit();
        }

        public static bool IsSubMenu(FloatMenu menu) => 
            fsmActive && menu.GetType() == fsmSubMenu;

        public static bool IsSubMenuOption(FloatMenuOption opt) => 
            fsmActive && opt.GetType() == fsmMenuOption;

        public static List<FloatMenuOption> SubMenuOptions(FloatMenuOption opt) =>
            IsSubMenuOption(opt) ? Traverse.Create(opt).Property<List<FloatMenuOption>>("Options").Value : null;

        public static Exception GroupsWithFinalizer(Exception __exception, ref bool __result)
        {
            if (__exception is not NullReferenceException) return __exception;
            __result = false;
            return null;
        }

        public static void Notify_ArchitectChanged()
        {
            if (mintDesPanelsCached is not null)
                mintDesPanelsCached.Invoke(DefDatabase<MainButtonDef>.GetNamedSilentFail("MintMenus").TabWindow) = null;
            rebindContextMenus?.Invoke();
        }

        public static IEnumerable<CodeInstruction> AddSearchBoxToModSettings(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            var idx1 = list.FindIndex(ins => ins.opcode == OpCodes.Ldc_I4_1);
            list.InsertRange(idx1, new[]
            {
                new CodeInstruction(OpCodes.Ldloca, 2),
                CodeInstruction.Call(typeof(ModCompatModule), nameof(DoSearchBox))
            });
            var idx2 = list.FindIndex(ins => ins.opcode == OpCodes.Ldloc_S && ins.operand is LocalBuilder {LocalIndex: 12});
            list.InsertRange(idx2, new[]
            {
                new CodeInstruction(OpCodes.Ldloca, 12),
                new CodeInstruction(OpCodes.Ldloc, 11),
                CodeInstruction.LoadField(AccessTools.Inner(AccessTools.TypeByName("HugsLib.Settings.Dialog_ModSettings"), "ModEntry"), "ModName"),
                CodeInstruction.Call(typeof(ModCompatModule), nameof(DoSearch))
            });
            return list;
        }

        public static void DoSearchBox(ref Rect inRect)
        {
            var rect = new Rect(0f, inRect.yMax, inRect.width, 40f).ContractedBy(5f);
            inRect.height += 50f;
            Widgets.Label(rect.TakeLeftPart(100f), "VUIE.Search".Translate());
            searchTerm = Widgets.TextField(rect, searchTerm);
        }

        public static void DoSearch(ref bool flag, string name)
        {
            if (flag || searchTerm.NullOrEmpty()) return;
            flag = !name.ToLower().Contains(searchTerm.ToLower());
        }
    }
}