using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using RimWorld;
using Verse;

namespace VUIE
{
    public class ArchitectPresetDef : Def
    {
        public List<ArchitectTabSaved> AddCats;
        public List<DesignationCategoryChange> Changes;
        public List<BuildableGroupDef> Groups;

        public List<DesignationCategoryDef> RemoveCats;

        public ArchitectPresetDef() => description = "An architect preset";

        public override void PostLoad()
        {
            base.PostLoad();
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                var module = UIMod.GetModule<ArchitectModule>();
                ArchitectLoadSaver.RestoreState(module.SavedStates[module.VanillaIndex]);
                if (Groups != null)
                    foreach (var buildableGroupDef in Groups)
                    foreach (var category in DefDatabase<DesignationCategoryDef>.AllDefs)
                        buildableGroupDef.RemoveChildren(category, true);
                if (RemoveCats != null)
                    Dialog_ConfigureArchitect.ArchitectCategoryTabs.RemoveAll(tab => RemoveCats.Contains(tab.def));
                if (AddCats != null)
                    foreach (var cat in AddCats)
                    {
                        var def = new DesignationCategoryDef
                        {
                            defName = cat.defName,
                            label = cat.label,
                            description = cat.description
                        };
                        ArchitectModule.DoDesInit = true;
                        def.ResolveDesignators();
                        ArchitectModule.DoDesInit = false;
                        def.AllResolvedDesignators.Clear();
                        def.AllResolvedDesignators.AddRange(cat.Designators.Select(DesignatorSaved.Load));
                        DefGenerator.AddImpliedDef(def);
                        var desTab = new ArchitectCategoryTab(def, ArchitectLoadSaver.Architect.quickSearchWidget.filter);
                        Dialog_ConfigureArchitect.ArchitectCategoryTabs.Add(desTab);
                    }

                if (Changes != null)
                    foreach (var change in Changes)
                        change.Apply();
                module.AddState(ArchitectLoadSaver.SaveState(label));
            });
        }
    }

    public class DesignationCategoryChange
    {
        public DesignationCategoryDef Category;
        public BuildableDef Def;

        public void Apply()
        {
            if (Def is null || Category is null) return;
            Def.designationCategory = Category;
        }

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            if (xmlRoot.ChildNodes.Count != 1)
            {
                Log.Error("[VUIE] Misconfigured DesignationCategoryChange: " + xmlRoot.OuterXml);
                return;
            }

            DirectXmlCrossRefLoader.wantedRefs.Add(new WantedRefForDesChange(this, WantedRefForDesChange.ChangeField.Def, xmlRoot.Name));
            DirectXmlCrossRefLoader.wantedRefs.Add(new WantedRefForDesChange(this, WantedRefForDesChange.ChangeField.Category, xmlRoot.FirstChild.Value));
        }
    }

    public class WantedRefForDesChange : DirectXmlCrossRefLoader.WantedRef
    {
        public enum ChangeField
        {
            Def,
            Category
        }

        private readonly ChangeField field;
        private readonly string target;

        private new readonly DesignationCategoryChange wanter;

        public WantedRefForDesChange(DesignationCategoryChange wanter, ChangeField field, string targetDefName)
        {
            this.wanter = wanter;
            base.wanter = wanter;
            this.field = field;
            target = targetDefName;
        }

        public override bool TryResolve(FailMode failReportMode)
        {
            if (wanter is null || target.NullOrEmpty())
            {
                if (failReportMode == FailMode.LogErrors) Log.Error("[VUIE] Invalid arguments to WantedRefForDesChange");
                return false;
            }

            switch (field)
            {
                case ChangeField.Def:
                    wanter.Def = DefDatabase<BuildableDef>.GetNamedSilentFail(target);
                    return true;
                case ChangeField.Category:
                    wanter.Category = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(target);
                    return true;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}