using Verse;
using Verse.AI;

namespace VUIE
{
    public class JobGiver_ManagedEquip : ThinkNode_JobGiver
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            if (!pawn.Map.EquipManager().Equipments.TryGetValue(pawn, out var equipList)) return null;
            return null;
        }
    }
}