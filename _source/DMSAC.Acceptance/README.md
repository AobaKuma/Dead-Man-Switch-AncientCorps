# DEFCON / company in-game acceptance (RimWorld 1.7)

Plays the corps DEFCON and companies inside the real game, now that both run on FFF's readiness system
(`Fortified.ReadinessDef`, `FFF_Company`, `WorldObjectComp_ReadinessPatrol`). Same scheme as
`_Dead-Man-Switch-Expedition/.source/DMSE.BVRAcceptance`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File _source/DMSAC.Acceptance/run.ps1 -OutDir <dir> [-TimeoutMinutes 20] [-Only R4,R9]
```

- Close RimWorld first (it locks the assemblies). Build FFF and `_source/AncientCorps` first: the runner references
  `1.7/Assemblies/AncientCorps.dll` and `_Fortified-Framework/1.7/Assemblies/Fortified.dll` and tests whatever is there.
- `run.ps1` copies `DMSAC.Acceptance.dll` into `1.7/Assemblies` for this run only and removes it afterwards, also on a
  crash or timeout. Never ship that DLL.
- The game starts with `-quicktest` and the active mod list as is. The runner deletes its temporary save
  (`DMSAC_Acceptance`) and quits the game when done. Your own saves are untouched.
- A full run takes about 5 minutes. `-Only` takes comma-separated test ids.
- **Any error logged while a test runs fails that test** (`log error: …` in the detail), so map generation, letters and
  raids are checked for silent errors too.

## Output (`<dir>`)

| File | Content |
|---|---|
| `dmsac_acceptance_results.tsv` | One row per test: id, PASS/FAIL, title, detail |
| `dmsac_acceptance_log.txt` | Every check (`ok:` / `FAIL:`), notes and tick stamps |
| `Player.log` | Game log of the run |

## Tests

| Id | Covers |
|---|---|
| R0 | `DMSAC_Defcon` (6 levels, corps worker, huntdown at V, camp world object and site part), every corps company def (no config errors, corps worker, name pack, leaders), patrol comps and their letter keys on the four site defs, legacy components still load, Keyed text resolves, site garrisons read the readiness level |
| R1 | Raise / drop / clamp, letters and their types per level, per-level drop and action timers, a raise at the top only re-rolls the timer, a due drop timer lowers the level on its own |
| R2 | Pressure accumulates, rises at 1 (several levels at once), keeps the remainder, clears on a drop and at the top |
| R3 | Built-in actions (raise, drop, quest with skip-if-active, takeover, disband), chance 0, `requireWar`, `requireHostileToPlayer`, a level change ends the action tick, an exclusive action ends it |
| R4 | Company deploy (corps-owned `AncientCorps.Company`, def allowed at the level, within the combat radius, not on a landmark, alone on its tile, site part, name, description) and every order: hold, takeover (renamed corps settlement, moves beside it), raze (ruins), split (second company takes the target), roam, withdraw |
| R5 | Peacetime trims to the level cap; level V declares war on a hostile humanlike faction, starts the huntdown, deploys companies spread over untargeted settlements; below V the huntdown stops but the war goes on; the end of the war falls back to level II with a letter |
| R6 | A company raid on the colony: leaders present, every platoon on the corps lord job, raid letter from the corps keys |
| R7 | A 52.5-point raid with the corps preset is raised to the corps' cheapest combat group, finds a strategy and arrives; raids without a preset faction keep their points |
| R8 | Site-tile searches, quest sites and company camps never land on landmark tiles; `WithoutLandmarks` behaviour |
| R9 | A logistic-terminal site map generates its structure without errors; the first patrol turns the corps hostile and raises DEFCON; later patrols add pressure, hurry the next action and send the reinforcement letter; heavy units join at level III |
| R10 | Entering a camp raises DEFCON, spawns the whole company as a defending garrison with commanders; without commanders the camp leaves with the player |
| R11 | The right-hand alert: inactive at 0, label with faction and level, drop countdown, pressure, war target |
| R13 | The legacy `GameComponent_DefconLevel` moves an old save's level, peak and war target into readiness once |
| R14 | Every corps installation site generates with DMS2's late-scatter ruins (`DMS_SiteStruct` / `DMS_RuinScattered`); their early-warning devices are corps-owned; no errors |
| R15 | The `DMSAC_EarlyWarningNet` augment (ruin scatter and satellite rings switched off so every device comes from it): cameras on outer walls scanning away from them, sensors beside doors, small scanners indoors, towers and FPV nests outdoors, all corps-owned and wired into the layout's power net; level 0 places only cameras and sensors; `applyAugments=false` places none |
| R12 | Level, pressure, war target, absolute timers and a company (class, def, name, target) survive save and reload. Last in the suite (it reloads) |

## Test environment

- Storyteller incidents are off. Readiness timers are frozen at the start of each test and after level changes
  (`Fixture.Freeze`), so DEFCON never acts or drops on its own unless a test waits for it.
- The corps uses goodwill; tests set the relation through goodwill (`Fixture.SetCorpsRelation`) and restore it.
- Company order odds are forced per test (`Fixture.ForceOrders`) and restored at cleanup.
- Companies, sites, quests, corps pawns on the home map and the huntdown are removed after every test.

To add a test, write an `IEnumerable<Wait>` method taking a `TestContext` and add it to `AcceptanceSuite.Build()` in
`Tests_World.cs`.
