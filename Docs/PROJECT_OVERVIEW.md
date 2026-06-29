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
   (`Shot`, `Foul`, `Minute`, `Morale`); `ISaveable.OnAfterDeserialize` rebuilds derived/ignored state. **Any
   `[JsonIgnore]` field that runtime logic reads must be rebuilt in `OnAfterDeserialize`** — forgetting this is a
   classic "works in-session, breaks after load" bug (e.g. the `Cup.roundList` stall).
7. **Reuse one system, don't build a parallel one.** New features are layered onto existing mechanics rather than
   duplicating them: the half-time **team talk reuses the 1-on-1 discussion reaction system**
   (`Event.Response` + `EventsManager.ReactionTable` + `Person.NewMorale`), reliances reuse `PlayerGroup`, and the
   `PlayerPickerPopup` is shared by reliances / training / the interview "compare" question. One tuning source, no drift.
8. **Pure logic, thin renderers.** Game logic lives in plain testable C# (`StatLeaderboards`, `TeamTalkReactions`,
   `InterviewAnswerGenerator`, `TeamTalkMinigame`, `MatchEngine`/`Phase`); the MonoBehaviour/`UIObject` is a thin
   view over it. You can unit-test or A/B the logic (see `TacticLab`) without the scene.
9. **Derive-on-read where consistency matters; denormalize only deliberately.** The stat leaderboards recompute from
   the fixtures' event logs every time (one source of truth, team & player totals can't disagree), instead of running
   counters that could drift. `ClubStats`/`LeagueTableEntry` *are* running counters — a deliberate, separate choice.

**Gameplay design rules (every feature should respect these)**
1. **Everything has a trade-off — any edge must carry a matching risk.** This is the project's north star. Reliances
   amplify a player's strengths *and* weaknesses; Complexity needs squad IQ or it penalises; high familiarity is
   great but switching setup costs it; a botched team talk *lowers* morale; a hairdryer fires up the right
   personalities and crushes the rest. If a new mechanic is pure upside, add an authored downside.
2. **Personality is read through behaviour, not labels.** Hidden personality is inferred by grouping answers
   (~3 buckets/question, groupings differing per question so intersecting distinguishes them) — never shown directly.
3. **Coarse groups over per-entity uniqueness.** Prefer ~3 grouped variations keyed on a trait/shape over 10 bespoke
   ones; it's less to author, and the *grouping itself* becomes the signal.
4. **Tunable, in one place.** Expose the knobs (thresholds, weights, severities) as consts/SerializeFields with a
   tuning cheat-sheet, so balance is data not buried magic numbers.

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
                  SaveManager, UIManager, IdManager, InjuryManager
  DataModels/     Person, Player, Manager, Team, Pub, Competition, League, Cup, Fixture,
                  CompetitionSeries, LeagueTableEntry, LeagueTemplate, Event, EventType,
                  CompetitionEvents, ClubStats, ScheduleEntry, TrainingSession, Drill, Formation
    Tactics/      Tactic (+ Mentality/TacticState), TacticInstruction (+ Reliance), TacticStats(legacy), TacticTemplate
    MatchSim/     Highlight + subclasses (Goal/Shot/Miss/Mistake/Possession/PossessionFail/Foul)
  TeamTalk/       TeamTalkReactions (tones + score→severity, the ACTIVE talk), TeamTalkController,
                  TeamTalkMinigame (+ GerrymanderGame/BalanceGame — dormant microgame path)
  Utilities/      Game, Match, MatchEngine, Phase, StatLeaderboards (stat aggregation),
                  FixtureGenerator, LinkBuilder, LinkHandler, KitColors
  Serialization/  GameState (=> CoreSaveState/SeasonSaveState/SeasonIndexEntry), ISaveable,
                  TeamConverter, *RefConverter (Team/Person/Competition/Fixture), ScriptableObjectRefConverter
  Interview/      InterviewQuestion (+ InterviewAnswerGenerator — grouped/bucketed answers)
  Analysis/       TacticLab (headless A/B sim harness for balancing — see "Analysis harness" below)
  UI/             page controllers (UIPages/, incl. StatsPageUI), widgets, dialogue, contexts,
                  BackButton, StatLeaderboardWidget/Row
    TeamTalk/     TeamTalkUI (squad arch of morale boxes), PlayerMoraleBoxUI
    Home/         DayTimelineWidget, DayBox, CompetitionContextWidget, CupTieRowUI,
                  CurrentRoundWidget, SquadStatusWidget, AdvanceOrHomeButton, PlayerRowUI
  Interfaces/     UIPage (abstract base), IDialogueContext
```

> **Adding a system, the house style:** pure logic in a plain C# class (testable) → a thin `UIObject`/`UIPage`
> renderer over it → reuse a shared component (`PlayerPickerPopup`, `TacticOptionToggle`, `DialogueUI`) rather than a
> new one → expose tunables as consts/SerializeFields → if it needs editor wiring, **write an `*_BUILD.md`** (see §14).

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
  `GetUpcomingFixture()` query the (current-season) FixturesManager. **Tactic now serializes** (formation,
  instructions, reliance slot bindings, mentality, familiarity weights) via `TeamConverter` ↔ `Tactic.CaptureState`.

### Pub (`DataModels/Pub.cs`)
CSV data: FAS ID, name, address, postcode, easting/northing, lat/long, local authority. `PostcodePrefix`
and `DistanceTo` (Haversine km) drive geographic team selection.

---

## 3. Competitions & Season Lineage

### Competition (abstract, `DataModels/Competition.cs`)
Plain C# base. `Id`, `SeriesId`, `SeasonYear`, `Name`, `Priority`, `Teams`, `Fixtures` (inline), `Rounds[]`
(`[JsonIgnore]`, rebuilt), `IsComplete`. Helpers: `BuildRoundsFromFixtures`, `GetUpcomingRound`/`GetMostRecentRound`.
- **`IsTeamAvailable(team, date)`** — a team must not have two matches **within 2 days** (`Math.Abs(diff) <= 2`).
  This is **per-team** (checks only fixtures involving that team), so different teams can play the same day.
- **`FindNextAvailableDate(t1, t2, from)`** — keeps a game on its intended weekday (no random jitter); only shifts
  forward if a team isn't available. `NextDayOfWeek(from, day)` (protected static) snaps to a weekday.

### League (`DataModels/League.cs`) — `: Competition, ISaveable`
Created from a `LeagueTemplate`. **Double round-robin**, **weekly on Saturdays** (rounds anchored via
`NextDayOfWeek(startDate, Saturday)`, `+7` days each — 20 teams = 38 weekends, Aug → ~May). `ApplyWinterBreak`
pushes any round landing in **Dec 24 – Jan 1** past the break, shifting the rest of the season → a ~2-week
Christmas break. Serializes `StandingsData` (sorted points→GD→GF). `PromotionLeague`/`RelegationLeague`
(`[JsonIgnore]`, re-linked on load by template name). `OnAfterDeserialize` rebuilds rounds + resolves `Template`.

### Cup (`DataModels/Cup.cs`) — `: Competition, ISaveable`
Single-elimination knockout, **midweek (Wednesdays)** between league weekends (ties anchored via
`NextDayOfWeek(roundDate, Wednesday)`; next round ~2 weeks later, **floored to today** so a behind-schedule cup never
makes an already-overdue tie). `TryGenerateNextRound` advances winners; drawn ties resolved by a strength-weighted
coin flip. **Save/restore fixed:** byes (`autoSecondRoundTeams`) are now serialized and the round structure
(`roundList`) is rebuilt in `OnAfterDeserialize`, so a cup loaded mid-run keeps progressing (it used to stall).

> Net player calendar: a league game most **Saturdays** (Aug→~May) with a **~2-week Christmas break**, a
> **midweek cup tie** when still in it, plus the schedule activities (§9) built around those.

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
  **`FormStats`** (Complexity/Intensity/Control/Threat/Security/Tempo, authored per asset) are now actually used:
  the formation **base-replaces** the tactic's Complexity/Intensity/Control/Threat/Security (so 5-3-2 starts
  defensive, 4-3-3 attacking), and **Tempo** drives the match engine's build-up choice (see §5).
- **Tactic** (`Tactics/Tactic.cs`): runtime per-team state, now the hub of the deep tactics systems. Holds the
  chosen `Formation`, the `TacticInstruction` list, the **reliance slot bindings** (`RelianceSlots`), an overall
  **`Mentality`**, a **`Familiarity`** score, the 12 derived `TacticStat`s, and a formation-derived **`Tempo`**.
  `RecalculateStats()` = `ApplyFormationBase()` (formation sets the base for Complexity/Intensity/Control/Threat/
  Security + Tempo) + instructions + **complementary synergies** + mentality + instruction floors/ceilings, then
  clamps. Now **serialized** via `CaptureState()`/`ApplyState()` ↔ `TacticState` (written by `TeamConverter`).
  - **Complementary instructions** (`TacticInstruction.complementaryInstructions`): a one-directional synergy list.
    Each entry names a partner `TacticInstruction` + a list of `TacticStat` perks; when both the owner and the
    partner are active, the owner adds those perks (step "1b" of `RecalculateStats`). List it on both to make a
    pairing mutual. (Mirror of `incompatibleInstructions`, which forbids pairs rather than rewarding them.)
  - **Mentality** (`Mentality` enum, UltraDefensive…UltraAttacking): one dial shifting a band of stats
    (Threat↑/Security↓/Intensity↑/width/…, extremes cost Stability). `BaseMentality` persists; `InMatchMentalityShift`
    is a temporary in-match nudge reset at full time (`BeginMatch`/`EndMatch`/`ShiftMentalityInMatch`).
  - **Reliance** (a property of `TacticInstruction`): an instruction may carry a reliance —
    `Reliance { PlayerStat[] stats; float multiplier; PlayerGroup[] eligibleGroups; float familiarityPenalty }`.
    An instruction **counts as a reliance when its `eligibleGroups` is non-empty** — there's no separate flag;
    `TacticInstruction.hasReliance` is a derived getter (`eligibleGroups.Length > 0`).
    When active it leans on **one player by SQUAD SLOT** (`RelianceSlots[instructionName]` = index in
    `Team.Players`, not a PersonID) — so it **follows the slot**: a subbed-on player inherits the reliance, and a
    formation change keeps it on the same player. In `AverageStats(tactic)` that slot's player gets `multiplier`×
    weight on his named `stats` (`IsReliantPlayer`/`RelianceBonus`). **`eligibleGroups`** (reusing `PlayerGroup`)
    does triple duty: it filters the picker; it **auto-disables** the instruction if the player later moves out of
    all those groups; and it **scopes which phases of play the effect applies to**. The match engine evaluates one
    position band per phase (Defenders for build-up, Attackers for finishing, the unions for transitions, …), and a
    reliance amplifies its stats only in phases whose band is *within* `eligibleGroups` — so a defenders reliance
    sways the build-up but not the finish, a target-man (attackers) reliance the finish but not the build-up
    (`RelianceBonus(player, stat, phaseGroup)` → `ReliancePhaseActive`). **`familiarityPenalty`** knocks the instruction's habituation weight
    when a sub/swap *changes* the reliant player (0 = no effect). All this is enforced by **`RefreshReliances()`**,
    called on every tactic/lineup/formation change and at match start (auto-bind missing, auto-disable invalid,
    apply change penalties). Any other trade-off (e.g. +Complexity) is just authored `statModifications`.
  - **Familiarity** (0–100, **muscle-memory model**): each tactical setting (the formation, and each instruction)
    has a habituation `weight` in `SettingWeights` (0 = used to OFF, 1 = used to ON). `Familiarity` is a **blend**:
    `FORMATION_FAMILIARITY_SHARE` (≈40%) from how drilled the team is in the **current formation** (one weight per
    formation; 0 for a formation never used) + the rest from the instructions' `avg(1 − |state − weight|)`. So
    **switching to an undrilled formation costs ~40%** from a settled side, while toggling one instruction costs
    much less — formation weighs far more than any single instruction. Weights only move toward current states when
    you **play or train** (`AdvanceFamiliarity` — from `Fixture.FinaliseResult` for both teams + every
    `TrainingSession.Execute`); **editing the tactic doesn't move them**, so flip-flopping a toggle or switching
    formation and back doesn't permanently drain familiarity (it recovers once the old setup is current again).
    A new team is seeded ~50% on its starting formation (`SeedStartingFormation`); ~3 months of a stable tactic →
    ~100%. Low Familiarity → more early mistakes (`StartingPhase`). Shown via the non-interactable `FamiliaritySlider`.
  - **Complexity → Intelligence (squad average)**: `Complexity` *is* the required average intelligence
    (`IntelligenceThreshold => Complexity`). If the **starting XI's average intelligence** (cached at match start via
    `RefreshMatchCache`) clears the bar, the side copes and **no one is penalised** — brainy players cover for one dim
    one. Only when the average falls short (`ShouldApplyComplexityPenalty`) do below-bar players take a
    **proportional** cut to their mental stats `ComplexityAffectedStats` (positioning/creativity/teamwork/composure/
    aggression — the whole mental group bar Intelligence) — `ComplexityPenaltyFraction` ≈ 50% at 10 below the bar
    (`shortfall × 0.05`, capped 90%), applied in `AverageStats(tactic)`. All the
    IQ maths uses **`Player.TacticalIntelligence`** = effective `GetStats().Intelligence` (boost + **morale** +
    **off-position** penalty), over the **on-pitch XI only**. The tactics **PositionUI** displays the same
    `TacticalIntelligence()`, so the intelligence you see on each player matches what the squad-IQ gate uses.
  - **In-match changes**: structural edits gated by `CanMakeStructuralChange` (out of match, or at half-time);
    mentality nudges allowed anytime (request 2).
- **TacticInstruction / TacticTemplate / TacticStats** (SOs + helper): named modifiers (HighLine, LowBlock,
  CounterPressing, …) with stat modifications, team/tactic dependencies, reliances, and incompatibilities;
  templates (Pep, Dyche, …) bundle a formation + instructions. (`TacticStats` is still a separate legacy helper;
  the live path is `Tactic`.) **UI controllers added**: `MentalitySelectorUI`, `TacticInfoUI` (familiarity /
  complexity / IQ-bar read-out). See `NEW_SYSTEMS_BUILD.md`.

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
- **Highlights** (`DataModels/MatchSim/`): Goal/Shot/Miss/Mistake/Possession/PossessionFail **+ FoulHighlight**,
  broadcast live to the match-sim UI when `trackHighlights` is on (the player's match).
- **Tactic context in resolution:** every phase now averages each side's group stats *through that team's tactic*
  (`AverageStats(tactic)`) so reliances (a reliant player's named stats get extra per-stat weight) and the
  complexity/intelligence penalty fold in automatically. Low Familiarity raises the early-mistake chance in `StartingPhase`.
- **Tempo → build-up:** in `StartingPhase` the choice between patient build (Build) and going direct (straight to
  Advance) is `WeightedAverage(Control, 100−Intensity, 100−Tempo)` — high formation `Tempo` skips build-up and goes
  direct more often; low Tempo builds from the back. (Tempo is formation-derived; it isn't a `TacticStat` slider.)
- **Fouls, cards & injuries:** `MatchEngine.TryFoul` (scaled by Fouling/Provoking/Aggression) records `Foul`s via
  `Match.RecordFoul` — yellows/reds and occasional injuries (Knock→ACL, death astronomically rare). For the
  **player's** team these become real consequences in `Fixture.FinaliseResult` → `InjuryManager.ProcessMatchConsequences`
  (bookings → suspensions, injuries → availability). Lineups are auto-cleaned (`Team.EnsureAvailableLineup`) before
  every match. See §16.

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

No transfer market — players are hired via **interview days** (now schedule-gated and wired).
- **RecruitmentManager**: `FreeAgentPool` (~30). Interviews only run **on a scheduled interview day**
  (`OnInterviewDay` → `NotifyInterviewDay`, wired in new-game + load), **one session per day** (`CanInterviewToday`).
  A session presents 5 candidates one at a time; hire 1 or reject (permanent). **Squad cap** `MAX_SQUAD_SIZE` (25):
  when full you must release someone first — `HirePlayer` blocks, `HirePlayerReplacing(new, drop)` does both.
- **InterviewQuestion / InterviewAnswerGenerator**: each question sorts the 10 personalities into **~3 response
  buckets**, and the answer text is keyed on the bucket — so different personalities give the same answer to a given
  question. The **bucketing differs per question** and *every* answer returns its bucket as a `PossiblePersonalities`
  clue, so combining questions distinguishes the hidden personality (not just the four probes — all questions narrow
  now). Ability questions add **stat-shape** variation (a clear standout stat vs one of many; specialist vs
  versatile), with the standout threshold shifted by bucket. A **`CompareToPlayer`** question opens the shared
  `PlayerPickerPopup` and scores ability by *signed √ of each stat difference, summed* → "about the same / an edge /
  miles off", biased by personality.
- **InterviewManager** (`UI/`): drives an interview via `DialogueUI` (≤5 questions); intersects buckets into
  `NarrowedPersonalities` (`NarrowedPersonalitiesText()` → e.g. "Cocky / Aggressive"). UI helper
  `InterviewQuestionButton`. `RecruitmentManager.debugEndlessInterviews` makes every day an interview day for testing.
  See `INTERVIEW_INMATCH_TEAMTALK_BUILD.md` / `WIRING_RECRUITMENT_HALFTIME_SIM.md`.

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
Owns `CurrentDay` (starts **2024-08-01**). `AdvanceDay()` now **only** increments the date and fires `NewDay`
(the UI/match/save flow is orchestrated by GameManager — see below). A listener/response counter
(`ConfirmAddedListener`/`RespondToAdvance`) still gates re-advancing until `NewDay` processing completes.

### ScheduleManager
Builds a **rolling 120-day** `ScheduleEntry` window for the player, **regenerated every advance** (from `GameManager.NewDay`)
so it stays full and picks up new-season fixtures. `GenerateSchedule(today)` places by **priority, shifting forward on a
clash so nothing is dropped** (`PlaceActivity`): **matches** (fixed) → **interviews** (fortnightly, fixed cadence) →
**pub socials = the day after each match** → **training** (weekly, prefers Wednesday) → **rest**. Interviews are
placed right after matches (so pub/training can't bump them), anchored to a fixed `interviewEpoch` (set once), on
their exact date and only shifting past a fixed match — so they get **their own day, never wander day-to-day, and
never vanish** (the past-date skip is keyed on the placed day). Training lands on real calendar Wednesdays.
`ScheduleEntryType` = Match/Training/Interview/RestDay/**PubTrip**. `ProcessToday()` raises
`OnMatchDay`/`OnTrainingDay`/`OnInterviewDay` (`OnTrainingDay` → `ExecuteTraining`; `OnInterviewDay` →
`RecruitmentManager.NotifyInterviewDay`). `GetUpcoming(n)` (today-inclusive), `GetNextTrainingDay()`.

### GameManager — orchestrator
- `Start()`: if a save exists, `Load()`; on **load failure** it does *not* show a half-loaded UI — it sets a static
  "start fresh" flag and **reloads the scene** for a clean slate (save left untouched). No save → `SetupGame()`
  (spawn teams → AddComps → seed recruitment → schedule → wire training day → UI → **initial full Save**).
- `NewDay(date)`: **`FixturesManager.CheckSeasonRollover()`** (starts the next season if the current one's fixtures are
  all played — the rollover hook), then regenerates the schedule window, then `ProcessToday()` (fires today's activity
  effects, e.g. training), then `RespondToAdvance()`. **No simulation/saving here** — that's in the advance coroutines.
- **Day/match flow** (new): matches are played **on their own day**, not after advancing.
  - `HasPendingPlayerMatch()` / `GetPendingPlayerMatch()` — the player's earliest unplayed fixture dated ≤ today.
  - `AdvanceOrPlay()` (the button entry point) → `PlayMatchRoutine` if a match is pending, else `AdvanceRoutine`.
  - `PlayMatchRoutine`: show `LoadingOverlay` → simulate that day's AI matches → `AutoSave` → hand to the interactive
    `MatchSimPageUI` (which returns to the home page on the **same day** and autosaves at full time).
  - `AdvanceRoutine`: show overlay → simulate due AI matches → `CalenderManager.AdvanceDay()` → **`AutoSave` (before
    the animation)** → hide overlay → `ShowHomePage()` (the day-strip slide then always plays cleanly).
  - `SimulateAiMatches(predicate)` — sims non-player fixtures a few per frame (keeps the spinner alive; the list can
    grow mid-loop as cups generate next rounds). `IsBusy` blocks re-entry / greys the button.
- `NewGame()`: deletes the save and reloads the scene.

**Daily loop:** (on home screen) press **Next Day** → overlay → AI matches sim → advance + autosave → strip animates.
On a match day the button reads **Play Game** → today's AI sim + your interactive match → returns to the *same* day
with all results in; you then advance separately. Autosave fires every advance and after your match (see §12).

---

## 10. Controllers Summary (singletons)

| Controller | Responsibility |
|---|---|
| `GameManager` | Boot, save/new-game decision, load fail-safe; day/match flow (advance + play coroutines, AI sim, autosave) |
| `IdManager` | Per-type ID allocators (Person/Team/Competition/Fixture/Series); `SeedFromState` on load. **Must be on a scene object.** |
| `TeamManager` | Loads pubs CSV, selects ~81 nearby pubs, spawns teams (kit colours via `KitColors`), `_byId` lookup, `MyTeam` = index 0 |
| `FixturesManager` | **Current-season** competitions + fixture/competition/series lookups; builds pyramid + cup; `CompetitionSeries` lineage; `StartNewSeason` (archive + unload); lazy history loading |
| `CalenderManager` | Current date + `AdvanceDay()` (increment + fire `NewDay` only) |
| `ScheduleManager` | Rolling 120-day activity schedule (regenerated each advance; priority + clash-shift); `GetNextTrainingDay` |
| `TrainingManager` | Ongoing drill (catalog-driven Boost system) + execution |
| `RecruitmentManager` | Free-agent pool + interview lifecycle; interview-day gating + squad cap (release-before-hire) |
| `InjuryManager` | Player availability: off-pitch injury/illness/death daily rolls, recovery, post-match cards/suspensions/injuries (**must be on a scene object**) |
| `TeamTalkController` | Half-time team talk: `DeliverTalk(response, severity)` applies a squad-wide morale swing via the discussion reaction system (one per match; the wrong tone dents morale). Dormant microgame path also present. |
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
  Discussion, Schedule, Training, Recruitment, **Stats** = `StatsPageUI`). Tracks `IsHomeActive`. Also keeps a
  **navigation history** (a stack of re-show delegates pushed by each `ShowX`); `Back()` returns to the previous page
  and the drop-on-any-button **`BackButton`** component calls it (live matches/discussions excluded; no-op mid-match).
- **Stat leaderboards** (`STATS_WIDGETS_BUILD.md`): `StatLeaderboardWidget` (+ row) renders ranked top-N tables —
  goals, assists, shots/on-target, saves, clean sheets, conceded, own goals, big misses, cards — for players **or**
  teams, derived on-read from a competition's fixtures by `StatLeaderboards`. Used on the home page (by team, stat
  dropdown), the My Team page (by player), and a dedicated `StatsPageUI`.
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
  - `AdvanceOrHomeButton`: the top-nav button has **3 states** — "Home" (off the home page → returns home),
    "Play Game" (home + a match pending today → plays it on the current day), "Next Day" (home + no match → advances).
    Routes through `GameManager.AdvanceOrPlay()`; greys out while `IsBusy`.
- **`LoadingOverlay`** (`UI/LoadingOverlay.cs`): full-screen spinner shown during AI sim / save so the day-strip
  animation always plays cleanly after. Singleton on an always-active object; toggles a child `root` panel; `Show()`/
  `Hide()` no-op if unwired. (Build notes were given to the user separately.)
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
Full save (`AutoSave`): **every day advance** (in `AdvanceRoutine`, *after* sim+advance but *before* the day-strip
animation), player-match completion (`MatchSimPageUI` full time + before the interactive match), new-game start.
Fast `SaveCore`: setting training, leaving the tactics page (`UIPage.OnHide`), responding to a player event.

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
- **Needed for the new UI (not yet authored / wired in-scene):** day-type icon sprites (match/training/interview/
  pub-trip/rest) for the home day strip; a spinner sprite for `LoadingOverlay`. The home dashboard and `LoadingOverlay`
  GameObjects must be **built/wired in the scene** (the C# is done) — see `HOME_SCREEN_BUILD.md`.

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
| `NEW_SYSTEMS_BUILD.md` | **Editor/UI wiring for the new systems** (tactics depth, in-match changes, injuries/suspensions, interview day, reliances/picker) + a tuning cheat-sheet |
| `INTERVIEW_INMATCH_TEAMTALK_BUILD.md` | In-depth build/explanation of in-match tactical changes, half-time, and interview day |
| `WIRING_RECRUITMENT_HALFTIME_SIM.md` | Field-by-field Inspector checklist (serialized fields, OnClick hooks) for recruitment/interview, match sim, half-time |
| `TEAM_TALK_UI.md` | Build guide for the choose-a-tone half-time team talk (squad arch + reuses the discussion reaction system) |
| `STATS_WIDGETS_BUILD.md` | Stat leaderboard widgets (top scorers/assists/saves/cards/clean sheets…) for the home/My Team/stats pages |

---

## 15. Known in-progress / rough edges (verify before building on)

- **Tactics ARE now serialized** (resolved): `TeamConverter` writes `Tactic.CaptureState()` (formation,
  instructions, reliance player bindings, mentality, familiarity weights) and `ApplyState` restores it. The "save
  on leaving tactics" trigger is now meaningful.
- **`Tactic` vs `TacticStats`** overlap remains: `TacticStats` is a **legacy** helper not used by the live match
  path (everything goes through `Tactic` now) — safe to delete once confirmed unreferenced.
- **Match-sim coverage:** fouls/cards/injuries are now **generated and wired for the player's team** (bookings →
  suspensions, injuries → availability via `InjuryManager`). **AI-team suspensions/injuries are not simulated**
  (their fouls are recorded for display but don't gate their availability) — a sensible future extension. Some shot
  types (Penalty/Free_Kick/Corner) are still only partially wired; `Fatigue` still isn't deeply driven by play.
- **Cup save/restore now correct** (resolved): bye teams (`autoSecondRoundTeams`) are serialized and `roundList` is
  rebuilt in `OnAfterDeserialize`, so a cup loaded mid-run keeps progressing; and round dates are floored to "today"
  so a behind-schedule cup never generates an already-overdue tie (the "play a 28-Aug game in September" bug).
- **History UI:** `LoadSeasonForViewing` exists but the past-seasons browsing UI isn't built yet.
- **Season rollover now wired** (resolved): `StartNewSeason()` was correct but **never called** — so once season 1's
  fixtures ran out the game stuck forever with no new games. Now `FixturesManager.CheckSeasonRollover()` (rolls over
  when `IsCurrentSeasonOver()` — i.e. every current-season fixture played) is invoked from `GameManager.NewDay()`,
  before the schedule rebuild, so the next season's fixtures are generated and picked up automatically. The check is
  fixture-based (not just `IsComplete` flags), so it also rescues a save stuck behind a stalled competition.
- **Interview days are now wired** (resolved): `OnInterviewDay` → `RecruitmentManager.NotifyInterviewDay` (new-game
  + load), and interviews are gated to that day (`CanInterviewToday`). Remaining editor work: build the question
  buttons (`InterviewQuestionButton`) and the release-before-hire panel — see `NEW_SYSTEMS_BUILD.md`.
- **`OnTrainingDay` wired twice:** in both `SetupGame` and `SaveManager.FinishRestore` (only one path runs per
  session, so harmless today, but watch for a double-subscribe if both ever run).
- **Existing saves keep old fixtures:** fixtures are baked into the season file at season start, so a save made
  before the weekly-fixtures / winter-break change keeps the old layout until its next rollover — start a new game
  to see the new schedule.
- **`Fixture.cs`** still contains a large commented-out legacy sim (historical only).
- **Recently fixed (context):** league fixtures were 14-days-apart → now **weekly (Sat) + midweek cup (Wed) + ~2-week
  Christmas break**; the day/match flow was "advance-then-play" → now **play-on-the-day** ("Play Game"); manager IDs
  were reallocated on load (`RegisterPerson` → `RegisterExisting`); `Shot` got a `[JsonConstructor]`; load failures
  now fail safe; the schedule is now a **rolling, epoch-anchored** window. **This pass:** **season rollover wired**
  (`StartNewSeason` was never called → stuck in season 1; now triggered from `GameManager.NewDay`); interview days are
  now **fixed-cadence and placed before pub/training** so they no longer wander or vanish day-to-day; the **cup
  save/restore + past-date** bugs above; stat tracking added to the match (`Match.Shot.keeper`, `TeamStats.shots`,
  engine-credited assists, `TeamStats.keeper`); a `BackButton` + `UIManager` navigation history.

---

## 16. Deep systems pass (tactics, in-match, injuries, interviews, team talks)

A large pass building out five interlocking systems around one **core design rule: every edge carries a matching
risk.** Code is complete and compiles; remaining work is editor/scene wiring documented in `NEW_SYSTEMS_BUILD.md`.

**16.1 Tactics depth (§4).** `Tactic` gained a **Mentality** dial (very defensive → all-out attack), **reliances**
(an instruction can lean on one chosen player → his named stats sway the team more, strengths *and* weaknesses), a
**muscle-memory Familiarity** model (per-setting habituation weights that only build through playing/training, so
tinkering doesn't drain them and ~3 months of one tactic reaches ~100%), and a **squad-average Complexity →
Intelligence** gate (penalties only bite when the XI's *average* intelligence falls short, so a smart side covers
for one dim player). The seam for reliances + complexity is `PlayerExtensions.AverageStats(tactic)`. Tactics
**serialize** (incl. `SettingWeights` + reliance bindings). New UI: `MentalitySelectorUI`, `TacticInfoUI` (squad-IQ
read-out), `FamiliaritySlider` (non-interactable).

*Reliances evolved (see §4 for the live detail):* they bind by **squad slot** (so they follow subs and survive
formation changes), carry **eligible position groups** (which both filter the picker/auto-disable AND scope *which
phases of play* the reliance affects — a defenders reliance sways build-up, not finishing) and a **familiarity
penalty** for changing the relied-on player. `hasReliance` is **derived** from a non-empty eligible-groups list (no
separate flag). Instructions can also declare **complementary** synergies — `complementaryInstructions`, granting
tactic-stat perks when a named partner instruction is also active (the positive mirror of `incompatibleInstructions`).

**16.6 Shared UI: toggle baseline + player picker.** Tactic toggle buttons share an abstract base
`TacticOptionToggle` (Toggle + colours + `OnToggleChange` + Set/SetInteractable); `TacticsToggle` derives from it.
**Reliances are ordinary instructions** (a `TacticInstruction` with `hasReliance`), so they flow through
`TacticGridLayout` like any instruction — toggling one on runs the player picker. A reusable modal
`PlayerPickerPopup` (+ `PlayerPickerRow`) picks one player from a supplied list — used by reliance instructions
(pick from the starting XI) and by training's `PlayerSlotUI` (five "click to add / X to remove" slots, squad minus
already-slotted). Build steps: `NEW_SYSTEMS_BUILD.md` §8.

**16.2 In-match tactical changes (§5).** During open play you can only **shout / change mentality**
(`MoreAttacking`/`MoreDefensive`/`SetMentalityInMatch`, temporary, reset at full time; they take effect at the next
simulated minute). At **half-time** the full tactics page opens for any structural change and resumes the match
without restarting (`MatchSimPageUI.OpenHalfTimeTactics` → `TacticsPageUI` Resume button → `UIManager.ResumeMatchFromTactics`
→ `ResumeDisplay`); structural edits are gated by `Tactic.CanMakeStructuralChange`/`IsHalfTime`.

**16.3 Injuries, suspensions & death (§5, §6).** New `Player` availability model (`CurrentInjury`/`InjuredUntil`/
`MatchesSuspended`/`YellowCards`/`IsDeceased`, with `IsAvailable`/`AvailabilityStatus`, recovery + serving). New
`InjuryManager` rolls daily off-pitch injuries/illness/death and processes post-match cards→suspensions and
injuries for the player's team; the engine generates fouls/cards/injuries (`TryFoul`, `FoulHighlight`,
`Match.RecordFoul`). `Team.EnsureAvailableLineup` benches the unavailable. Morale impact **scales with severity**
(`ApplyInjuryMorale`): a knock only nicks the player; an ACL hits him hard and ripples to the squad; a death rocks
everyone. **Must add `InjuryManager` to the scene.**

**16.4 Interview day (§7).** Schedule-gated (`OnInterviewDay` wired), one session/day, squad cap with
release-before-hire. `InterviewAnswerGenerator` now keys answers on **~3 response buckets per question** (not 10
bespoke per-personality lines); the bucketing **differs per question** and *every* answer returns its bucket as the
clue, so `InterviewManager` intersects them into `NarrowedPersonalities` — combining questions distinguishes the
hidden personality. Ability questions add **shape variation** (a clear standout stat vs one of many; specialist vs
versatile) with the threshold shifted by bucket. A **CompareToPlayer** question opens `PlayerPickerPopup` to pick any
squad member and scores ability by *signed √ of each stat difference, summed* → "about the same / an edge / miles
off", biased by personality. `RecruitmentManager.debugEndlessInterviews` makes every day an interview day for testing.
UI helper: `InterviewQuestionButton`. See `INTERVIEW_INMATCH_TEAMTALK_BUILD.md` / `WIRING_RECRUITMENT_HALFTIME_SIM.md`.

**16.5 Half-time team talk — reuses the discussion system (`TEAM_TALK_UI.md`).** The active talk is a squad-wide
version of the 1-on-1 discussion: you pick one `Event.Response` (Praise/Rage/Encourage/…), and every squad member
reacts via the **same** `EventsManager.ReactionTable` (keyed on personality) and `Person.NewMorale`, with the
**severity taken from the half-time scoreline** (`TeamTalkReactions.SeverityFromScore`: 1-up → Pleasant, 3-down →
Pressing). `TeamTalkUI` lays every player out as a morale box along a shallow arch and flashes how each took it;
`TeamTalkController.DeliverTalk(response, severity)` applies it. (The `TeamTalkMinigame` classes remain but dormant.)

**16.7 Stat leaderboards (`STATS_WIDGETS_BUILD.md`).** The match now persists every shot (`TeamStats.shots`, with a
`keeper` ref), engine-credited assists, and the keeper who played (`TeamStats.keeper`). `StatLeaderboards` derives
ranked top-N tables (goals, assists, shots/on-target, saves, clean sheets, goals conceded, own goals, big misses,
cards) for players **or** teams **on read** from a competition's fixtures — no stored counters. One configurable
`StatLeaderboardWidget` (+ row, link-clickable) serves the home page (by team, with a stat dropdown), the My Team
page (by player), and a dedicated `StatsPageUI`.

**16.8 Analysis harness — `TacticLab` (Assets/_Scripts/Analysis).** A headless A/B sim that builds synthetic dummy
teams (random stats averaging to a target you set, natural at their formation slot), runs N paired-RNG games per
"variation", and logs average results — so you can measure the effect of a reliance/instruction, formation, weak
keeper, or familiarity level in isolation. Pure tooling; not shipped in the game loop.

**16.9 Back navigation.** `UIManager` keeps a navigation history (a stack of re-show delegates pushed by each
`ShowX`); a `BackButton` component (drop on any Button, auto-hooks) calls `UIManager.Back()` to return to the
previous page. Live matches and discussions are excluded; `Back()` is a no-op mid-match.
