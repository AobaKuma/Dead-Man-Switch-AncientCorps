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
            PawnKindDef leader = leaderKindDef.RandomElementWithFallback();
            if (leader != null) yield return leader;
            if (!fixedPawnkind.NullOrEmpty())
            {
                foreach (var item in fixedPawnkind)
                {
                    if (item?.kind == null) continue;
                    // selectionWeight 在固定的班組編制裡是「數量」，用整數解釋（XML 一律寫整數）。
                    int count = (int)item.selectionWeight;
                    for (int i = 0; i < count; i++)
                    {
                        yield return item.kind;
                    }
                }
            }
            if (!memberKindDefs.NullOrEmpty())
            {
                int members = memberCountRange.RandomInRange;
                for (int i = 0; i < members; i++)
                {
                    PawnKindDef kind = memberKindDefs.RandomElement();
                    if (kind != null) yield return kind;
                }
            }
        }
    }
}
