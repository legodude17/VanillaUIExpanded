using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class ArchitectModule : Module
    {
        public enum GroupDisplayType
        {
            SquareGrid,
            ExpandGrid,
            Vanilla
        }

        public static ArchitectCategoryTab CurrentTab;

        public static Gizmo overrideMouseOverGizmo;
        private static readonly FastInvokeHandler architectIcons;
        private static readonly AccessTools.FieldRef<object, Dictionary<string, Texture2D>> iconsCache;

        private static Dictionary<string, string> iconChanges = new();
        private static readonly Func<string, Texture2D> loadIcon;
        private static readonly AccessTools.FieldRef<object, List<ArchitectCategoryTab>> mintDesPanelsCached;

        private static readonly Dictionary<string, string> truncationCache = new();
        public int ActiveIndex = -1;
        private string buffer;

        public GroupDisplayType GroupDisplay = GroupDisplayType.SquareGrid;
        public bool GroupOpenLeft;
        public int MaxSize = 4;
        public List<ArchitectSaved> SavedStates = new();
        public int VanillaIndex = -1;

        static ArchitectModule()
        {
            var type = AccessTools.TypeByName("ArchitectIcons.ArchitectIconsMod");
            MethodInfo method = null;
            if (type is not null) method = AccessTools.Method(type, "DoArchitectButton");
            if (method is not null) architectIcons = MethodInvoker.GetHandler(method);
            type = AccessTools.TypeByName("ArchitectIcons.Resources");
            if (type is not null) iconsCache = AccessTools.FieldRefAccess<Dictionary<string, Texture2D>>(type, "iconsCache");
            if (type is not null) method = AccessTools.Method(type, "FindArchitectTabCategoryIcon");
            if (method is not null) loadIcon = AccessTools.MethodDelegate<Func<string, Texture2D>>(method);
            type = AccessTools.TypeByName("DubsMintMenus.MainTabWindow_MintArchitect");
            if (type is not null) mintDesPanelsCached = AccessTools.FieldRefAccess<List<ArchitectCategoryTab>>(type, "desPanelsCached");
        }

        public static bool MintCompat => mintDesPanelsCached is not null;

        public static bool IconsActive => architectIcons is not null;

        public static float ArchitectWidth => 100f + (architectIcons is not null ? 16f : 0f);
        public override string LabelKey => "VUIE.Architect";

        public static void MintRefresh() => mintDesPanelsCached.Invoke(DefDatabase<MainButtonDef>.GetNamedSilentFail("MintMenus").TabWindow) = null;

        public static void SetIcon(string defName, string path)
        {
            iconChanges[defName] = path;
            UIMod.Settings.Write();
            iconsCache()[defName] = loadIcon(path);
        }

        public static bool DoArchitectButton(Rect rect, string label, float barPercent = 0f, float textLeftMargin = -1f, SoundDef mouseoverSound = null,
            Vector2 functionalSizeOffset = default, Color? labelColor = null, bool highlight = false)
        {
            if (architectIcons is null)
                return Widgets.ButtonTextSubtle(rect, label, barPercent, textLeftMargin, mouseoverSound, functionalSizeOffset, labelColor, highlight);
            return (bool) architectIcons.Invoke(null, rect, label, barPercent, textLeftMargin, mouseoverSound, functionalSizeOffset, labelColor, highlight);
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            if (Current.ProgramState != ProgramState.Playing) listing.Label("VUIE.Architect.Disabled.NotPlaying".Translate());
            else if (ActiveIndex == VanillaIndex) listing.Label("VUIE.Architect.Disabled.Vanilla".Translate());
            else if (listing.ButtonText("VUIE.Architect.Open".Translate())) Find.WindowStack.Add(new Dialog_ConfigureArchitect());
            listing.GapLine();
            listing.Label("VUIE.Architect.GroupDisplay".Translate() + ":");
            listing.Indent();
            listing.ColumnWidth -= 12f;
            if (listing.RadioButton("VUIE.Architect.GroupDisplay.SquareGrid".Translate(), GroupDisplay == GroupDisplayType.SquareGrid, 0f,
                "VUIE.Architect.GroupDisplay.SquareGrid.Desc".Translate()))
                GroupDisplay = GroupDisplayType.SquareGrid;
            if (listing.RadioButton("VUIE.Architect.GroupDisplay.ExpandGrid".Translate(), GroupDisplay == GroupDisplayType.ExpandGrid, 0f,
                "VUIE.Architect.GroupDisplay.ExpandGrid.Desc".Translate()))
                GroupDisplay = GroupDisplayType.ExpandGrid;
            if (listing.RadioButton("VUIE.Vanilla".Translate(), GroupDisplay == GroupDisplayType.Vanilla, 0f, "VUIE.Architect.GroupDisplay.Vanilla.Desc".Translate()))
                GroupDisplay = GroupDisplayType.Vanilla;
            listing.Gap(6f);
            switch (GroupDisplay)
            {
                case GroupDisplayType.SquareGrid:
                    listing.TextFieldNumericLabeled("VUIE.Architect.MaxGridDepth".Translate(), ref MaxSize, ref buffer, 2, 4);
                    break;
                case GroupDisplayType.ExpandGrid:
                    listing.TextFieldNumericLabeled("VUIE.Architect.MaxGridWidth".Translate(), ref MaxSize, ref buffer, 1, 10);
                    break;
                case GroupDisplayType.Vanilla:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            listing.ColumnWidth += 12f;
            listing.Outdent();
            if (Current.ProgramState == ProgramState.Playing)
            {
                listing.Label("VUIE.Architect.Example".Translate());
                listing.Gap(GizmoGridDrawer.GizmoSpacing.y);
                var gizRect = listing.GetRect(Gizmo.Height);
                GizmoDrawer.DrawGizmos(Gen.YieldSingle(new Designator_Group(
                    Dialog_ConfigureArchitect.ArchitectCategoryTabs.SelectMany(tab => tab.def.AllResolvedDesignators).Take(20).ToList(),
                    "VUIE.Architect.Example".Translate())), gizRect, false, (gizmo, vector2) => true);
                listing.Gap(GizmoGridDrawer.GizmoSpacing.y);
            }

            listing.GapLine(6f);
            listing.CheckboxLabeled("VUIE.Architect.LeftClick".Translate(), ref GroupOpenLeft, "VUIE.Architect.LeftClick.Desc".Translate());
            listing.GapLine();
            listing.Label("VUIE.Architect.Config".Translate() + ":");
            foreach (var state in SavedStates.ToList())
            {
                var rect = listing.GetRect(30f);
                var row = new WidgetRow(rect.x, rect.y, UIDirection.RightThenDown);
                var text = state.Name.Truncate(80f, truncationCache);
                row.Label(text);
                row.Gap(100f - Text.CalcSize(text).x);
                row.Label("VUIE.Vanilla".Translate() + ":");
                row.Icon(state.Vanilla ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
                row.Gap(12f);
                row.Label("VUIE.Active".Translate() + ":");
                row.Icon(SavedStates.IndexOf(state) == ActiveIndex ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
                row.Gap(12f);
                if (row.ButtonText("VUIE.SetActive".Translate())) SetActive(state);

                row.Gap(3f);
                if (row.ButtonText("VUIE.Remove".Translate()))
                {
                    if (state.Vanilla) Messages.Message("VUIE.Architect.Disabled.RemoveVanilla".Translate(), MessageTypeDefOf.RejectInput);
                    else
                    {
                        SavedStates.Remove(state);
                        if (ActiveIndex >= SavedStates.Count) SetActive(SavedStates.Count - 1);
                    }
                }

                row.Gap(3f);
                if (row.ButtonText("VUIE.Export".Translate())) Find.WindowStack.Add(new Dialog_ArchitectList_Export(state));
            }

            listing.Gap(6f);
            var butRect = listing.GetRect(30f);
            if (Widgets.ButtonText(butRect.LeftHalf(), "VUIE.Architect.AddConfig".Translate()))
                Dialog_TextEntry.GetString(str => AddState(ArchitectLoadSaver.SaveState(str)));
            if (Widgets.ButtonText(butRect.RightHalf(), "VUIE.Import".Translate())) Find.WindowStack.Add(new Dialog_ArchitectList_Import(state => SavedStates.Add(state)));

            listing.End();
        }

        public void SetActive(ArchitectSaved state)
        {
            ActiveIndex = SavedStates.IndexOf(state);
            ArchitectLoadSaver.RestoreState(state);
        }

        public void SetActive(int index)
        {
            ActiveIndex = index;
            ArchitectLoadSaver.RestoreState(SavedStates[index]);
        }

        public void AddState(ArchitectSaved state)
        {
            var i = 1;
            while (SavedStates.Any(s => s.Name == state.Name)) state.Name = $"Untitled {i++}";
            SavedStates.Add(state);
        }

        public override void SaveSettings()
        {
            base.SaveSettings();
            Scribe_Values.Look(ref VanillaIndex, "vanilla", -1);
            Scribe_Values.Look(ref ActiveIndex, "active", -1);
            Scribe_Collections.Look(ref SavedStates, "states", LookMode.Deep);
            Scribe_Values.Look(ref GroupDisplay, "displayType");
            Scribe_Values.Look(ref GroupOpenLeft, "openOnLeft");
            Scribe_Collections.Look(ref iconChanges, "architectIconChanges", LookMode.Value, LookMode.Value);
            iconChanges ??= new Dictionary<string, string>();
            if (Scribe.mode == LoadSaveMode.LoadingVars && iconsCache is not null)
                foreach (var iconChange in iconChanges)
                    iconsCache()[iconChange.Key] = loadIcon(iconChange.Value);
        }

        public static Texture2D LoadIcon(string path) => loadIcon(path);

        public static IEnumerable<string> AllPossibleIcons()
        {
            yield return "wrongsign";

            var directoryInfo = new DirectoryInfo(Path.Combine(GenFilePaths.SaveDataFolderPath, "ArchitectIcons"));
            if (!directoryInfo.Exists) directoryInfo.Create();
            foreach (var file in directoryInfo.EnumerateFiles()) yield return file.Name;

            foreach (var mod in LoadedModManager.RunningMods)
            foreach (var tex in mod.textures.contentList.Keys.Where(key => key.StartsWith("UI/ArchitectIcons/")).Select(key => key.Replace("UI/ArchitectIcons/", "")))
                yield return tex;
        }

        public override void LateInit()
        {
            base.LateInit();
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                foreach (var tab in ArchitectLoadSaver.Architect.desPanelsCached) tab.def.ResolveDesignators();
                var vanilla = ArchitectLoadSaver.SaveState("VUIE.Vanilla".Translate(), true);
                var me = UIMod.GetModule<ArchitectModule>();
                me.SavedStates ??= new List<ArchitectSaved>();
                if (me.VanillaIndex >= 0)
                    me.SavedStates[me.VanillaIndex] = vanilla;
                else
                {
                    me.VanillaIndex = me.SavedStates.Count;
                    me.SavedStates.Add(vanilla);
                }

                if (me.ActiveIndex < 0) me.ActiveIndex = me.VanillaIndex;
                if (me.ActiveIndex != me.VanillaIndex) ArchitectLoadSaver.EnsureCached(me.SavedStates[me.ActiveIndex]);
            });
        }

        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(ArchitectCategoryTab), nameof(ArchitectCategoryTab.DesignationTabOnGUI)),
                new HarmonyMethod(typeof(ArchitectModule), nameof(UpdateCurrentTab)));
            harm.Patch(AccessTools.Method(typeof(ArchitectCategoryTab), "Matches"), new HarmonyMethod(typeof(ArchitectModule), nameof(CustomMatch)));
            harm.Patch(AccessTools.Method(typeof(ArchitectCategoryTab), "CacheSearchState"), postfix: new HarmonyMethod(typeof(ArchitectModule), nameof(FixUnique)));
            harm.Patch(AccessTools.Method(typeof(GizmoGridDrawer), nameof(GizmoGridDrawer.DrawGizmoGrid)),
                postfix: new HarmonyMethod(typeof(ArchitectModule), nameof(OverrideMouseOverGizmo)));
            harm.Patch(AccessTools.Method(typeof(MainTabWindow_Architect), nameof(MainTabWindow_Architect.CacheDesPanels)),
                postfix: new HarmonyMethod(typeof(ArchitectModule), nameof(FixDesPanels)));
            harm.Patch(AccessTools.Method(typeof(BuildCopyCommandUtility), nameof(BuildCopyCommandUtility.FindAllowedDesignatorRecursive)),
                postfix: new HarmonyMethod(typeof(ArchitectModule), nameof(FindAllowedDesignatorInGroup)));
            harm.Patch(AccessTools.Method(typeof(MapInterface), nameof(MapInterface.Notify_SwitchedMap)),
                postfix: new HarmonyMethod(typeof(ArchitectModule), nameof(PostMapChanged)));
        }

        public static void PostMapChanged()
        {
            var me = UIMod.GetModule<ArchitectModule>();
            ArchitectLoadSaver.EnsureCached(me.SavedStates[me.ActiveIndex]);
        }

        public static void FindAllowedDesignatorInGroup(Designator designator, BuildableDef buildable, bool mustBeVisible, ref Designator_Build __result)
        {
            if (__result != null || designator is not Designator_Group {Elements: var elements}) return;
            foreach (var element in elements)
                if (BuildCopyCommandUtility.FindAllowedDesignatorRecursive(element, buildable, mustBeVisible) is { } found)
                {
                    __result = found;
                    return;
                }
        }

        public static void FixDesPanels(MainTabWindow_Architect __instance)
        {
            var me = UIMod.GetModule<ArchitectModule>();
            if (me.ActiveIndex != me.VanillaIndex) ArchitectLoadSaver.RestoreState(me.SavedStates[me.ActiveIndex], __instance);
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
            var me = UIMod.GetModule<ArchitectModule>();
            foreach (var def in DefDatabase<BuildableGroupDef>.AllDefs)
            {
                if (def.presetName.NullOrEmpty())
                {
                    def.RemoveChildren(__instance);
                    continue;
                }

                var index = me.SavedStates.FindIndex(state => state.Name == def.presetName);
                if (index == -1)
                {
                    index = me.SavedStates.Count;
                    me.AddState(ArchitectLoadSaver.SaveState(def.presetName));
                }

                ArchitectLoadSaver.RestoreState(me.SavedStates[index]);
                def.RemoveChildren(__instance);
                me.SavedStates[index] = ArchitectLoadSaver.SaveState(def.presetName);
            }
        }
    }

    public interface ICustomCommandMatch
    {
        bool Matches(QuickSearchFilter filter);
        Command UniqueSearchMatch(QuickSearchFilter filter);
    }
}