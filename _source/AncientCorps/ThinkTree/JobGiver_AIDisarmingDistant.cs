using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.Noise;

namespace AncientCorps
{
    public class JobGiver_AIDisarmingDistant : ThinkNode_JobGiver
    {
        public bool attackAllInert;

        private static List<Building> tmpTrashableBuildingCandidates = new List<Building>();

        //優先攻擊目標，電池，生物鐵發電機，
        private static List<Building> tmpTrashableBuildingCandidatesTopPriority = new List<Building>();

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_AIDisarmingDistant obj = (JobGiver_AIDisarmingDistant)base.DeepCopy(resolve);
            obj.attackAllInert = attackAllInert;
            return obj;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            List<Building> allBuildingsColonist = pawn.Map.listerBuildings.allBuildingsColonist;
            if (allBuildingsColonist.Count == 0)
            {
                return null;
            }
            tmpTrashableBuildingCandidates.Clear();
            tmpTrashableBuildingCandidatesTopPriority.Clear();
            foreach (Building item in allBuildingsColonist)
            {
                if (IsTargetTopPriority(item)) tmpTrashableBuildingCandidatesTopPriority.Add(item);
                else if (IsTarget(item)) tmpTrashableBuildingCandidates.Add(item);
            }
            if (tmpTrashableBuildingCandidates.Count == 0 && tmpTrashableBuildingCandidatesTopPriority.Count == 0)
            {
                return null;
            }
            var weapon = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon);
            weapon?.RemoveWhere(x => !x.def.IsRangedWeapon);

            for (int i = 0; i < 75; i++)
            {
                Building building = tmpTrashableBuildingCandidates.RandomElement();
                if (attackAllInert)
                {
                    Thing item = weapon.RandomElement();
                    if (item != null && pawn.CanReach(item, PathEndMode.Touch, Danger.Some) && !item.IsBurning())
                    {
                        Job job = TrashUtility.TrashJob(pawn, item, attackAllInert);
                        if (job != null)
                        {
                            return job;
                        }
                    }
                }
                if (Rand.Chance(0.6f) || building == null) //60%機率選擇優先攻擊目標
                {
                    building = tmpTrashableBuildingCandidatesTopPriority.RandomElement();
                }
                if (TrashUtility.ShouldTrashBuilding(pawn, building, attackAllInert))
                {
                    Job job = TrashUtility.TrashJob(pawn, building, attackAllInert);
                    if (job != null)
                    {
                        return job;
                    }
                }
            }
            return null;
        }
        private bool IsTargetTopPriority(Building item)
        {
            if (item.GetType().IsAssignableFrom(typeof(Building_Battery))) return true;
            if (item.GetType().IsAssignableFrom(typeof(Building_BioferriteGenerator))) return true;
            return false;
        }
        private bool IsTarget(Building item)
        {
            if (item.GetType().IsAssignableFrom(typeof(Building_MultiTileDoor))) return true;
            if (item.GetType().IsAssignableFrom(typeof(Building_Door))) return true;
            if (item.def.building.isTrap) return true;
            return false;
        }
    }
}