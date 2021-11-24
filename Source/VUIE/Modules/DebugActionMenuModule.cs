using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class DebugActionMenuModule : Module
    {
        private static readonly List<string> debugPath = new();
        private static readonly Queue<string> autoSelect = new();
        public static List<List<string>> Favorites = new();
        private static readonly HashSet<string> noStar = new();

        public static bool DoFavorites = true;
        public static bool HideNotMatch;

        private static Dialog_DebugActionsMenu curInstance;
        public override string LabelKey => "VUIE.Debug.ActionMenu";


        public override void DoPatches(Harmony harm)
        {
            if (DoFavorites)
            {
                harm.Patch(AccessTools.Constructor(typeof(Dialog_DebugActionsMenu)), postfix: new HarmonyMethod(GetType(), nameof(PostConstructed)));
                harm.Patch(AccessTools.Method(typeof(Listing_Standard), nameof(Listing_Standard.ButtonDebug)), new HarmonyMethod(GetType(), nameof(PreButtonDebug)),
                    new HarmonyMethod(GetType(), nameof(PostButtonDebug)), new HarmonyMethod(GetType(), nameof(TranspileButtonDebug)));
            }

            if (HideNotMatch)
            {
                harm.Patch(AccessTools.Method(typeof(Dialog_DebugOptionLister), nameof(Dialog_DebugOptionLister.DebugAction)),
                    new HarmonyMethod(GetType(), nameof(CheckFilter)));
                harm.Patch(AccessTools.Method(typeof(Dialog_DebugOptionLister), nameof(Dialog_DebugOptionLister.DebugToolMap)),
                    new HarmonyMethod(GetType(), nameof(CheckFilter)));
                harm.Patch(AccessTools.Method(typeof(Dialog_DebugOptionLister), nameof(Dialog_DebugOptionLister.DebugToolWorld)),
                    new HarmonyMethod(GetType(), nameof(CheckFilter)));
                harm.Patch(AccessTools.Method(typeof(Dialog_DebugOptionLister), nameof(Dialog_DebugOptionLister.DebugToolMapForPawns)),
                    new HarmonyMethod(GetType(), nameof(CheckFilter)));
            }
        }

        private void UnDoPatches(Harmony harm)
        {
            harm.Unpatch(AccessTools.Constructor(typeof(Dialog_DebugActionsMenu)), HarmonyPatchType.Postfix, harm.Id);
            harm.Unpatch(AccessTools.Method(typeof(Listing_Standard), nameof(Listing_Standard.ButtonDebug)), HarmonyPatchType.All, harm.Id);
            harm.Unpatch(AccessTools.Method(typeof(Dialog_DebugOptionLister), nameof(Dialog_DebugOptionLister.DebugAction)), HarmonyPatchType.Prefix, harm.Id);
            harm.Unpatch(AccessTools.Method(typeof(Dialog_DebugOptionLister), nameof(Dialog_DebugOptionLister.DebugToolMap)), HarmonyPatchType.Prefix, harm.Id);
            harm.Unpatch(AccessTools.Method(typeof(Dialog_DebugOptionLister), nameof(Dialog_DebugOptionLister.DebugToolWorld)), HarmonyPatchType.Prefix, harm.Id);
            harm.Unpatch(AccessTools.Method(typeof(Dialog_DebugOptionLister), nameof(Dialog_DebugOptionLister.DebugToolMapForPawns)), HarmonyPatchType.Prefix, harm.Id);
        }

        public static void PostConstructed(Dialog_DebugActionsMenu __instance)
        {
            debugPath.Clear();
            __instance.debugActions.InsertRange(0, Favorites.Select(MakeFavoriteOption));
            curInstance = __instance;
        }

        private static Dialog_DebugActionsMenu.DebugActionOption MakeFavoriteOption(List<string> fave) => new()
        {
            label = fave.Join(delimiter: " "),
            category = "Favorites",
            actionType = DebugActionType.Action,
            action = () =>
            {
                autoSelect.Clear();
                foreach (var v in fave) autoSelect.Enqueue(v);
            }
        };

        public static bool PreButtonDebug(string label, ref bool __result)
        {
            if (autoSelect.Count == 0 || label != autoSelect.Peek()) return true;
            autoSelect.Dequeue();
            __result = true;
            return false;
        }

        public static void PostButtonDebug(string label, ref bool __result, Listing_Standard __instance)
        {
            var rect = new Rect(__instance.curX, __instance.curY - 22f - __instance.verticalSpacing, 22f, 22f);
            if (__result) debugPath.Add(label);
            if (noStar.Contains(label))
            {
                TooltipHandler.TipRegionByKey(rect, "VUIE.UnFavorite");
                if (!Widgets.ButtonText(rect, "☆")) return;
                Favorites.RemoveAll(fave => fave.Join(delimiter: " ") == label);
                curInstance?.debugActions.RemoveAll(action => action.label == label);
                UIMod.Settings.Write();
            }
            else
            {
                TooltipHandler.TipRegionByKey(rect, "VUIE.Favorite");
                if (!Widgets.ButtonText(rect, "★")) return;
                Favorites.Add(debugPath.Append(label).ToList());
                curInstance?.debugActions.Insert((int) curInstance?.debugActions.FindLastIndex(action => action.category == "Favorites") + 1,
                    MakeFavoriteOption(Favorites[Favorites.Count - 1]));
                UIMod.Settings.Write();
            }
        }

        public static IEnumerable<CodeInstruction> TranspileButtonDebug(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var info1 = AccessTools.Field(typeof(Listing), nameof(Listing.curX));
            var info2 = AccessTools.PropertyGetter(typeof(Listing), nameof(Listing.ColumnWidth));
            var label1 = generator.DefineLabel();
            var label2 = generator.DefineLabel();
            var info3 = AccessTools.Field(typeof(DebugActionMenuModule), nameof(DoFavorites));
            foreach (var instruction in instructions)
            {
                yield return instruction;

                if (instruction.LoadsField(info1))
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, info3);
                    yield return new CodeInstruction(OpCodes.Brfalse, label1);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 22f);
                    yield return new CodeInstruction(OpCodes.Add);
                    yield return new CodeInstruction(OpCodes.Nop).WithLabels(label1);
                }

                if (instruction.Calls(info2))
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, info3);
                    yield return new CodeInstruction(OpCodes.Brfalse, label2);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 22f);
                    yield return new CodeInstruction(OpCodes.Sub);
                    yield return new CodeInstruction(OpCodes.Nop).WithLabels(label2);
                }
            }
        }

        public static bool CheckFilter(string label, Dialog_DebugOptionLister __instance) => !HideNotMatch || __instance.FilterAllows(label);

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("VUIE.Debug.Favorites".Translate(), ref DoFavorites);
            listing.CheckboxLabeled("VUIE.Debug.HideNotMatch".Translate(), ref HideNotMatch, "VUIE.Debug.HideNotMatch.Desc".Translate());
            listing.End();
        }

        public override void SaveSettings()
        {
            base.SaveSettings();

            Scribe_Values.Look(ref DoFavorites, "debugFavoritesActive", true);
            Scribe_Values.Look(ref HideNotMatch, "hideNotMatch");

            var faves = Favorites.Select(fave => new Fave {path = fave}).ToList();
            Scribe_Collections.Look(ref faves, "debugFavorites", LookMode.Deep);
            Favorites = faves is null ? new List<List<string>>() : faves.Select(fave => fave.path).ToList();

            noStar.Clear();
            foreach (var favorite in Favorites) noStar.Add(favorite.Join(delimiter: " "));

            UnDoPatches(UIMod.Harm);
            DoPatches(UIMod.Harm);
        }

        private struct Fave : IExposable
        {
            public List<string> path;

            public void ExposeData()
            {
                Scribe_Collections.Look(ref path, "path", LookMode.Value);
            }
        }
    }
}