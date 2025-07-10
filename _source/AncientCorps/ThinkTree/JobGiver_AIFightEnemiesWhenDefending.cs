using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AncientCorps
{
    public class JobGiver_AIFightEnemiesWhenDefending :  JobGiver_AIFightEnemy
    {
        protected override bool ExtraTargetValidator(Pawn pawn, Thing target)
        {
            if (!base.ExtraTargetValidator(pawn, target))
            {
                return false;
            }
            if (target is Pawn pawnTarget)
            {
                if (!HasWeapon(pawnTarget) && (pawn.Position - pawnTarget.Position).SqrMagnitude >= 5 * 5)
                {
                    return false;
                }
            }
            return true;
        }
        private static bool HasWeapon(Pawn pawn)
        {
            if (pawn.equipment?.Primary != null)
            {
                return true;
            }
            foreach (var comp in pawn.AllComps)
            {
                if (comp.GetType().IsAssignableFrom(typeof(CompTurretGun)) && (comp as CompTurretGun).AutoAttack)
                {
                    return true;
                }
            }
            //背包炮塔没见过
            return false;
        }
        protected override bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verbToUse = null)
        {
            Thing enemyTarget = pawn.mindState.enemyTarget;
            bool allowManualCastWeapons = !pawn.IsColonist && !pawn.IsSubhuman;
            Verb verb = verbToUse ?? pawn.TryGetAttackVerb(enemyTarget, allowManualCastWeapons, this.allowTurrets);
            if (verb == null)
            {
                dest = IntVec3.Invalid;
                return false;
            }
            return CastPositionFinder.TryFindCastPosition(new CastPositionRequest
            {
                caster = pawn,
                target = enemyTarget,
                verb = verb,
                maxRangeFromTarget = verb.verbProps.range,
                wantCoverFromTarget = (verb.verbProps.range > 5f),
                validator = vec3 =>
                {
                    Pawn pawnToFollow = (pawn.GetLord().LordJob as LordJob_DefendTargetAndAssaultColony)?.defendedTarget;
                    if (pawnToFollow == null || !pawnToFollow.Spawned||pawnToFollow.Downed || pawnToFollow.Position.InHorDistOf(vec3, JobGiver_FollowDefendedTarget.FollowDistance) )
                    {
                        return true;
                    }
                    return false;
                }
            }, out dest);
        }
    }
}