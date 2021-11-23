using RimWorld;
using UnityEngine;
using Verse;

namespace VUIE
{
    public class MainTabWindow_Equipment : MainTabWindow
    {
        public MapComponent_EquipManager Manager => Find.CurrentMap.EquipManager();

        public override void DoWindowContents(Rect inRect)
        {
        }
    }
}