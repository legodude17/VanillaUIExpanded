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
        public static ArchitectCategoryTab CurrentTab;

        public static Gizmo overrideMouseOverGizmo;
        public static bool DoDesInit = true;
        public int ActiveIndex = -1;
        public List<ArchitectSaved> SavedStates = new List<ArchitectSaved>();
        public int VanillaIndex = -1;

        static ArchitectModule()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                foreach (var tab in ArchitectLoadSaver.Architect.desPanelsCached) tab.def.ResolveDesignators();
                var vanilla = ArchitectLoadSaver.SaveState("Vanilla", true);
                var me = UIMod.GetModule<ArchitectModule>();
                if (me.SavedStates == null) me.SavedStates = new List<ArchitectSaved>();
                if (me.VanillaIndex >= 0)
                {
                    me.SavedStates[me.VanillaIndex] = vanilla;
                }
                else
                {
                    me.VanillaIndex = me.SavedStates.Count;
                    me.SavedStates.Add(vanilla);
                }

                if (me.ActiveIndex < 0) me.ActiveIndex = me.VanillaIndex;
                if (me.ActiveIndex != me.VanillaIndex)
                {
                    ArchitectLoadSaver.RestoreState(me.SavedStates[me.ActiveIndex]);
                    DoDesInit = false;
                }
            });
        }

        public override string Label => "Architect Menu";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            if (listing.ButtonText("Open Architect Configuration Dialog")) Find.WindowStack.Add(new Dialog_ConfigureArchitect());
            listing.GapLine();
            listing.Label("Architect Configurations:");
            foreach (var state in SavedStates.ToList())
            {
                var rect = listing.GetRect(30f);
                var row = new WidgetRow(rect.x, rect.y, UIDirection.RightThenDown);
                row.Label(state.Name);
                row.Gap(3f);
                row.Label("Is Vanilla:");
                row.Icon(state.Vanilla ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
                row.Gap(12f);
                if (row.ButtonText("Set Active"))
                {
                    ActiveIndex = SavedStates.IndexOf(state);
                    ArchitectLoadSaver.RestoreState(state);
                }

                row.Gap(3f);
                if (row.ButtonText("Remove")) SavedStates.Remove(state);
            }

            listing.Gap(6f);
            if (listing.ButtonText("Add Configuration")) Dialog_TextEntry.GetString(str => SavedStates.Add(ArchitectLoadSaver.SaveState(str)));

            listing.End();
        }

        public override void SaveSettings()
        {
            base.SaveSettings();
            Scribe_Values.Look(ref VanillaIndex, "vanilla", -1);
            Scribe_Values.Look(ref ActiveIndex, "active", -1);
            Scribe_Collections.Look(ref SavedStates, "states", LookMode.Deep);
        }

        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(DesignationCategoryDef), "ResolveDesignators"), postfix: new HarmonyMethod(typeof(ArchitectModule), nameof(FixDesig)));
            harm.Patch(AccessTools.Method(typeof(ArchitectCategoryTab), nameof(ArchitectCategoryTab.DesignationTabOnGUI)),
                new HarmonyMethod(typeof(ArchitectModule), nameof(UpdateCurrentTab)));
            harm.Patch(AccessTools.Method(typeof(ArchitectCategoryTab), "Matches"), new HarmonyMethod(typeof(ArchitectModule), nameof(CustomMatch)));
            harm.Patch(AccessTools.Method(typeof(ArchitectCategoryTab), "CacheSearchState"), postfix: new HarmonyMethod(typeof(ArchitectModule), nameof(FixUnique)));
            harm.Patch(AccessTools.Method(typeof(GizmoGridDrawer), nameof(GizmoGridDrawer.DrawGizmoGrid)),
                postfix: new HarmonyMethod(typeof(ArchitectModule), nameof(OverrideMouseOverGizmo)));
            harm.Patch(AccessTools.Method(typeof(DesignationCategoryDef), nameof(DesignationCategoryDef.ResolveDesignators)),
                new HarmonyMethod(typeof(ArchitectModule), nameof(SkipDesInit)));
        }

        public static bool SkipDesInit()
        {
            return DoDesInit;
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
            foreach (var def in DefDatabase<BuildableGroupDef>.AllDefs) def.RemoveChildren(__instance);
            // var def = new BuildableGroupDef
            // {
            //     defName = __instance.defName,
            //     label = __instance.label,
            //     defs = __instance.AllResolvedDesignators.OfType<Designator_Build>().Select(d => d.entDef).ToList(),
            //     category = __instance
            // };
            // def.RemoveChildren(__instance);
        }
    }

    public class BuildableGroupDef : Def
    {
        public DesignationCategoryDef category;
        public int columns;
        public List<BuildableDef> defs;
        private Designator_Group designatorGroup;
        public int rows;
        public bool scroll;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var er in base.ConfigErrors()) yield return er;

            if (defs.NullOrEmpty()) yield return "Must provide defs";
            if (category == null) yield return "Must provide category";
            if (!scroll && rows > 0) yield return "Only provide rows when scrollable";
        }

        public void RemoveChildren(DesignationCategoryDef def)
        {
            var inGroup = new List<Designator>();
            foreach (var designator in def.AllResolvedDesignators.ToList())
                switch (designator)
                {
                    case Designator_Build build when defs.Contains(build.entDef) || defName == "Everything":
                    {
                        inGroup.Add(designator);
                        def.AllResolvedDesignators.Remove(designator);
                        break;
                    }
                    case Designator_Dropdown dropdown:
                    {
                        foreach (var element in dropdown.Elements.OfType<Designator_Build>().Where(build2 => defs.Contains(build2.entDef) || defName == "Everything").ToList())
                        {
                            inGroup.Add(element);
                            dropdown.Elements.Remove(element);
                        }

                        if (dropdown.Elements.Count == 0) def.AllResolvedDesignators.Remove(designator);


                        break;
                    }
                }

            if (!inGroup.Any()) return;

            if (designatorGroup == null)
            {
                designatorGroup = new Designator_Group(inGroup, label)
                {
                    Columns = columns
                };
                category.resolvedDesignators.Add(designatorGroup);
            }
            else
            {
                designatorGroup.Elements.AddRange(inGroup);
            }
        }
    }

    public interface ICustomCommandMatch
    {
        bool Matches(QuickSearchFilter filter);
        Command UniqueSearchMatch(QuickSearchFilter filter);
    }
}