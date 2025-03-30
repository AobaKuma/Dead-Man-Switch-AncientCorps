using RimWorld;
using System.Collections.Generic;
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

            TaggedString name = NameGenerator.GenerateName(company.NamePack);
            //訊息
            Find.LetterStack.ReceiveLetter("DMSAC_CompanyRaid".Translate(name), "DMSAC_CompanyRaid_Desc".Translate(faction.Name, name), LetterDefOf.ThreatBig);

            for (int i = 0; i < company.squadCountRange.RandomInRange; i++)
            {
                PlatoonMaker maker = company.squads.RandomElement();

                if (map != null && RCellFinder.TryFindRandomPawnEntryCell(out var result, map, CellFinder.EdgeRoadChance_Animal + 0.2f))
                {
                    Pawn leaderPawn = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(maker.leaderKindDef.RandomElement(), faction), result, map, WipeMode.VanishOrMoveAside);

                    List<Pawn> group = new List<Pawn>();
                    foreach (PawnGenOption pawnOption in maker.fixedPawnkind)
                    {
                        for (int j = 0; j < (int)pawnOption.selectionWeight; j++)
                        {
                            Pawn squadPawnFix = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(pawnOption.kind, faction), result, map, WipeMode.VanishOrMoveAside);
                            group.Add(squadPawnFix);
                        }
                    }
                    for (int k = 0; k < maker.memberCountRange.RandomInRange; k++)
                    {
                        Pawn squadPawnFix = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(maker.memberKindDefs.RandomElement(), faction), result, map, WipeMode.VanishOrMoveAside);
                        group.Add(squadPawnFix);
                    }
                    LordMaker.MakeNewLord(faction, new LordJob_DefendTargetAndAssaultColony(leaderPawn), map, group);
                }
            }
        }
    }
}