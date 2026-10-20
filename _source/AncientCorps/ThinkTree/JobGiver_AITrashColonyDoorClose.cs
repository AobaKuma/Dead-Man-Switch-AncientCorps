using RimWorld;
using Verse;
using Verse.AI;

namespace AncientCorps
{
    public class JobGiver_AITrashColonyDoorClose : ThinkNode_JobGiver
    {
        private const int CloseSearchRadius = 15;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!pawn.HostileTo(Faction.OfPlayer))
            {
                return null;
            }
            CellRect cellRect = CellRect.CenteredOn(pawn.Position, CloseSearchRadius);
            foreach (IntVec3 pos in cellRect)
            {
                if (!pos.InBounds(pawn.Map)) continue;
                foreach (var item in pos.GetThingList(pawn.Map))
                {
                    if (item == null) continue;
                    if (!GenSight.LineOfSight(pawn.Position, pos, pawn.Map)) continue;
                    if (item.def.IsRangedWeapon)
                    {
                        Job job = TrashUtility.TrashJob(pawn, item);
                        if (job != null)
                        {
                            return job;
                        }
                    }
                    // 用 is 而不是 GetType().IsAssignableFrom(...)：後者的參數順序會讓自動門等子類永遠不被選中。
                    if (item is Building_Door door && TrashUtility.ShouldTrashBuilding(pawn, door))
                    {
                        Job job = TrashUtility.TrashJob(pawn, door);
                        if (job != null)
                        {
                            return job;
                        }
                    }
                }

            }
            return null;
        }
    }
}
