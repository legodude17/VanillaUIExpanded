using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VUIE
{
    public static class ArchitectLoadSaver
    {
        private static MainTabWindow_Architect architect;

        private static readonly Dictionary<ArchitectSaved, List<List<Designator>>> CACHE = new();
        public static MainTabWindow_Architect Architect => architect ?? (MainTabWindow_Architect) MainButtonDefOf.Architect.TabWindow;

        public static ArchitectSaved SaveState(string name, bool vanilla = false) => new()
        {
            Name = name,
            Vanilla = vanilla,
            Tabs = Architect.desPanelsCached.Select(ArchitectTabSaved.Save).ToList()
        };

        public static bool EnsureCached(ArchitectSaved saved)
        {
            Log.Message($"[VUIE] Checking cache for {saved.Name}");
            if (CACHE.ContainsKey(saved)) return true;

            if (!UnityData.IsInMainThread)
            {
                Log.Warning("[VUIE] Attempted to generate designators while not in the main thread!");
                return false;
            }

            Log.Message($"[VUIE] Generating designators for: {saved.Name}");

            CACHE.Add(saved, saved.Tabs.Select(tab => tab.Designators.Select(DesignatorSaved.Load).Where(d => d is not null).ToList()).ToList());
            return true;
        }

        public static void RestoreState(ArchitectSaved saved, MainTabWindow_Architect instance = null)
        {
            Log.Message("[VUIE] Restoring saved architect state: " + saved.Name);
            architect = instance;
            if (!EnsureCached(saved)) return;
            Architect.desPanelsCached.Clear();
            foreach (var (tab, contents) in saved.Tabs.Zip(CACHE[saved], (tabSaved, list) => (tabSaved, list)))
            {
                var def = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(tab.defName);
                if (def is null)
                {
                    def = new DesignationCategoryDef
                    {
                        defName = tab.defName,
                        label = tab.label
                    };
                    DefGenerator.AddImpliedDef(def);
                }

                if (tab.Designators != null)
                {
                    def.resolvedDesignators ??= new List<Designator>();
                    def.AllResolvedDesignators.Clear();
                    def.AllResolvedDesignators.AddRange(contents);
                }

                var desTab = new ArchitectCategoryTab(def, Architect.quickSearchWidget.filter);
                Architect.desPanelsCached.Add(desTab);
            }

            architect = null;

            if (ArchitectModule.MintCompat) ArchitectModule.MintRefresh();
        }
    }

    public struct ArchitectSaved : IExposable
    {
        public string Name;
        public bool Vanilla;
        public List<ArchitectTabSaved> Tabs;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Name, "name");
            Scribe_Values.Look(ref Vanilla, "vanilla");
            Scribe_Collections.Look(ref Tabs, "tabs", LookMode.Deep);
        }
    }

    public struct ArchitectTabSaved : IExposable
    {
        // ReSharper disable InconsistentNaming

        public string label;
        public string defName;
        public string description;

        // ReSharper enable InconsistentNaming
        public List<DesignatorSaved> Designators;

        public void ExposeData()
        {
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Collections.Look(ref Designators, "designators", LookMode.Deep);
        }

        public static ArchitectTabSaved Save(ArchitectCategoryTab tab) => new()
        {
            label = tab.def.label,
            defName = tab.def.defName,
            description = tab.def.description,
            Designators = tab.def.AllResolvedDesignators.Select(DesignatorSaved.Save).ToList()
        };
    }

    public struct DesignatorSaved : IExposable
    {
        public string Type;
        public string AdditionalData;
        public string Name;
        public List<DesignatorSaved> Elements;
        public float Order;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Type, "type");
            Scribe_Values.Look(ref Name, "name");
            Scribe_Values.Look(ref AdditionalData, "data");
            Scribe_Values.Look(ref Order, "order");
            Scribe_Collections.Look(ref Elements, "elements", LookMode.Deep);
        }

        public static Designator Load(DesignatorSaved saved)
        {
            Designator des;
            var type = AccessTools.TypeByName(saved.Type);
            if (type is null) return null;
            if (Dialog_ConfigureArchitect.SpecialHandling.ContainsKey(type))
                des = Dialog_ConfigureArchitect.SpecialHandling[type].Load(saved.AdditionalData, type);
            else
                switch (saved.Type)
                {
                    case "VUIE.Designator_Group":
                        des = new Designator_Group(saved.Elements.Select(Load).Where(d => d is not null).ToList(), saved.Name);
                        if (!((Designator_Group) des).Elements.Any()) return null;
                        break;
                    case "RimWorld.Designator_Dropdown":
                        var dropdown = new Designator_Dropdown();
                        foreach (var designator in saved.Elements.Select(Load).Where(d => d is not null)) dropdown.Add(designator);
                        if (dropdown.activeDesignator is null) return null;
                        des = dropdown;
                        break;
                    default:
                        des = (Designator) Activator.CreateInstance(AccessTools.TypeByName(saved.Type));
                        des.isOrder = true;
                        break;
                }

            if (des is null) return null;

            des.order = saved.Order;

            return des;
        }

        public static DesignatorSaved Save(Designator des)
        {
            var saved = new DesignatorSaved
            {
                Type = des.GetType().FullName,
                Order = des.order
            };

            if (Dialog_ConfigureArchitect.SpecialHandling.ContainsKey(des.GetType()))
                saved.AdditionalData = Dialog_ConfigureArchitect.SpecialHandling[des.GetType()].Save(des);
            else
                switch (des)
                {
                    case Designator_Dropdown dropdown:
                        saved.Elements = dropdown.Elements.Select(Save).ToList();
                        break;
                    case Designator_Group group:
                        saved.Elements = group.Elements.Select(Save).ToList();
                        saved.Name = group.label;
                        break;
                }

            return saved;
        }
    }
}