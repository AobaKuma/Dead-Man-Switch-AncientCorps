using RimWorld;
using System.Linq;
using Verse;

namespace AncientCorps
{
    public class KrepostAttackTransition : MusicTransition
    {
        public override bool IsTransitionSatisfied()
        {
            if (!base.IsTransitionSatisfied())
            {
                return false;
            }
            return Find.BossgroupManager.ReservedByBossgroup(bossgroupKidDef: DMS_DefOf.DMS_Mech_Krepost);
        }
    }

    public class CompUseEffect_PlaySong : CompUseEffect
    {
        private int delayTicks = -1;
        private CompProperties_UseEffectPlaySong Props => (CompProperties_UseEffectPlaySong)props;
        public override float OrderPriority => Props.orderPriority;

        public override void DoEffect(Pawn usedBy)
        {
            if(usedBy.Map == Find.CurrentMap)
            {
                if (Props.delayTicks <= 0)
                {
                    PlaySong();
                }
                else
                {
                    Find.MusicManagerPlay.ForceFadeoutAndSilenceFor(Props.delayTicks, 250,true);
                    delayTicks = Props.delayTicks;
                }
            }
        }
        public override void CompTick()
        {
            base.CompTick();
            if (delayTicks > 0)
            {
                delayTicks--;
            }
            if (delayTicks == 0)
            {
                PlaySong();
                delayTicks = -1;
            }
        }
        public void PlaySong()
        {
            if (Find.MusicManagerPlay.CurrentSong != Props.song) Find.MusicManagerPlay.ForcePlaySong(Props.song, ignorePrefsVolume: false);
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref delayTicks, "delayTicks", -1);
        }
    }
    public class CompProperties_UseEffectPlaySong : CompProperties_UseEffect
    {
        public SongDef song;
        public int delayTicks = 275;
        public float orderPriority = -999f;
        public CompProperties_UseEffectPlaySong()
        {
            compClass = typeof(CompUseEffect_PlaySong);
        }
    }
}

