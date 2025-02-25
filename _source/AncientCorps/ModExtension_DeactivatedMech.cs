using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AncientCorps
{
    public class ModExtension_DeactivatedMech : DefModExtension
    {
        public float spawnChance = 0.25f;
        public float weaponChance = 0.25f;
        public float weaponscatterRange = 1.25f;
        public Vector3 innerPawnDrawOffset;
        public FloatRange innerPawnRandomRotation = new FloatRange(0f, 0f);
        public GraphicData bottomGraphic;
        public List<PawnGenOption> possibleGeneratePawn;
        public IntRange age;
    }
}