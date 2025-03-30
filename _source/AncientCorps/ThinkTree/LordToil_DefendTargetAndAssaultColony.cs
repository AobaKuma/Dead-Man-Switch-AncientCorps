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
        private static void LetPawnExit(Pawn victim)
        {
            PawnDuty pawnDuty = new PawnDuty(DutyDefOf.ExitMapBest);
            pawnDuty.locomotion = LocomotionUrgency.Jog;
            pawnDuty.canDig = false;
            victim.mindState.duty = pawnDuty;
            if (victim.jobs.curJob != null)
            {
                victim.jobs.EndCurrentJob(JobCondition.InterruptForced, true, true);
            }
        }
        public override void Notify_PawnDamaged(Pawn victim, DamageInfo dinfo)
        {
            if (victim.Spawned && !victim.Downed && victim.mindState.duty.def.defName != "ExitMapBest" &&victim.health.summaryHealth.SummaryHealthPercent < EscapeHealth)
            {
                LetPawnExit(victim);
            }
        }
        private const float EscapeHealth = 0.25f;
        public override void Notify_PawnLost(Pawn victim, PawnLostCondition cond)
        {
            if (cond == PawnLostCondition.Incapped && victim.Spawned)
            {
                (lord.LordJob as LordJob_DefendTargetAndAssaultColony).downedPawns.Add(victim);
            }
        }

        public override void LordToilTick()
        {
            if (Find.TickManager.TicksAbs % 600 == 5)
            {
                foreach (var pawn in lord.ownedPawns)
                {
                    if (pawn.equipment.Primary == null)
                    {
                        LetPawnExit(pawn);
                    }
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
            for (int i = 0; i < this.lord.ownedPawns.Count; i++)
            {
                if (this.lord.ownedPawns[i].mindState != null)
                {
                    if (this.lord.ownedPawns[i] == (lord.LordJob as LordJob_DefendTargetAndAssaultColony).defendedTarget)
                    {
                        this.lord.ownedPawns[i].mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
                        this.lord.ownedPawns[i].mindState.duty.attackDownedIfStarving = true;
                        this.lord.ownedPawns[i].mindState.duty.pickupOpportunisticWeapon = false;
                        CompCanBeDormant compCanBeDormant = this.lord.ownedPawns[i].TryGetComp<CompCanBeDormant>();
                        compCanBeDormant?.WakeUp();
                        continue;
                    }
                    this.lord.ownedPawns[i].mindState.duty = new PawnDuty(DefDatabase<DutyDef>.GetNamed("DMS_XF_Bodyguard"));
                    this.lord.ownedPawns[i].mindState.duty.attackDownedIfStarving = true;
                    this.lord.ownedPawns[i].mindState.duty.pickupOpportunisticWeapon = false;
                    CompCanBeDormant compCanBeDormantB = this.lord.ownedPawns[i].TryGetComp<CompCanBeDormant>();
                    compCanBeDormantB?.WakeUp();
                }
            }
        }
    }
}