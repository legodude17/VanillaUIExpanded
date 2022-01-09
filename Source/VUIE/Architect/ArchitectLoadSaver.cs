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
        public static MainTabWindow_Architect Architect => (MainTabWindow_Architect) MainButtonDefOf.Architect.TabWindow;

        public static ArchitectSaved SaveState(string name, bool vanilla = false) => new()
        {
            Name = name,
            Vanilla = vanilla,
            Tabs = Architect.desPanelsCached.Select(ArchitectTabSaved.Save).ToList()
        };

        public static void RestoreState(ArchitectSaved saved)
        {
            Architect.desPanelsCached.Clear();
            foreach (var tab in saved.Tabs)
            {
                var def = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(tab.defName) ?? new DesignationCategoryDef
                {
                    defName = tab.defName,
                    label = tab.label
                };
                ArchitectModule.DoDesInit = true;
                def.ResolveDesignators();
                if (tab.Designators != null)
                {
                    def.AllResolvedDesignators.Clear();
                    def.AllResolvedDesignators.AddRange(tab.Designators.Select(DesignatorSaved.Load));
                }

                var desTab = new ArchitectCategoryTab(def, Architect.quickSearchWidget.filter);
                Architect.desPanelsCached.Add(desTab);
            }
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
            defName = tab.def.LabelCap,
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
            if (Dialog_ConfigureArchitect.SpecialHandling.ContainsKey(type))
                des = Dialog_ConfigureArchitect.SpecialHandling[type].Load(saved.AdditionalData, type);
            else
                switch (saved.Type)
                {
                    case "VUIE.Designator_Group":
                        des = new Designator_Group(saved.Elements.Select(Load).ToList(), saved.Name);
                        break;
                    case "RimWorld.Designator_Dropdown":
                        var dropdown = new Designator_Dropdown();
                        foreach (var designator in saved.Elements.Select(Load)) dropdown.Add(designator);
                        des = dropdown;
                        break;
                    default:
                        des = (Designator) Activator.CreateInstance(AccessTools.TypeByName(saved.Type));
                        des.isOrder = true;
                        break;
                }

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