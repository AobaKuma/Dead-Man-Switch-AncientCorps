using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AncientCorps
{
    //大致上是生成的更符合編制的襲擊，然後小隊是會跟著領隊的速度移動的。
    public class CompanyDef : Def
    {
        public RulePackDef NamePack;
        public IntRange squadCountRange = new IntRange(1, 2);
        public List<PlatoonDef> squads;
        public FactionDef defaultFaction;
        public int defconRating = 0;
        [NoTranslate]
        public string IconPath = "UI/Icons/Company/Default";
        public int combatRadius = 10;
        public override void ResolveReferences()
        {
            base.ResolveReferences();
            defaultFaction = DMSAC_DefOf.DMS_AncientCorps;
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
            for (int i = 0; i < squads.Count; i++)
            {
                PlatoonDef squad = squads[i];
                if (squad == null)
                {
                    yield return $"squads[{i}] is null.";
                    continue;
                }
                if (squad.leaderKindDef.NullOrEmpty())
                {
                    yield return $"squads[{i}] ({squad.defName}) has no leaderKindDef.";
                }
                if (squad.memberKindDefs.NullOrEmpty() && squad.memberCountRange.max > 0)
                {
                    yield return $"squads[{i}] ({squad.defName}) has no memberKindDefs but memberCountRange allows members.";
                }
            }
        }
        public IEnumerable<Pawn> GeneratePawns()
        {
            Faction f = Find.FactionManager.FirstFactionOfDef(defaultFaction);
            if (f == null) yield break;
            for (int i = 0; i < squadCountRange.RandomInRange; i++)
            {
                PlatoonDef squad = squads.RandomElement();
                if (squad == null) continue;
                foreach (var kind in squad.GeneratePawnKind())
                {
                    if (kind == null) continue;
                    yield return PawnGenerator.GeneratePawn(kind, f);
                }
            }
        }
    }
}
