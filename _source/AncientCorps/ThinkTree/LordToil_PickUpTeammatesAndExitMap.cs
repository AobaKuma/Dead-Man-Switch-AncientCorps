using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AncientCorps
{
    public class LordToil_PickUpTeammatesAndExitMap : LordToil_ExitMap
    {
        public LordToil_PickUpTeammatesAndExitMap(LocomotionUrgency locomotion = LocomotionUrgency.None, bool canDig = false, bool interruptCurrentJob = false)
        {
            this.data = new LordToilData_ExitMap();
            this.Data.locomotion = locomotion;
            this.Data.canDig = canDig;
            this.Data.interruptCurrentJob = interruptCurrentJob;
        }
        public override DutyDef ExitDuty => DefDatabase<DutyDef>.GetNamed("DMS_XF_ExitAndPickUpTeammates");
    }
}