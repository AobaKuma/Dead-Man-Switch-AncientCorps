using RimWorld;
using UnityEngine;
using Verse;
using Verse.Noise;
using System.Reflection;
using System;

namespace AncientCorps
{
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
        public void ThrowExhaust(Vector3 drawPos, float evaluate)
        {
            if (parent.Position.ShouldSpawnMotesAt(parent.MapHeld))
            {
                if (Props.ExhaustFleck != null)
                {
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(drawPos, Projectile.MapHeld, Props.ExhaustFleck);
                    dataStatic.scale = Props.ExhaustCurve.Evaluate(evaluate);
                    dataStatic.rotationRate = Rand.Range(-60, 60);
                    dataStatic.velocityAngle = Vector3.Angle(postPosition,drawPos);
                    dataStatic.velocitySpeed = dataStatic.scale * Projectile.def.projectile.SpeedTilesPerTick;
                    dataStatic.solidTimeOverride = 0.2f * (1f - (evaluate + 0.1f));
                    parent.MapHeld.flecks.CreateFleck(dataStatic);
                }
                if (Props.SmokeFleck != null)
                {
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(drawPos, Projectile.MapHeld, Props.SmokeFleck);
                    dataStatic.scale = Props.SmokeCurve.Evaluate(evaluate);
                    dataStatic.rotationRate = Rand.Range(-30, 30);
                    dataStatic.velocityAngle = Rand.Range(-180, 180);
                    dataStatic.velocitySpeed = Mathf.Clamp01(1 - dataStatic.scale);
                    parent.MapHeld.flecks.CreateFleck(dataStatic);
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

