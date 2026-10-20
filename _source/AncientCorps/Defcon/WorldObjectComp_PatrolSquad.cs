using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
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
        // 取樣間隔：IsHashIntervalTick 需要固定常數，所以先用它做節流，再用絕對 tick 判定是否該出巡。
        // Fixed sampling interval: IsHashIntervalTick needs a constant, so it only throttles; the actual
        // schedule is an absolute tick stamp.
        private const int SampleInterval = 250;

        private bool greetTriggered = false;
        // 舊版欄位（相對 tick 數），只保留讀檔相容。Legacy field kept for save compatibility only.
        public int timeToSpawn = 0;
        private int nextPatrolTick = 0;
        protected int latestPatrolTick = 180000;

        protected GameComponent_DefconLevel Defcon => Current.Game?.GetComponent<GameComponent_DefconLevel>();
        private int Level => Defcon?.Level ?? 0;
        private float Scale => Mathf.Max(Defcon?.GetCurrentScale ?? 1f, 0.1f);
        protected int countPartol = 0;

        public WorldObjectCompProperties_PatrolSquad Props => props as WorldObjectCompProperties_PatrolSquad;

        public override void PostMapGenerate()
        {
            base.PostMapGenerate();
            ScheduleNext();
        }

        /// <summary>
        /// 依目前警戒等級排下一次巡邏。scale 越小（警戒越高）間隔越短。
        /// Schedule the next patrol; the smaller the DEFCON scale, the sooner it comes.
        /// </summary>
        private void ScheduleNext(int forcedTicks = 0)
        {
            int min = Mathf.Max(Props?.MBTTime ?? 30000, 1);
            int max = Mathf.Max(latestPatrolTick, min + 1);
            int baseTicks = forcedTicks > 0 ? forcedTicks : new IntRange(min, max).RandomInRange;
            int scaled = Mathf.Max(Mathf.RoundToInt(baseTicks * Scale), min);
            nextPatrolTick = Find.TickManager.TicksGame + scaled;
            timeToSpawn = scaled;
            latestPatrolTick = Mathf.RoundToInt(Mathf.Clamp(timeToSpawn * Rand.Range(0.25f, 0.75f), min, timeToSpawn));
        }

        public override void CompTick()
        {
            if (!base.ParentHasMap) return;
            if (!parent.IsHashIntervalTick(SampleInterval)) return;
            if (nextPatrolTick <= 0)
            {
                ScheduleNext();
                return;
            }
            if (Find.TickManager.TicksGame < nextPatrolTick) return;
            SpawnPatrol();
            ScheduleNext();
        }

        public void SpawnPatrol()
        {
            Map map = ((MapParent)parent).Map;
            if (map == null) return;
            Faction faction = AncientCorpsUltility.Corps;
            if (faction == null) return;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var result, map, CellFinder.EdgeRoadChance_Animal + 0.2f)) return;

            float weightSelector(PawnGenOption PawnGenOption) => PawnGenOption.selectionWeight;
            List<Pawn> group = new List<Pawn>();

            int pawnCount = Mathf.Max(1, Mathf.RoundToInt(Rand.Gaussian(Props.averagePawnCount - 1, Props.spawnCountSigma / Scale) + 1));
            for (int i = 0; i < pawnCount; i++)
            {
                PawnGenOption option = Props.possibleGeneratePawn.RandomElementByWeight(weightSelector);
                if (option?.kind == null) continue;
                group.Add((Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(option.kind, faction), result, map, Rot4.Random));
            }
            // Defcon I 及以上會加派中型機兵
            if (Level >= 1 && !Props.possibleGeneratePawn_Medium.NullOrEmpty())
            {
                int mediumCount = Mathf.CeilToInt(3f / Scale);
                for (int i = 0; i < mediumCount; i++)
                {
                    PawnGenOption option = Props.possibleGeneratePawn_Medium.RandomElementByWeight(weightSelector);
                    if (option?.kind == null) continue;
                    group.Add((Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(option.kind, faction), result, map, Rot4.Random));
                }
            }
            // Defcon III 及以上會加派重型機兵（原本寫 Level > 3，和註解差一級）
            if (Level >= 3 && !Props.possibleGeneratePawn_Heavy.NullOrEmpty())
            {
                int heavyCount = Mathf.CeilToInt(1f / Scale);
                for (int i = 0; i < heavyCount; i++)
                {
                    PawnGenOption option = Props.possibleGeneratePawn_Heavy.RandomElementByWeight(weightSelector);
                    if (option?.kind == null) continue;
                    group.Add((Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(option.kind, faction), result, map, Rot4.Random));
                }
            }
            if (group.Count == 0) return;

            // 原本會撿「地圖上任何一個」StageThenAttack lord，可能把巡邏隊併進別派系的襲擊。
            // Only reuse a StageThenAttack lord of our own faction.
            Lord lord = map.lordManager.lords.Find((Lord l2) =>
                l2.LordJob is LordJob_StageThenAttack && l2.faction == faction);
            if (lord == null)
            {
                lord = LordMaker.MakeNewLord(faction, new LordJob_StageThenAttack(faction, map.Center, Rand.Range(1, 10)), map, group);
            }
            else
            {
                lord.AddPawns(group);
            }
            if (!greetTriggered)
            {
                AncientCorpsUltility.SetHostileToPlayer(map, group.RandomElement());
            }
            else
            {
                //一支巡邏部隊已經抵達設施，更多的後續部隊將在更短的時間內陸續增援。
                Find.LetterStack.ReceiveLetter("DMSAC_PatrolSquad".Translate(), "DMSAC_PatrolSquadDesc".Translate(), LetterDefOf.ThreatSmall);
                countPartol++;
                if (countPartol >= Props.patrolCountToRaiseLevel)
                {
                    Defcon?.LevelRise();
                    countPartol = 0;
                }
                Defcon?.ResetActionInterval();
            }
            greetTriggered = true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextPatrolTick, "nextPatrolTick", 0);
            Scribe_Values.Look(ref timeToSpawn, "timeToSpawn", 0);
            Scribe_Values.Look(ref greetTriggered, "greetTriggered", false);
            Scribe_Values.Look(ref countPartol, "countPartol", 0);
            Scribe_Values.Look(ref latestPatrolTick, "latestPatrolTick", 180000);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && nextPatrolTick <= 0)
            {
                // 舊存檔只有相對 tick：換算成絕對時間點，避免一讀檔就立刻出巡。
                ScheduleNext(Mathf.Max(timeToSpawn, Props?.MBTTime ?? 30000));
            }
        }
    }
}
