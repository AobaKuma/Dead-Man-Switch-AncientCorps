using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AncientCorps
{
    public class LordJob_DefendTargetAndAssaultColony : LordJob
    {
        /// <summary>
        /// 存檔用。Lord.ExposeData 以 Scribe_Deep.Look(ref lordJob, ...) 還原，沒有 ctorArgs，
        /// 所以反序列化需要一個無參數建構子（原本只有 (Pawn)，載入存檔時會擲 MissingMethodException）。
        /// Save-compatibility ctor: Lord.ExposeData deep-loads the lord job without ctor args.
        /// </summary>
        public LordJob_DefendTargetAndAssaultColony()
        {
        }

        public LordJob_DefendTargetAndAssaultColony(Pawn defendedTarget)
        {
            this.defendedTarget = defendedTarget;
        }
        public override bool GuiltyOnDowned => true;

        /// <summary>取得 pawn 所屬的守衛 lord；pawn 沒有 lord 或 lord 型別不同時回 null。</summary>
        public static LordJob_DefendTargetAndAssaultColony GetFor(Pawn pawn)
        {
            if (pawn == null) return null;
            return pawn.GetLord()?.LordJob as LordJob_DefendTargetAndAssaultColony;
        }

        public Pawn DefendedTargetOrNull =>
            defendedTarget != null && defendedTarget.Spawned && !defendedTarget.Downed ? defendedTarget : null;

        public bool DefendedTargetLost => DefendedTargetOrNull == null;

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();

            LordToil_DefendTargetAndAssaultColony lordToil_Attack = new LordToil_DefendTargetAndAssaultColony();
            stateGraph.AddToil(lordToil_Attack);

            LordToil_ExitMap lordToil_ExitMap = new LordToil_PickUpTeammatesAndExitMap(LocomotionUrgency.Sprint, true, true);
            lordToil_ExitMap.useAvoidGrid = true;
            stateGraph.AddToil(lordToil_ExitMap);

            LordToil lordToil_AssaultColony = new LordToil_AssaultColony(false, false);
            lordToil_AssaultColony.useAvoidGrid = true;
            stateGraph.AddToil(lordToil_AssaultColony);

            // 原本有兩條 Trigger_PawnLost(..., defendedTarget) 的轉移，但它們永遠不會觸發：
            // Trigger_PawnLost 只對「本 lord 的 pawn」發出的訊號有反應，而指揮官在
            // GameComponent_RaidCompany 裡是掛在另一個 lord 上的。這裡改成由 toil 自己處理：
            // 指揮官倒下後，護衛的 JobGiver_FollowDefendedTarget 會失效、改由 JobGiver_AIFightEnemiesWhenDefending
            // 正常交戰（等同轉入強攻），所以不需要額外的圖轉移。
            // The two Trigger_PawnLost transitions could never fire (the commander belongs to another lord);
            // once the commander is lost the bodyguard think tree degrades to normal engagement by itself,
            // which is the same outcome as the old "escalate to assault" branch.

            int timeoutTicks = new IntRange(26000, 38000).RandomInRange;

            Transition transitionC = new Transition(lordToil_Attack, lordToil_ExitMap, false, true);
            transitionC.AddTrigger(new Trigger_TicksPassed(timeoutTicks));
            transitionC.AddPreAction(new TransitionAction_Message("MessageRaidersGivenUpLeaving".Translate(AssaulterPlural, AssaulterName)));
            stateGraph.AddTransition(transitionC, false);

            Transition transitionCA = new Transition(lordToil_Attack, lordToil_ExitMap, false, true);
            transitionCA.AddTrigger(new Trigger_BecameNonHostileToPlayer());
            transitionCA.AddPreAction(new TransitionAction_Message("MessageRaidersLeaving".Translate(AssaulterPlural, AssaulterName)));
            stateGraph.AddTransition(transitionCA, false);

            Transition transitionD = new Transition(lordToil_AssaultColony, lordToil_ExitMap, false, true);
            transitionD.AddTrigger(new Trigger_TicksPassed(timeoutTicks));
            transitionD.AddPreAction(new TransitionAction_Message("MessageRaidersGivenUpLeaving".Translate(AssaulterPlural, AssaulterName)));
            stateGraph.AddTransition(transitionD, false);

            Transition transitionDA = new Transition(lordToil_AssaultColony, lordToil_ExitMap, false, true);
            transitionDA.AddTrigger(new Trigger_BecameNonHostileToPlayer());
            transitionDA.AddPreAction(new TransitionAction_Message("MessageRaidersLeaving".Translate(AssaulterPlural, AssaulterName)));
            stateGraph.AddTransition(transitionDA, false);

            return stateGraph;
        }
        public void CheckDownedPawns()
        {
            for (int i = downedPawns.Count - 1; i >= 0; i--)
            {
                Pawn pawn = downedPawns[i];
                if (pawn == null || !pawn.Spawned || pawn.Dead || !pawn.Downed)
                {
                    downedPawns.RemoveAt(i);
                }
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref defendedTarget, "defendedTarget", false);
            Scribe_Collections.Look(ref downedPawns, "downedPawns", LookMode.Reference);
            if (downedPawns == null)
            {
                downedPawns = new List<Pawn>();
            }
        }

        // 指揮官可能已死亡/離開地圖，Faction 也可能為 null（例如被清空派系），所有訊息都要能安全取用。
        private Faction AssaulterFaction
        {
            get
            {
                if (defendedTarget != null && defendedTarget.Faction != null)
                {
                    return defendedTarget.Faction;
                }
                return lord?.faction;
            }
        }
        private string AssaulterPlural => AssaulterFaction?.def?.pawnsPlural?.CapitalizeFirst() ?? "raiders";
        private string AssaulterName => AssaulterFaction?.Name ?? "the raiders";

        public Pawn defendedTarget;
        public List<Pawn> downedPawns = new List<Pawn>();
    }
}
