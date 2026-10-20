using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AncientCorps
{
    public class LordToil_PickUpTeammatesAndExitMap : LordToil_ExitMap
    {
        // 與原版 LordToil_ExitMap 相同的選擇性參數建構子：存檔還原時會以無參數方式建立。
        // Same optional-parameter shape as vanilla LordToil_ExitMap so deep-loading can recreate it.
        public LordToil_PickUpTeammatesAndExitMap(LocomotionUrgency locomotion = LocomotionUrgency.None, bool canDig = false, bool interruptCurrentJob = false)
        {
            this.data = new LordToilData_ExitMap();
            this.Data.locomotion = locomotion;
            this.Data.canDig = canDig;
            this.Data.interruptCurrentJob = interruptCurrentJob;
        }
        public override DutyDef ExitDuty => DMSAC_DefOf.DMS_XF_ExitAndPickUpTeammates;
    }
}
