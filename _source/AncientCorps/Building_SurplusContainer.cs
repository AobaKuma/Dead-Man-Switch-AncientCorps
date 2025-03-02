using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AncientCorps
{
    public class Building_SurplusContainer : Building_Casket
    {
        public int tickToOpen = 200;

        public bool initialized = false;

        public Graphic openedGraphic = null;

        public ModExtension_Lootbox Extension => def.GetModExtension<ModExtension_Lootbox>();

        public override Graphic Graphic
        {
            get
            {
                if (base.HasAnyContents)
                {
                    return base.Graphic;
                }
                if (openedGraphic == null)
                {
                    Graphic graphic = base.Graphic;
                    if (Extension.openedGraphicdata != null)
                    {
                        openedGraphic = Extension.openedGraphicdata.GraphicColoredFor(this);
                        return openedGraphic;
                    }
                    openedGraphic = base.Graphic;
                }
                return openedGraphic;
            }
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            contentsKnown = false;
            if (initialized) return;
            Extension.loots.RandomElement().root.Generate().ForEach(delegate (Thing t)
            {
                if (t.Spawned)
                {
                    t.DeSpawn();
                }
                innerContainer.TryAddOrTransfer(t);
            });
            initialized = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref initialized, "initialized", defaultValue: false);
            Scribe_Values.Look(ref tickToOpen, "tickToOpen", 0);
        }
    }
    public class ModExtension_Lootbox : DefModExtension
    {
        public SoundDef sound;

        public GraphicData openedGraphicdata;

        public List<ThingSetMakerDef> loots = new List<ThingSetMakerDef>();
    }

}