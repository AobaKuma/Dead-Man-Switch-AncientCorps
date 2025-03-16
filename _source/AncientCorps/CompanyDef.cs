using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace AncientCorps
{
    //大致上是生成的更符合編制的襲擊，然後小隊是會跟著領隊的速度移動的。
    public class CompanyDef : Def
    {
        public RulePackDef NamePack;
        public int count = 1;
        public List<PlatoonMaker> squads;
        public FactionDef defaultFaction = DMS_DefOf.DMS_AncientCorps;

        public override void ResolveReferences()
        {
            base.ResolveReferences();
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string item in base.ConfigErrors())
            {
                yield return item;
            }
            if (squads.NullOrEmpty())
            {
                yield return "squads is null.";
                yield break;
            }
        }
    }
    [Serializable]
    public class PlatoonMaker
    {
        public RulePackDef nameDef;
        public List<PawnKindDef> leaderKindDef;

        public List<PawnKindDef> fixedPawnkind;
        public IntRange memberCountRange;
        public List<PawnKindDef> memberKindDefs;

        public IEnumerable<PawnKindDef> GeneratePawnKind()
        {
            yield return leaderKindDef.RandomElement();
            if (!fixedPawnkind.NullOrEmpty())
                foreach (PawnKindDef item in fixedPawnkind)
                {
                    yield return item;
                }
            for (int i = 0; i < memberCountRange.RandomInRange; i++)
            {
                yield return leaderKindDef.RandomElement();
            }
        }
    }
}