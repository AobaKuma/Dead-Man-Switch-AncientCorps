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
        public int averagePawnCount = 5;
        public int spawnCountSigma = 5;
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
        public WorldObjectCompProperties_PatrolSquad Props => props as WorldObjectCompProperties_PatrolSquad;
        public override void PostMapGenerate()
        {
            base.PostMapGenerate();
            timeToSpawn = new IntRange(15000, latestPatrolTick).RandomInRange;
            latestPatrolTick = (int)Mathf.Clamp((timeToSpawn * 0.8f), 60000, 999999);
        }
        public override void CompTick()
        {
            if (!base.ParentHasMap || !parent.IsHashIntervalTick(timeToSpawn))
            {
                return;
            }
            SpawnPatrol();
            timeToSpawn = new IntRange(60000, 180000).RandomInRange;
        }
        public void SpawnPatrol()
        {
            Map map = ((MapParent)parent).Map;
            if (map != null && RCellFinder.TryFindRandomPawnEntryCell(out var result, map, CellFinder.EdgeRoadChance_Animal + 0.2f))
            {
                Faction faction = Find.FactionManager.FirstFactionOfDef(DMS_DefOf.DMS_AncientCorps);
                List<Pawn> group = new List<Pawn>();
                for (int i = 0; i < Rand.Gaussian(Props.averagePawnCount - 1, Props.spawnCountSigma) + 1; i++)
                {
                    float weightSelector(PawnGenOption PawnGenOption) => PawnGenOption.selectionWeight;
                    Pawn pawn = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(Props.possibleGeneratePawn.RandomElementByWeight(weightSelector).kind, faction), result, map, Rot4.Random);
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
                greetTriggered = true;
            }
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref timeToSpawn, "timeToSpawn", 0);
            Scribe_Values.Look(ref greetTriggered, "greetTriggered", false);
            Scribe_Values.Look(ref latestPatrolTick, "latestPatrolTick", 180000);
        }
    }
}