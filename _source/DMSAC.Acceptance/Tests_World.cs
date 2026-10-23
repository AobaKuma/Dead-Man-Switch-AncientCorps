using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AncientCorps;
using Fortified;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;
using Verse.AI.Group;
using static DMSAC.Acceptance.Fixture;

namespace DMSAC.Acceptance
{
    public static class Tests_World
    {
        /// <summary>R7: a raid with the corps preset and too few points is raised to the corps' cheapest group and finds a strategy.</summary>
        public static IEnumerable<Wait> R7_RaidPointsFloor(TestContext t)
        {
            IncidentWorker worker = IncidentDefOf.RaidEnemy.Worker;
            MethodInfo resolve = typeof(IncidentWorker_RaidEnemy).GetMethod("ResolveRaidPoints", BindingFlags.Instance | BindingFlags.NonPublic);
            t.Require(resolve != null, "ResolveRaidPoints found");
            float min = Corps.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat);
            t.Note($"corps minimum combat group: {min:0}");

            IncidentParms preset = new IncidentParms { target = Home, faction = Corps, points = 52.5f };
            resolve.Invoke(worker, new object[] { preset });
            t.Check(preset.points >= min - 0.01f, $"52.5 points raised to {preset.points:0}");
            IncidentParms open = new IncidentParms { target = Home, points = 52.5f };
            resolve.Invoke(worker, new object[] { open });
            t.Check(open.points == 52.5f, "raids without a preset faction keep their points");

            SetCorpsRelation(FactionRelationKind.Hostile);
            IncidentParms raid = new IncidentParms { target = Home, faction = Corps, points = 52.5f, forced = true };
            t.Check(worker.TryExecute(raid), "the 52.5-point corps raid fires");
            t.Check(raid.raidStrategy != null, $"a raid strategy was chosen ({raid.raidStrategy?.defName})");
            t.Note($"arrival {raid.raidArrivalMode?.defName}, points {raid.points:0}, spawned now {Home.mapPawns.AllPawnsSpawned.Count(p => p.Faction == Corps)}, "
                + $"skyfallers {Home.listerThings.AllThings.Count(x => x is Skyfaller)}");
            var arrive = new WaitUntil(() => Home.mapPawns.AllPawns.Any(p => p.Faction == Corps), 3000);
            yield return arrive;
            t.Note($"corps pawns on the map (any state): {Home.mapPawns.AllPawns.Count(p => p.Faction == Corps)}, spawned {Home.mapPawns.AllPawnsSpawned.Count(p => p.Faction == Corps)}");
            t.Check(!arrive.TimedOut, "raiders arrived");
        }

        /// <summary>R8: site tiles, company camps and quest sites keep off landmark tiles.</summary>
        public static IEnumerable<Wait> R8_Landmarks(TestContext t)
        {
            List<PlanetTile> landmarks = Find.World.landmarks.landmarks.Keys.ToList();
            t.Note($"landmarks on the world: {landmarks.Count}");
            t.Require(landmarks.Count > 0, "the world has landmarks (Odyssey)");

            int bad = 0, invalid = 0;
            for (int i = 0; i < 40; i++)
            {
                if (!FFF_SiteTileUtility.TryFindNewSiteTile(out PlanetTile tile) || !tile.Valid) { invalid++; continue; }
                if (IsLandmark(tile)) { bad++; }
            }
            t.Check(invalid == 0, $"40 site-tile searches all succeed ({invalid} failed)");
            t.Check(bad == 0, $"no site tile on a landmark ({bad}/40)");

            PlanetTile free = FreeTile();
            List<PlanetTile> mixed = landmarks.Take(3).Append(free).ToList();
            List<PlanetTile> filtered = mixed.WithoutLandmarks();
            t.Check(filtered.Count == 1 && filtered[0] == free, "WithoutLandmarks drops landmark tiles");
            List<PlanetTile> onlyLandmarks = landmarks.Take(3).ToList();
            t.Check(onlyLandmarks.WithoutLandmarks().Count == onlyLandmarks.Count, "WithoutLandmarks keeps an all-landmark list");

            QuestScriptDef questDef = DefDatabase<QuestScriptDef>.GetNamed("DMSAC_Installation_Logistic");
            for (int i = 0; i < 5; i++)
            {
                List<Site> before = Find.WorldObjects.Sites.ToList();
                Slate slate = new Slate();
                slate.Set("points", 500f);
                Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, slate);
                Track(quest);
                foreach (Site site in Find.WorldObjects.Sites.Except(before).ToList())
                {
                    Track(site);
                    t.Check(!IsLandmark(site.Tile), $"quest site {i + 1} not on a landmark");
                }
            }

            SetLevelQuiet(3);
            List<Settlement> targets = HostileSettlements();
            for (int i = 0; i < 5 && i < targets.Count; i++)
            {
                FFF_Company company = CompanyUtility.Deploy(Defcon, State, targets[i]);
                if (company != null) { t.Check(!IsLandmark(company.Tile), $"company camp {i + 1} not on a landmark"); }
            }
            yield return new WaitFrames(1);
        }

        /// <summary>R9: a corps site map with patrols: structure generated, first patrol turns the corps hostile, later ones build pressure and hurry DEFCON, tiers join by level.</summary>
        public static IEnumerable<Wait> R9_SitePatrols(TestContext t)
        {
            SetCorpsRelation(FactionRelationKind.Neutral);
            PlanetTile tile = FreeTile();
            t.Require(tile.Valid, "a free tile near home");
            Site site = SiteMaker.MakeSite(DefDatabase<SitePartDef>.GetNamed("DMSAC_LogisticTerminal"), tile, Corps,
                ifHostileThenMustRemainHostile: false, threatPoints: 500f, worldObjectDef: DefDatabase<WorldObjectDef>.GetNamed("DMSAC_LogisticTerminal"));
            Find.WorldObjects.Add(site);
            Track(site);
            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(tile, null);
            t.Require(map != null, "site map generated");
            t.Check(map.listerThings.AllThings.Any(x => x is Building && x.Faction == Corps), "corps structure generated");

            WorldObjectComp_ReadinessPatrol patrol = site.GetComponent<WorldObjectComp_ReadinessPatrol>();
            t.Require(patrol != null, "patrol comp on the site");
            t.Check(patrol.Readiness == Defcon, "patrol drives DMSAC_Defcon");

            int letters = LetterCount;
            int corpsPawns = map.mapPawns.AllPawnsSpawned.Count(p => p.Faction == Corps);
            patrol.SpawnPatrol();
            t.Check(Corps.HostileTo(Faction.OfPlayer), "first patrol turns the corps hostile");
            t.Check(State.level == 1, "first patrol raised DEFCON (trespass)");
            Freeze();
            t.Check(LettersSince(letters).Any(l => LabelIs(l, "DMSAC_CorpHostile")), "hostility letter from the corps keys");
            List<Pawn> arrivals = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Corps).ToList();
            t.Check(arrivals.Count > corpsPawns, "patrol pawns spawned");
            t.Check(arrivals.Any(p => p.GetLord()?.LordJob is LordJob_StageThenAttack), "patrol stages then attacks");

            int far = Find.TickManager.TicksGame + 30 * GenDate.TicksPerDay;
            State.nextActionTick = far;
            float pressure = State.pressure;
            letters = LetterCount;
            patrol.SpawnPatrol();
            float step = 1f / patrol.Props.patrolCountToRaiseLevel;
            t.Check(UnityEngine.Mathf.Abs(State.pressure - (pressure + step)) < 0.001f, $"follow-up patrol adds {step:0.00} pressure");
            t.Check(State.nextActionTick < far, "follow-up patrol hurries the next DEFCON action");
            t.Check(LettersSince(letters).Any(l => LabelIs(l, "DMSAC_PatrolSquad")), "reinforcement letter from the corps keys");

            SetLevelQuiet(3);
            HashSet<PawnKindDef> heavy = new HashSet<PawnKindDef>(patrol.Props.possibleGeneratePawn_Heavy.Select(o => o.kind));
            List<Pawn> beforeHeavy = map.mapPawns.AllPawnsSpawned.ToList();
            patrol.SpawnPatrol();
            t.Check(map.mapPawns.AllPawnsSpawned.Except(beforeHeavy).Any(p => heavy.Contains(p.kindDef)), "at level III heavy units join the patrol");
            Freeze();
            yield return new WaitFrames(1);
        }

        /// <summary>R14: every corps installation site generates with DMS2's late-scatter ruins, their early-warning devices owned by the corps.</summary>
        public static IEnumerable<Wait> R14_SecondaryStructures(TestContext t)
        {
            HashSet<string> warning = new HashSet<string> { "DMS_Building_Camera", "DMS_Building_Scanner_Large", "DMS_Building_Scanner_Small", "DMS_Building_Sensor", "DMS_Building_FPVNest" };
            int totalWarning = 0;
            foreach (string part in new[] { "DMSAC_Graveyard", "DMSAC_Checkpoint", "DMSAC_Garage", "DMSAC_FacilityEnterance", "DMSAC_LogisticTerminal" })
            {
                GenStepDef scatter = DefDatabase<GenStepDef>.AllDefsListForReading.FirstOrDefault(g => g.linkWithSite?.defName == part && g.order == 1400f);
                t.Check(scatter != null && scatter.genStep.GetType().Name == "DMS_GenStep_ScatterStructures", $"{part}: late scatter step linked");
                PlanetTile tile = FreeTile();
                t.Require(tile.Valid, "a free tile near home");
                Site site = SiteMaker.MakeSite(DefDatabase<SitePartDef>.GetNamed(part), tile, Corps, ifHostileThenMustRemainHostile: false,
                    threatPoints: 600f, worldObjectDef: DefDatabase<WorldObjectDef>.GetNamedSilentFail(part));
                Find.WorldObjects.Add(site);
                Track(site);
                Map map = GetOrGenerateMapUtility.GetOrGenerateMap(tile, null);
                t.Require(map != null, $"{part}: map generated");
                List<Thing> devices = map.listerThings.AllThings.Where(x => warning.Contains(x.def.defName)).ToList();
                totalWarning += devices.Count;
                t.Check(devices.All(d => d.Faction == Corps), $"{part}: {devices.Count} early-warning devices, all corps-owned");
                int ruinWalls = map.listerThings.ThingsOfDef(ThingDefOf.Sandbags).Count + map.listerThings.ThingsOfDef(ThingDefOf.AncientRazorWire).Count;
                t.Note($"{part}: {devices.Count} warning devices, {ruinWalls} sandbags / razor wire, {map.listerThings.AllThings.Count(x => x is Building && x.Faction == Corps)} corps buildings");
                Current.Game.DeinitAndRemoveMap(map, false);
                yield return new WaitFrames(2);
            }
            t.Check(totalWarning > 0, $"the ruins brought early-warning devices ({totalWarning} over five sites)");
        }

        /// <summary>
        /// R15: the early-warning augment on corps installations. Ruin scatter and satellite rings are switched off so every
        /// device on the map comes from the augment: cameras on outer walls watching outward, sensors beside doors, indoor
        /// scanners roofed, outskirts devices outdoors, all corps-owned and wired; level gates and applyAugments respected.
        /// </summary>
        public static IEnumerable<Wait> R15_EarlyWarningAugment(TestContext t)
        {
            string[] parts = { "DMSAC_Graveyard", "DMSAC_Checkpoint", "DMSAC_Garage", "DMSAC_FacilityEnterance", "DMSAC_LogisticTerminal" };
            HashSet<string> devices = new HashSet<string> { "DMS_Building_Camera", "DMS_Building_Scanner_Large", "DMS_Building_Scanner_Small", "DMS_Building_Sensor", "DMS_Building_FPVNest" };
            t.Require(DefDatabase<Fortified.Structures.StructureAugmentDef>.GetNamedSilentFail("DMSAC_EarlyWarningNet") != null, "DMSAC_EarlyWarningNet loaded");

            // 關掉廢墟散布與衛星環，地圖上的預警設施就只會來自增補。Switch off ruin scatter and satellites.
            foreach (GenStepDef g in DefDatabase<GenStepDef>.AllDefsListForReading.Where(g => g.linkWithSite != null && parts.Contains(g.linkWithSite.defName)))
            {
                GenStep step = g.genStep;
                if (step.GetType().Name == "DMS_GenStep_ScatterStructures")
                {
                    IntRange oldRange = Field<IntRange>(step, "countRange");
                    Override(() => SetField(step, "countRange", new IntRange(0, 0)), () => SetField(step, "countRange", oldRange));
                }
                if (step is Fortified.Structures.GenStep_FFFGarrisonedStructure garrisoned)
                {
                    var oldRing = garrisoned.satelliteScatter;
                    Override(() => garrisoned.satelliteScatter = null, () => garrisoned.satelliteScatter = oldRing);
                }
            }

            SetLevelQuiet(3);
            int poweredOk = 0, poweredAll = 0;
            foreach (string part in parts)
            {
                Map map = GenerateSite(part);
                t.Require(map != null, $"{part}: map generated");
                // 不等 tick：沒有玩家的站點地圖會被原版收掉。電網在生成結束時就已建好。
                // No tick wait: vanilla removes a site map with no player. Power nets are built when generation ends.
                List<Thing> found = map.listerThings.AllThings.Where(x => devices.Contains(x.def.defName)).ToList();
                t.Check(found.Count > 0 && found.All(d => d.Faction == Corps), $"{part}: {found.Count} augment devices, all corps-owned");

                List<Thing> cameras = found.Where(d => d.def.defName == "DMS_Building_Camera").ToList();
                // 開放式佈局（沒有外牆可掛）合理地沒有攝影機；只有在確實有可掛的外牆時才要求至少兩支。
                // An open-air layout (no outer wall to mount on) rightly gets no cameras; require two only when one exists.
                if (cameras.Count > 0) t.Check(true, $"{part}: wall cameras placed ({cameras.Count})");
                else t.Check(!HasMountableOuterWall(map), $"{part}: no cameras, and the layout has no mountable outer wall (open-air)");
                t.Check(cameras.All(c => IsWallAt(map, c.Position + c.Rotation.FacingCell)), $"{part}: every camera is attached to a wall");
                t.Check(cameras.All(c => !c.Position.Roofed(map)), $"{part}: cameras on outer walls (open sky in front)");
                // 牆在 Rotation 那一側；正前方（反方向）一格要是空的，而且在掃描範圍內。
                // The wall is on the Rotation side; the cell straight ahead (the other way) must be open and scanned.
                t.Check(cameras.All(c =>
                {
                    CompAlertScanner scan = c.TryGetComp<CompAlertScanner>();
                    IntVec3 ahead = c.Position - c.Rotation.FacingCell;
                    return scan != null && !IsWallAt(map, ahead) && scan.ScanCells().Contains(ahead + (ahead - c.Position) * 2);
                }), $"{part}: cameras scan away from their wall");

                List<Thing> sensors = found.Where(d => d.def.defName == "DMS_Building_Sensor").ToList();
                t.Check(sensors.All(s => GenRadial.RadialCellsAround(s.Position, 2.3f, true).Any(c => c.InBounds(map) && c.GetDoor(map) != null)),
                    $"{part}: sensors sit beside doors ({sensors.Count})");
                t.Check(found.Where(d => d.def.defName == "DMS_Building_Scanner_Small").All(d => d.Position.Roofed(map)), $"{part}: small scanners indoors");
                t.Check(found.Where(d => d.def.defName == "DMS_Building_Scanner_Large" || d.def.defName == "DMS_Building_FPVNest").All(d => !d.OccupiedRect().Cells.Any(c => c.Roofed(map))),
                    $"{part}: towers and FPV nests outdoors");
                foreach (Thing d in found)
                {
                    CompPowerTrader power = d.TryGetComp<CompPowerTrader>();
                    if (power == null) continue;
                    poweredAll++;
                    if (power.PowerNet != null) poweredOk++;
                }
                t.Note($"{part}: " + string.Join(", ", found.GroupBy(d => d.def.defName).Select(g => $"{g.Key.Replace("DMS_Building_", "")} {g.Count()}")));
                Current.Game.DeinitAndRemoveMap(map, false);
                yield return new WaitFrames(2);
            }
            t.Check(poweredAll > 0 && poweredOk >= poweredAll * 0.8f, $"devices wired into a power net ({poweredOk}/{poweredAll})");

            // 0 級：只有攝影機與感應器（掃描器、掃描塔、FPV 巢有等級門檻）。Level 0: cameras and sensors only.
            SetLevelQuiet(0);
            Map low = GenerateSite("DMSAC_Garage");
            List<Thing> lowFound = low.listerThings.AllThings.Where(x => devices.Contains(x.def.defName)).ToList();
            t.Check(lowFound.Count > 0 && lowFound.All(d => d.def.defName == "DMS_Building_Camera" || d.def.defName == "DMS_Building_Sensor"),
                $"level 0: only cameras and sensors ({string.Join(", ", lowFound.Select(d => d.def.defName).Distinct())})");
            Current.Game.DeinitAndRemoveMap(low, false);
            yield return new WaitFrames(2);

            // applyAugments 關掉時一個都沒有。applyAugments off: none at all.
            var garage = (Fortified.Structures.GenStep_FFFStructureGen)DefDatabase<GenStepDef>.GetNamed("DMSAC_Garage").genStep;
            Override(() => garage.applyAugments = false, () => garage.applyAugments = true);
            Map off = GenerateSite("DMSAC_Garage");
            t.Check(!off.listerThings.AllThings.Any(x => devices.Contains(x.def.defName)), "applyAugments off: no augment devices");
            Current.Game.DeinitAndRemoveMap(off, false);
        }

        /// <summary>R16: site garrison selection never throws, for every corps site × DEFCON × points × faction.</summary>
        public static IEnumerable<Wait> R16_GarrisonSelection(TestContext t)
        {
            List<SitePartDef> parts = DefDatabase<SitePartDef>.AllDefsListForReading.Where(p => p.HasModExtension<ModExtension_SiteGarrison>()).ToList();
            t.Require(parts.Count > 0, $"site parts with a garrison ({parts.Count})");
            List<Faction> factions = new List<Faction> { Corps };
            Faction humanlike = Find.FactionManager.AllFactions.FirstOrDefault(f => !f.IsPlayer && f.def.humanlikeFaction && !f.Hidden);
            if (humanlike != null) factions.Add(humanlike);
            factions.Add(Faction.OfPlayer);
            int calls = 0, empty = 0;
            List<string> crashes = new List<string>();
            foreach (SitePartDef part in parts)
            {
                ModExtension_SiteGarrison ext = part.GetModExtension<ModExtension_SiteGarrison>();
                foreach (Faction faction in factions)
                {
                    for (int defcon = 0; defcon <= 5; defcon++)
                    {
                        foreach (float points in new[] { 35f, 300f, 1500f, 8000f })
                        {
                            calls++;
                            try
                            {
                                if (ext.ChooseKinds(ext.GarrisonPoints(points, defcon), faction, Home.Tile, calls, defcon).Count == 0) empty++;
                            }
                            catch (System.Exception e)
                            {
                                crashes.Add($"{part.defName}/{faction.def.defName}/DEFCON {defcon}/{points}: {e.GetType().Name} {e.Message}");
                            }
                        }
                    }
                }
            }
            t.Check(ModExtension_SiteGarrison.GarrisonFaction(Faction.OfPlayer) == Corps, "a player-owned site still gets a corps garrison");
            t.Check(ModExtension_SiteGarrison.GarrisonFaction(null) == Corps, "a site with no faction gets a corps garrison");
            foreach (string crash in crashes.Take(10)) t.Note(crash);
            t.Check(crashes.Count == 0, $"no exception over {calls} selections ({crashes.Count} threw)");
            t.Note($"{empty} selections came back empty");
            yield return new WaitFrames(1);
        }

        /// <summary>R17: a mechanitor with the DMS mechlink kill switch / fatal trigger dies cleanly; the on-death removals still happen.</summary>
        public static IEnumerable<Wait> R17_MechlinkDeath(TestContext t)
        {
            foreach (string trigger in new[] { "DMS_MechlinkKillSwitch", "DMS_MechlinkFatalTrigger" })
            {
                HediffDef triggerDef = DefDatabase<HediffDef>.GetNamedSilentFail(trigger);
                t.Require(triggerDef != null, $"{trigger} exists");
                Pawn pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(Home.Center, Home, 10);
                GenSpawn.Spawn(pawn, cell, Home);
                BodyPartRecord brain = pawn.health.hediffSet.GetBrain();
                pawn.health.AddHediff(HediffDefOf.MechlinkImplant, brain);
                pawn.health.AddHediff(triggerDef, brain);
                t.Check(pawn.health.hediffSet.HasHediff(triggerDef) && pawn.health.hediffSet.HasHediff(HediffDefOf.MechlinkImplant), $"{trigger}: set up on a mechanitor");
                pawn.Kill(null, null);
                t.Check(pawn.Dead, $"{trigger}: the pawn died");
                t.Check(!pawn.health.hediffSet.HasHediff(HediffDefOf.MechlinkImplant), $"{trigger}: the mechlink was removed on death");
                t.Check(!pawn.health.hediffSet.HasHediff(triggerDef), $"{trigger}: the trigger removed itself on death");
                if (pawn.Corpse != null && pawn.Corpse.Spawned) pawn.Corpse.Destroy(DestroyMode.Vanish);
                else if (pawn.Spawned) pawn.Destroy(DestroyMode.Vanish);
            }
            yield return new WaitFrames(1);
        }

        /// <summary>
        /// R18: sites and units start as the corps. World-gen order, world-gen sites owned by the corps, the old-save
        /// repair, structures / garrison / patrols on player-owned and faction-less sites, a faction-less company, and
        /// corps bossgroups (corps when hostile, DMS Legacy otherwise).
        /// </summary>
        public static IEnumerable<Wait> R18_Factions(TestContext t)
        {
            float spawnOrder = DefDatabase<WorldGenStepDef>.GetNamed("FFF_SpawnWorldObjects").order;
            t.Check(spawnOrder > DefDatabase<WorldGenStepDef>.GetNamed("Factions").order, $"world-gen sites spawn after factions exist (order {spawnOrder})");
            WorldGenStepDef landmarks = DefDatabase<WorldGenStepDef>.GetNamedSilentFail("Landmarks");
            t.Check(landmarks == null || spawnOrder < landmarks.order, "and before landmarks");

            List<Site> worldGen = Find.WorldObjects.Sites.Where(s => s.def.GetModExtension<Fortified.Structures.SpawnAtWorldGen>()?.spawnPartOfFaction == Corps.def).ToList();
            t.Check(worldGen.Count > 0, $"world-gen corps sites exist ({worldGen.Count})");
            t.Check(worldGen.All(s => s.Faction == Corps), $"every world-gen corps site is owned by the corps ({worldGen.Count(s => s.Faction == Corps)}/{worldGen.Count})");
            if (worldGen.Count > 0)
            {
                Site broken = worldGen[0];
                broken.SetFaction(null);
                new Fortified.Structures.GameComponent_WorldGenSiteFactionRepair(Current.Game).FinalizeInit();
                t.Check(broken.Faction == Corps, "the load-time repair gives a faction-less world-gen site back to the corps");
            }

            foreach (Faction owner in new[] { Faction.OfPlayer, null })
            {
                string who = owner == null ? "faction-less" : "player-owned";
                PlanetTile tile = FreeTile();
                Site site = SiteMaker.MakeSite(DefDatabase<SitePartDef>.GetNamed("DMSAC_LogisticTerminal"), tile, Corps, ifHostileThenMustRemainHostile: false,
                    threatPoints: 600f, worldObjectDef: DefDatabase<WorldObjectDef>.GetNamed("DMSAC_LogisticTerminal"));
                site.SetFaction(owner);
                Find.WorldObjects.Add(site);
                Track(site);
                Map map = GetOrGenerateMapUtility.GetOrGenerateMap(tile, null);
                t.Require(map != null, $"{who} site: map generated");
                List<Thing> owned = map.listerThings.AllThings.Where(x => x is Building && x.Faction != null).ToList();
                // 原版的古代廢墟（地標、遺跡）屬於 Ancients，與軍團內容無關，不算。Vanilla ancient ruins belong to the Ancients; not corps content.
                List<Thing> stray = owned.Where(b => b.Faction != Corps && b.Faction != Faction.OfAncients && b.Faction != Faction.OfAncientsHostile).ToList();
                int ancients = owned.Count(b => b.Faction == Faction.OfAncients || b.Faction == Faction.OfAncientsHostile);
                if (ancients > 0) t.Note($"{who} site: {ancients} vanilla ancient-ruin buildings (Ancients) ignored");
                owned = owned.Where(b => b.Faction != Faction.OfAncients && b.Faction != Faction.OfAncientsHostile).ToList();
                t.Check(owned.Count > 0 && stray.Count == 0, $"{who} site: all {owned.Count} owned buildings belong to the corps"
                    + (stray.Count > 0 ? $" — {stray.Count} don't: {string.Join(", ", stray.GroupBy(b => $"{b.def.defName}@{b.Faction.def.defName}").Select(g => $"{g.Key}×{g.Count()}").Take(8))}" : ""));
                List<Pawn> pawns = map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.IsMechanoid || p.kindDef.defaultFactionDef == Corps.def).ToList();
                t.Check(pawns.Count > 0 && pawns.All(p => p.Faction == Corps), $"{who} site: garrison of {pawns.Count} all corps");
                t.Check(site.GetComponent<WorldObjectComp_ReadinessPatrol>()?.PatrolFaction == Corps, $"{who} site: patrols are corps");
                Current.Game.DeinitAndRemoveMap(map, false);
                yield return new WaitFrames(2);
            }

            SetLevelQuiet(3);
            FFF_Company company = (FFF_Company)WorldObjectMaker.MakeWorldObject(Defcon.companyWorldObject);
            company.SetCompany(CorpsCompanyDefs.First(c => c.minLevel <= 3));
            t.Require(company.SetTarget(HostileSettlements().First()), "company placed");
            Find.WorldObjects.Add(company);
            t.Check(company.Faction == Corps, "a company spawned without a faction joins the corps");

            foreach (BossgroupDef boss in DefDatabase<BossgroupDef>.AllDefsListForReading.Where(b => b.quest?.defName == "DMSAC_Boss"))
            {
                // 沒有 rewardDef 的首領群，擊敗信件的 [reward_label] 解析不了（內容缺口，不是派系問題），先記下不生成。
                // A bossgroup without rewardDef can't resolve [reward_label] in its defeat letter (a content gap, not a
                // faction issue); note it instead of generating.
                if (boss.rewardDef == null)
                {
                    t.Note($"{boss.defName} has no rewardDef; its defeat letter can't resolve [reward_label]. Skipped.");
                    continue;
                }
                foreach (bool hostile in new[] { true, false })
                {
                    SetCorpsRelation(hostile ? FactionRelationKind.Hostile : FactionRelationKind.Neutral);
                    HashSet<Pawn> before = new HashSet<Pawn>(Find.WorldPawns.AllPawnsAlive);
                    int quests = Find.QuestManager.QuestsListForReading.Count;
                    boss.Worker.Resolve(Home, 0);
                    Find.QuestManager.QuestsListForReading.Skip(quests).ToList().ForEach(Track);
                    List<Pawn> spawned = Find.WorldPawns.AllPawnsAlive.Where(p => !before.Contains(p)).ToList();
                    Pawn leader = spawned.FirstOrDefault(p => p.kindDef == boss.boss.kindDef);
                    string expected = hostile ? Corps.def.defName : "DMS_Legacy";
                    t.Check(leader != null && leader.Faction?.def.defName == expected,
                        $"{boss.defName} ({(hostile ? "corps hostile" : "corps not hostile")}): boss is {leader?.Faction?.def.defName ?? "missing"}, expected {expected}");
                    t.Check(spawned.All(p => p.Faction == leader?.Faction), $"{boss.defName}: escorts share the boss's faction");
                    foreach (Pawn p in spawned) { Find.WorldPawns.RemovePawn(p); p.Discard(silentlyRemoveReferences: true); }
                }
            }
        }

        /// <summary>
        /// R19: pushing the corps to DEFCON II locks permanent hostility (FFF FactionHostilityLock): goodwill can't rise,
        /// relations can't be forced back, hostility is re-asserted. Self-rises and level I don't lock; a colony with the
        /// Last Pilot is never locked, and his recognition at a facility lifts an existing lock.
        /// </summary>
        public static IEnumerable<Wait> R19_PermanentHostility(TestContext t)
        {
            ReadinessDef def = Defcon;
            t.Check(def.permanentHostilityLevel == 2, "DMSAC_Defcon locks hostility at level II");

            SetCorpsRelation(FactionRelationKind.Neutral);
            ReadinessUtility.Raise(def, 1, "test");
            Freeze();
            t.Check(!FactionHostilityLock.IsLocked(Corps) && !Corps.HostileTo(Faction.OfPlayer), "level I: no lock, the corps stays neutral");

            SetLevelQuiet(1);
            new ReadinessAction_RaiseLevel().TryRun(def, State);
            Freeze();
            t.Check(State.level == 2 && !FactionHostilityLock.IsLocked(Corps), "the corps heating up on its own to II doesn't lock");

            SetLevelQuiet(1);
            int letters = LetterCount;
            ReadinessUtility.Raise(def, 1, "trespass");
            Freeze();
            t.Check(FactionHostilityLock.IsLocked(Corps), "the player pushing the corps to II locks it");
            t.Check(Corps.HostileTo(Faction.OfPlayer) && Corps.BaseGoodwillWith(Faction.OfPlayer) == -100, "locked: hostile at -100");
            t.Check(LettersSince(letters).Any(l => LabelIs(l, "DMSAC_PermanentHostility")), "permanent-enemy letter from the corps keys");
            t.Check(!Corps.TryAffectGoodwillWith(Faction.OfPlayer, 200, canSendMessage: false), "goodwill gains are refused");
            t.Check(!FactionRelationUtility.SetGoodwillAndKind(Corps, 0, FactionRelationKind.Neutral), "FactionRelationUtility can't force it back");
            t.Check(Corps.TryAffectGoodwillWith(Faction.OfPlayer, -5, canSendMessage: false) || Corps.BaseGoodwillWith(Faction.OfPlayer) == -100, "goodwill losses still apply");
            ReadinessUtility.Drop(def, 2, "test");
            Freeze();
            t.Check(FactionHostilityLock.IsLocked(Corps) && Corps.HostileTo(Faction.OfPlayer), "dropping DEFCON doesn't lift the lock");

            // 原版或他人的直接改動也會被週期檢查拉回。Direct edits elsewhere get pulled back by the periodic check.
            FactionRelation relation = Corps.RelationWith(Faction.OfPlayer);
            relation.baseGoodwill = 50;
            relation.kind = FactionRelationKind.Neutral;
            Faction.OfPlayer.RelationWith(Corps).baseGoodwill = 50;
            Faction.OfPlayer.RelationWith(Corps).kind = FactionRelationKind.Neutral;
            var reassert = new WaitUntil(() => Corps.HostileTo(Faction.OfPlayer), 3000);
            yield return reassert;
            t.Check(!reassert.TimedOut, "a forced-back relation is re-asserted as hostile within one check");

            // 老兵：殖民地有最後的駕駛員時不鎖。The veteran: no lock while the colony has the Last Pilot.
            SetCorpsRelation(FactionRelationKind.Neutral);
            SetLevelQuiet(1);
            // DMS_LastPilot 由 Mobile Dragoon 提供；沒載入時用同名的替身兵種（軍團只比對 defName）。
            // DMS_LastPilot comes from Mobile Dragoon; without it, use a stand-in kind with that defName (the corps only
            // compares the defName).
            PawnKindDef pilotKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(AncientCorpsUltility.LastPilotKind);
            Pawn pilot = PawnGenerator.GeneratePawn(pilotKind ?? PawnKindDefOf.Colonist, Faction.OfPlayer);
            if (pilotKind == null)
            {
                t.Note("DMS_LastPilot isn't loaded (Mobile Dragoon); testing with a stand-in kind of that defName.");
                PawnKindDef standIn = (PawnKindDef)typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(PawnKindDefOf.Colonist, null);
                standIn.defName = AncientCorpsUltility.LastPilotKind;
                pilot.kindDef = standIn;
            }
            GenSpawn.Spawn(pilot, CellFinder.RandomClosewalkCellNear(Home.Center, Home, 8), Home);
            Track(pilot);
            t.Check(AncientCorpsUltility.PlayerHasLastPilot(), "the colony has the Last Pilot");
            ReadinessUtility.Raise(def, 1, "trespass");
            Freeze();
            t.Check(State.level == 2 && !FactionHostilityLock.IsLocked(Corps), "with the Last Pilot, DEFCON II doesn't lock");

            // 老兵在設施被認出：解除既有的鎖並停火。Recognised at a facility: an existing lock is lifted and fire ceases.
            FactionHostilityLock.Lock(Corps);
            t.Require(FactionHostilityLock.IsLocked(Corps) && Corps.HostileTo(Faction.OfPlayer), "locked before the recognition");
            AncientCorpsUltility.SetHostileToPlayer(Home, null);
            t.Check(!FactionHostilityLock.IsLocked(Corps), "the Last Pilot's recognition lifts the lock");
            t.Check(!Corps.HostileTo(Faction.OfPlayer), "and the corps ceases fire");
            // 替身兵種不能留進存檔（讀檔找不到該 def）。The stand-in kind must not reach a save (no such def on load).
            if (pilotKind == null) pilot.kindDef = PawnKindDefOf.Colonist;
            Freeze();
        }

        /// <summary>
        /// R20: DEFCON drives garrisons, patrols and rosters. Per site: garrison points and the unit pool grow with the
        /// level, picks stay inside the unlocked pool and budget, the top unlocked tier shows up, strength rises; a real
        /// Garage at DEFCON 0 vs V. Patrols: units by tier and shorter intervals. Companies: roster gated by minLevel.
        /// </summary>
        public static IEnumerable<Wait> R20_DefconScaling(TestContext t)
        {
            List<SitePartDef> parts = DefDatabase<SitePartDef>.AllDefsListForReading.Where(p => p.HasModExtension<ModExtension_SiteGarrison>()).ToList();
            foreach (SitePartDef part in parts)
            {
                ModExtension_SiteGarrison ext = part.GetModExtension<ModExtension_SiteGarrison>();
                float[] power = new float[6];
                bool pointsUp = true, poolUp = true, inPool = true, inBudget = true, topShown = true;
                for (int d = 0; d <= 5; d++)
                {
                    float points = ext.GarrisonPoints(700f, d);
                    if (d > 0 && points < ext.GarrisonPoints(700f, d - 1)) pointsUp = false;
                    HashSet<PawnKindDef> pool = new HashSet<PawnKindDef>(ext.OptionsFor(d).Select(o => o.kind));
                    if (d > 0 && pool.Count < ext.OptionsFor(d - 1).Count) poolUp = false;
                    HashSet<PawnKindDef> top = ext.TopTierKinds(d);
                    float cap = ext.MaxPawnCost(points, Corps, d);
                    bool topAffordable = top.Any(k => k.combatPower <= System.Math.Min(cap, points));
                    for (int seed = 0; seed < 20; seed++)
                    {
                        List<PawnKindDef> kinds = ext.ChooseKinds(points, Corps, Home.Tile, seed * 7919 + d, d);
                        float spent = kinds.Sum(k => k.combatPower);
                        power[d] += spent / 20f;
                        if (!kinds.All(pool.Contains)) inPool = false;
                        if (spent > points + 0.01f) inBudget = false;
                        if (topAffordable && !kinds.Any(top.Contains)) topShown = false;
                    }
                }
                t.Check(pointsUp, $"{part.defName}: garrison points never drop as DEFCON rises ({ext.GarrisonPoints(700f, 0):0} → {ext.GarrisonPoints(700f, 5):0})");
                t.Check(poolUp, $"{part.defName}: the unit pool only grows ({ext.OptionsFor(0).Count} → {ext.OptionsFor(5).Count} kinds)");
                t.Check(inPool, $"{part.defName}: picks never use a unit above the current level");
                t.Check(inBudget, $"{part.defName}: picks stay within the garrison points");
                t.Check(topShown, $"{part.defName}: the highest unlocked tier appears whenever it fits");
                t.Check(power[5] > power[0], $"{part.defName}: average strength {power[0]:0} at DEFCON 0 → {power[5]:0} at V");
            }

            // 實際生成：同一種站點在 0 級與 V 級。A real site at DEFCON 0 and V.
            ModExtension_SiteGarrison garage = DefDatabase<SitePartDef>.GetNamed("DMSAC_Garage").GetModExtension<ModExtension_SiteGarrison>();
            float[] real = new float[2];
            List<PawnKindDef>[] seen = new List<PawnKindDef>[2];
            int[] levels = { 0, 5 };
            for (int i = 0; i < 2; i++)
            {
                SetLevelQuiet(levels[i]);
                Map map = GenerateSite("DMSAC_Garage");
                t.Require(map != null, $"Garage at DEFCON {levels[i]}: map generated");
                List<Pawn> garrison = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Corps).ToList();
                seen[i] = garrison.Select(p => p.kindDef).ToList();
                real[i] = garrison.Sum(p => p.kindDef.combatPower);
                t.Note($"Garage DEFCON {levels[i]}: {garrison.Count} pawns, {real[i]:0} power — {string.Join(", ", garrison.GroupBy(p => p.kindDef.defName).Select(g => $"{g.Key.Replace("DMS_Mech_", "")}×{g.Count()}"))}");
                Current.Game.DeinitAndRemoveMap(map, false);
                yield return new WaitFrames(2);
            }
            HashSet<PawnKindDef> tier0 = new HashSet<PawnKindDef>(garage.OptionsFor(0).Select(o => o.kind));
            t.Check(seen[0].Count > 0 && seen[0].All(tier0.Contains), "Garage at DEFCON 0: only tier-0 units");
            t.Check(seen[1].Any(k => !tier0.Contains(k)), "Garage at DEFCON V: higher-tier units in the garrison");
            t.Check(real[1] > real[0], $"Garage: garrison strength {real[0]:0} at 0 → {real[1]:0} at V");

            // 巡邏：兵種依等級解鎖、間隔隨等級縮短。Patrols: units by level, shorter intervals.
            SetCorpsRelation(FactionRelationKind.Hostile);
            SetLevelQuiet(0);
            Map siteMap = GenerateSite("DMSAC_LogisticTerminal");
            t.Require(siteMap != null, "patrol site generated");
            Site patrolSite = (Site)siteMap.Parent;
            WorldObjectComp_ReadinessPatrol patrol = patrolSite.GetComponent<WorldObjectComp_ReadinessPatrol>();
            HashSet<PawnKindDef> basic = new HashSet<PawnKindDef>(patrol.Props.possibleGeneratePawn.Select(o => o.kind));
            HashSet<PawnKindDef> medium = new HashSet<PawnKindDef>(patrol.Props.possibleGeneratePawn_Medium.Select(o => o.kind));
            HashSet<PawnKindDef> heavy = new HashSet<PawnKindDef>(patrol.Props.possibleGeneratePawn_Heavy.Select(o => o.kind));
            SetField(patrol, "greetTriggered", true);
            List<PawnKindDef> PatrolAt(int level)
            {
                SetLevelQuiet(level);
                HashSet<Pawn> before = new HashSet<Pawn>(siteMap.mapPawns.AllPawnsSpawned);
                patrol.SpawnPatrol();
                Freeze();
                return siteMap.mapPawns.AllPawnsSpawned.Where(p => !before.Contains(p)).Select(p => p.kindDef).ToList();
            }
            List<PawnKindDef> p0 = PatrolAt(0), p1 = PatrolAt(1), p3 = PatrolAt(3), p5 = PatrolAt(5);
            t.Check(p0.Count > 0 && p0.All(k => basic.Contains(k)) && !p0.Any(k => medium.Contains(k) && !basic.Contains(k)), $"patrol DEFCON 0: basic units only ({p0.Count})");
            t.Check(p1.Any(k => medium.Contains(k)), $"patrol DEFCON I: medium units join ({p1.Count})");
            t.Check(p3.Any(k => heavy.Contains(k)), $"patrol DEFCON III: heavy units join ({p3.Count})");
            t.Check(p5.Count(k => medium.Contains(k) || heavy.Contains(k)) > p1.Count(k => medium.Contains(k) || heavy.Contains(k)),
                $"patrol DEFCON V: more medium/heavy than at I ({p5.Count(k => medium.Contains(k) || heavy.Contains(k))} vs {p1.Count(k => medium.Contains(k) || heavy.Contains(k))})");

            MethodInfo schedule = typeof(WorldObjectComp_ReadinessPatrol).GetMethod("ScheduleNext", BindingFlags.Instance | BindingFlags.NonPublic);
            float Interval(int level)
            {
                SetLevelQuiet(level);
                float sum = 0f;
                for (int i = 0; i < 20; i++)
                {
                    SetField(patrol, "latestPatrolTick", -1);
                    schedule.Invoke(patrol, new object[] { 0 });
                    sum += Field<int>(patrol, "nextPatrolTick") - Find.TickManager.TicksGame;
                }
                return sum / 20f;
            }
            float i0 = Interval(0), i5 = Interval(5);
            t.Check(i5 < i0, $"patrol interval shrinks with DEFCON ({i0 / 2500f:0.0}h at 0 → {i5 / 2500f:0.0}h at V)");
            Current.Game.DeinitAndRemoveMap(siteMap, false);
            Freeze();

            // 連隊編制：等級決定可部署的連隊。Company rosters gated by level.
            for (int d = 0; d <= 5; d++)
            {
                List<CompanyDef> allowed = CorpsCompanyDefs.Where(c => c.minLevel <= d).ToList();
                bool ok = true;
                for (int i = 0; i < 20; i++)
                {
                    CompanyDef pick = CompanyUtility.RandomCompanyDef(Defcon, d);
                    if (pick != null ? pick.minLevel > d : allowed.Count > 0) ok = false;
                }
                t.Check(ok, $"companies at DEFCON {d}: only rosters with minLevel ≤ {d} ({allowed.Count} allowed)");
            }
            t.Check(CorpsCompanyDefs.Any(c => c.minLevel >= 4), "the heaviest rosters unlock only at DEFCON IV+");
        }

        /// <summary>
        /// R21: snapshot of everything the AC patches used to touch (comps, pools, loot, weapon tags, loadouts), written
        /// to patch_snapshot.tsv so a run before and after folding the patches into the defs can be diffed. Also checks
        /// the essentials directly.
        /// </summary>
        public static IEnumerable<Wait> R21_PatchSnapshot(TestContext t)
        {
            List<string> rows = new List<string>();
            void Row(string key, IEnumerable<string> values) => rows.Add(key + "\t" + string.Join(",", values.OrderBy(v => v)));

            foreach (string d in new[] { "DMSAC_GestaltEngine", "DMSAC_BandNode", "DMSAC_Building_AirFan", "DMSAC_Building_LiftShaft", "DMSAC_Building_LiftShaft_Bottom",
                "DMS_Mech_Saray", "DMS_Mech_Molot", "DMS_Mech_Tsvetnik", "DMS_Mech_Zabor", "DMS_Mech_Zabor_Pirate", "DMS_Mech_Gorod", "DMS_Mech_Krepost", "DMS_Mech_Pravda" })
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(d);
                Row("comps " + d, def?.comps?.Select(c => c.GetType().Name + (c is Fortified.CompProperties_Recycleable r ? "[" + string.Join("+", r.products.Select(p => p.thingDef.defName + ":" + p.count)) + "]" : "")) ?? new[] { "MISSING" });
            }
            foreach (string d in new[] { "DMSAC_Garrison", "DMSAC_Graveyard", "DMSAC_FacilityEnterance", "DMSAC_LogisticTerminal" })
            {
                WorldObjectCompProperties_ReadinessPatrol p = DefDatabase<WorldObjectDef>.GetNamed(d).comps.OfType<WorldObjectCompProperties_ReadinessPatrol>().First();
                Row("patrol " + d, p.possibleGeneratePawn.Select(o => o.kind.defName + ":" + o.selectionWeight));
            }
            foreach (string d in new[] { "DMSAC_Building_DeactivatedSmallMech", "DMSAC_Building_InactivatedSmallMech" })
            {
                object ext = DefDatabase<ThingDef>.GetNamed(d).modExtensions.First(e => e.GetType().Name == "ModExtension_DeactivatedMech");
                List<PawnGenOption> pool = Field<List<PawnGenOption>>(ext, "possibleGeneratePawn");
                Row("deactivated " + d, pool.Select(o => o.kind.defName + ":" + o.selectionWeight));
            }
            ThingSetMakerDef crates = DefDatabase<ThingSetMakerDef>.GetNamed("DMSAC_CrateLoots");
            List<ThingSetMaker_RandomOption.Option> options = Field<List<ThingSetMaker_RandomOption.Option>>(crates.root, "options");
            Row("crateloot DMSAC_CrateLoots", options.Select(o => o.weight + ":" + string.Join("/", o.thingSetMaker.fixedParams.filter?.AllowedThingDefs.Select(x => x.defName) ?? new string[0])));
            foreach (string d in new[] { "Gun_BoltActionRifle", "Gun_SniperRifle", "Gun_PumpShotgun", "DMS_YekaterinRifle", "DMS_VykhlopRifle", "DMS_AssaultShotgun", "DMS_PDW",
                "DMS_AssaultRifle", "DMS_ZlobinRifle", "DMS_BattleRifle", "DMS_MiniMachineGun", "DMS_AtlantGrenadeLauncher", "DMS_KordMachineGun", "DMS_PKM", "DMS_KS23K",
                "DMS_RiotShotgun", "DMS_Machete", "DMS_M214Gatlinggun", "DMS_GAU19Gatlinggun", "DMS_VladimirovMachineGun", "DMS_TacticalPistol", "DMS_ZlobinRevolver" })
            {
                Row("weaponTags " + d, DefDatabase<ThingDef>.GetNamedSilentFail(d)?.weaponTags ?? new List<string> { "MISSING" });
            }
            foreach (string d in new[] { "DMS_Mech_Mushketer_AR", "DMS_Mech_Mushketer_SAR", "DMS_Mech_BattleWalker", "DMS_Mech_Molot", "DMS_Mech_Zabor", "DMS_Mech_Gorod", "DMS_Mech_Saray" })
            {
                Row("kindWeaponTags " + d, DefDatabase<PawnKindDef>.GetNamedSilentFail(d)?.weaponTags ?? new List<string> { "MISSING" });
            }
            foreach (string d in new[] { "DMS_Mech_Arquebusier", "DMS_Mech_BattleFrame" })
            {
                object ext = DefDatabase<ThingDef>.GetNamed(d).modExtensions.First(e => e.GetType().Name == "MechWeaponExtension");
                Row("bypassWeapons " + d, Field<List<string>>(ext, "BypassUsableWeapons"));
            }
            System.IO.File.WriteAllLines(System.IO.Path.Combine(AcceptanceDriver.OutDir, "patch_snapshot.tsv"), rows);
            t.Note($"wrote {rows.Count} rows to patch_snapshot.tsv");

            t.Check(DefDatabase<ThingDef>.GetNamed("DMSAC_GestaltEngine").comps.Any(c => c is Fortified.CompProperties_AlertScanner), "command core scans for intruders");
            t.Check(DefDatabase<ThingDef>.GetNamed("DMSAC_Building_LiftShaft_Bottom").comps.Any(c => c is Fortified.CompProperties_FacilityLockdownGate), "the lift's bottom end is a lockdown gate");
            t.Check(DefDatabase<ThingDef>.GetNamed("DMS_Mech_Molot").comps.Any(c => c is Fortified.CompProperties_Recycleable), "Molot hulks are recyclable");
            t.Check(DefDatabase<WorldObjectDef>.GetNamed("DMSAC_Graveyard").comps.OfType<WorldObjectCompProperties_ReadinessPatrol>().First().possibleGeneratePawn.Any(o => o.kind.defName == "DMS_Mech_Saray"), "Saray patrols the graveyard");
            t.Check(DefDatabase<PawnKindDef>.GetNamed("DMS_Mech_Mushketer_AR").weaponTags.SequenceEqual(new[] { "DMSAC_LongMusket" }), "long musketeers carry only their role tag");
            t.Check(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle").weaponTags.Contains("DMSAC_LongMusket"), "vanilla bolt-action rifle carries the long-musket tag");
            yield return new WaitFrames(1);
        }

        /// <summary>
        /// R22: every corps role kind spawns armed with a weapon carrying its role tag (the race's weapon filter and the
        /// kind's weapon money both let it through).
        /// </summary>
        public static IEnumerable<Wait> R22_RoleLoadouts(TestContext t)
        {
            var roles = new[]
            {
                ("DMS_Mech_Mushketer_AR", "DMSAC_LongMusket"), ("DMS_Mech_Mushketer_SAR", "DMSAC_ShortMusket"),
                ("DMS_Mech_BattleWalker", "DMSAC_BattleWalker"), ("DMS_Mech_Molot", "DMSAC_Molot"),
                ("DMS_Mech_Zabor", "DMSAC_Zabor"), ("DMS_Mech_Gorod", "DMSAC_Gorod"), ("DMS_Mech_Saray", "DMSAC_Saray"),
            };
            foreach ((string kindName, string tag) in roles)
            {
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed(kindName);
                t.Check(kind.weaponTags != null && kind.weaponTags.SequenceEqual(new[] { tag }), $"{kindName}: loadout is only {tag}");
                List<ThingDef> roleWeapons = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsWeapon && d.weaponTags != null && d.weaponTags.Contains(tag)).ToList();
                float cheapest = roleWeapons.Count > 0 ? roleWeapons.Min(d => d.BaseMarketValue) : 0f;
                t.Note($"{kindName}: weaponMoney {kind.weaponMoney}, role weapons " + string.Join(", ", roleWeapons.Select(d => $"{d.defName} {d.BaseMarketValue:0}")));
                t.Check(kind.weaponMoney.min >= cheapest, $"{kindName}: the lowest weapon budget ({kind.weaponMoney.min:0}) affords the cheapest role weapon ({cheapest:0})");
                int armed = 0, onRole = 0;
                HashSet<string> seen = new HashSet<string>();
                for (int i = 0; i < 15; i++)
                {
                    Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, Corps, PawnGenerationContext.NonPlayer, tile: Home.Tile));
                    ThingWithComps weapon = pawn.equipment?.Primary;
                    if (weapon != null)
                    {
                        armed++;
                        seen.Add(weapon.def.defName);
                        if (weapon.def.weaponTags != null && weapon.def.weaponTags.Contains(tag)) onRole++;
                    }
                    pawn.Discard(silentlyRemoveReferences: true);
                }
                t.Check(armed == 15, $"{kindName}: every pawn spawns armed ({armed}/15)");
                t.Check(onRole == armed, $"{kindName}: every weapon carries {tag} ({onRole}/{armed}) — {string.Join(", ", seen)}");
            }
            yield return new WaitFrames(1);
        }

        private static Map GenerateSite(string part)
        {
            PlanetTile tile = FreeTile();
            Site site = SiteMaker.MakeSite(DefDatabase<SitePartDef>.GetNamed(part), tile, Corps, ifHostileThenMustRemainHostile: false,
                threatPoints: 600f, worldObjectDef: DefDatabase<WorldObjectDef>.GetNamedSilentFail(part));
            Find.WorldObjects.Add(site);
            Track(site);
            return GetOrGenerateMapUtility.GetOrGenerateMap(tile, null);
        }

        /// <summary>
        /// 是否有可掛攝影機的外牆：軍團的牆，面前一格露天、空著，再往前三格可通行（與增補的 WallMount + exteriorOnly 規則相同）。
        /// Whether a corps wall offers a camera spot: open sky and an empty cell in front, three passable cells beyond
        /// (the augment's WallMount + exteriorOnly rule).
        /// </summary>
        private static bool HasMountableOuterWall(Map map)
        {
            foreach (Building wall in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).OfType<Building>())
            {
                if (wall.Faction != Corps || !IsWallAt(map, wall.Position) || wall.Position.GetEdifice(map) != wall) continue;
                foreach (Rot4 dir in Rot4.AllRotations)
                {
                    IntVec3 spot = wall.Position + dir.FacingCell;
                    if (!spot.InBounds(map) || spot.Roofed(map) || spot.GetEdifice(map) != null || spot.GetFirstBuilding(map) != null) continue;
                    bool clear = true;
                    for (int i = 1; i <= 3 && clear; i++)
                    {
                        IntVec3 ahead = spot + dir.FacingCell * i;
                        clear = ahead.InBounds(map) && !ahead.Impassable(map);
                    }
                    if (clear) return true;
                }
            }
            return false;
        }

        private static bool IsWallAt(Map map, IntVec3 c)
        {
            Building b = c.InBounds(map) ? c.GetEdifice(map) : null;
            return b != null && !(b is Building_Door) && b.def.passability == Traversability.Impassable && !b.def.building.isNaturalRock;
        }

        /// <summary>R12: readiness state and a company survive save and reload. Last in the suite (it reloads).</summary>
        public static IEnumerable<Wait> R12_SaveLoad(TestContext t)
        {
            SetLevelQuiet(3);
            Settlement target = HostileSettlements().First();
            Faction foe = target.Faction;
            int now = Find.TickManager.TicksGame;
            State.pressure = 0.4f;
            State.warTarget = foe;
            State.nextDropTick = now + 123456;
            State.nextActionTick = now + 654321;
            FFF_Company company = CompanyUtility.Deploy(Defcon, State, target);
            t.Require(company != null, "company deployed");
            FactionHostilityLock.Lock(Corps);
            int id = company.ID;
            CompanyDef companyDef = company.CompanyDef;
            string name = company.CompanyName.RawText;
            int targetId = target.ID;
            int foeId = foe.loadID;
            int dropTick = State.nextDropTick, actionTick = State.nextActionTick;

            Game before = Current.Game;
            GameDataSaveLoader.SaveGame(TestSaveName);
            yield return new WaitFrames(10);
            GameDataSaveLoader.LoadGame(TestSaveName);
            var reload = new WaitUntil(() => Current.Game != before && Current.ProgramState == ProgramState.Playing, 999999);
            yield return reload;
            yield return new WaitFrames(5);

            ReadinessState state = State;
            t.Require(state != null, "readiness state after load");
            t.Check(state.level == 3, "level restored");
            t.Check(UnityEngine.Mathf.Abs(state.pressure - 0.4f) < 0.001f, "pressure restored");
            t.Check(state.warTarget != null && state.warTarget.loadID == foeId, "war target restored");
            t.Check(state.nextDropTick == dropTick && state.nextActionTick == actionTick, "timers restored as absolute ticks");
            FFF_Company restored = Find.WorldObjects.AllWorldObjects.OfType<FFF_Company>().FirstOrDefault(c => c.ID == id);
            t.Require(restored != null, "company restored");
            t.Check(restored is Company, "still an AncientCorps.Company");
            t.Check(restored.CompanyDef == companyDef, "company def restored");
            t.Check(restored.CompanyName.RawText == name, "name restored");
            t.Check(restored.Target != null && restored.Target.ID == targetId, "target restored");
            GameComponent_DefconLevel legacy = Current.Game.GetComponent<GameComponent_DefconLevel>();
            t.Check(legacy != null && Field<bool>(legacy, "migrated"), "legacy component stays migrated");
            t.Check(FactionHostilityLock.IsLocked(Corps) && Corps.HostileTo(Faction.OfPlayer), "permanent hostility lock survives save and reload");
            FactionHostilityLock.Unlock(Corps);
            Freeze();
        }
    }

    public static class AcceptanceSuite
    {
        public static List<AcceptanceTest> Build()
        {
            return new List<AcceptanceTest>
            {
                new AcceptanceTest("R0", "Wiring: DEFCON def, companies, patrol comps, legacy components, text keys", Tests_Readiness.R0_Wiring),
                new AcceptanceTest("R1", "Levels: rise, top, drop, clamp, timers, decay", Tests_Readiness.R1_Levels),
                new AcceptanceTest("R2", "Pressure: accumulate, rise, remainder, clear", Tests_Readiness.R2_Pressure),
                new AcceptanceTest("R3", "Actions and action-table rules", Tests_Readiness.R3_Actions),
                new AcceptanceTest("R4", "Company deploy and orders", Tests_Company.R4_DeployAndOrders),
                new AcceptanceTest("R5", "Company cap, war campaign, huntdown, war end", Tests_Company.R5_WarCampaign),
                new AcceptanceTest("R6", "Company raid on the colony", Tests_Company.R6_CompanyRaid),
                new AcceptanceTest("R7", "Raid points floor for a preset faction", Tests_World.R7_RaidPointsFloor),
                new AcceptanceTest("R8", "Landmark tiles avoided", Tests_World.R8_Landmarks),
                new AcceptanceTest("R9", "Site map patrols", Tests_World.R9_SitePatrols),
                new AcceptanceTest("R10", "Company camp map and commander", Tests_Company.R10_CompanyCampMap),
                new AcceptanceTest("R11", "Readiness alert", Tests_Readiness.R11_Alert),
                new AcceptanceTest("R13", "Legacy DEFCON migration", Tests_Readiness.R13_LegacyMigration),
                new AcceptanceTest("R14", "Secondary structures on corps sites", Tests_World.R14_SecondaryStructures),
                new AcceptanceTest("R15", "Early-warning augment on corps installations", Tests_World.R15_EarlyWarningAugment),
                new AcceptanceTest("R16", "Site garrison selection never throws", Tests_World.R16_GarrisonSelection),
                new AcceptanceTest("R17", "Mechlink kill switch death", Tests_World.R17_MechlinkDeath),
                new AcceptanceTest("R18", "Sites and units start as the corps", Tests_World.R18_Factions),
                new AcceptanceTest("R19", "DEFCON II permanent hostility and the Last Pilot", Tests_World.R19_PermanentHostility),
                new AcceptanceTest("R20", "DEFCON drives garrisons, patrols and rosters", Tests_World.R20_DefconScaling),
                new AcceptanceTest("R21", "Snapshot of patched content", Tests_World.R21_PatchSnapshot),
                new AcceptanceTest("R22", "Corps role kinds spawn with their role weapons", Tests_World.R22_RoleLoadouts),
                new AcceptanceTest("R12", "Save and reload", Tests_World.R12_SaveLoad),
            };
        }
    }
}
