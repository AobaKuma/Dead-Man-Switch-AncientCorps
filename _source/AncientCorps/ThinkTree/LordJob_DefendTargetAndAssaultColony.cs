using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AncientCorps
{
    public class LordJob_DefendTargetAndAssaultColony : LordJob
    {
        public LordJob_DefendTargetAndAssaultColony(Pawn defendedTarget)
        {
            this.defendedTarget = defendedTarget;
        }
        public override bool GuiltyOnDowned => true;

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            
            LordToil_DefendTargetAndAssaultColony lordToil_Attack = new LordToil_DefendTargetAndAssaultColony();
            stateGraph.AddToil(lordToil_Attack);
            
            LordToil_ExitMap lordToil_ExitMap = new LordToil_PickUpTeammatesAndExitMap(LocomotionUrgency.Jog, true, true);
            lordToil_ExitMap.useAvoidGrid = true;
            stateGraph.AddToil(lordToil_ExitMap);
            
            Transition transitionA = new Transition(lordToil_Attack, lordToil_ExitMap, true, true);
            transitionA.AddTrigger(new Trigger_PawnLost(PawnLostCondition.Incapped, defendedTarget));
            stateGraph.AddTransition(transitionA, true);
            
            LordToil lordToil_AssaultColony = new LordToil_AssaultColony(false, false);
            lordToil_AssaultColony.useAvoidGrid = true;
            stateGraph.AddToil(lordToil_AssaultColony);
            
            Transition transitionB = new Transition(lordToil_Attack, lordToil_AssaultColony, true, true);
            transitionB.AddTrigger(new Trigger_PawnLost(PawnLostCondition.Undefined, defendedTarget));
            stateGraph.AddTransition(transitionB, false);

            int timeoutTicks = new IntRange(26000, 38000).RandomInRange;
            
            Transition transitionC = new Transition(lordToil_Attack, lordToil_ExitMap, false, true);
            transitionC.AddTrigger(new Trigger_TicksPassed(timeoutTicks));
            transitionC.AddPreAction(new TransitionAction_Message("MessageRaidersGivenUpLeaving".Translate(this.AssaulterFaction.def.pawnsPlural.CapitalizeFirst(), this.AssaulterFaction.Name), null, 1f));
            stateGraph.AddTransition(transitionC, false);

            Transition transitionCA = new Transition(lordToil_Attack, lordToil_ExitMap, false, true);
            transitionCA.AddTrigger(new Trigger_BecameNonHostileToPlayer());
            transitionCA.AddPreAction(new TransitionAction_Message("MessageRaidersLeaving".Translate(this.AssaulterFaction.def.pawnsPlural.CapitalizeFirst(), this.AssaulterFaction.Name), null, 1f));
            stateGraph.AddTransition(transitionCA, false);
            
            
            Transition transitionD = new Transition(lordToil_AssaultColony, lordToil_ExitMap, false, true);
            transitionD.AddTrigger(new Trigger_TicksPassed(timeoutTicks));
            transitionD.AddPreAction(new TransitionAction_Message("MessageRaidersGivenUpLeaving".Translate(this.AssaulterFaction.def.pawnsPlural.CapitalizeFirst(), this.AssaulterFaction.Name), null, 1f));
            stateGraph.AddTransition(transitionD, false);

            Transition transitionDA = new Transition(lordToil_AssaultColony, lordToil_ExitMap, false, true);
            transitionDA.AddTrigger(new Trigger_BecameNonHostileToPlayer());
            transitionDA.AddPreAction(new TransitionAction_Message("MessageRaidersLeaving".Translate(this.AssaulterFaction.def.pawnsPlural.CapitalizeFirst(), this.AssaulterFaction.Name), null, 1f));
            stateGraph.AddTransition(transitionDA, false);
            
            return stateGraph;
        }
        public void CheckDownedPawns()
        {
            foreach (var pawn in downedPawns)
            {
                if (!pawn.Spawned || pawn.Dead || !pawn.Downed)
                {
                    _removeElements.Add(pawn);
                }
            }
            while (_removeElements.Any())
            {
                downedPawns.Remove(_removeElements.Last());
                _removeElements.RemoveLast();
            }
        }
        public override void ExposeData()
        {
            Scribe_References.Look(ref defendedTarget, "defendedTarget", false);
            Scribe_Collections.Look(ref downedPawns,"downedPawns");
        }
        private Faction AssaulterFaction => defendedTarget.Faction;
        public         Pawn       defendedTarget;
        public         List<Pawn> downedPawns    = new List<Pawn>();
        private static List<Pawn> _removeElements = new List<Pawn>();
    }
}