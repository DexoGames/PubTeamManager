# Pub Team Manager — Project Overview

A Unity football (soccer) management game where the player manages a team based on a real-world UK
pub. The fantasy hook: every "club" is a real pub loaded from a CSV of UK pubs, and the league
structure, players, and matches are all procedurally generated around the player's chosen pub
("The Hobbit" in Southampton by default).

The game is single-save, calendar-driven, and turn-based at the day level: the player advances one
day at a time, and the schedule decides what happens (match, training, interview, rest). Matches are
resolved by a phase-based simulation engine. Squad management revolves around **morale** and
**personality** systems rather than money/transfers.

> This document is a high-level map written for future context. Code is the source of truth — verify
> specific method/field names against the files before relying on them, as the project is evolving.

---

## 1. Tech & Conventions

- **Engine:** Unity, C# (MonoBehaviour singletons for controllers, ScriptableObjects for editor-authored data).
- **Serialization:** Newtonsoft.Json (not Unity's JsonUtility). Models carry their own `[JsonProperty]`/
  `[JsonIgnore]`/`[JsonConverter]` attributes. Custom converters live in `Assets/_Scripts/Serialization`.
- **Singleton pattern:** Almost every controller is `public static T Instance { get; }` set in `Awake()`.
- **Scripts root:** `Assets/_Scripts/`, organized into `Controllers`, `DataModels`, `UI`, `Utilities`,
  `Serialization`, `Interfaces`, `Interview`.
- **Authored data:** `Assets/Resources/` — loaded at runtime via `Resources.Load`/`LoadAll`.

### Folder layout (scripts)
```
_Scripts/
  Controllers/    GameManager, TeamManager, FixturesManager, CalenderManager, EventsManager,
                  PersonManager, RecruitmentManager, ScheduleManager, TrainingManager,
                  SaveManager, UIManager
  DataModels/     Person, Player, Manager, Team, Pub, Competition, League, Cup, Fixture,
                  LeagueTableEntry, LeagueTemplate, Event, EventType, CompetitionEvents,
                  ClubStats, ScheduleEntry, TrainingSession, Formation
    Tactics/      Tactic, TacticStats, TacticInstruction, TacticTemplate
    MatchSim/     Highlight + subclasses (Goal/Shot/Miss/Mistake/Possession/PossessionFail)
  Utilities/      Game, Match, MatchEngine, Phase, FixtureGenerator, LinkBuilder, LinkHandler
  Serialization/  GameState, ISaveable, TeamConverter, *RefConverter, ScriptableObjectRefConverter
  Interview/      InterviewQuestion (+ InterviewAnswerGenerator)
  UI/             page controllers, widgets, dialogue, contexts
  Interfaces/     UIPage (abstract base), IDialogueContext
```

---

## 2. Core People Model

### Person (`DataModels/Person.cs`)
Base class for `Player` and `Manager`.
- Identity: `PersonID`, `FirstName`/`Surname`/`FullName`, `DateOfBirth`, `Team` ref.
- `PersonalityType` enum (10): Aggressive, Calm, Cautious, Cocky, Driven, Kind, Lazy, Shy, Silly, Smart.
- `Rating` enum: F, E, D, C, B, A, S.
- `GeneratePerson()` randomizes name (from big hardcoded name lists), DOB (1982–2007), personality,
  a `RatingOffset` (−10..+10 hidden modifier), and starting morale.
- **Morale** is a struct of `Mood` + `Passion` (each 0–100), plus per-person `IdealMood`/`IdealPassion`
  derived from personality (via `IdealMorale`) with ±5 jitter. The *distance* from current morale to
  ideal drives color (`GetMoraleColor`), face sprite (`GetMoraleSprite`), and stat penalties.
- `NewMorale(...)` converts an event reaction + severity into mood/passion deltas.
- **Personality compatibility** matrix: `GetPersonalityCompatibility(self, other)` returns −2..+2;
  helpers `GetBestCompatiblePersonality` / `GetWorstCompatiblePersonality`. Used by interviews and
  (intended) squad chemistry.

### Player (`DataModels/Player.cs`)
- `RawStats` (`Stats` struct): 18 skills in an `int[]` indexed by `PlayerStat` enum, plus `Height` and
  a `Dictionary<Position, PositionStrength>`.
  - **Skills (18):** Shooting, Passing, Tackling, Dribbling, Crossing, Heading / Positioning,
    Intelligence, Creativity, Teamwork, Composure, Aggression / Pace, Strength, Jumping, Agility,
    Stamina, Durability. (`PlayerStat` also includes `Height` as a 19th pseudo-stat.)
  - **Derived composites** (`[JsonIgnore]`): Attacking, Midfield, Defending, Mental, Physical, Goalkeeping —
    weighted blends of raw skills.
- **Positions:** 12-value `Position` enum (GK, LB, CB, RB, DM, LM, CM, RM, LW, AM, RW, ST).
  `PositionStrength`: None, Poor, Okay, Good, Natural. Players get one Natural position plus random
  spread, with a "spilling" bonus to related positions (`GetSimilarPositions`).
- **Stat pipeline:** `GetRawStats()` → `GetStatsFor(position)` (penalizes off-position) →
  `MoraleModifier` (applies morale-distance penalties) → `GetStats()` is the in-match effective stats.
  `PersonalityModifier` shapes raw stats at creation (e.g. Aggressive raises Aggression, lowers Composure).
- **Ratings:** `GetAverage(position)` is a position-weighted score; `GetRating(position)` maps it
  (+RatingOffset) to the F–S scale.
- `Fatigue` and `TacticFamiliarity` (0–100, grown by tactical training) exist; fatigue not yet deeply used.
- Training hooks: `ModifyStat`, `ImprovePositionalStrength`.
- `PlayerExtensions.AverageStats(...)` averages a group of players' effective stats (used heavily by the match engine).

### Manager (`DataModels/Manager.cs`)
- `ManStats` (`Stats`): manager `Skills` (Intelligence, Teamwork, Composure, Agression, Resilience),
  a chosen `Formation`, a `TacticTemplate`, and its `Instructions`.
- Derived: `Tactics`, `Motivation`, `Communication`.
- `TacticsMatch(mine, other)` gives a tactical edge (reduced 15% if your formation is in the opponent's `Inferiors`).
- AI managers pick a random formation + template at creation.

### Team (`DataModels/Team.cs`) — a **ScriptableObject**
- `Name`, `YearFounded`, `TeamColor`, `teamId`.
- `Players` list (squad). Convenience filters by role: `Defenders`, `Midfielders`, `Attackers`,
  `Goalkeeper`, `WidePlayers`, `StartingPlayers` (first 11), `Substitutes` (rest). `GetGroup(PlayerGroup)`.
- Owns `Manager`, `Tactic`, `ClubStats`, `KitNumbers`.
- **Team-level match stats** (blend player ability with tactic): `Security`, `Threat`, `Control`,
  `Creativity`, `Intensity`, `Stability`, `Pressure`, `DefensiveWidth`, `AttackingWidth`, `Fouling`, `Provoking`.
- `GenerateTeam()` creates a Manager, Tactic, and 21 players (kit numbers 1–21).
- `GetAllCompetitions()`, `GetMainLeague()`, `GetUpcomingFixture()` query FixturesManager.

### Pub (`DataModels/Pub.cs`)
Plain data from the CSV: FAS ID, name, address, postcode, easting/northing, lat/long, local authority.
`PostcodePrefix` (outward code) and `DistanceTo` (Haversine km) drive team selection by geography.

---

## 3. Competitions

### Competition (abstract, `DataModels/Competition.cs`)
Plain C# base (not SO). Has `Name`, `Priority`, `Teams`, `Fixtures`, `Rounds[]`, `IsComplete`.
Helpers: fixture date-finding (`FindNextAvailableDate`, avoids same-team clashes within ±1 day),
`BuildRoundsFromFixtures` (rebuild round grouping after load), `GetUpcomingRound`/`GetMostRecentRound`.

### League (`DataModels/League.cs`) — `: Competition, ISaveable`
- Created from a `LeagueTemplate` (`PromotionSpots`, `PlayoffSpots`, `RelegationSpots`, `Priority`).
- **Double round-robin** fixture generation, rounds 14 days apart.
- Maintains `standings` (`LeagueTableEntry` list), updated per result, sorted by points → GD → GF.
- `CheckSeasonEnd()` fires `CompetitionEvents` for champion / promotion / relegation / season complete.
- `PromotionLeague` / `RelegationLeague` link the pyramid.

### Cup (`DataModels/Cup.cs`) — `: Competition, ISaveable`
- Single-elimination knockout. Handles byes (`autoSecondRoundTeams`) to reach a power of two.
- `TryGenerateNextRound()` advances winners; drawn ties resolved by `ResolveDrawnCupMatch`
  (strength-weighted coin flip as a penalty-shootout proxy).

### Fixture (`DataModels/Fixture.cs`) — `ISaveable`
- Home/away teams, `Date`, `Round`, `Competition` (`[JsonIgnore]`, re-wired on load), `Match.Result`, `BeenPlayed`.
- `SimulateFixture()` runs a `Match` and `FinaliseResult()` records club stats, fires win/loss events for
  the player's team, advances cup rounds / updates league standings.
- Contains large blocks of **commented-out legacy** match-sim code (an older phase model) — historical only.

### LeagueTableEntry / LeagueTemplate / CompetitionEvents
- `LeagueTableEntry`: per-team points, GF/GA, W/D/L, derived `played` and `goalDifference`.
- `LeagueTemplate` (SO in `Resources/Competitions/Leagues`): blueprint for a league incl. promotion/relegation template links.
- `CompetitionEvents`: static C# events (`OnLeagueWon`, `OnPromoted`, `OnRelegated`, `OnCupWon`, `OnSeasonComplete`)
  that any system can subscribe to (FixturesManager records titles into `ClubStats`).

### League structure (set up in `FixturesManager.AddComps`)
4 leagues of 20 teams each (Premier / Div1 / Div2 / Div3) + one cup ("Papa Johns Cup") with all teams.
81 teams total = 80 league teams + the player's team. Promotion/relegation chains link the four divisions.

---

## 4. Formations & Tactics

### Formation (`DataModels/Formation.cs`) — SO
- `Name`, `Positions[]` (each = a `Player.Position` ID + a `Vector2Int` pitch location), `FormStats`
  (Complexity, Intensity, Control, Threat, Security, Tempo), and `Inferiors[]` (formations this one beats).
- Assets in `Resources/Formations/Usable` (4-4-2, 4-3-3, 4-2-3-1, 5-3-2) and `Other/AllPositions`.
- `SquadRole(i)` / `Subposition(i)` produce display labels (adds L/R based on x location, "SUB" beyond 11).

### Tactic (`DataModels/Tactics/Tactic.cs`)
Runtime per-team tactical state. Holds the chosen `Formation`, a list of `TacticInstruction`s, and 12 tactic
stats (`TacticStat` enum): Complexity, Intensity, Control, Stability, Pressure, Security, Threat, Creativity,
DefensiveWidth, AttackingWidth, Fouling, Provoking. Stats start at 50 (Complexity 25).
- `RecalculateStats()` resets to defaults then applies each instruction's stat modifications and
  **tactic dependencies** (`ApplyTacticDependencies` enforces min/max-style floors/ceilings between stats).

### TacticInstruction / TacticTemplate / TacticStats (SOs + helper)
- `TacticInstruction` (SO, `Resources/Tactics/Instructions`, e.g. HighLine, LowBlock, CounterPressing,
  OverlapFullbacks, HoofItLong, LongRangeShots, LobIt, BrexitTackles): a named modifier carrying
  `StatModification`s, `TeamDependency`s (rely on a `PlayerStat` of a `PlayerGroup`), `TacticDependency`s
  (stat floors/ceilings), `Reliance`s (position+stat weighting), and `incompatibleInstructions`.
- `TacticTemplate` (SO, `Resources/Tactics/Templates`, e.g. Pep, Dyche, Alternate, Nothing): a Formation +
  a set of Instructions; managers start from one.
- `TacticStats`: a mutable stat container with dependency/reliance bookkeeping (alternate to `Tactic`'s
  inline fields; used when composing instruction effects).

---

## 5. Match Simulation

The simulation is **phase-based**: each "minute" runs a possession sequence that recursively flows through
attacking/defending phases until a shot is attempted or possession is lost.

### Match (`Utilities/Match.cs`)
- Holds home/away teams, `Result`, `currentMin` (a `Minute`), and a `MatchEngine`.
- `SimulateMatch()` → `SimulateHalf(First)` + `SimulateHalf(Second)` (extra time supported via `Half` enum).
- Per-minute loop bounded by `HalfConditions` (base minute + random stoppage).
- `Result` aggregates per-team `TeamStats` (goals as a list of `Shot`s, fouls, possession).
- `Shot` (type/outcome/team/shooter/assister/xG/minute) and `Foul` (card/offender/victim/injury/minute) structs.
- `RecordShot(...)` adds goals to the result and broadcasts highlights.
- `ShotType` enum (Strike, Tap_In, Header, Solo, Stylish, Screamer, Penalty, Free_Kick, Corner, Own_Goal,
  Deflection) and `ShotOutcome` (Goal, Saved, Post, Miss, BadMiss).
- `trackHighlights` + `BroadcastHighlight` UnityEvent feed the live match UI.

### MatchEngine (`Utilities/MatchEngine.cs`)
The heart of the sim. `SimulateMinute()` decides the attacking team by possession share
(`Control / (homeControl + awayControl)`), then runs phases:

- **Phase graph** (`Phase.Type`): Build → Progress → Probe (shot) ; Advance → Penetrate (shot) ;
  Counter → Break (shot). Defensive turnovers flip attacker/defender and feed `overflow` momentum into
  the next phase. `StartingPhase` can trigger an early `MistakeHighlight` turnover based on Stability.
- Each phase has a `*Logic` method building `Phase.Parameters` (weighted attacking/defending player stats +
  tactic stats, exponents, thresholds). `ResolvePhase` blends tactical difference and ability difference
  (non-linear, weighted by how lopsided the tactical matchup is), adds randomness (driven by Stability),
  and returns a signed result (success/fail/neutral).
- `AttemptShot(...)`: picks a weighted-random shooter (`SelectWeightedRandomPlayer`, position-weighted toward
  forwards), selects a `ShotType` by phase + base xG, computes on-target chance from shooter skill/composure,
  then applies a **non-linear goalkeeper effect** (`NonLinearKeeperEffect`) to get final goal probability.
- `WinningComplacency` lets a comfortably-leading team ease off.
- `SimulateProbabilites(xg)` is a debug Monte-Carlo helper.

### Phase (`Utilities/Phase.cs`)
Defines `Phase.Parameters` (the tunable inputs to `ResolvePhase`) and the `Phase.Type` enum.

### Highlights (`DataModels/MatchSim/`)
`Highlight` abstract base (Team, Minute, Duration, `Describe()`), subclassed for Goal, Shot, Miss, Mistake,
Possession, PossessionFail. Broadcast live during a tracked sim and rendered by the match-sim UI.

---

## 6. Morale, Events & Discussions

This is a central pillar: results and random life events shift player morale, and the manager responds
through dialogue to nudge morale back toward each player's ideal.

### EventType (`DataModels/EventType.cs`) — SO
Template for an event: `tag` (Basic = win/loss, Special = everything else), `description` (with
`<1>`/`<all>`/`<w1>` token placeholders), `discussion` text, `odds`, `noAffected`, `moodChange`, `severity`
(`Dire`…`Momentous`, centered on `Irrelevant`). Assets in `Resources/Events` (+ `Events/Random`:
Divorce, Birth, Death, Sick, Night Out, Pub Fight, Inflation, etc.).

### Event (`DataModels/Event.cs`)
Runtime instance: an `EventType`, affected `Person`s, date, custom words. `ReadDescription` substitutes
the tokens. Defines:
- `Response` enum (manager's choice): Praise, Encourage, Challenge, Persuade, Inspire, Galvanise, Rage, Deflect.
- `Reaction` enum: Terrible…Amazing.
- `ReactionSeverityChange` adjusts the base reaction by response vs. severity.

### EventsManager (`Controllers/EventsManager.cs`)
- Subscribes to `NewDay`: `AddRandomEvent` (chance-based, picks from `Events/Random` by weighted odds,
  may surface a `Notification`) and `CheckEvents` (expires events older than 7 days).
- `AddWinEvent`/`AddLoseEvent` triggered from `Fixture.FinaliseResult` for the player's team.
- Builds the big **`ReactionTable`**: `(Response × PersonalityType) → Reaction` — the core of how each
  personality responds to each managerial response. This is the data behind "talking to players."

### Discussions (UI/Contexts/DiscussionContext.cs + DialogueUI)
`IDialogueContext` abstracts a dialogue scene. `DiscussionContext` wraps an Event+Person, surfacing the
event description and an opening line, plus the person's morale face/color. The player chooses a `Response`;
reaction + morale change follow from the tables above.

---

## 7. Recruitment & Interviews

No transfer market — players are hired via **interview days**.

- **RecruitmentManager** (`Controllers/RecruitmentManager.cs`): maintains a `FreeAgentPool` (target 30
  free agents). An interview session presents **5 candidates one at a time**; you may hire **1** or reject
  (rejection is permanent). `HirePlayer` adds to your squad; `ReleasePlayer` removes (optionally back to pool).
- **InterviewQuestion / InterviewAnswerGenerator** (`Interview/InterviewQuestion.cs`): question types
  (ask about a stat, biggest strength/weakness, best/worst personality fit, preferred position, career goals).
  Answers are generated from the player's real stats **filtered through personality** — e.g. Cocky overestimates
  and dodges weaknesses, Shy underestimates, Smart is accurate. `InterviewAnswer` exposes actual vs. perceived
  value (`IsAccurate`) so a sharp manager can read between the lines.
- **InterviewManager** (`UI/InterviewManager.cs`): drives one interview through `DialogueUI`, max 5 questions,
  then hire/reject via RecruitmentManager. Uses `InterviewContext` (personality-flavored greeting).

---

## 8. Training

- **TrainingSession** (`DataModels/TrainingSession.cs`): a `TrainingType` (Technical, Mental, Physical,
  Tactical, Social, Positional) with optional target stat / position / selected players.
  - Technical/Mental/Physical → stat boost to starting XI (`ExecuteStatBoost`).
  - Tactical → grows `TacticFamiliarity` (scaled by Intelligence).
  - Social → pushes morale toward a healthy baseline.
  - Positional → improves `PositionStrength` for selected players with **diminishing returns** by group size.
- **TrainingManager** (`Controllers/TrainingManager.cs`): holds the week's `CurrentSession`, exposes
  `GetAvailableTrainingOptions()` (the menu), and `ExecuteTraining()` runs on scheduled training days.

---

## 9. Calendar, Schedule & Game Loop

### CalenderManager (`Controllers/CalenderManager.cs`)
Owns `CurrentDay` (starts **2024-08-01**). `AdvanceDay()` increments the date, fires the `NewDay`
UnityEvent, and uses a listener/response counter so all systems finish processing before the UI moves on.
Lots of date-formatting helpers (`ShortDate`, `DaysAgo`, etc.).

### ScheduleManager (`Controllers/ScheduleManager.cs`)
Builds a 120-day-ahead `ScheduleEntry` map for the player's team: match days (from fixtures), interview days
(every 14 days), training (Wednesdays), else rest. `ProcessToday()` raises `OnMatchDay`/`OnTrainingDay`/
`OnInterviewDay`. `ScheduleEntry` = date + `ScheduleEntryType` (+ optional fixture).

### GameManager (`Controllers/GameManager.cs`) — orchestrator
- `Start()`: auto-loads a save if present, else `SetupGame()` (spawn teams → add competitions → seed
  recruitment pool → generate schedule → wire training day → UI setup).
- `NewDay(date)`: processes today's schedule, simulates all due AI fixtures, defers the **player's** fixture
  to an interactive match-sim (`PlayerMatchSim` action), and auto-saves every Monday.

**Daily loop:** advance day → schedule processes activity → AI matches simulate → player match (if any) →
events/morale update → UI returns to home/match page.

---

## 10. Controllers Summary (singletons)

| Controller | Responsibility |
|---|---|
| `GameManager` | Boot, save/new-game decision, per-day orchestration |
| `TeamManager` | Loads pubs from `open_pubs` CSV, selects 81 geographically-near pubs, spawns/generates teams, `MyTeam` = index 0 |
| `FixturesManager` | Owns competitions + fixture lookups, builds league pyramid + cup, `StartNewSeason` (promotion/relegation + regen), records titles |
| `CalenderManager` | Current date + day advancement + `NewDay` event |
| `ScheduleManager` | Per-day activity schedule for the player |
| `TrainingManager` | Training options + execution |
| `RecruitmentManager` | Free-agent pool + interview-session lifecycle |
| `EventsManager` | Random/result events, morale changes, reaction tables |
| `PersonManager` | Global registry of all `Person`s by `PersonID` |
| `SaveManager` | JSON save/load via `GameState` |
| `UIManager` | Page switching (`HideAllUI` + `Show*`) |

---

## 11. UI Layer

- **UIPage** (`Interfaces/UIPage.cs`): abstract base for pages. `Show(...)` overloads for different payloads
  (Player/Manager/Team/Fixture/Event+Person). On show it activates its element root and calls `Setup()` on
  every `UIObject` child (self-populating widgets).
- **UIManager** routes to page singletons: PlayerDetails, TeamDetails, Competition (fixtures/table), Home,
  MatchSim, Tactics, Discussion, Schedule, Training, Recruitment. (Pages live in `UI/UIPages/`; some older
  duplicates at `UI/` root were deleted in the current working tree — see git status.)
- **Widgets** (`UI/`): drag-and-drop squad/formation editing (`DraggableUI`, `FormationUI`,
  `BenchManager`, `PositionUI`), stat displays, league table, tactics sliders/toggles, dialogue
  (`DialogueUI`, `ResponseManager`, `ResponseButtonUI`), notifications, morale UI.
- **LinkBuilder / LinkHandler** (`Utilities/`): build clickable rich-text links (e.g. player names) that
  route to detail pages.

---

## 12. Serialization / Save System

- **GameState** (`Serialization/GameState.cs`): root container — `CurrentDay`, `PlayerTeamId`, `Teams`,
  `Competitions`, `FreeAgents`, `Events`.
- **SaveManager**: serializes `GameState` with Newtonsoft (TypeNameHandling.Auto, ignore reference loops),
  to `Application.persistentDataPath/autosave.json`. `RestoreGameState` re-wires cross-references after load:
  team restore, competition/fixture back-references, promotion/relegation links (via templates), free agents,
  events, schedule regen.
- **Converters** (`Serialization/`): `TeamConverter` (full team graph + a registry to resolve refs),
  `TeamRefConverter`/`PersonRefConverter`/`CompetitionRefConverter` (serialize as IDs, resolve on load),
  `ScriptableObjectRefConverter` (resolve SOs by name from a Resources path — used for Formation/TacticTemplate).
- **ISaveable**: `OnAfterDeserialize()` hook (League/Cup rebuild `Rounds`, resolve templates; Event resolves
  EventType + affected persons).
- Because `Fixture.Competition` and `Competition.Fixtures` would be circular, fixtures are restored and the
  back-reference is re-wired manually in `RestoreCompetitions`.

---

## 13. Authored Data (`Assets/Resources/`)

- `open_pubs` (CSV TextAsset) — real UK pub dataset; the source of all club names/locations.
- `Teams/` — a few template Team SOs (the spawner clones `teams[0]` and renames per pub).
- `Formations/Usable/` — 4-4-2, 4-3-3, 4-2-3-1, 5-3-2; `Formations/Other/AllPositions`.
- `Tactics/Instructions/` & `Tactics/Templates/` — tactical building blocks & manager presets.
- `Events/` & `Events/Random/` — win/loss + flavor life events.
- `Responses/` — managerial response assets.
- `Competitions/Leagues/` — the four `LeagueTemplate`s (1–4) with promotion/relegation links.
- `Art/Morale/` — morale face sprites (Happy/Sad/Angry/etc.).

---

## 14. Design Intentions & Notable Gaps

**Intended pillars**
- *Geography-driven fantasy:* your club and rivals are real nearby pubs.
- *People over money:* no transfers/wages; squad-building is interviews + reading personalities, and
  performance is gated by **morale alignment** to each player's personality-defined ideal.
- *Readable but deep match sim:* a deterministic-ish phase graph blending player ability and tactical
  matchup, surfaced live via highlights.
- *Tactical identity:* formations + stackable instructions with dependencies/incompatibilities and
  player-stat reliances, countered by opponent formations (`Inferiors`).
- *Long-term progression:* multi-season league pyramid with promotion/relegation, cup runs, and lifetime `ClubStats`.

**Known in-progress / rough edges (verify before building on):**
- Interview logic was the latest work (see recent commits) and the UI flow is still settling.
- `Fixture.cs` contains a large commented-out legacy sim; `Tactic` vs `TacticStats` overlap (two stat
  containers) suggests an in-progress refactor.
- `Fatigue`, `TacticFamiliarity`, fouls/cards/injuries (`Foul`, `Card`, `InjuryType`) and `Penalty`/`Free_Kick`/
  `Corner` shot types are modeled but only partially wired into the sim.
- `StartNewSeason` exists but isn't obviously triggered automatically at season end yet.
- `PersonManager.RegisterPerson` returns `People.Count` as the ID, which can collide with `PersonID`
  expectations on restore — an area to double-check when touching save/load or person lookups.
- Several `UI/` page scripts were moved into `UI/UIPages/` (old copies deleted in the working tree).
