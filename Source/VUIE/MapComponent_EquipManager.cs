using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace VUIE
{
    public class MapComponent_EquipManager : MapComponent
    {
        private static MapComponent_EquipManager localCache;
        public List<Building_WorkTable> BillGivers = new();

        public Dictionary<Pawn, List<Equipment>> Equipments;

        public MapComponent_EquipManager(Map map) : base(map) => localCache = this;

        public static MapComponent_EquipManager Get(Map map) => localCache.map.uniqueID == map.uniqueID ? localCache : localCache = map.GetComponent<MapComponent_EquipManager>();

        public IEnumerable<ThingDef> AllCraftableThings() =>
            from giver in BillGivers from recipe in giver.def.AllRecipes from product in recipe.products select product.thingDef;

        public IEnumerable<ThingDef> AllPossibleStuffs(ThingDef item) =>
            from kv in map.resourceCounter.AllCountedAmounts where kv.Key.IsStuff && kv.Key.stuffProps.CanMake(item) select kv.Key;

        public void AddPawn(Pawn pawn)
        {
            var list = new List<Equipment>();
            list.AddRange(pawn.equipment.AllEquipmentListForReading.Select(eq => new Equipment(eq, true, Equipment.EquipSource.Equipment)));
            list.AddRange(pawn.apparel.WornApparel.Select(ap => new Equipment(ap, true, Equipment.EquipSource.Apparel)));
            list.AddRange(pawn.inventory.innerContainer.Select(t => new Equipment(t, true, Equipment.EquipSource.Inventory)));
            Equipments.Add(pawn, list);
        }

        public void MakeRoomFor(Pawn pawn, Equipment equipment)
        {
            var equipList = Equipments[pawn];
            switch (equipment.Source)
            {
                case Equipment.EquipSource.Equipment:
                    if (equipment.ThingDef.equipmentType == EquipmentType.Primary)
                        equipList.RemoveAll(eq => eq.Source == Equipment.EquipSource.Equipment && eq.ThingDef.equipmentType == EquipmentType.Primary);
                    break;
                case Equipment.EquipSource.Apparel:
                    foreach (var eq in equipList.Where(eq => eq.Source == Equipment.EquipSource.Apparel).Reverse())
                        if (!ApparelUtility.CanWearTogether(eq.ThingDef, equipment.ThingDef, pawn.RaceProps.body))
                            equipList.Remove(eq);
                    break;
                case Equipment.EquipSource.Inventory:
                default:
                    break;
            }
        }

        public void Equip(Pawn pawn, Thing equipment, Equipment.EquipSource source)
        {
            var newEq = new Equipment(equipment, false, source);
            MakeRoomFor(pawn, newEq);
            Equipments[pawn].Add(newEq);
        }

        public void Make(Pawn pawn, Equipment.EquipSource source, Building_WorkTable makeAt, ThingDef toMake, ThingDef stuff = null)
        {
            Equipments[pawn].Add(new Equipment(source, makeAt, toMake, stuff));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref Equipments, "equipments", LookMode.Reference, LookMode.Deep);
        }
    }

    public sealed class Equipment : IExposable
    {
        public enum EquipSource
        {
            Equipment,
            Apparel,
            Inventory
        }

        public enum EquipType
        {
            Equipped,
            OnMap,
            ToMake
        }

        private Thing equipped;

        public EquipSource Source;
        private ThingDef stuff;
        private Thing toEquip;
        private ThingDef toMake;

        private Building_WorkTable workSite;

        public Equipment(Thing thing, bool equipped, EquipSource source)
        {
            if (equipped) this.equipped = thing;
            else toEquip = thing;
            Source = source;
        }

        public Equipment(EquipSource source, Building_WorkTable makeAt, ThingDef make, ThingDef stuff = null)
        {
            workSite = makeAt;
            toMake = make;
            this.stuff = stuff;
            Source = source;
        }

        public ThingDef ThingDef => Thing?.def ?? toMake;
        public Thing Thing => equipped ?? toEquip;

        public EquipType Type => equipped is not null ? EquipType.Equipped : toEquip is not null ? EquipType.OnMap : EquipType.ToMake;

        public void ExposeData()
        {
            Scribe_References.Look(ref equipped, "equipped");
            Scribe_References.Look(ref toEquip, "toEquip");
            Scribe_References.Look(ref workSite, "workSite");
            Scribe_Defs.Look(ref toMake, "toMake");
            Scribe_Defs.Look(ref stuff, "stuff");
        }

        public Job TryGiveJob() => null;

        public void Made(Thing made)
        {
            workSite = null;
            toMake = stuff = null;

            toEquip = made;
        }

        public void Equipped(Thing now)
        {
            toEquip = null;
            equipped = now;
        }
    }

    public class EquipManagerModule : Module
    {
        public override void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(Building_WorkTable), nameof(Building_WorkTable.SpawnSetup)),
                postfix: new HarmonyMethod(typeof(EquipManagerModule), nameof(RegisterBillGiver)));
            harm.Patch(AccessTools.Method(typeof(Building), nameof(Building.DeSpawn)),
                postfix: new HarmonyMethod(typeof(EquipManagerModule), nameof(DeregisterBillGiver)));
        }

        public static void RegisterBillGiver(Building_WorkTable __instance)
        {
            __instance.Map.EquipManager().BillGivers.Add(__instance);
        }

        public static void DeregisterBillGiver(Building __instance)
        {
            if (__instance is Building_WorkTable workTable)
                __instance.Map.EquipManager().BillGivers.Remove(workTable);
        }
    }
}