using System.Collections.Generic;
using Verse;

namespace AncientCorps
{
    public class CompSpawnChance : ThingComp
    {
        public CompProperties_SpawnChance Props => (CompProperties_SpawnChance)props;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && !DebugSettings.godMode && !Rand.Chance(Props.spawnChance))
            {
                parent.DeSpawnOrDeselect();
                parent.Destroy();
            }
        }
    }
    public class CompProperties_SpawnChance : CompProperties
    {
        public float spawnChance = 1f;
        public CompProperties_SpawnChance()
        {
            compClass = typeof(CompSpawnChance);
        }
    }
}