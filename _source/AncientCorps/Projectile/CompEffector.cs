using UnityEngine;
using Verse;

namespace AncientCorps
{
    public class CompEffector : ThingComp
    {
        private Effecter _effect;
        public CompProperties_Effector Props => props as CompProperties_Effector;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (_effect == null)
            {
                MakeEffector();
            }
        }
        public override void CompTick()
        {
            if (!parent.Spawned || parent.Map == null) return;

            if (_effect == null)
            {
                MakeEffector();
            }
            _effect.EffectTick(parent, parent);
        }
        private void MakeEffector()
        {
            _effect?.Cleanup();
            _effect = Props.effecter.SpawnAttached(parent, parent.Map);
            _effect.offset = Props.offset;
        }

        public override void Notify_DefsHotReloaded()
        {
            base.Notify_DefsHotReloaded();
            MakeEffector();
        }

        public override void PostDeSpawn(Map map)
        {
            _effect.Cleanup();
            base.PostDeSpawn(map);
        }
    }
    public class CompProperties_Effector : CompProperties
    {
        public EffecterDef effecter;
        public Vector3 offset;

        public CompProperties_Effector()
        {
            compClass = typeof(CompEffector);
        }
    }
}