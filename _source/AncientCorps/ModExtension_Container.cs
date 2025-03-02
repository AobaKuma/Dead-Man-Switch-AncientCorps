using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AncientCorps
{
    public class ModExtension_Container : DefModExtension
    {
        public SoundDef sound;

        public GraphicData openedGraphicdata;

        public List<ThingSetMakerDef> loots = new List<ThingSetMakerDef>();
    }

}