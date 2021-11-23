using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse.AI;

namespace VUIE
{
    public class JobDriver_ManagedEquip : JobDriver_Equip
    {
        public override IEnumerable<Toil> MakeNewToils() =>
            base.MakeNewToils().Append(Toils_General.Do(() => pawn.Map.EquipManager().Equip(pawn, job.targetA.Thing, Equipment.EquipSource.Equipment)));
    }

    public class JobDriver_ManagedWear : JobDriver_Wear
    {
        public override IEnumerable<Toil> MakeNewToils() =>
            base.MakeNewToils().Append(Toils_General.Do(() => pawn.Map.EquipManager().Equip(pawn, Apparel, Equipment.EquipSource.Apparel)));
    }

    public class JobDriver_ManagedPickUp : JobDriver_TakeInventory
    {
        public override IEnumerable<Toil> MakeNewToils() => base.MakeNewToils().Append(Toils_General.Do(() =>
            pawn.Map.EquipManager().Equip(pawn, job.targetA.Thing, Equipment.EquipSource.Inventory)));
    }


}