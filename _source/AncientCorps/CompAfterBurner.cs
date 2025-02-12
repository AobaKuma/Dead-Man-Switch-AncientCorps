using RimWorld;
using UnityEngine;
using Verse;
using Verse.Noise;
using System.Reflection;
using System;

namespace AncientCorps
{
    public class CompLaunchEffect : ThingComp
    { 
    
    }
    public class CompAfterBurner : ThingComp
    {
        public CompProperties_AfterBurner Props => (CompProperties_AfterBurner)this.props;
        Projectile Projectile => parent as Projectile;
        public bool drawOnProjectile = false;
        private int lifeTime = 0;
        //Vector3 originVector = Vector3.zero;
        //Vector3 destinationVector = Vector3.zero;

        private Vector3 postPosition = Vector3.zero;
        public override void PostPostMake()
        {
            base.PostPostMake();
            lifeTime = Props.lifeTime;
        }
        public override void CompTick()
        {
            if (drawOnProjectile) return;
            if (parent.Spawned && lifeTime > 0)
            {

                ThrowExhaust(parent.DrawPos, 1 - (lifeTime / Props.lifeTime));
                lifeTime--;
            }
        }
        public void ThrowLaunchSmoke(Vector3 drawPos, float angle)
        {
            if (parent.Position.ShouldSpawnMotesAt(parent.MapHeld))
            {
                float rawAngle = angle;
                for (int i = 0; i < 8; i++)
                {
                    float variantion = Rand.Range(-60, 60);
                    if (Props.SmokeFleck != null)
                    {
                        FleckCreationData dataStatic = FleckMaker.GetDataStatic(drawPos, Projectile.MapHeld, Props.SmokeFleck);
                        dataStatic.spawnPosition = drawPos + CircleConst.GetAngle(rawAngle + variantion) * 2f;
                        dataStatic.orbitSnapStrength = 0.1f;
                        dataStatic.scale = Rand.Range(0.5f, 4);
                        dataStatic.rotationRate = Rand.Range(-5, 5);
                        dataStatic.velocityAngle = rawAngle + variantion;
                        dataStatic.velocitySpeed = (4.01f - dataStatic.scale) * 2;
                        dataStatic.targetSize = 5f;
                        parent.MapHeld.flecks.CreateFleck(dataStatic);
                    }
                    if (Props.ExhaustFleck != null)
                    {
                        FleckCreationData dataStatic = FleckMaker.GetDataStatic(drawPos, Projectile.MapHeld, Props.ExhaustFleck);
                        dataStatic.spawnPosition = drawPos + CircleConst.GetAngle(rawAngle) * 2f;
                        dataStatic.scale = Rand.Range(1f, 3);
                        dataStatic.velocityAngle = rawAngle;
                        dataStatic.velocitySpeed = (5 - dataStatic.scale) * 4;
                        dataStatic.targetSize = 0f;
                        parent.MapHeld.flecks.CreateFleck(dataStatic);
                    }
                }
            }
        }
        public void ThrowExhaust(Vector3 drawPos, float evaluate)
        {
            if (parent.Position.ShouldSpawnMotesAt(parent.MapHeld))
            {
                for (int i = 0; i < 3; i++)
                {
                    if (Props.ExhaustFleck != null)
                    {
                        FleckCreationData dataStatic = FleckMaker.GetDataStatic(drawPos, Projectile.MapHeld, Props.ExhaustFleck);
                        dataStatic.scale = Rand.Range(0, Props.ExhaustCurve.Evaluate(evaluate));
                        dataStatic.rotationRate = Rand.Range(-60, 60);
                        dataStatic.velocityAngle = Vector3.Angle(postPosition, drawPos);
                        dataStatic.velocitySpeed = dataStatic.scale * Projectile.def.projectile.SpeedTilesPerTick;
                        dataStatic.solidTimeOverride = 0.2f * (1f - (evaluate + 0.1f));
                        parent.MapHeld.flecks.CreateFleck(dataStatic);
                    }
                    if (Props.SmokeFleck != null)
                    {
                        FleckCreationData dataStatic = FleckMaker.GetDataStatic(drawPos, Projectile.MapHeld, Props.SmokeFleck);
                        dataStatic.scale = Rand.Range(0, Props.SmokeCurve.Evaluate(evaluate));
                        dataStatic.rotationRate = Rand.Range(-30, 30);
                        dataStatic.velocityAngle = Rand.Range(-180, 180);
                        dataStatic.velocitySpeed = Mathf.Clamp01(1 - dataStatic.scale);
                        parent.MapHeld.flecks.CreateFleck(dataStatic);
                    }
                }
                postPosition = drawPos;
            }
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lifeTime, "AfterBurner_LifeTime");
            Scribe_Values.Look(ref postPosition, "AfterBurner_postPosition");
        }
    }
    public class CompProperties_AfterBurner : CompProperties
    {
        public CompProperties_AfterBurner() { this.compClass = typeof(CompAfterBurner); }
        public int lifeTime = 60;
        public FleckDef ExhaustFleck;
        public SimpleCurve ExhaustCurve;
        public FleckDef SmokeFleck;
        public SimpleCurve SmokeCurve;
    }
}

