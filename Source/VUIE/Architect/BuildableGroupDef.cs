using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace VUIE
{
    public class BuildableGroupDef : Def
    {
        public DesignationCategoryDef category;
        public List<BuildableDef> defs;
        private Designator_Group designatorGroup;
        public string presetName;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var er in base.ConfigErrors()) yield return er;

            if (defs.NullOrEmpty()) yield return "Must provide defs";
            if (category == null) yield return "Must provide category";
        }

        public void RemoveChildren(DesignationCategoryDef def, bool fromPreset = false)
        {
            if (!fromPreset && DefDatabase<ArchitectPresetDef>.AllDefs.Any(apd => apd.Groups.Contains(this))) return;
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
                designatorGroup = new Designator_Group(inGroup, label);
                category.resolvedDesignators.Add(designatorGroup);
            }
            else
            {
                designatorGroup.Elements.AddRange(inGroup);
            }
        }
    }
}