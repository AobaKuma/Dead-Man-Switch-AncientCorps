using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AncientCorps
{
    public class CompAbilityEffect_ActiveProtectionSystem : CompAbilityEffect
    {
        private new CompProperties_ActiveProtectionSystem Props => (CompProperties_ActiveProtectionSystem)props;

        private Pawn Pawn => parent.pawn;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            isActive = true;
            tickRemain = Props.activeTicks;

        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            if (Pawn.Faction == Faction.OfPlayer)
            {
                return false;
            }
            if (parent.CanCast && !parent.Casting)
            {
                foreach (IntVec3 cell in GenAdj.OccupiedRect(Pawn).ExpandedBy(Props.Size))
                {
                    List<Thing> list = Pawn.MapHeld.thingGrid.ThingsListAt(cell).Where((v) => v is Projectile).ToList();
                    for (int i = 0; i < list.Count; i++)
                    {
                        Thing thing2 = list[i];
                        if (IsTargetProjectile(thing2))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        private int count;
        private bool isActive;
        protected int tickRemain;
        public override void CompTick()
        {
            if (!isActive) return;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(Pawn).ExpandedBy(Props.Size))
            {
                List<Thing> list = Pawn.MapHeld.thingGrid.ThingsListAt(cell).Where((v) => v is Projectile).ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    Thing thing2 = list[i];
                    if (IsTargetProjectile(thing2) && Vector3.Distance(thing2.DrawPos, Pawn.DrawPos) < Props.Size)
                    {
                        if (Rand.Range(0f, 1f) > Props.chanceToFail)
                            DoIntercept(thing2 as Projectile);
                    }
                }
            }
            tickRemain--;
            if (tickRemain <= 0)
            {
                isActive = false;
            }
        }
        public override string CompInspectStringExtra()
        {
            if (isActive)
                return "DMSAC_APS_TickRemain:{0}".Translate(tickRemain.TicksToSeconds());
            else
                return base.CompInspectStringExtra();
        }
        private void DoIntercept(Projectile target)
        {
            if (target == null) return;
            if (Props.fleckDef != null)
            {
                var projectile = target as Projectile;
                if (Pawn.DrawPos.ShouldSpawnMotesAt(Pawn.Map, false))
                {
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(target.DrawPos, target.Map, Props.fleckDef, Rand.Range(0.5f, 1.5f));
                    dataStatic.rotation = target.Rotation.AsAngle;
                    dataStatic.targetSize = 0;
                    dataStatic.velocityAngle = target.Rotation.AsAngle;
                    dataStatic.velocity = projectile.Rotation.AsVector2;
                    dataStatic.velocitySpeed = Rand.Range(target.def.projectile.speed / 2, target.def.projectile.speed);
                    Pawn.Map.flecks.CreateFleck(dataStatic);
                }
            }
            if (Props.spawnLeaving != null)
            {
                if (Rand.Range(0f, 1f) > 0.8f)
                    GenSpawn.Spawn(Props.spawnLeaving, target.Position, target.Map);
            }
            target.Destroy();
        }
        private bool IsTargetProjectile(Thing target)
        {
            if (target is null) return false;

            if (target is Projectile)
            {
                if (target.def.defName.Contains("rocket") || target.def.defName.Contains("missile") || target.def.defName.Contains("grenade"))
                {
                    if (Props.ignoreThings.Contains(target.def.defName)) return false;
                    return true;
                }
                if (!Props.interceptThings.Where((v => target.def.defName == v)).FirstOrDefault().NullOrEmpty()) return true;
            }
            return false;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isActive, "isActive", true);
            Scribe_Values.Look(ref tickRemain, "tickRemain", 100);
        }
    }
    public class CompProperties_ActiveProtectionSystem : CompProperties_AbilityEffect
    {
        public int Size = 6;
        public EffecterDef effecterDef;

        public FleckDef fleckDef;
        public ThingDef spawnLeaving;
        public float chanceToFail = 0.8f;
        public int activeTicks = 1500;
        public List<string> interceptThings = new List<string>();
        public List<string> ignoreThings = new List<string>();

        public CompProperties_ActiveProtectionSystem()
        {
            compClass = typeof(CompAbilityEffect_ActiveProtectionSystem);
        }
    }
}
