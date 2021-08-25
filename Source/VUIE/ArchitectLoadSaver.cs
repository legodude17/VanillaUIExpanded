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

        public static ArchitectSaved SaveState(string name, bool vanilla = false)
        {
            return new ArchitectSaved
            {
                Name = name,
                Vanilla = vanilla,
                Tabs = Architect.desPanelsCached.Select(ArchitectTabSaved.Save).ToList()
            };
        }

        public static void RestoreState(ArchitectSaved saved)
        {
            Architect.desPanelsCached.Clear();
            foreach (var tab in saved.Tabs)
            {
                var def = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(tab.DefName) ?? new DesignationCategoryDef
                {
                    defName = tab.DefName,
                    label = tab.Label
                };
                ArchitectModule.DoDesInit = true;
                def.ResolveDesignators();
                def.AllResolvedDesignators.Clear();
                def.AllResolvedDesignators.AddRange(tab.Designators.Select(DesignatorSaved.Load));
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
        public string Label;
        public string DefName;
        public List<DesignatorSaved> Designators;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Label, "label");
            Scribe_Values.Look(ref DefName, "defName");
            Scribe_Collections.Look(ref Designators, "designators", LookMode.Deep);
        }

        public static ArchitectTabSaved Save(ArchitectCategoryTab tab)
        {
            return new ArchitectTabSaved
            {
                Label = tab.def.label,
                DefName = tab.def.LabelCap,
                Designators = tab.def.AllResolvedDesignators.Select(DesignatorSaved.Save).ToList()
            };
        }
    }

    public struct DesignatorSaved : IExposable
    {
        public string Type;
        public string EntDefName;
        public string Name;
        public List<DesignatorSaved> Elements;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Type, "type");
            Scribe_Values.Look(ref Name, "name");
            Scribe_Values.Look(ref EntDefName, "entDef");
            Scribe_Collections.Look(ref Elements, "elements", LookMode.Deep);
        }

        public static Designator Load(DesignatorSaved saved)
        {
            Designator des;
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
                case "RimWorld.Designator_Build":
                    var entDef = (BuildableDef) DefDatabase<ThingDef>.GetNamedSilentFail(saved.EntDefName) ?? DefDatabase<TerrainDef>.GetNamedSilentFail(saved.EntDefName);
                    des = new Designator_Build(entDef);
                    break;
                default:
                    des = (Designator) Activator.CreateInstance(AccessTools.TypeByName(saved.Type));
                    des.isOrder = true;
                    break;
            }

            return des;
        }

        public static DesignatorSaved Save(Designator des)
        {
            var saved = new DesignatorSaved
            {
                Type = des.GetType().FullName
            };

            switch (des)
            {
                case Designator_Build build:
                    saved.EntDefName = build.entDef.defName;
                    break;
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