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
                            defName = cat.DefName,
                            label = cat.Label
                        };
                        ArchitectModule.DoDesInit = true;
                        def.ResolveDesignators();
                        def.AllResolvedDesignators.Clear();
                        def.AllResolvedDesignators.AddRange(cat.Designators.Select(DesignatorSaved.Load));
                        var desTab = new ArchitectCategoryTab(def, ArchitectLoadSaver.Architect.quickSearchWidget.filter);
                        Dialog_ConfigureArchitect.ArchitectCategoryTabs.Add(desTab);
                    }

                if (Changes != null)
                    foreach (var change in Changes)
                        change.Apply();
                module.SavedStates.Add(ArchitectLoadSaver.SaveState(label));
            });
        }
    }

    public class DesignationCategoryChange
    {
        public DesignationCategoryDef Category;
        public ThingDef ThingDef;

        public void Apply()
        {
            ThingDef.designationCategory = Category;
        }

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            if (xmlRoot.ChildNodes.Count != 1)
            {
                Log.Error("Misconfigured DesignationCategoryChange: " + xmlRoot.OuterXml);
                return;
            }

            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "ThingDef", xmlRoot.Name);
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "Category", xmlRoot.FirstChild.Value);
        }
    }
}