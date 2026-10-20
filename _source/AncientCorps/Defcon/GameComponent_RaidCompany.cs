using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace AncientCorps
{
    public class GameComponent_RaidCompany : GameComponent
    {
        public GameComponent_RaidCompany(Game game)
        {
        }
        public override void ExposeData()
        {
            base.ExposeData();
        }

        public void RaidCompany(Map map, CompanyDef company)
        {
            if (map == null) return;
            if (company == null)
            {
                // 原本用 DefDatabase.GetRandom()，會抽到非 AncientCorps 的連（也就沒有對應派系）。
                // The old GetRandom() could pick a company that does not belong to the AncientCorps.
                company = DefDatabase<CompanyDef>.AllDefsListForReading
                    .Where(c => c.defaultFaction == DMSAC_DefOf.DMS_AncientCorps && c.squads != null && c.squads.Count > 0)
                    .RandomElementWithFallback();
            }
            if (company == null)
            {
                Log.Error("DMSAC: RaidCompany called without a usable CompanyDef.");
                return;
            }

            Faction faction = Find.FactionManager.FirstFactionOfDef(company.defaultFaction);
            if (faction == null)
            {
                Log.Error("DMSAC: company faction is null, skipping raid.");
                return;
            }

            List<Pawn> commanders = new List<Pawn>();
            int squads = Mathf.Max(1, company.squadCountRange.RandomInRange);
            for (int i = 0; i < squads; i++)
            {
                PlatoonDef maker = company.squads.RandomElement();
                if (maker == null || maker.leaderKindDef.NullOrEmpty()) continue;
                if (!RCellFinder.TryFindRandomPawnEntryCell(out var result, map, CellFinder.EdgeRoadChance_Animal + 0.2f)) continue;

                PawnKindDef leaderKind = maker.leaderKindDef.RandomElement();
                if (leaderKind == null) continue;
                Pawn leaderPawn = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(leaderKind, faction), result, map, WipeMode.VanishOrMoveAside);
                if (leaderPawn == null) continue;

                // 指揮官與護衛放進「同一個」lord：原本指揮官被丟進另一個 LordJob_AssaultThings，
                // 導致 DefendTargetAndAssaultColony 的所有「目標倒下」邏輯永遠對不上人。
                // Commander and escorts share one lord; the old code put the commander in a separate
                // LordJob_AssaultThings, so the defend-target graph could never see him.
                List<Pawn> group = new List<Pawn>() { leaderPawn };
                foreach (PawnGenOption pawnOption in maker.fixedPawnkind ?? new List<PawnGenOption>())
                {
                    if (pawnOption?.kind == null) continue;
                    int count = Mathf.Max(0, (int)pawnOption.selectionWeight);
                    for (int j = 0; j < count; j++)
                    {
                        Pawn squadPawnFix = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(pawnOption.kind, faction), GetRandomCell(leaderPawn), map, WipeMode.VanishOrMoveAside);
                        if (squadPawnFix != null) group.Add(squadPawnFix);
                    }
                }
                int memberCount = maker.memberCountRange.RandomInRange;
                for (int k = 0; k < memberCount; k++)
                {
                    PawnKindDef memberKind = maker.memberKindDefs.RandomElementWithFallback();
                    if (memberKind == null) continue;
                    Pawn squadPawnFix = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(memberKind, faction), GetRandomCell(leaderPawn), map, WipeMode.VanishOrMoveAside);
                    if (squadPawnFix != null) group.Add(squadPawnFix);
                }
                LordMaker.MakeNewLord(faction, new LordJob_DefendTargetAndAssaultColony(leaderPawn), map, group);
                commanders.Add(leaderPawn);
            }

            TaggedString name = NameGenerator.GenerateName(company.NamePack);
            //訊息
            Find.LetterStack.ReceiveLetter("DMSAC_CompanyRaid".Translate(name), "DMSAC_CompanyRaid_Desc".Translate(faction.Name, name), LetterDefOf.ThreatBig, new LookTargets(commanders), faction);
        }

        private IntVec3 GetRandomCell(Pawn pawn)//pawn是leaderPawn
        {
            CellRect rect = CellRect.CenteredOn(pawn.Position, 10);
            for (int i = 0; i < 35; i++)
            {
                //之後大概會有個展開大小的參數在CompanyDef裡;
                IntVec3 cell = rect.RandomCell;
                if (!cell.InBounds(pawn.Map)) continue;
                if (!cell.Standable(pawn.Map)) continue;
                if (pawn.CanReach(cell, Verse.AI.PathEndMode.Touch, Danger.Some))
                {
                    return cell;
                }
            }
            return pawn.Position;
        }
    }
}
