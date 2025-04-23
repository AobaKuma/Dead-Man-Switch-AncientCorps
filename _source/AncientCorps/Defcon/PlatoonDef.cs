using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AncientCorps
{
    public class PlatoonDef : Def
    {
        public List<PawnKindDef> leaderKindDef;
        public List<PawnGenOption> fixedPawnkind;
        public IntRange memberCountRange;
        public List<PawnKindDef> memberKindDefs;

        public IEnumerable<PawnKindDef> GeneratePawnKind()
        {
            yield return leaderKindDef.RandomElement();
            foreach (var item in fixedPawnkind)
            {
                for (int i = 0; i < (int)item.selectionWeight; i++)
                {
                    yield return item.kind;
                }
            }
            for (int i = 0; i < memberCountRange.RandomInRange; i++)
            {
                yield return memberKindDefs.RandomElement();
            }
        }
    }
}