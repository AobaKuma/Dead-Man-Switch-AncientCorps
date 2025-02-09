using UnityEngine;
using Verse;

namespace AncientCorps
{
    public class Projectile_Parabola : Projectile_Explosive
    {
        protected CompAfterBurner compAfterBurner;
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
        }
        public override void Tick()
        {
            base.Tick();
            if (Spawned && compAfterBurner != null)
            {
                compAfterBurner.drawOnProjectile = true;
                float num = ArcHeightFactor * GenMath.InverseParabola(base.DistanceCoveredFraction);
                Vector3 drawPos = DrawPos;
                Vector3 vector = drawPos + new Vector3(0f, 0f, 1f) * num;
                compAfterBurner.ThrowExhaust(vector + LookTowards.normalized * 0.05f, Progress);
            }
        }
    }
}
