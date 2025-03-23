using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace AncientCorps
{
    public class WorldObjectCompProperties_PatrolSquad : WorldObjectCompProperties
    {
        public List<PawnGenOption> possibleGeneratePawn;
        public List<PawnGenOption> possibleGeneratePawn_Medium;
        public List<PawnGenOption> possibleGeneratePawn_Heavy;
        public int averagePawnCount = 5;
        public int spawnCountSigma = 5;
        public int MBTTime = 30000;
        public int patrolCountToRaiseLevel = 3;
        public WorldObjectCompProperties_PatrolSquad()
        {
            compClass = typeof(WorldObjectComp_PatrolSquad);
        }

        public override IEnumerable<string> ConfigErrors(WorldObjectDef parentDef)
        {
            foreach (string item in base.ConfigErrors(parentDef))
            {
                yield return item;
            }
            if (!typeof(MapParent).IsAssignableFrom(parentDef.worldObjectClass))
            {
                yield return parentDef.defName + " has WorldObjectCompProperties_PatrolSquad but it's not MapParent.";
            }
            if (possibleGeneratePawn.NullOrEmpty())
            {
                yield return parentDef.defName + " has NullOrEmpty possibleGeneratePawn";
            }
        }
    }
    public class WorldObjectComp_PatrolSquad : WorldObjectComp
    {
        private bool greetTriggered = false;
        public int timeToSpawn = 0;
        protected int latestPatrolTick = 180000;

        protected GameComponent_DefconLevel Defcon => Current.Game.GetComponent<GameComponent_DefconLevel>();
        private int Level => Defcon.Level;
        protected int countPartol = 0;

        public WorldObjectCompProperties_PatrolSquad Props => props as WorldObjectCompProperties_PatrolSquad;
        public override void PostMapGenerate()
        {
            base.PostMapGenerate();
            timeToSpawn = (int)(new IntRange(Props.MBTTime, latestPatrolTick).RandomInRange * Defcon.GetCurrentScale);
            latestPatrolTick = (int)(Mathf.Clamp(timeToSpawn * Rand.Range(0.25f, 0.75f), Props.MBTTime, timeToSpawn) * Defcon.GetCurrentScale);
            Log.Message(timeToSpawn + " " + latestPatrolTick);
        }
        public override void CompTick()
        {
            if (!base.ParentHasMap || !parent.IsHashIntervalTick(timeToSpawn))
            {
                return;
            }
            SpawnPatrol();
        }
        public void SpawnPatrol()
        {
            Map map = ((MapParent)parent).Map;
            if (map != null && RCellFinder.TryFindRandomPawnEntryCell(out var result, map, CellFinder.EdgeRoadChance_Animal + 0.2f))
            {
                Faction faction = AncientCorpsUltility.Corps;
                float weightSelector(PawnGenOption PawnGenOption) => PawnGenOption.selectionWeight;

                List<Pawn> group = new List<Pawn>();
                for (int i = 0; i < Rand.Gaussian(Props.averagePawnCount - 1, Props.spawnCountSigma / Defcon.GetCurrentScale) + 1; i++)
                {
                    Pawn pawn = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(Props.possibleGeneratePawn.RandomElementByWeight(weightSelector).kind, faction), result, map, Rot4.Random);
                    group.Add(pawn);
                }
                if (Level > 0 && !Props.possibleGeneratePawn_Medium.NullOrEmpty()) //Defcon I及以上會加派中型機兵
                    for (int i = 0; i < 3 / Defcon.GetCurrentScale; i++)
                    {
                        Pawn pawn = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(Props.possibleGeneratePawn_Medium.RandomElementByWeight(weightSelector).kind, faction), result, map, Rot4.Random);
                        group.Add(pawn);
                    }
                if (Level > 3 && !Props.possibleGeneratePawn_Heavy.NullOrEmpty()) //Defcon III及以上會加派重型機兵
                    for (int i = 0; i < 1 / Defcon.GetCurrentScale; i++)
                    {
                        Pawn pawn = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(Props.possibleGeneratePawn_Heavy.RandomElementByWeight(weightSelector).kind, faction), result, map, Rot4.Random);
                        group.Add(pawn);
                    }
                Lord lord = map.lordManager.lords.Find((Lord l2) => l2.LordJob.GetType() == typeof(LordJob_StageThenAttack));
                if (lord == null)
                {
                    lord = LordMaker.MakeNewLord(faction, new LordJob_StageThenAttack(faction, map.Center, Rand.Range(1, 10)), map, group);
                }
                else
                {
                    lord.AddPawns(group);
                }
                if (!greetTriggered) AncientCorpsUltility.SetHostileToPlayer(map, group.RandomElement());
                else
                {
                    //一支巡邏部隊已經抵達設施，更多的後續部隊將在更短的時間內陸續增援。
                    Find.LetterStack.ReceiveLetter("DMSAC_PatrolSquad".Translate(), "DMSAC_PatrolSquadDesc".Translate(), LetterDefOf.ThreatSmall);
                    countPartol++;
                    if (countPartol >= Props.patrolCountToRaiseLevel)
                    {
                        Defcon.LevelRise();
                        countPartol = 0;
                    }
                    Defcon.ResetActionInterval();
                }
                greetTriggered = true;
            }
            timeToSpawn = new IntRange(Props.MBTTime, latestPatrolTick).RandomInRange;
            latestPatrolTick = (int)(Mathf.Clamp(timeToSpawn * Rand.Range(0.25f, 0.75f), Props.MBTTime, timeToSpawn) * Defcon.GetCurrentScale);
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref timeToSpawn, "timeToSpawn", 0);
            Scribe_Values.Look(ref greetTriggered, "greetTriggered", false);
            Scribe_Values.Look(ref countPartol, "countPartol", 0);
            Scribe_Values.Look(ref latestPatrolTick, "latestPatrolTick", 180000);
        }
    }
}