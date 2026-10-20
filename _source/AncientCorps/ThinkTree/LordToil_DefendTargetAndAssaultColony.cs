using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AncientCorps
{
    public class LordToil_DefendTargetAndAssaultColony : LordToil
    {
        public override bool ForceHighStoryDanger => true;
        public override bool AllowSatisfyLongNeeds => false;

        private const float EscapeHealth = 0.25f;

        private LordJob_DefendTargetAndAssaultColony DefendJob =>
            lord?.LordJob as LordJob_DefendTargetAndAssaultColony;

        private static void LetPawnExit(Pawn victim)
        {
            PawnDuty pawnDuty = new PawnDuty(DutyDefOf.ExitMapBest);
            pawnDuty.locomotion = LocomotionUrgency.Sprint;
            pawnDuty.canDig = false;
            victim.mindState.duty = pawnDuty;
            if (victim.jobs.curJob != null)
            {
                victim.jobs.EndCurrentJob(JobCondition.InterruptForced, true, true);
            }
        }

        /// <summary>
        /// 固定砲塔型機兵（稜堡、多砲塔城邦）的 equipment.Primary 恆為 null，
        /// 只看 Primary 會讓它們在 600 tick 內全部自動離場。
        /// Fixed-mount mechs (Krepost, multi-turret Gorod) always have a null Primary,
        /// so checking only Primary made them all walk off the map.
        /// </summary>
        private static bool HasUsableWeapon(Pawn pawn)
        {
            if (pawn.equipment?.Primary != null)
            {
                return true;
            }
            foreach (ThingComp comp in pawn.AllComps)
            {
                if (comp is CompTurretGun turret && turret.AutoAttack)
                {
                    return true;
                }
                if (comp is Fortified.CompMultipleTurretGun)
                {
                    return true;
                }
            }
            return false;
        }

        public override void Notify_PawnDamaged(Pawn victim, DamageInfo dinfo)
        {
            // mindState.duty 可能尚未指派（剛生成/剛切 toil），直接讀 def 會 NRE。
            bool alreadyLeaving = victim.mindState.duty != null && victim.mindState.duty.def == DutyDefOf.ExitMapBest;
            if (victim.Spawned && !victim.Downed && !alreadyLeaving && victim.health.summaryHealth.SummaryHealthPercent < EscapeHealth)
            {
                LetPawnExit(victim);
            }
        }

        public override void Notify_PawnLost(Pawn victim, PawnLostCondition cond)
        {
            LordJob_DefendTargetAndAssaultColony job = DefendJob;
            if (job == null || victim == null) return;
            if (cond == PawnLostCondition.Incapped && victim.Spawned)
            {
                job.downedPawns.Add(victim);
            }
        }

        public override void LordToilTick()
        {
            LordJob_DefendTargetAndAssaultColony job = DefendJob;
            if (job == null) return;
            // 用 lord.loadID 錯開，避免所有 lord 在同一個絕對 tick 一起掃描（原本是 TicksAbs % 600 == 5）。
            int stagger = lord.loadID % 600;
            if (Find.TickManager.TicksGame % 600 != stagger)
            {
                return;
            }
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed) continue;
                if (!HasUsableWeapon(pawn))
                {
                    LetPawnExit(pawn);
                }
            }
        }

        public override void Init()
        {
            base.Init();
            LessonAutoActivator.TeachOpportunity(ConceptDefOf.Drafting, OpportunityType.Critical);
        }

        public override void UpdateAllDuties()
        {
            LordJob_DefendTargetAndAssaultColony job = DefendJob;
            for (int i = 0; i < this.lord.ownedPawns.Count; i++)
            {
                Pawn pawn = this.lord.ownedPawns[i];
                if (pawn.mindState == null) continue;
                if (job != null && pawn == job.defendedTarget)
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
                    pawn.mindState.duty.attackDownedIfStarving = true;
                    pawn.mindState.duty.pickupOpportunisticWeapon = false;
                    pawn.TryGetComp<CompCanBeDormant>()?.WakeUp();
                    continue;
                }
                pawn.mindState.duty = new PawnDuty(DMSAC_DefOf.DMS_XF_Bodyguard);
                pawn.mindState.duty.attackDownedIfStarving = true;
                pawn.mindState.duty.pickupOpportunisticWeapon = false;
                pawn.TryGetComp<CompCanBeDormant>()?.WakeUp();
            }
        }
    }
}
