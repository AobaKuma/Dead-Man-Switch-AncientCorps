using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace AncientCorps
{
    public class CompRandomColorOnSpawn : ThingComp
    {

        public CompProperties_RandomColorOnSpawn Props => (CompProperties_RandomColorOnSpawn)this.props;
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if(!respawningAfterLoad)
            {
                if (!Rand.Chance(Props.colorChance)) return;
                if (parent is Building building)
                {   if (Props.colorDefs.NullOrEmpty()) return;
                    building.ChangePaint(Props.colorDefs.RandomElement());
                }
                else 
                {
                    if (Props.colorGenerator == null) return;
                    this.parent.SetColor(Props.colorGenerator.NewRandomizedColor()); 
                }
            }
        }
    }
    public class CompProperties_RandomColorOnSpawn : CompProperties
    {
        public CompProperties_RandomColorOnSpawn() { this.compClass = typeof(CompRandomColorOnSpawn); }
        public ColorGenerator colorGenerator = null;
        public float colorChance = 0.5f;
        public List<ColorDef> colorDefs = new List<ColorDef>();
    }
}

