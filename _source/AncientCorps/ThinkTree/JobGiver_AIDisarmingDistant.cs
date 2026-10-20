using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace AncientCorps
{
    public class JobGiver_AIDisarmingDistant : ThinkNode_JobGiver
    {
        public bool attackAllInert;

        private static readonly List<Building> tmpTrashableBuildingCandidates = new List<Building>();

        //優先攻擊目標，電池，生物鐵發電機，
        private static readonly List<Building> tmpTrashableBuildingCandidatesTopPriority = new List<Building>();

        // 複製一份可攻擊的遠程武器：直接對 lister 的內部清單做 RemoveWhere 會把非遠程武器永久除名。
        // Work on a copy: RemoveWhere on the lister's own list would permanently un-list weapons map-wide.
        private static readonly List<Thing> tmpRangedWeapons = new List<Thing>();

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
                if (item == null) continue;
                if (IsTargetTopPriority(item)) tmpTrashableBuildingCandidatesTopPriority.Add(item);
                else if (IsTarget(item)) tmpTrashableBuildingCandidates.Add(item);
            }
            if (tmpTrashableBuildingCandidates.Count == 0 && tmpTrashableBuildingCandidatesTopPriority.Count == 0)
            {
                return null;
            }

            tmpRangedWeapons.Clear();
            foreach (Thing thing in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon))
            {
                if (thing != null && thing.def.IsRangedWeapon && !thing.IsBurning())
                {
                    tmpRangedWeapons.Add(thing);
                }
            }

            for (int i = 0; i < 75; i++)
            {
                if (attackAllInert && tmpRangedWeapons.Count > 0)
                {
                    Thing item = tmpRangedWeapons.RandomElement();
                    if (item != null && pawn.CanReach(item, PathEndMode.Touch, Danger.Some))
                    {
                        Job job = TrashUtility.TrashJob(pawn, item, attackAllInert);
                        if (job != null)
                        {
                            return job;
                        }
                    }
                }
                // 60%機率選擇優先攻擊目標；優先清單為空時退回一般清單。
                bool preferTopPriority = tmpTrashableBuildingCandidatesTopPriority.Count > 0
                    && (tmpTrashableBuildingCandidates.Count == 0 || Rand.Chance(0.6f));
                Building building = preferTopPriority
                    ? tmpTrashableBuildingCandidatesTopPriority.RandomElement()
                    : tmpTrashableBuildingCandidates.RandomElementWithFallback();
                if (building == null)
                {
                    continue;
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
            // 用 is 而不是 GetType().IsAssignableFrom(...)，後者的參數順序會讓子類（改裝電池等）永遠不被選中。
            return item is Building_Battery || item is Building_BioferriteGenerator;
        }
        private bool IsTarget(Building item)
        {
            return item is Building_MultiTileDoor
                || item is Building_Door
                || (item.def.building != null && item.def.building.isTrap);
        }
    }
}
