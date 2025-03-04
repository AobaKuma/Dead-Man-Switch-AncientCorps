using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AncientCorps
{
    public class Projectile_Parabola : Projectile_Explosive
    {
        protected CompAfterBurner compAfterBurner;
        protected CompGuided compGuided;
        public override Vector3 ExactPosition
        {
            get
            {
                Vector3 vector = (destination - origin).Yto0() * DistanceCoveredFraction;
                return origin.Yto0() + vector + Vector3.up * def.Altitude;
            }
        }
        private float ArcHeightFactor
        {
            get
            {
                float num = def.projectile.arcHeightFactor;
                float num2 = (destination - origin).MagnitudeHorizontalSquared();
                if (Mathf.Pow(num, 2) > num2 * 0.2f * 0.2f)
                {
                    num = Mathf.Sqrt(num2) * 0.2f;
                }
                return num;
            }
        }
        protected float Progress => base.DistanceCoveredFraction;
        protected Vector3 LookTowards => new Vector3(destination.x - origin.x, def.Altitude, destination.z - origin.z + ArcHeightFactor * Accelerate);
        public override Quaternion ExactRotation => Quaternion.LookRotation(LookTowards);
        protected float Accelerate => 5f - 10f * Progress;
        public override Vector3 DrawPos => ExactPosition + new Vector3(0f, 0f, 1f) * ArcHeightFactor * GenMath.InverseParabola(Progress);

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            compAfterBurner = this.TryGetComp<CompAfterBurner>();
            compGuided = this.TryGetComp<CompGuided>();
            compAfterBurner?.ThrowLaunchSmoke(origin, (origin - destination).ToAngleFlat() + 90);
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (respawningAfterLoad)
            {
                compAfterBurner = this.TryGetComp<CompAfterBurner>();
                compGuided = this.TryGetComp<CompGuided>();
            }
        }
        public override void Tick()
        {
            Log.Message(compGuided != null);
            if (compGuided != null) Guide();
            base.Tick();
            if (Spawned && compAfterBurner != null)
            {
                compAfterBurner.drawOnProjectile = true;
                float num = ArcHeightFactor * GenMath.InverseParabola(base.DistanceCoveredFraction);
                Vector3 drawPos = DrawPos;
                Vector3 vector = drawPos + new Vector3(0f, 0f, 1f) * num;
                compAfterBurner.ThrowExhaust(vector - LookTowards.normalized * 0.1f, Progress);
            }
        }
        private Pawn TargetPawn;
        private void Guide()
        {
            if (Find.TickManager.TicksThisFrame - this.TickSpawned == 1)
            {
                Pawn firstPawn = base.DestinationCell.GetFirstPawn(base.Map);
                if (firstPawn == null)
                {
                    List<IntVec3> list = new List<IntVec3>();
                    list = GenRadial.RadialCellsAround(base.DestinationCell, 5, useCenter: true).ToList();
                    foreach (IntVec3 item in list)
                    {
                        if (item.GetFirstPawn(base.Map) != null)
                        {
                            firstPawn = item.GetFirstPawn(base.Map);
                        }
                    }
                }
                if (firstPawn != null)
                {
                    TargetPawn = firstPawn;
                }
            }
            if ((this.Progress > 0 && this.Progress < 0.9f) & (TargetPawn != null))
            {
                Log.Message($"Guiding {TargetPawn.Name}");
                destination = TargetPawn.DrawPos;
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
        }
    }
}