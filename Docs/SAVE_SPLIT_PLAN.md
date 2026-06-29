# Save System v2 — Per-Season Files + Archived Match Slimming

> Status: **PLAN (awaiting go-ahead before execution)**
> Goal: split the monolithic save into a cross-season core file + one file per season,
> write past-season files once (not every autosave), and slim archived matches that don't
> involve the player's team — keeping autosave cost roughly constant as years pass.

---

## 1. Objectives

1. **One file per season** holding that season's competitions + fixtures.
2. **One overarching "core" file** for everything that persists across seasons.
3. **Archived match slimming**: for completed seasons, matches **not involving the player's team** drop fouls / possession / non-essential data, keeping **final score + starting XIs** (and goals/scorers, which are cheap). The player's own matches keep **full detail in every season**.
4. **Performance**: autosave should write only `core` + the *current* season; past-season files are written once at rollover and never rewritten. Memory holds only the current season.

---

## 2. Current architecture (recap)

- `SaveManager.Save()` → `CollectGameState()` → `JsonConvert.SerializeObject(GameState)` → one `autosave.json`.
- `GameState` fields: `CurrentDay`, `PlayerTeamId`, `Next*Id` (5 counters), `Teams` (`TeamConverter`), `Competitions` (`List<Competition>`, polymorphic League/Cup, **fixtures inline**), `CompetitionSeries`, `FreeAgents`, `Events`, `CurrentTraining`.
- `Competition.Fixtures` is a serialized `List<Fixture>`; `Rounds`/`Template`/`Promotion`/`Relegation` are `[JsonIgnore]` and rebuilt on load.
- A `Fixture.Result` (`Match.Result`) stores `home`/`away` `TeamStats`, each = team ref (ID) + `List<Shot> goals` + `List<Foul> fouls` + `possession`. Players inside shots/fouls are ID refs (`PersonRefConverter`). **Fouls dominate the size; lineups are NOT stored.**
- `FixturesManager` keeps **all** seasons in memory (`Competitions`, `_competitionsById`, `_fixturesById`, `allFixtures`). `StartNewSeason()` currently retains finished instances in memory.
- `FinaliseResult()` (Fixture) is the common path for AI sim and the player's interactive match (`MatchSimPageUI`).

### Cross-season vs per-season data
| Persists across seasons (→ core file) | Per-season (→ season file) |
|---|---|
| CurrentDay, PlayerTeamId, Next*Id counters | The 4 leagues + 1 cup instances for that year |
| Teams (players, **ClubStats**, Boost, positions) | …their inline `Fixtures` (the bulk) |
| FreeAgents, Events, CurrentTraining | League standings (`StandingsData`) |
| CompetitionSeries lineage + season index | |

ClubStats (titles, lifetime totals) lives on `Team`, so it stays in core automatically.

---

## 3. New file layout (folder per slot)

```
<persistentDataPath>/
└── autosave/                         ← one folder per save slot
    ├── core.json                     ← cross-season state + season index (rewritten every save)
    ├── season_2024.json             ← season 1 (written once when it completes; immutable)
    ├── season_2025.json             ← season 2 …
    └── season_2026.json             ← CURRENT season (rewritten every save until it completes)
```

- `GetSavePath(slot)` → `GetSlotDir(slot)` = `<persistentDataPath>/<slot>/`.
- `HasSave` = `core.json` exists. `DeleteSave`/`NewGame` = delete the whole slot folder (and legacy `autosave.json` if present).

### File schemas

**`core.json`** (`CoreSaveState`):
```
CurrentDay, PlayerTeamId
NextPersonId, NextTeamId, NextCompetitionId, NextFixtureId, NextSeriesId
Teams            (TeamConverter, inline players)
FreeAgents
Events
CurrentTraining
CompetitionSeries                       (lineage)
CurrentSeasonYear : int
Seasons : List<SeasonIndexEntry>        { int Year; List<int> CompetitionIds }   ← the file index
```

**`season_<year>.json`** (`SeasonSaveState`):
```
Year : int
Competitions : List<Competition>        (polymorphic; fixtures + standings inline)
```

> Both files use the existing serializer settings (`TypeNameHandling.Auto` is required for the `List<Competition>` polymorphism and for `Event`).

---

## 4. Data-model additions

### 4.1 Capture starting XIs (so archived games can keep them)
- Add `public List<int> lineup;` to `Match.TeamStats` (IDs of the 11 who started).
- In `Fixture.FinaliseResult()` add `CaptureLineups()`:
  ```csharp
  private void CaptureLineups()
  {
      var r = Result;
      r.home.lineup = HomeTeam.StartingPlayers.Select(p => p.PersonID).ToList();
      r.away.lineup = AwayTeam.StartingPlayers.Select(p => p.PersonID).ToList();
      Result = r; // struct write-back
  }
  ```
- Captured for **all** fixtures (cheap: 22 IDs ≈ ~150 B/fixture) since the history feature wants them.

### 4.2 Slimming an archived, non-player match
- Add `Fixture.SlimForArchive()`:
  - Keep: `Result.home/away.team`, `.goals` (gives score + scorers), `.lineup`, final score.
  - Clear: `.fouls` (→ empty list), `.possession` (→ 0).
  - Optionally also clear `goals` shot metadata beyond shooter/assist if we want it even smaller — **default: keep goals** (they're tiny and valuable).
- A fixture "involves the player" if `HomeTeam.TeamId == PlayerTeamId || AwayTeam.TeamId == PlayerTeamId`.

This removes ~70%+ of a fixture's bytes (fouls) while preserving score, scorers, and who played.

---

## 5. SaveManager v2 flow

### 5.1 Save (autosave / manual)
1. `EnsureSlotDir(slot)`.
2. Build `CoreSaveState` (everything except competitions) + `Seasons` index (from the in-memory current season + previously-archived entries tracked in core) → write `core.json`.
3. Build `SeasonSaveState` for the **current** season (`FixturesManager.GetCurrentSeasonCompetitions()`) → write `season_<currentYear>.json`.
4. **Do not** touch past-season files.

→ Autosave cost ≈ core (teams/players, ~constant) + current season (~constant). Flat over time.

### 5.2 Load
1. Read `core.json` → `IdManager.SeedFromState`, set date, restore Teams (TeamConverter), FreeAgents, Events, CurrentTraining, CompetitionSeries, `CurrentSeasonYear`, `Seasons` index.
2. Read `season_<CurrentSeasonYear>.json` → restore that season's competitions (wire fixtures→competition, `BuildRoundsFromFixtures`, standings, promotion/relegation links, register in FixturesManager). (This is today's `RestoreCompetitions`, scoped to one season.)
3. Past seasons are **not** loaded (lazy).

### 5.3 Season rollover (in `FixturesManager.StartNewSeason`)
1. Compute promotion/relegation from the completing season (as today).
2. **Archive the completing season:** for each of its fixtures not involving the player → `SlimForArchive()`; write `season_<oldYear>.json`; add/update its `Seasons` index entry.
3. **Unload** the completing season from memory (remove from `Competitions`, `_competitionsById`, `_fixturesById`, `allFixtures`, `fixturesByTeam`) — keep the `CompetitionSeries` lineage + index entry.
4. Create the new season's competitions in memory (as today); set `CurrentSeasonYear`.
5. Next autosave writes the new current-season file.

### 5.4 History loading (API now, UI later)
- `FixturesManager.LoadSeasonForViewing(int year)` → read `season_<year>.json`, deserialize competitions into a **temporary** cache (not merged into the live `Competitions`), return them for a future history UI. `UnloadViewedSeason()` clears the cache.
- Resolution of team/player refs still works because teams/players live in core (always loaded).

---

## 6. FixturesManager memory model change

- In-memory `Competitions` now holds **only the current season**.
- Add `GetCurrentSeasonCompetitions()` and `CurrentSeasonYear`.
- `ArchiveAndUnloadSeason(year)` helper used by rollover.
- Audit existing readers of `Competitions` (e.g. `Team.GetAllCompetitions`, `GetMainLeague`) — these *should* operate on the current season only, which is the correct behaviour. History reads go through the new loader. **This audit is part of execution.**

---

## 7. Backward compatibility / migration

- Old single-file `autosave.json` is **incompatible** and ignored; `HasSave` looks for the new `core.json`. `DeleteSave`/`NewGame` remove both the new folder and any legacy file. (Project is in development; existing saves are disposable — start a new game.)

---

## 8. Edge cases & safeguards

- **First season:** no archives; core `Seasons` has the single current entry once it completes.
- **Mid-season save:** current-season file holds partially-played fixtures (mixed `BeenPlayed`); load resumes fine.
- **Missing/corrupt season file:** load logs an error and continues with empty competitions rather than crashing.
- **Standings integrity after slimming:** standings are stored on `League.StandingsData` (independent of fouls) and scores derive from `goals` (kept) → slimming does not affect tables.
- **Player in multiple comps:** their league *and* cup ties are both kept full in the archive.
- **Lineups for subs:** only the starting XI is recorded (per the brief).

---

## 9. Execution stages

1. **Data model:** `TeamStats.lineup`, `Fixture.CaptureLineups()` (in `FinaliseResult`), `Fixture.SlimForArchive()`, `Match.Result` slim helper.
2. **Split serialization containers:** add `CoreSaveState` + `SeasonSaveState` (+ `SeasonIndexEntry`); keep `GameState` only if still useful, else retire.
3. **FixturesManager:** current-season-only memory, `GetCurrentSeasonCompetitions`, `CurrentSeasonYear`, `ArchiveAndUnloadSeason`, `LoadSeasonForViewing`/`UnloadViewedSeason`.
4. **SaveManager:** folder-per-slot paths; `Save` writes core + current season; `Load` reads core + current season; `RestoreCompetitionsFromSeason` (scoped); update `HasSave`/`DeleteSave`.
5. **StartNewSeason:** archive (slim non-player) + write season file + unload, then build new season.
6. **GameManager/NewGame:** confirm new-game + delete paths use the folder layout.
7. **Audit** readers of `FixturesManager.Competitions` for current-season-only correctness.

---

## 10. Risks

- Largest risk is the **memory-model change** (current-season-only). Mitigated by the audit step (§6/§9.7) and by keeping the history loader available for any code that genuinely needs past data.
- Save-format change invalidates existing saves (acceptable in dev).
- Cannot compile/run Unity here — each stage written defensively; in-editor verification required (new game → play a season → rollover → confirm `season_*.json` files appear, autosave stays fast, reload restores current season, and a player match in an archived season still has full detail while an AI match is slimmed).

---

## 11. Verification checklist (post-execution)
- [ ] New game creates `<slot>/core.json` + `<slot>/season_<year>.json`.
- [ ] Autosave rewrites only core + current season (past files' timestamps unchanged).
- [ ] Rollover writes the finished season's file once, then unloads it from memory.
- [ ] Archived AI-vs-AI match: no fouls/possession, but score + starting XIs (+ scorers) present.
- [ ] Archived player match: full detail retained.
- [ ] Reload restores date, teams, training, current standings/fixtures.
- [ ] `LoadSeasonForViewing(pastYear)` returns that season's competitions for display.
- [ ] Save size/time stays roughly flat across many seasons.
