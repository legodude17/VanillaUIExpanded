using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    [StaticConstructorOnStartup]
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
        public static bool DoDesInit = true;
        public int ActiveIndex = -1;
        private string buffer;

        public GroupDisplayType GroupDisplay = GroupDisplayType.SquareGrid;
        public bool GroupOpenLeft;
        public int MaxSize = 4;
        public List<ArchitectSaved> SavedStates = new();
        public int VanillaIndex = -1;

        static ArchitectModule()
        {
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
                if (me.ActiveIndex == me.VanillaIndex) return;
                ArchitectLoadSaver.RestoreState(me.SavedStates[me.ActiveIndex]);
                DoDesInit = false;
            });
        }

        public override string Label => "VUIE.Architect".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            if (Current.ProgramState == ProgramState.Playing && listing.ButtonText("VUIE.Architect.Open".Translate())) Find.WindowStack.Add(new Dialog_ConfigureArchitect());
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
            listing.Gap(6f);
            listing.CheckboxLabeled("VUIE.Architect.LeftClick".Translate(), ref GroupOpenLeft, "VUIE.Architect.LeftClick.Desc".Translate());
            listing.GapLine();
            listing.Label("VUIE.Architect.Config".Translate() + ":");
            foreach (var state in SavedStates.ToList())
            {
                var rect = listing.GetRect(30f);
                var row = new WidgetRow(rect.x, rect.y, UIDirection.RightThenDown);
                row.Label(state.Name);
                row.Gap(50f);
                row.Label("VUIE.Vanilla".Translate() + ":");
                row.Icon(state.Vanilla ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
                row.Gap(12f);
                row.Label("VUIE.Active".Translate() + ":");
                row.Icon(SavedStates.IndexOf(state) == ActiveIndex ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
                row.Gap(12f);
                if (row.ButtonText("VUIE.SetActive".Translate()))
                {
                    ActiveIndex = SavedStates.IndexOf(state);
                    ArchitectLoadSaver.RestoreState(state);
                }

                row.Gap(3f);
                if (row.ButtonText("VUIE.Remove".Translate()))
                {
                    SavedStates.Remove(state);
                    if (ActiveIndex >= SavedStates.Count) ActiveIndex = SavedStates.Count - 1;
                }

                row.Gap(3f);
                if (row.ButtonText("VUIE.Export".Translate())) Find.WindowStack.Add(new Dialog_ArchitectList_Export(state));
            }

            listing.Gap(6f);
            var butRect = listing.GetRect(30f);
            if (Widgets.ButtonText(butRect.LeftHalf(), "VUIE.Architect.AddConfig".Translate()))
                Dialog_TextEntry.GetString(str => SavedStates.Add(ArchitectLoadSaver.SaveState(str)));
            if (Widgets.ButtonText(butRect.RightHalf(), "VUIE.Import".Translate())) Find.WindowStack.Add(new Dialog_ArchitectList_Import(state => SavedStates.Add(state)));

            listing.End();
        }

        public override void SaveSettings()
        {
            base.SaveSettings();
            Scribe_Values.Look(ref VanillaIndex, "vanilla", -1);
            Scribe_Values.Look(ref ActiveIndex, "active", -1);
            Scribe_Collections.Look(ref SavedStates, "states", LookMode.Deep);
            Scribe_Values.Look(ref GroupDisplay, "displayType");
            Scribe_Values.Look(ref GroupOpenLeft, "openOnLeft");
        }

        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(ArchitectCategoryTab), nameof(ArchitectCategoryTab.DesignationTabOnGUI)),
                new HarmonyMethod(typeof(ArchitectModule), nameof(UpdateCurrentTab)));
            harm.Patch(AccessTools.Method(typeof(ArchitectCategoryTab), "Matches"), new HarmonyMethod(typeof(ArchitectModule), nameof(CustomMatch)));
            harm.Patch(AccessTools.Method(typeof(ArchitectCategoryTab), "CacheSearchState"), postfix: new HarmonyMethod(typeof(ArchitectModule), nameof(FixUnique)));
            harm.Patch(AccessTools.Method(typeof(GizmoGridDrawer), nameof(GizmoGridDrawer.DrawGizmoGrid)),
                postfix: new HarmonyMethod(typeof(ArchitectModule), nameof(OverrideMouseOverGizmo)));
            harm.Patch(AccessTools.Method(typeof(DesignationCategoryDef), nameof(DesignationCategoryDef.ResolveDesignators)),
                new HarmonyMethod(typeof(ArchitectModule), nameof(SkipDesInit)), new HarmonyMethod(typeof(ArchitectModule), nameof(FixDesig)));
        }

        public static bool SkipDesInit() => DoDesInit;

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
                    me.SavedStates.Add(ArchitectLoadSaver.SaveState(def.presetName));
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