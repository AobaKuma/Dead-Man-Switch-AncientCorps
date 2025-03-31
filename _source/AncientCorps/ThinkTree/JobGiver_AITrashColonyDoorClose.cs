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
                    if (item.def.IsRangedWeapon)
                    {
                        if (item != null && GenSight.LineOfSight(pawn.Position, pos, pawn.Map))
                        {
                            Job job = TrashUtility.TrashJob(pawn, item);
                            if (job != null)
                            {
                                return job;
                            }
                        }
                    }
                    if (item.GetType().IsAssignableFrom(typeof(Building_Door)))
                    {
                        if (item != null && TrashUtility.ShouldTrashBuilding(pawn, item as Building) && GenSight.LineOfSight(pawn.Position, pos, pawn.Map))
                        {
                            Job job = TrashUtility.TrashJob(pawn, item);
                            if (job != null)
                            {
                                return job;
                            }
                        }
                    }
                }
                
            }
            return null;
        }
    }
}