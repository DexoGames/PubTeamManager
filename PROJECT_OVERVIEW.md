# Pub Team Manager — Project Overview

A Unity football (soccer) management game where the player manages a team based on a real-world UK
pub. The fantasy hook: every "club" is a real pub loaded from a CSV of UK pubs, and the league
structure, players, and matches are all procedurally generated around the player's chosen pub
("The Hobbit" in Southampton by default).

The game is calendar-driven and turn-based at the day level: the player advances one day at a time
(only from the home screen), and the schedule decides what happens (match, training, interview, pub
trip, rest). Matches are resolved by a phase-based simulation engine. Squad management revolves
around **morale** and **personality** systems rather than money/transfers.

> This document is the high-level map written so a fresh session can get full context. Code is the
> source of truth — verify specific method/field names against the files before relying on them, as
> the project is actively evolving. Companion design/build docs are listed in §14.

---

## 0. Aims, Objectives & Design Principles

**What the game is trying to be**
- A **Football-Manager-style** management sim with a charming, low-stakes "pub league" identity:
  you take a real local pub from the bottom and build it up across seasons.
- **Data-driven and history-rich:** the long-term vision is that you can *look back at past seasons*
  — view old league tables, cup runs, and individual games (scorelines, scorers, line-ups) — like
  modern Football Manager. This goal directly shapes the ID and save architecture (below).

**Fundamental objectives (the player fantasy)**
- *Geography-driven fantasy:* your club and your rivals are real nearby pubs (selected by postcode
  proximity / Haversine distance from your pub).
- *People over money:* there is **no transfer market or wages**. Squad-building is done through
  **interviews** (reading personalities and stats), and player performance is gated by **morale
  alignment** to each player's personality-defined ideal — managed through dialogue.
- *Readable but deep match sim:* a phase-graph engine blending player ability and tactical matchup,
  surfaced live via highlights.
- *Tactical identity:* formations + stackable instructions with dependencies/incompatibilities and
  player-stat reliances, countered by opposing formations.
- *Long-term progression:* a multi-season league pyramid with promotion/relegation, cup runs,
  squad development (training), and lifetime club stats — all persisted for posterity.

**Engineering design principles (followed throughout the codebase)**
1. **Stable IDs at the serialization boundary, direct references at runtime.** Every persistent
   entity (Person, Team, Competition, Fixture, CompetitionSeries) has a stable integer ID allocated
   by `IdManager`. The live object graph uses direct C# references for ergonomics; custom JSON
   converters translate references ⇄ IDs on save/load. Managers expose **O(1) ID lookups**.
2. **Managers own their entities and their lookups.** `PersonManager`, `TeamManager`,
   `FixturesManager` each keep a `Dictionary<int, T>` index plus list(s); resolution is null-safe and
   subclass-tolerant (`is`/`as`, not `GetType()==`).
3. **Bounded, scalable saves.** Cross-season state and the *current* season are the only things held
   in memory and rewritten on autosave; finished seasons are archived to their own files once and
   never rewritten, so save cost stays ≈ constant as years pass (see §12).
4. **Content as data.** Editor-authored content lives in ScriptableObjects / Resources (formations,
   tactics, events, league templates) or static catalogs (`DrillCatalog`); systems read from these.
5. **Self-populating UI.** Pages (`UIPage`) call `Setup()` on every `UIObject` child on show, so most
   widgets refresh themselves; coordinators only handle special cases (e.g. animation).
6. **Defensive serialization.** Mutable structs that must round-trip carry a `[JsonConstructor]`
   (`Shot`, `Foul`, `Minute`, `Morale`); `ISaveable.OnAfterDeserialize` rebuilds derived/ignored state.

---

## 1. Tech & Conventions

- **Engine:** Unity, C# (MonoBehaviour singletons for controllers, ScriptableObjects for editor-authored data).
- **Serialization:** Newtonsoft.Json (not Unity's JsonUtility). `TypeNameHandling.Auto`, ignore
  reference loops, ignore nulls, indented. Models carry their own `[JsonProperty]`/`[JsonIgnore]`/
  `[JsonConverter]` attributes. Custom converters live in `Assets/_Scripts/Serialization`.
- **Singleton pattern:** Almost every controller is `public static T Instance { get; }` set in `Awake()`.
  ⚠️ **`IdManager` must be on a scene GameObject** (same managers object) or allocations NRE.
- **Tweening:** DOTween (Demigiant) is available incl. the UI module (`DOAnchorPos`, `DOSizeDelta`, `DOFade`).
- **Scripts root:** `Assets/_Scripts/`, organized into `Controllers`, `DataModels`, `UI`, `Utilities`,
  `Serialization`, `Interfaces`, `Interview`.
- **Authored data:** `Assets/Resources/` — loaded at runtime via `Resources.Load`/`LoadAll`.

### Folder layout (scripts)
```
_Scripts/
  Controllers/    GameManager, TeamManager, FixturesManager, CalenderManager, EventsManager,
                  PersonManager, RecruitmentManager, ScheduleManager, TrainingManager,
                  SaveManager, UIManager, IdManager
  DataModels/     Person, Player, Manager, Team, Pub, Competition, League, Cup, Fixture,
                  CompetitionSeries, LeagueTableEntry, LeagueTemplate, Event, EventType,
                  CompetitionEvents, ClubStats, ScheduleEntry, TrainingSession, Drill, Formation
    Tactics/      Tactic, TacticStats, TacticInstruction, TacticTemplate
    MatchSim/     Highlight + subclasses (Goal/Shot/Miss/Mistake/Possession/PossessionFail)
  Utilities/      Game, Match, MatchEngine, Phase, FixtureGenerator, LinkBuilder, LinkHandler, KitColors
  Serialization/  GameState (=> CoreSaveState/SeasonSaveState/SeasonIndexEntry), ISaveable,
                  TeamConverter, *RefConverter (Team/Person/Competition/Fixture), ScriptableObjectRefConverter
  Interview/      InterviewQuestion (+ InterviewAnswerGenerator)
  UI/             page controllers (UIPages/), widgets, dialogue, contexts
    Home/         DayTimelineWidget, DayBox, CompetitionContextWidget, CupTieRowUI,
                  CurrentRoundWidget, SquadStatusWidget, AdvanceOrHomeButton, PlayerRowUI
  Interfaces/     UIPage (abstract base), IDialogueContext
```

---

## 2. Core People Model

### Person (`DataModels/Person.cs`)
Base class for `Player` and `Manager`.
- Identity: `PersonID` (allocated by `IdManager`), `FirstName`/`Surname`/`FullName`, `DateOfBirth`, `Team` ref (`[JsonConverter(TeamRefConverter)]`).
- `PersonalityType` enum (10): Aggressive, Calm, Cautious, Cocky, Driven, Kind, Lazy, Shy, Silly, Smart.
- `Rating` enum: F, E, D, C, B, A, S.
- `GeneratePerson()` randomizes name, DOB, personality, a `RatingOffset` (hidden modifier), starting morale,
  and registers the person via `PersonManager.RegisterPerson(this)` (which assigns the `PersonID`).
- **Morale** is a `struct` (`Mood` + `Passion`, 0–100) with per-person `IdealMood`/`IdealPassion` from
  personality (±5 jitter). Has a `[JsonConstructor]`. Distance to ideal drives colour, face sprite, stat penalties.

### PersonManager (`Controllers/PersonManager.cs`)
Global registry. `Dictionary<int, Person> _byId` + `List<Person> People`.
- `RegisterPerson(person)` → allocates a **new** `PersonID` via `IdManager` and indexes (used at creation).
- `RegisterExisting(person)` → indexes **without** reallocating (used on load, keeps saved IDs).
- `GetPerson(id)` / `GetPlayer(id)` / `GetManager(id)` (subclass-tolerant) / `Unregister` / `Clear`.

### Player (`DataModels/Player.cs`)
- `RawStats` (`Stats` struct): 18 skills (`int[]` indexed by `PlayerStat`), `Height`, and a
  `Dictionary<Position, PositionStrength>`. Derived composites (`[JsonIgnore]`): Attacking, Midfield,
  Defending, Mental, Physical, Goalkeeping.
- **Positions:** 12-value `Position` enum; `PositionStrength` None→Poor→Okay→Good→Natural.
- **Stat pipeline:** `GetRawStats()` → `GetBoostedRawStats()` (adds the temporary training Boost) →
  `GetStatsFor(position)` (off-position penalty) → `MoraleModifier` → `GetStats()` (effective in-match stats).
- **Training "Boost" layer (temporary):** `int[] Boost` (0..`MAX_BOOST`=15 per skill), applied on top of raw
  skills in `GetBoostedRawStats`. `ApplyBoostSession(trainedStats, build, decay)` builds trained stats and
  decays the rest; `GetBoost(stat)`, `EnsureBoost()` guard old saves.
- **Positional progression:** `Dictionary<Position,int> PositionProgress`; `TickPositionalRoll(pos)` rolls
  50% to increment the counter, levels up `PositionStrength` at thresholds (`ProgressThreshold`: None→Poor 2,
  Poor→Okay 3, Okay→Good 4, Good→Natural 8) and resets. (Replaced the old continuous `ImprovePositionalStrength`.)
- `Fatigue` (public getter), `TacticFamiliarity` (0–100). `ModifyStat` still exists (permanent change utility).

### Manager (`DataModels/Manager.cs`)
- `ManStats`: manager skills, a chosen `Formation`, a `TacticTemplate` + its `Instructions`. Derived
  `Tactics`/`Motivation`/`Communication`. `TacticsMatch` gives a tactical edge vs an opposing formation.

### Team (`DataModels/Team.cs`) — a **ScriptableObject**
- `Name`, `YearFounded`, `teamId` (via `IdManager`), `TeamColor` **and `AwayColor`**.
- **Kit colours** are deterministic from the pub identity via `KitColors` (FNV-1a hash → curated 30-colour
  palette for home; away picked from palette entries that contrast with home). Serialized in `TeamConverter`.
- `Players` list + role filters (`Defenders`/`Midfielders`/`Attackers`/`Goalkeeper`/`WidePlayers`/
  `StartingPlayers` (first 11)/`Substitutes`). Owns `Manager`, `Tactic`, `ClubStats`, `KitNumbers`.
- Team-level match stats blend player ability with tactic (`Security`, `Threat`, `Control`, …).
- `GenerateTeam()` creates a Manager, Tactic, and 21 players. `GetAllCompetitions()`/`GetMainLeague()`/
  `GetUpcomingFixture()` query the (current-season) FixturesManager. **Tactic is not yet serialized** (resets on load).

### Pub (`DataModels/Pub.cs`)
CSV data: FAS ID, name, address, postcode, easting/northing, lat/long, local authority. `PostcodePrefix`
and `DistanceTo` (Haversine km) drive geographic team selection.

---

## 3. Competitions & Season Lineage

### Competition (abstract, `DataModels/Competition.cs`)
Plain C# base. `Id`, `SeriesId`, `SeasonYear`, `Name`, `Priority`, `Teams`, `Fixtures` (inline), `Rounds[]`
(`[JsonIgnore]`, rebuilt), `IsComplete`. Helpers: `FindNextAvailableDate`, `BuildRoundsFromFixtures`,
`GetUpcomingRound`/`GetMostRecentRound`.

### League (`DataModels/League.cs`) — `: Competition, ISaveable`
Created from a `LeagueTemplate`. **Double round-robin** (rounds ~14 days apart). Serializes `StandingsData`
(`LeagueTableEntry` list), sorted points→GD→GF. `PromotionLeague`/`RelegationLeague` (`[JsonIgnore]`, re-linked
on load by template name). `OnAfterDeserialize` rebuilds rounds + resolves the `Template` by name.

### Cup (`DataModels/Cup.cs`) — `: Competition, ISaveable`
Single-elimination knockout with byes (`autoSecondRoundTeams`, `[JsonIgnore]` — **not persisted**, so only
the *current round's ties* are recoverable, not a full historical bracket tree). `TryGenerateNextRound`
advances winners; drawn ties resolved by a strength-weighted coin flip.

### Fixture (`DataModels/Fixture.cs`) — `ISaveable`
- `Id` (via `IdManager`, allocated in the parameterized ctor only), Home/away teams (`TeamRefConverter`),
  `Date`, `Round`, `Competition` (`[JsonIgnore]`, re-wired on load), `Match.Result`, `BeenPlayed`.
- `FinaliseResult()` records club stats, fires win/loss events for the player's team, advances cup rounds /
  updates league standings, and **`CaptureLineups()`** (records both starting XIs into the result).
- `SlimForArchive()` strips a result for long-term storage (drops fouls + possession; keeps score, scorers,
  line-ups) — used for non-player matches when a season is archived. `InvolvesTeam(id)` gates it.

### CompetitionSeries (`DataModels/CompetitionSeries.cs`) — season lineage
Links each season's `Competition` **instance** across years (each season is its own instance). Fields: `Id`
(via `IdManager`), `Name`, `TemplateName` (anchors to the editor template by name), `SeasonCompetitionIds`.
`Seasons` resolves the instances via `FixturesManager.GetCompetition`. This is the spine of the
"view past seasons" feature.

### Structure (set up in `FixturesManager.AddComps`)
4 leagues of 20 (Premier/Div1/Div2/Div3) + one cup ("Papa Johns Cup"). ~81 teams = 80 league + player.
Each competition is registered into a `CompetitionSeries` (created/looked-up by template name) with its
`SeasonYear`. `StartNewSeason()` (see §12) rolls over: applies promotion/relegation into fresh rosters,
creates new instances linked to the same series, then **archives + unloads** the finished season.

---

## 4. Formations & Tactics

- **Formation** (SO): `Positions[]` (each a `Player.Position` + pitch `Vector2Int`), `FormStats`, `Inferiors[]`
  (formations it beats). Assets in `Resources/Formations/Usable` (4-4-2, 4-3-3, 4-2-3-1, 5-3-2) + `Other/AllPositions`.
- **Tactic** (`Tactics/Tactic.cs`): runtime per-team state — chosen `Formation`, `TacticInstruction` list, and 12
  tactic stats (`TacticStat`). `RecalculateStats()` applies instruction modifications + dependencies (floors/ceilings).
- **TacticInstruction / TacticTemplate / TacticStats** (SOs + helper): named modifiers (HighLine, LowBlock,
  CounterPressing, …) with stat modifications, team/tactic dependencies, reliances, and incompatibilities;
  templates (Pep, Dyche, …) bundle a formation + instructions. ⚠️ `Tactic` vs `TacticStats` overlap suggests
  an in-progress refactor; **`Team.Tactic` is not currently serialized**.

---

## 5. Match Simulation

Phase-based: each "minute" runs a possession sequence flowing through attacking/defending phases until a
shot is attempted or possession is lost.

- **Match** (`Utilities/Match.cs`): home/away teams, `Result`, `currentMin` (`Minute`), a `MatchEngine`.
  `Result` aggregates per-team `TeamStats` = team ref + `List<Shot> goals` + `List<Foul> fouls` + `possession`
  + **`List<int> lineup`** (starting XI PersonIDs, captured at `FinaliseResult`).
  - `Shot` (type/outcome/team/shooter/assister/xG/minute) and `Foul` (card/offender/victim/injury/minute) are
    **structs with `[JsonConstructor]`s** (required — Newtonsoft can't set members on a value-type via its value
    provider). Player refs inside use `PersonRefConverter` (serialize as IDs).
- **MatchEngine** (`Utilities/MatchEngine.cs`): `SimulateMinute()` picks the attacking team by possession share,
  runs the **phase graph** (Build→Progress→Probe; Advance→Penetrate; Counter→Break), resolves each phase by
  blending tactical + ability differences with Stability-driven randomness, and `AttemptShot` selects a weighted
  shooter/shot-type and applies a non-linear keeper effect for the goal probability.
- **Highlights** (`DataModels/MatchSim/`): Goal/Shot/Miss/Mistake/Possession/PossessionFail, broadcast live to the
  match-sim UI when `trackHighlights` is on (the player's match).

---

## 6. Morale, Events & Discussions

A central pillar: results and random life events shift player morale; the manager responds through dialogue to
nudge morale toward each player's ideal.

- **EventType** (SO, `Resources/Events` + `Events/Random`): template — `tag` (Basic = win/loss, Special = else),
  tokenized `description`/`discussion`, `odds`, `noAffected`, `moodChange`, `severity`.
- **Event** (`DataModels/Event.cs`): runtime instance (EventType, affected `Person`s, date, custom words).
  `Response` enum (Praise, Encourage, Challenge, Persuade, Inspire, Galvanise, Rage, Deflect); `Reaction` enum
  (Terrible…Amazing); `ReactionSeverityChange`.
- **EventsManager**: on `NewDay` adds random events (weighted) + expires old ones; `AddWinEvent`/`AddLoseEvent`
  from `Fixture.FinaliseResult`; builds the **`ReactionTable`** `(Response × PersonalityType) → Reaction`.
- **Discussions**: `DiscussionPageUI.Response(...)` applies the reaction + morale change, removes the event,
  and triggers a `SaveCore()` (see §12). `IDialogueContext`/`DiscussionContext`/`DialogueUI` drive the scene.

---

## 7. Recruitment & Interviews

No transfer market — players are hired via **interview days**.
- **RecruitmentManager**: `FreeAgentPool` (~30). An interview presents 5 candidates one at a time; hire 1 or
  reject (permanent). `HirePlayer`/`ReleasePlayer` use `RegisterExisting` (free agents already have IDs).
- **InterviewQuestion / InterviewAnswerGenerator**: question types answered from real stats **filtered through
  personality** (Cocky overestimates, Shy underestimates, Smart accurate); `InterviewAnswer.IsAccurate` exposes
  actual vs perceived.
- **InterviewManager** (`UI/`): drives an interview via `DialogueUI` (≤5 questions) then hire/reject.

---

## 8. Training (the "Boost" system)

Training applies a **temporary, decaying per-stat "Boost"** layer (FM-style match sharpness) plus permanent
positional development. The player sets one ongoing drill that repeats every training day until changed.

- **Drill / DrillId / DrillCatalog** (`DataModels/Drill.cs`): static catalog of **16 drills**. Each `Drill` has
  `Id` (`DrillId` enum — append-only, persisted), `Name`, `TrainingType` (Technical/Mental/Physical/Tactical/
  Social/Positional), `AffectedStats[]`, `Description`. `IsBoostDrill` = Technical/Mental/Physical.
- **TrainingSession** (`DataModels/TrainingSession.cs`): the player's chosen instruction — **serializable, ID-based**
  (`DrillId Drill`, `Player.Position? TargetPosition`, `List<int> SelectedPlayerIds`; resolves `Drill` via
  `DrillCatalog` and players via `PersonManager`). `Execute(team)` over the **whole squad**:
  - Boost drills: `+1` to affected stats (cap 15), `-1` decay to the rest, via `Player.ApplyBoostSession`.
  - Tactical → `TacticFamiliarity`; Social → morale; Positional → `Player.TickPositionalRoll` for ≤5 players.
  - Tunables: `MAX_BOOST=15`, `BUILD_PER_SESSION=1`, `DECAY_PER_SESSION=1`, `MAX_POSITIONAL_PLAYERS=5`.
- **TrainingManager**: `GetDrills()`→`DrillCatalog.All`; `SetTraining(session)` **persists only** (no execute);
  `ExecuteTraining()` runs on scheduled training days (default drill = Fitness). `CurrentSession` is saved in
  `CoreSaveState.CurrentTraining`.
- **TrainingPageUI**: drill list + info panel + positional sub-panel (position dropdown + up-to-5 player
  multiselect with rating image + clickable name links); "Set Training" persists and `SaveCore()`s. (Build guide:
  `TRAINING_UI_LAYOUT.md`.)

---

## 9. Calendar, Schedule & Game Loop

### CalenderManager
Owns `CurrentDay` (starts **2024-08-01**). `AdvanceDay()` increments the date, fires `NewDay`, then shows the
player's match (if any) or the home page. A listener/response counter (`ConfirmAddedListener`/`RespondToAdvance`)
gates re-advancing until processing completes.

### ScheduleManager
Builds a 120-day-ahead `ScheduleEntry` map for the player: match days (from fixtures), interview (every 14d),
training (Wednesdays), **pub trip (non-match Sundays)**, else rest. `ScheduleEntryType` = Match/Training/
Interview/RestDay/**PubTrip**. `ProcessToday()` raises `OnMatchDay`/`OnTrainingDay`/`OnInterviewDay`.
`GetUpcoming(n)` (today-inclusive), `GetNextTrainingDay()`.

### GameManager — orchestrator
- `Start()`: if a save exists, `Load()`; on **load failure** it does *not* show a half-loaded UI — it sets a
  static "start fresh" flag and **reloads the scene** for a clean slate (save left untouched). No save → `SetupGame()`
  (spawn teams → AddComps → seed recruitment → schedule → wire training day → UI → **initial full Save**).
- `NewDay(date)`: processes the schedule, simulates due AI fixtures, defers the player's fixture to an interactive
  match-sim (`PlayerMatchSim`), and **autosaves every day** (the player's match completion autosaves again).
- `NewGame()`: deletes the save and reloads the scene.

**Daily loop:** (on home screen) advance day → schedule processes → AI matches simulate → player match (if any) →
events/morale update → autosave → home dashboard animates to the new day.

---

## 10. Controllers Summary (singletons)

| Controller | Responsibility |
|---|---|
| `GameManager` | Boot, save/new-game decision, load fail-safe, per-day orchestration |
| `IdManager` | Per-type ID allocators (Person/Team/Competition/Fixture/Series); `SeedFromState` on load. **Must be on a scene object.** |
| `TeamManager` | Loads pubs CSV, selects ~81 nearby pubs, spawns teams (kit colours via `KitColors`), `_byId` lookup, `MyTeam` = index 0 |
| `FixturesManager` | **Current-season** competitions + fixture/competition/series lookups; builds pyramid + cup; `CompetitionSeries` lineage; `StartNewSeason` (archive + unload); lazy history loading |
| `CalenderManager` | Current date + day advancement + `NewDay` |
| `ScheduleManager` | Per-day activity schedule (incl. PubTrip); `GetNextTrainingDay` |
| `TrainingManager` | Ongoing drill (catalog-driven Boost system) + execution |
| `RecruitmentManager` | Free-agent pool + interview lifecycle |
| `EventsManager` | Random/result events, morale changes, reaction tables |
| `PersonManager` | Global `Person` registry by ID (`RegisterPerson`/`RegisterExisting`/`Get*`) |
| `SaveManager` | Multi-file JSON save/load (core + per-season), archival, `SaveCore` |
| `UIManager` | Page switching (`HideAllUI` + `Show*`); `IsHomeActive` |

---

## 11. UI Layer

- **UIPage** (`Interfaces/UIPage.cs`): abstract page base. `Show(...)` overloads activate the element root, call
  `OnShow(...)`, then `SetupUI()` which calls `Setup()` on every `UIObject` child (self-populating widgets).
  Now also has an **`OnHide()`** hook fired when a *visible* page is navigated away (e.g. `TacticsPageUI.OnHide`
  → `SaveCore()` to persist tactics).
- **UIManager** routes to page singletons (PlayerDetails, TeamDetails, Competition, Home, MatchSim, Tactics,
  Discussion, Schedule, Training, Recruitment). Tracks `IsHomeActive`.
- **Home dashboard** (`UI/Home/` + `UIPages/HomePageUI.cs`): a coordinator + independent widgets. (Plan:
  `HOME_SCREEN_PLAN.md`; editor build: `HOME_SCREEN_BUILD.md`.)
  - `HomePageUI` (coordinator): only manages the day strip — on show it detects a **+1-day advance** (vs the last
    shown day) and animates, else snaps; other panels are `UIObject`s auto-refreshed by `SetupUI`.
  - `DayTimelineWidget` + `DayBox`: animated **upcoming-days strip** (8 days; today = lead box, larger **and taller**;
    future days small). On advance the strip **DOTween-slides left and resizes** (top-anchored, lead vs small heights),
    today exits left, a fresh day enters from the right via a reserved "spare" box; day-type icons per `ScheduleEntryType`.
  - `CompetitionContextWidget` + `CupTieRowUI`: bottom-left panel keyed to the **next fixture** — a 5-row league
    table window centred on the player (if a league game) or the **current knockout round's ties** (if a cup tie),
    with a main-league fallback. `CupRoundName(ties)` names the round.
  - `CurrentRoundWidget`: this gameweek's fixtures (ported from the old HomePageUI). `SquadStatusWidget`: fatigue /
    low-morale flags with clickable links. Events inbox reuses `PlayerEventsUI`.
  - `AdvanceOrHomeButton`: the top-nav button advances the day only on the home page; elsewhere it becomes a "Home"
    button (you must be home to advance — which also guarantees the slide animation is on-screen).
- **Other widgets**: drag-and-drop squad/formation editing (`FormationUI`, `BenchManager`, `PositionUI`,
  `PlayerRowUI`), stat displays (`UIStatDisplay` incl. rating sprites), league table, tactics sliders/toggles,
  dialogue (`DialogueUI`, `ResponseManager`), notifications, morale UI.
- **LinkBuilder / LinkHandler**: clickable rich-text links (`<link="player/{id}">`) routing to detail pages.

---

## 12. ID & Save System (v2 — multi-file)

### ID allocation
`IdManager` holds five per-type counters (`NextPersonId/TeamId/CompetitionId/FixtureId/SeriesId`) with
`Allocate*Id()`. Counters are stored in the core save and re-seeded via `SeedFromState` on load so post-load
entities never collide.

### Reference converters (`Serialization/`)
`TeamConverter` (writes a team's full graph: id, name, year, `TeamColor`+`AwayColor`, players inline, manager,
stats; registers teams/players into the managers during read via `RegisterExisting`). `TeamRefConverter`,
`PersonRefConverter`, `CompetitionRefConverter`, `FixtureRefConverter` serialize references as **IDs** and resolve
them through the managers on read. `ScriptableObjectRefConverter` resolves SOs by name from Resources.

### File layout (folder per slot)
```
<persistentDataPath>/<slot>/        (slot = "autosave")
  core.json            cross-season state, rewritten every save
  season_<year>.json   one per season; the CURRENT season is rewritten each save,
                       finished seasons written ONCE at rollover (then immutable)
```
- **CoreSaveState** (`Serialization/GameState.cs`): `CurrentDay`, `PlayerTeamId`, the 5 `Next*Id` counters,
  `Teams` (TeamConverter), `FreeAgents`, `Events`, `CurrentTraining`, `CompetitionSeries`, `CurrentSeasonYear`,
  `Seasons` (`List<SeasonIndexEntry>` = year → competition IDs).
- **SeasonSaveState**: `Year` + `Competitions` (polymorphic League/Cup, fixtures + standings inline).
- **SeasonIndexEntry**: `Year` + `CompetitionIds`.

### SaveManager API
- `Save(slot)` → writes `core.json` + the **current** season file. `SaveCore(slot)` → writes **only** core (fast;
  falls back to a full save if no season file exists). `Load(slot)` → reads core (teams/players register first),
  then the current season file, then schedule/training wiring; returns `false` on failure (GameManager handles it).
- `HasSave` checks `core.json`; `DeleteSave` removes the slot folder (+ legacy `autosave.json`).
- `ArchiveSeason(year, comps)` (called at rollover) slims **non-player** matches then writes the season file once.
- `LoadSeasonForViewing(year)` deserializes a past season into a **viewing cache** (`FixturesManager.RegisterViewedSeason`)
  for a future history UI, without merging into live state.

### Memory model & rollover
`FixturesManager` holds **only the current season** in memory; past seasons live on disk and are lazy-loaded.
`StartNewSeason()`: applies promotion/relegation into fresh rosters, creates new instances linked to the same
`CompetitionSeries`, advances `CurrentSeasonYear` + the season index, then **archives (slims non-player) + unloads**
the finishing season. → autosave cost stays ≈ constant (core + one season) regardless of years played.

### Autosave triggers
Full save: **every day advance**, player-match completion, new-game start. Fast `SaveCore`: setting training,
leaving the tactics page (`UIPage.OnHide`), responding to a player event.

### ISaveable
`OnAfterDeserialize()` rebuilds ignored/derived state (League/Cup rebuild `Rounds` + resolve templates; Event
resolves EventType + persons). Mutable structs that round-trip carry `[JsonConstructor]`s.

---

## 13. Authored Data (`Assets/Resources/`)

- `open_pubs` (CSV TextAsset) — real UK pub dataset (source of all club names/locations).
- `Teams/` — template Team SOs (spawner clones `teams[0]` and renames per pub).
- `Formations/Usable/` (4-4-2, 4-3-3, 4-2-3-1, 5-3-2) + `Other/AllPositions`.
- `Tactics/Instructions/` & `Tactics/Templates/` — tactical building blocks & manager presets.
- `Events/` & `Events/Random/` — win/loss + flavour life events; `Responses/` — managerial response assets.
- `Competitions/Leagues/` — the four `LeagueTemplate`s with promotion/relegation links.
- `Art/Morale/` — morale face sprites; `Art/Ratings/` — F–S rating badge sprites (`UIStatDisplay.GetRatingSprite`).
- **Needed for the new UI:** day-type icon sprites (match/training/interview/pub-trip/rest) for the home day strip.

---

## 14. Companion docs (plans / build guides)

| Doc | Purpose |
|---|---|
| `SAVE_SYSTEM_PLAN.md` | Original ID-migration + reference-by-ID plan |
| `SAVE_SPLIT_PLAN.md` | Save v2: per-season files + archived-match slimming |
| `TRAINING_SYSTEM_PLAN.md` | The Boost training system design |
| `TRAINING_UI_LAYOUT.md` | Editor build guide for the training page |
| `HOME_SCREEN_PLAN.md` | Home dashboard design + decisions |
| `HOME_SCREEN_BUILD.md` | Step-by-step editor build guide for the home screen |

---

## 15. Known in-progress / rough edges (verify before building on)

- **Tactics aren't serialized:** `TeamConverter` doesn't write `Team.Tactic`, and `RestoreTeamState` rebuilds a
  default tactic — chosen formation/instructions don't survive a reload (player *ordering* does, via the `Players`
  list). The "save on leaving tactics" trigger is wired but needs tactic serialization to be meaningful.
- **`Tactic` vs `TacticStats`** overlap (two stat containers) — likely mid-refactor.
- **Match-sim coverage:** fouls/cards/injuries (`Foul`, `Card`, `InjuryType`) and some shot types
  (Penalty/Free_Kick/Corner) are modelled but only partially wired; `Fatigue` is read by the squad-status widget
  but not deeply driven by play yet.
- **Cup history:** byes aren't persisted, so only the current round's ties are recoverable (no full historical tree).
- **History UI:** `LoadSeasonForViewing` exists but the past-seasons browsing UI isn't built yet.
- **Season rollover trigger:** `StartNewSeason()` archives/unloads correctly but where it's invoked at season end
  (and schedule regeneration after rollover) should be confirmed.
- **`Fixture.cs`** still contains a large commented-out legacy sim (historical only).
- **Recently fixed (context):** manager IDs were being reallocated on load (`RegisterPerson` → now `RegisterExisting`),
  which had corrupted person lookups; `Shot` now has a `[JsonConstructor]`; load failures now fail safe.
