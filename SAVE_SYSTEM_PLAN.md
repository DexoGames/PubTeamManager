# Save System & ID Reference Migration Plan

> Status: **approved, ready to execute.** Written for Pub Team Manager (Unity / C# / Newtonsoft.Json).
> Code is the source of truth — verify method/field names against the files before relying on them.

## Goal

Make the game **data-driven and robust**: every persistent entity has a stable, allocator-backed
integer ID; every cross-reference serializes as that ID; every lookup goes through a manager with
O(1), null-safe resolution. This is the foundation for **historical data** — being able to look back
on past seasons, re-open old fixtures, and inspect their results/stats (Football Manager style).

Runtime stays **direct-reference**: IDs are only the persistence key and the manager lookup key. The
in-memory object graph keeps real C# references (`Fixture.HomeTeam` is a real `Team`, etc.), so no
gameplay call site that walks `.Team` / `.HomeTeam` changes.

## Decisions (locked in)

- **Per-type counters**, not one global counter. Person/Team/Competition/Fixture IDs each stay small
  and contiguous within their type.
- **Fixtures are full ID-bearing entities** — required so historical games are individually
  addressable and queryable for past-season browsing.
- **`IdManager` is its own singleton** (separate `MonoBehaviour`), for separation of concerns and
  testability, rather than folding counters into `GameManager`.
- **ScriptableObject assets** (`Formation`, `TacticTemplate`, `EventType`) stay keyed by asset name —
  they are editor assets, not runtime entities. No ID migration for them.
- **Each season is its own `Competition` instance**, retained after it ends (not re-initialised in
  place). Instances of the same competition across years are linked by a **`CompetitionSeries`**
  lineage (its own stable ID), so the UI can browse "all seasons of the Premier Division."

---

## Current state (analysis)

The serialization design is sound: models serialize themselves via Newtonsoft attributes (no DTO
layer), owned data is inline, cross-references are written as keys via custom converters, and the
runtime graph is all direct references. The problems are in **ID allocation** and **resolution
ordering**, not the overall shape.

### Four keying schemes today

| Reference target | Key | Converter | Resolves via |
|---|---|---|---|
| `Person`/`Player`/`Manager` | `PersonID` (int) | `PersonRefConverter` | `PersonManager.GetPerson` |
| `Team` | `TeamId` (int) | `TeamRefConverter` | static registry, then `TeamManager.GetTeam` |
| `Competition` | `Name` (string) | `CompetitionRefConverter` | `FixturesManager.Competitions` |
| `Formation`/`TacticTemplate`/`EventType` | asset `name` (string) | `ScriptableObjectRefConverter` | `Resources.LoadAll` |

Fixtures are currently **not** ID-addressable — they are owned inline by `Competition.Fixtures` and
the only back-reference (`Fixture.Competition`) is `[JsonIgnore]` and rebuilt on load.

### Problems to fix

1. **Broken ID allocation.** `PersonManager.RegisterPerson` returns `People.Count` (1-based count, not
   a stable unique ID); the `nextPersonID` field is dead code. `TeamId` is positional
   (`SetTeamId(i)` from the spawn loop). Any removal (retirement, free agent leaving) makes the next
   allocation collide with a live ID. Neither comes from an allocator that survives across sessions.
2. **Resolution depends on manager population order.** `TeamRefConverter` needs the static
   `TeamConverter.DeserializedTeams` registry because `TeamManager` isn't populated when fixtures
   deserialize. `PersonRefConverter` resolves immediately against `PersonManager`, which only works
   because persons happen to be registered before `Events` restore — an implicit ordering contract.
3. **Unsafe lookups.** `PersonManager.GetPlayer/GetManager` do `FirstOrDefault(...).GetType()` (NRE on
   miss) and use exact `GetType() == typeof(Player)` (breaks on subclasses). All lookups are O(n).
4. **Competitions keyed by display name** — collision hazard, and display rename breaks references.
5. **No single source of truth** for "give me entity X" — each manager reimplements its own lookup.

---

## Stage 1 — Central per-type ID allocators

**New file: `Assets/_Scripts/Controllers/IdManager.cs`**
- Singleton `MonoBehaviour` (same `Awake` guard pattern as the other managers).
- Per-type counters: `NextPersonId`, `NextTeamId`, `NextCompetitionId`, `NextFixtureId`,
  `NextSeriesId` (start 0).
- Allocators: `AllocatePersonId()`, `AllocateTeamId()`, `AllocateCompetitionId()`,
  `AllocateFixtureId()`, `AllocateSeriesId()` — return current value, then increment.
- `SeedFromState(...)` — set all counters on load so entities created after a load continue past the
  highest restored ID (no collisions with restored entities).

**`Serialization/GameState.cs`** — add persisted counters
(`NextPersonId`, `NextTeamId`, `NextCompetitionId`, `NextFixtureId`, `NextSeriesId`) and the
`List<CompetitionSeries> CompetitionSeries` collection (see Stage 4b).

**`Controllers/SaveManager.cs`**
- `CollectGameState()` reads the four counters from `IdManager`.
- `RestoreGameState()` calls `IdManager.SeedFromState(...)` **first**, before any entity registration.

**`Controllers/PersonManager.cs`**
- `RegisterPerson(person)` assigns `person.PersonID = IdManager.AllocatePersonId()` for new persons.
- Add `RegisterExisting(person)` (or an `assignId` flag) for restore — keeps the deserialized
  `PersonID`, just indexes it.
- Delete dead `nextPersonID` field.

**`DataModels/Person.cs`** — `GeneratePerson()` keeps calling `RegisterPerson(this)` but relies on it
to *set* the ID rather than returning a count.

**`Controllers/TeamManager.cs`** — replace positional `team.SetTeamId(i)` with
`team.SetTeamId(IdManager.AllocateTeamId())`. Player-team-is-index-0 logic stays (ordering, not identity).

---

## Stage 2 — Uniform, safe, O(1) manager lookups

**`Controllers/PersonManager.cs`**
- Back with `Dictionary<int, Person>` (keep a `List`/`IReadOnlyCollection` view if other code iterates).
- `GetPerson(id)` → `TryGetValue`.
- Fix `GetPlayer`/`GetManager`: null-safe, use `p as Player` / `p is Manager` (subclass-tolerant),
  log + return null on miss instead of NRE.
- Add `Unregister(int id)` for retirements / free agents leaving — now safe with allocator IDs.

**`Controllers/TeamManager.cs`**
- Back `GetTeam(id)` with `Dictionary<int, Team>`, populated in `SpawnTeams` and `RestoreTeamsFromState`.

---

## Stage 3 — Stable integer Competition IDs

**`DataModels/Competition.cs`** — add `public int Id;`, allocated via
`IdManager.AllocateCompetitionId()` when `FixturesManager.AddComps` builds competitions.

**`Serialization/CompetitionRefConverter.cs`** — switch from `Name` (string) to `Id` (int): write `Id`,
resolve via new `FixturesManager.GetCompetition(int)`. `Name` becomes display-only.

**`Controllers/FixturesManager.cs`** — add `Dictionary<int, Competition>` + `GetCompetition(int)`;
populate in `AddComps` and `RestoreCompetitions`.

---

## Stage 4 — Fixtures as ID-bearing entities (historical data foundation)

**`DataModels/Fixture.cs`**
- Add `public int Id;`, allocated via `IdManager.AllocateFixtureId()` in the fixture constructor
  (and assigned/preserved on deserialize).
- Add a parameterless-ctor path that keeps the deserialized `Id`.

**`Controllers/FixturesManager.cs`**
- Add `Dictionary<int, Fixture>` keyed by `Fixture.Id`; expose `GetFixture(int)`.
- `RegisterFixtures` indexes by ID. Existing name/team-based lookups remain but are backed by the dict.
- This is what lets the UI request "fixture #1234" from any past season and render its result/stats.

**New: `FixtureRefConverter`** (`Assets/_Scripts/Serialization/FixtureRefConverter.cs`)
- For any field that references a fixture by ID (e.g. future "match report" links, season archives,
  highlight references). Writes `Fixture.Id`, resolves via `FixturesManager.GetFixture`.
- Owned-inline fixtures (`Competition.Fixtures`) still serialize fully; this converter is only for
  *references* to a fixture from elsewhere.

---

## Stage 4b — Per-season competition instances + `CompetitionSeries` lineage

This is the historical-data backbone. Today `FixturesManager.StartNewSeason` **reuses** the same
`League` objects (`league.Initialize(...)`) and **clears** `allFixtures` — so past seasons are
destroyed. We change rollover so each season is a fresh, retained instance, and link instances of the
same competition across years.

**New file: `Assets/_Scripts/DataModels/CompetitionSeries.cs`** (plain C# class, serialized)
- `int Id` — from `IdManager.AllocateSeriesId()`; stable lineage identity.
- `string Name` — display name of the lineage (e.g. "Premier Division", "Papa Johns Cup").
- `string TemplateName` — anchor to the editor blueprint (`LeagueTemplate` name / cup name) used to
  match a series when a new season is created.
- `List<int> SeasonCompetitionIds` — the `Competition.Id`s of each season's instance, in season order.
- Helper accessors resolve those IDs to live `Competition`s via `FixturesManager`.

**`DataModels/Competition.cs`** — in addition to `Id` (Stage 3), add:
- `int SeriesId` — links the instance back to its `CompetitionSeries`.
- `int SeasonYear` (and/or `string SeasonLabel` like "2024/25") — which season this instance is.
- `bool IsComplete` already exists — used to distinguish active vs archived instances.

**`Controllers/FixturesManager.cs`**
- Hold `List<CompetitionSeries>` + `Dictionary<int, CompetitionSeries>`; add
  `GetSeries(int)`, `GetOrCreateSeries(templateName, displayName)`,
  `GetSeasonsOfSeries(int) -> ordered List<Competition>`, and `GetActiveCompetitions()`
  (`Competitions.Where(c => !c.IsComplete)`).
- `AddComps`: for each template, `GetOrCreateSeries(...)`, create the first season's instance, set its
  `SeriesId`/`SeasonYear`, and append its `Id` to the series.
- **Rewrite `StartNewSeason`:**
  1. Mark current instances `IsComplete = true` and **keep** them in `Competitions` (do **not** clear
     `allFixtures`; old fixtures stay queryable — they're already excluded from "upcoming" queries by
     the `!BeenPlayed` / date filters).
  2. Apply promotion/relegation to team rosters (existing movement logic).
  3. Create **new** `Competition` instances (new `Id`, same `SeriesId`, `SeasonYear + 1`) from the same
     series for both leagues and the cup; append their `Id`s to their series.
  4. Re-link promotion/relegation on the new instances (the series records the structural lineage;
     per-instance links are re-established each season as today).

**New: `CompetitionSeriesRefConverter`** (optional) — if any field needs to reference a series by ID.
The `CompetitionSeries` collection itself serializes inline in `GameState`.

**UI payoff:** a "competition" hub page lists every season via `GetSeasonsOfSeries(seriesId)`; each
season opens its retained `Competition` (final table, full fixture list); each fixture opens by
`Fixture.Id` to show its stored result/stats.

---

## Stage 5 — Two-pass load; remove ordering hacks

Rework `SaveManager.RestoreGameState` into two explicit passes:
- **Pass 1 — instantiate & register:** deserialize all Teams (+ owned Players/Managers), Competitions,
  Fixtures, FreeAgents; register each into its manager's dictionary by ID, *without* resolving any
  cross-references yet.
- **Pass 2 — resolve refs:** every ref-converter (`Team`/`Person`/`Competition`/`Fixture`) resolves
  against fully-populated managers. Then run `OnAfterDeserialize` fixups (`Rounds[]`,
  promotion/relegation, fixture→competition back-refs, `Event` person resolution).

**Result:**
- Delete `TeamConverter.DeserializedTeams` static registry and `ClearRegistry()`.
- `TeamRefConverter` resolves purely via `TeamManager.GetTeam`.
- "Persons before events" becomes explicit and order-independent.

Riskiest stage (touches restore path + converter internals) — land Stages 1–4 and verify a save/load
round-trip first.

---

## Stage 6 — ScriptableObject reference cleanup (optional, no ID change)

`Formation`/`TacticTemplate`/`EventType` stay name-keyed via `ScriptableObjectRefConverter`. Optional:
centralize resolution in one `AssetRegistry` that caches `Resources.LoadAll` per path so loads aren't
scattered/repeated. No ID migration.

---

## Verification per stage (Unity editor)

1. New game → teams/persons/competitions/fixtures spawn with unique, contiguous per-type IDs.
2. Advance days, play fixtures, hire a free agent.
3. Auto-save → quit → reload → standings, fixtures, player↔team links, events, and the player's own
   team all resolve; newly created entities after load get fresh non-colliding IDs.
4. (Stage 4+) Request a past fixture by ID and confirm its result/stats render correctly.

## Touched files

- **New:** `Controllers/IdManager.cs`, `DataModels/CompetitionSeries.cs`,
  `Serialization/FixtureRefConverter.cs` (+ optional `CompetitionSeriesRefConverter.cs`,
  `AssetRegistry.cs`)
- `Serialization/GameState.cs`, `Controllers/SaveManager.cs`
- `Controllers/PersonManager.cs`, `Controllers/TeamManager.cs`, `Controllers/FixturesManager.cs`
- `DataModels/Person.cs`, `DataModels/Team.cs`, `DataModels/Competition.cs`, `DataModels/Fixture.cs`
- `Serialization/CompetitionRefConverter.cs`, `Serialization/TeamRefConverter.cs`,
  `Serialization/TeamConverter.cs`
