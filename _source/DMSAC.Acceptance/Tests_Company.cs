using System.Collections.Generic;
using System.Linq;
using AncientCorps;
using Fortified;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using static DMSAC.Acceptance.Fixture;

namespace DMSAC.Acceptance
{
    public static class Tests_Company
    {
        /// <summary>R4: deploying a company and every order (hold, takeover, raze, split, withdraw, roam).</summary>
        public static IEnumerable<Wait> R4_DeployAndOrders(TestContext t)
        {
            ReadinessDef def = Defcon;
            SetLevelQuiet(3);
            List<Settlement> targets = HostileSettlements();
            t.Require(targets.Count >= 3, $"three settlements hostile to the corps ({targets.Count})");

            Settlement target = targets[0];
            FFF_Company company = CompanyUtility.Deploy(def, State, target);
            t.Require(company != null, "company deployed");
            t.Check(company is Company, "the camp is an AncientCorps.Company");
            t.Check(company.Faction == Corps, "owned by the corps");
            t.Check(company.CompanyDef != null && company.CompanyDef.minLevel <= 3, $"company def allowed at level III ({company.CompanyDef?.defName})");
            t.Check(company.Target == target, "targets the settlement");
            float distance = Distance(company.Tile, target.Tile);
            t.Check(distance <= company.CompanyDef.combatRadius + 1f, $"camps within the combat radius ({distance:0.0} ≤ {company.CompanyDef.combatRadius})");
            t.Check(!IsLandmark(company.Tile), "not on a landmark tile");
            t.Check(Find.WorldObjects.ObjectsAt(company.Tile).Count() == 1, "alone on its tile");
            t.Check(company.parts.Any(p => p.def.defName == "DMSAC_GarrisonSite"), "has the garrison site part");
            t.Check(!company.CompanyName.NullOrEmpty() && company.Label == company.CompanyName.RawText, $"named from its name pack ({company.Label})");
            t.Check(company.GetDescription().Contains(target.Name), "description names the target");

            ForceOrders(hold: 1f);
            PlanetTile tile = company.Tile;
            company.IssueOrder();
            t.Check(company.Target == target && company.Tile == tile, "hold: nothing changes");

            ForceOrders(takeover: 1f);
            PlanetTile targetTile = target.Tile;
            company.IssueOrder();
            Settlement captured = Find.WorldObjects.SettlementBases.FirstOrDefault(s => s.Tile == targetTile);
            t.Check(target.Destroyed, "takeover: the old settlement is gone");
            t.Check(captured != null && captured.Faction == Corps, "takeover: a corps settlement stands on the same tile");
            t.Check(captured != null && captured.Name != target.Name, "takeover: renamed");
            t.Check(!company.HasTarget, "takeover: the company is free again");
            float beside = Distance(company.Tile, targetTile);
            if (!t.Check(beside <= 1.5f, $"takeover: moved next to the captured settlement ({beside:0.0})")) { t.Note("no free neighbour tile, company stayed"); }

            Settlement second = targets[1];
            PlanetTile secondTile = second.Tile;
            t.Require(company.SetTarget(second), "re-targeted the second settlement");
            ForceOrders(raze: 1f);
            company.IssueOrder();
            t.Check(second.Destroyed, "raze: settlement destroyed");
            t.Check(Find.WorldObjects.AllWorldObjects.OfType<DestroyedSettlement>().Any(d => d.Tile == secondTile), "raze: ruins left on its tile");

            Settlement third = targets[2];
            t.Require(company.SetTarget(third), "re-targeted the third settlement");
            int before = CompanyUtility.CompaniesOf(Corps).Count;
            ForceOrders(split: 1f);
            company.IssueOrder();
            List<FFF_Company> all = CompanyUtility.CompaniesOf(Corps);
            t.Check(all.Count == before + 1, "split: a second company appeared");
            t.Check(all.Any(c => c != company && c.Target == third), "split: it took over the target");
            t.Check(!company.HasTarget, "split: the original moved on");

            FFF_Company other = all.First(c => c != company);
            SetField(other, "targetSettlement", null);
            ForceOrders();
            PlanetTile from = other.Tile;
            other.IssueOrder();
            t.Check(!other.Destroyed, "roam: an idle company with no disband roll stays");
            t.Check(Distance(other.Tile, from) <= other.CompanyDef.combatRadius / 2 + 1f, "roam: stays within half its combat radius");
            t.Check(!IsLandmark(other.Tile), "roam: never onto a landmark");

            ForceOrders(idleDisband: 1f);
            company.IssueOrder();
            t.Check(company.Destroyed, "withdraw: an idle company disbands");
            yield return new WaitFrames(1);
        }

        /// <summary>R5: company cap in peacetime, war declaration, campaign deployment, huntdown, and the end of the war.</summary>
        public static IEnumerable<Wait> R5_WarCampaign(TestContext t)
        {
            ReadinessDef def = Defcon;
            SetLevelQuiet(1);
            List<Settlement> targets = HostileSettlements();
            t.Require(targets.Count > 0, "settlements hostile to the corps exist");
            for (int i = 0; i < 6; i++) { CompanyUtility.Deploy(def, State, targets[i % targets.Count]); }
            int deployed = CompanyUtility.CompaniesOf(Corps).Count;
            t.Require(deployed > def.levels[1].maxCompanies, $"more companies than the level I cap ({deployed})");
            CompanyUtility.MaintainCompanies(def, State);
            t.Check(CompanyUtility.CompaniesOf(Corps).Count == def.levels[1].maxCompanies, "peacetime trims to the cap");
            new ReadinessAction_DisbandCompanies().TryRun(def, State);

            int letters = LetterCount;
            ReadinessUtility.SetLevel(def, 5, "test");
            Freeze();
            Faction foe = State.warTarget;
            t.Require(foe != null, "level V declares a war");
            t.Check(foe.def.humanlikeFaction && !foe.IsPlayer && foe.HostileTo(Corps), $"war target is a hostile humanlike faction ({foe.Name})");
            t.Check(LettersSince(letters).Any(l => LabelIs(l, "DMSAC_World_Declaration")), "declaration letter sent");
            t.Check(HuntdownUtility.IsActive(Huntdown), "level V starts the huntdown");

            int enemySettlements = Find.WorldObjects.SettlementBases.Count(s => s.Faction == foe);
            CompanyUtility.MaintainCompanies(def, State);
            List<FFF_Company> companies = CompanyUtility.CompaniesOf(Corps);
            t.Check(companies.Count >= 1 && companies.Count <= def.levels[5].maxCompanies, $"wartime deploys companies ({companies.Count})");
            t.Check(companies.All(c => c.Target != null && c.Target.Faction == foe), "every company targets the enemy");
            int distinct = companies.Select(c => c.Target).Distinct().Count();
            t.Check(distinct == System.Math.Min(companies.Count, enemySettlements), $"objectives spread over untargeted settlements first ({distinct} distinct)");

            ReadinessUtility.Drop(def, 1, "test");
            Freeze();
            t.Check(!HuntdownUtility.IsActive(Huntdown), "dropping below V stops the huntdown");
            t.Check(State.warTarget == foe, "the war goes on below V");

            letters = LetterCount;
            def.Worker.EndWar(State);
            Freeze();
            t.Check(State.warTarget == null, "EndWar clears the target");
            t.Check(State.level == def.levelAfterWar, $"EndWar falls back to level {def.levelAfterWar}");
            t.Check(LettersSince(letters).Any(l => LabelIs(l, "DMSAC_World_WarOver")), "war-over letter sent");
            yield return new WaitFrames(1);
        }

        /// <summary>R6: a company raid on the home map, with the corps lord job and letter.</summary>
        public static IEnumerable<Wait> R6_CompanyRaid(TestContext t)
        {
            CompanyDef company = CorpsCompanyDefs.First(c => c.minLevel <= 2);
            int letters = LetterCount;
            t.Check(CompanyUtility.RaidMap(Home, company, Corps), $"RaidMap with {company.defName}");
            List<Pawn> raiders = Home.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Corps).ToList();
            t.Check(raiders.Count > 0, $"raiders spawned ({raiders.Count})");
            t.Check(raiders.Any(p => company.IsLeaderKind(p.kindDef)), "platoon leaders present");
            List<Lord> lords = raiders.Select(p => p.GetLord()).Distinct().ToList();
            t.Check(lords.All(l => l?.LordJob is LordJob_DefendTargetAndAssaultColony), "each platoon guards its commander (corps lord job)");
            string prefix = Tr("DMSAC_CompanyRaid").Split('{')[0];
            t.Check(LettersSince(letters).Any(l => l.def == LetterDefOf.ThreatBig && l.Label.RawText.StartsWith(prefix)), "raid letter from the corps keys");
            yield return new WaitFrames(1);
        }

        /// <summary>R10: entering a company camp raises DEFCON, spawns the whole company as garrison, and the camp leaves without a commander.</summary>
        public static IEnumerable<Wait> R10_CompanyCampMap(TestContext t)
        {
            ReadinessDef def = Defcon;
            SetLevelQuiet(2);
            FFF_Company company = CompanyUtility.Deploy(def, State, HostileSettlements().First());
            t.Require(company != null, "company deployed");
            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(company.Tile, null);
            t.Require(map != null, "camp map generated");
            t.Check(State.level == 3, "entering the camp raised DEFCON by one");
            Freeze();
            List<Pawn> garrison = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Corps).ToList();
            t.Check(garrison.Count > 0, $"garrison spawned ({garrison.Count})");
            t.Check(garrison.Any(p => p.GetLord()?.LordJob is LordJob_MechanoidsDefend), "garrison defends the camp");
            t.Check(company.CompanyDef.Worker.HasCommander(map, Corps), "a commander is present");
            t.Note($"corps buildings on the camp map: {map.listerThings.AllThings.Count(x => x is Building && x.Faction == Corps)}");
            foreach (Pawn leader in garrison.Where(p => company.CompanyDef.IsLeaderKind(p.kindDef)).ToList()) { leader.Destroy(DestroyMode.Vanish); }
            foreach (Pawn leader in map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Corps && company.CompanyDef.IsLeaderKind(p.kindDef)).ToList()) { leader.Destroy(DestroyMode.Vanish); }
            t.Check(!company.CompanyDef.Worker.HasCommander(map, Corps), "no commander once the leaders are gone");
            bool remove = company.ShouldRemoveMapNow(out bool alsoWorldObject);
            t.Check(remove && alsoWorldObject, "with no player and no commander the whole camp leaves");
            yield return new WaitFrames(1);
        }
    }
}
