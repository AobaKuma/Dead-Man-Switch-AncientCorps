using RimWorld;
using System;
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
            if (company == null) company = DefDatabase<CompanyDef>.GetRandom();

            Faction faction = Find.FactionManager.FirstFactionOfDef(company.defaultFaction);
            if (faction == null) { Log.Error("Company faction have null faction, using pirate instead"); faction = Find.FactionManager.OfPirates; }

            List<Thing> targetThing = new List<Thing>();
            targetThing.AddRange(map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon));
            targetThing.AddRange(map.listerThings.ThingsInGroup(ThingRequestGroup.WasteProducer));
            targetThing.AddRange(map.listerThings.ThingsInGroup(ThingRequestGroup.SubcoreScanner));
            targetThing.AddRange(map.listerThings.ThingsMatching(ThingRequest.ForDef(ThingDefOf.Door)));
            targetThing.AddRange(map.listerThings.ThingsMatching(ThingRequest.ForDef(ThingDefOf.TrapSpike)));
            targetThing.RemoveAll(x => x.Faction != Faction.OfPlayer);

            List<Pawn> commanders = new List<Pawn>();
            for (int i = 0; i < company.squadCountRange.RandomInRange; i++)
            {
                PlatoonMaker maker = company.squads.RandomElement();

                if (map != null && RCellFinder.TryFindRandomPawnEntryCell(out var result, map, CellFinder.EdgeRoadChance_Animal + 0.2f))
                {
                    Pawn leaderPawn = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(maker.leaderKindDef.RandomElement(), faction), result, map, WipeMode.VanishOrMoveAside);
                    LordMaker.MakeNewLord(faction, new LordJob_AssaultThings(faction, targetThing.Where(x => leaderPawn.CanReach(x, Verse.AI.PathEndMode.Touch, Danger.Deadly)).ToList()), map, new List<Pawn>() { leaderPawn });
                    commanders.Add(leaderPawn);
                    List<Pawn> group = new List<Pawn>();
                    foreach (PawnGenOption pawnOption in maker.fixedPawnkind)
                    {
                        for (int j = 0; j < (int)pawnOption.selectionWeight; j++)
                        {
                            Pawn squadPawnFix = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(pawnOption.kind, faction), GetRandomCell(leaderPawn), map, WipeMode.VanishOrMoveAside);
                            group.Add(squadPawnFix);
                        }
                    }
                    for (int k = 0; k < maker.memberCountRange.RandomInRange; k++)
                    {
                        Pawn squadPawnFix = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(maker.memberKindDefs.RandomElement(), faction), GetRandomCell(leaderPawn), map, WipeMode.VanishOrMoveAside);
                        group.Add(squadPawnFix);
                    }
                    LordMaker.MakeNewLord(faction, new LordJob_DefendTargetAndAssaultColony(leaderPawn), map, group);
                }
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
                if (pawn.CanReach(cell, Verse.AI.PathEndMode.Touch, Danger.Some))
                {
                    return cell;
                }
            }
            return pawn.Position;
        }
    }
}