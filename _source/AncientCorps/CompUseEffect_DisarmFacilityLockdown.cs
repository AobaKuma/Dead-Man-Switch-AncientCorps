using Fortified;
using RimWorld;
using Verse;

namespace AncientCorps
{
    /// <summary>
    /// 指揮核心的「停用本地指揮」順便解除 FFF 的設施封鎖。
    /// 核心之後會被 CompUseEffect_DestroySelf 以 Vanish 移除，而 FFF 的封鎖中控在 Vanish 時不會通知 MapComponent；
    /// 如果當下正在倒數或已封鎖，狀態就會永遠停在 Locked，既沒有中控可以駭、也沒有地表緊急解鎖（只在 Sealed 時開放）。
    /// 所以在使用當下就照「中控被駭入」處理，等同解除封鎖。
    /// The command core's "disable local command" also disarms FFF's facility lockdown. The core is then removed by
    /// CompUseEffect_DestroySelf with DestroyMode.Vanish, which FFF's controller deliberately ignores; mid-countdown or
    /// locked, the facility would stay Locked forever with no controller to hack and no surface override (that only
    /// opens once Sealed). Treating the shutdown as a controller hack disarms it instead.
    /// </summary>
    public class CompUseEffect_DisarmFacilityLockdown : CompUseEffect
    {
        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            MapComponent_FacilityLockdown lockdown = MapComponent_FacilityLockdown.For(parent.MapHeld);
            if (lockdown == null || lockdown.Controller != parent) return;
            lockdown.Notify_ControllerHacked(usedBy);
        }
    }

    public class CompProperties_UseEffect_DisarmFacilityLockdown : CompProperties_UseEffect
    {
        public CompProperties_UseEffect_DisarmFacilityLockdown()
        {
            compClass = typeof(CompUseEffect_DisarmFacilityLockdown);
        }
    }
}
