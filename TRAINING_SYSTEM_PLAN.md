# Training System — Implementation Plan

> Status: **Plan (not yet executed)**
> Terminology: the temporary, trainable, decaying stat layer is called **Boost**.

---

## 1. Goals (from design brief)

1. Keep **all existing training drills**.
2. Player **selects one drill** as the team's ongoing training regimen.
3. The page **tells you when the next training session is**.
4. Most drills apply **Boost**: a temporary per-stat bonus that
   - applies to **all squad players** (not just the starting XI),
   - **builds up over time** (≈ +1 per training session),
   - **caps at +15** above the player's actual stat,
   - **fades/decays** when that stat isn't actively trained.
5. **Positional training**: select **up to 5 players** + a target position; permanently improves their **position strength** via a per-session dice roll with per-level thresholds.
6. **Set-and-forget**: whatever you set in the training menu **persists forever** (across saves) and **repeats every training day** until you change it.
7. **Future-proofing**: leave room for **per-day training scheduling** later without reworking execution.
8. Selecting a drill shows **visual info** about it (type, affected stats, expected effect, next session).
9. **Well-structured UI**; hand-build the prettier UI pieces in-editor (checklist provided).

---

## 2. Current state evaluation

### What already works
- `TrainingManager` singleton, with `CurrentSession` and `ExecuteTraining()`.
- It is wired to `ScheduleManager.OnTrainingDay` in **both** `GameManager.SetupGame()` and `SaveManager.RestoreGameState()`, so training auto-fires on training days.
- `TrainingSession.Execute()` routes by `TrainingType` and has working tactic-familiarity and social/morale logic.
- `TrainingPageUI` lists drills, colour-codes by type, has a preview text field.
- `ScheduleManager` already marks **Wednesday** as a `Training` day and raises `OnTrainingDay`.

### What's unfinished / wrong
| Problem | Where | Fix |
|---|---|---|
| Stat boosts are **permanent** (`ModifyStat` writes to `RawStats`) | `TrainingSession.ExecuteStatBoost` | Replace with temporary **Boost** layer |
| Only **starting XI** trained | all `Execute*` loops use `team.StartingPlayers` | Use whole squad (`team.Players`) |
| **Confirm executes instantly** | `TrainingPageUI.ConfirmTraining` | Confirm only **sets**; execution stays on `OnTrainingDay` |
| **Nothing persists** | `CurrentSession` not in `GameState` | Persist a serializable training record |
| Positional model is a continuous accumulator | `Player.ImprovePositionalStrength` | Replace with 50% roll + thresholds |
| **Direct `Player` refs** in `TrainingSession` (bad for save) | `TrainingSession.SelectedPlayers` | Store `List<int>` IDs, resolve via `PersonManager` |
| No "next training day" helper | `ScheduleManager` | Add `GetNextTrainingDay()` |
| Preview is bare | `TrainingPageUI.UpdatePreview` | Rich info panel |
| No positional player picker UI | — | New sub-panel (controller + editor prefab) |

### Key facts confirmed in code (constraints the design must respect)
- `Player.SKILL_NO = 18`; `RawStats.Skills` is an `int[18]`. `PlayerStat` enum: `Shooting=0 … Durability=17`, `Height=18` (Height stored separately).
- Stats pipeline: `GetStats()` → `GetStatsFor(position)` (if the player has a valid formation slot) → otherwise `GetRawStats()`; then `MoraleModifier`.
- `GetStatsFor()` clones raw stats and **scales non-physical skills** by position strength: `skill / (1 + (4 - strength)/3)`. Physical skills (Pace…Durability) are **not** scaled.
- `PositionStrength`: `None=0, Poor=1, Okay=2, Good=3, Natural=4`.
- `PersonManager.GetPlayer(int id)` now exists (ID system) — use it to resolve selected players on load.
- `TeamConverter` serializes `Player` objects inline (`serializer.Serialize(writer, value.Players)` / `ToObject<List<Player>>`), so **new public `Player` fields auto-serialize**. Newtonsoft serializes `Dictionary<enum,int>` with string keys by default (same as the existing `RawStats.Positions`).

---

## 3. Terminology — "Boost"

- The temporary per-stat layer is **Boost**.
- Per-stat value range: **0 … 15** (the cap = max amount a stat can be raised above its actual value).
- UI language: e.g. *"Shooting Boost +12 / 15"*, *"Boost fading"*.
- Code: `Player.Boost` (int[]), `BUILD_PER_SESSION`, `DECAY_PER_SESSION`, `MAX_BOOST`.

---

## 4. Architecture overview

```
DrillCatalog (static data)  ──defines──►  Drill (id, name, type, affected stats, description)
                                              ▲
TrainingSession (saved record) ──references──┘  by DrillId
   • DrillId
   • TargetPosition?         (positional only)
   • List<int> SelectedPlayerIds  (positional only)
   • [JsonIgnore] SelectedPlayers => ids → PersonManager.GetPlayer

TrainingManager (singleton)
   • CurrentSession : TrainingSession        (persisted via GameState)
   • SetTraining(session)                     (called by UI "Set")
   • ExecuteTraining()                        (called by OnTrainingDay)
   • GetDrills() => DrillCatalog.All

Player
   • int[] Boost            (0..15 each, persisted)
   • Dictionary<Position,int> PositionProgress  (persisted)
   • GetBoostedRawStats()   (raw + boost, used inside stats pipeline)
   • TickPositionalRoll(pos)  (50% counter++ / threshold / level-up)

ScheduleManager
   • GetNextTrainingDay()    (next scheduled Training entry)
```

**Separation of concerns:** `Drill` = static content; `TrainingSession` = the player's chosen, serializable instruction; `TrainingManager` = orchestration; `Player` = where effects live.

---

## 5. The Boost system (temporary stat layer)

### 5.1 Data
Add to `Player`:
```csharp
public int[] Boost = new int[SKILL_NO];   // 0..15 per skill, temporary, persisted
```
- Parameterless ctor / deserialization must guarantee a non-null length-18 array (guard in a helper `EnsureBoost()` since old saves won't have it).

### 5.2 Applying Boost to effective stats
Add a helper and route the pipeline through it:
```csharp
private Stats GetBoostedRawStats()
{
    Stats s = GetRawStats();
    EnsureBoost();
    for (int i = 0; i < SKILL_NO; i++)
        s.Skills[i] = Mathf.Min(100, s.Skills[i] + Boost[i]);
    return s;
}
```
- Replace `GetRawStats()` with `GetBoostedRawStats()` **inside** `GetStatsFor()` and inside the non-positional branch of `GetStats()`.
- **Design decision:** Boost is added at the **raw skill level**, *before* position scaling. Consequences:
  - The cap "no more than +15 above the actual stat" is enforced on the underlying skill (the number shown on the player card).
  - A boosted skill is still scaled down for off-positions (a +15 shooting boost is worth less to a player playing out of position). This is intended — boost rewards players used in their strong positions.
  - *Alternative considered:* add Boost to the final effective stat after scaling. Rejected — harder to reason about and to display "+X / 15" against the visible stat.

### 5.3 Build & decay (per training session)
Constants on `TrainingSession` (or a `TrainingConfig` static):
```csharp
const int MAX_BOOST          = 15;
const int BUILD_PER_SESSION  = 1;   // trained stats
const int DECAY_PER_SESSION  = 1;   // untrained stats
```
On each session, for **every player in the squad**:
- For each stat in the drill's `affectedStats`: `Boost[i] = min(MAX_BOOST, Boost[i] + BUILD_PER_SESSION)`.
- For every **other** stat: `Boost[i] = max(0, Boost[i] - DECAY_PER_SESSION)`.

Implications (intended):
- Specialising in one drill keeps 1–2 stats sharp and lets the rest sit near 0.
- **Non-boost drills** (Tactical / Social / Positional) train no stats → **all** boost decays that session (opportunity cost of doing positional/social work).
- A stat reaches full +15 after ~15 sessions (~15 weeks) and fades over a similar window. Both rates are tunable.

---

## 6. Drill catalog

### 6.1 `Drill` definition + `DrillId`
```csharp
public enum DrillId
{
    Shooting, Passing, Defending, Dribbling, Crossing, Heading,       // Technical
    Positioning, TacticalAwareness, CreativePlay, TeamBonding, Composure, // Mental
    Fitness, Strength,                                                 // Physical
    TacticDrills,        // Tactical
    TeamSocial,          // Social
    Positional           // Positional
}

public class Drill
{
    public DrillId Id;
    public string Name;
    public TrainingType Type;
    public PlayerStat[] AffectedStats;   // empty for Tactical/Social/Positional
    public string Description;
}
```

### 6.2 `DrillCatalog` (static, ports all current drills)
| DrillId | Name | Type | Affected stats |
|---|---|---|---|
| Shooting | Shooting Practice | Technical | Shooting |
| Passing | Passing Drills | Technical | Passing |
| Defending | Defensive Training | Technical | Tackling |
| Dribbling | Dribbling Drills | Technical | Dribbling |
| Crossing | Crossing Practice | Technical | Crossing |
| Heading | Heading Drills | Technical | Heading |
| Positioning | Positioning Drills | Mental | Positioning |
| TacticalAwareness | Tactical Awareness | Mental | Intelligence |
| CreativePlay | Creative Play | Mental | Creativity |
| TeamBonding | Team Bonding Drills | Mental | Teamwork |
| Composure | Composure Training | Mental | Composure |
| Fitness | Fitness Training | Physical | Pace, Stamina |
| Strength | Strength & Conditioning | Physical | Strength, Jumping |
| TacticDrills | Tactic Drills | Tactical | — (tactic familiarity) |
| TeamSocial | Team Social Activity | Social | — (morale) |
| Positional | Positional Training | Positional | — (position strength) |

> Note: a couple of Physical drills now affect **two** stats (Fitness→Pace+Stamina, Strength→Strength+Jumping), which is trivial now that affected stats are a list. Easy to tune.

- `DrillCatalog.All` returns the ordered list; `DrillCatalog.Get(DrillId)` returns one.
- *Future option:* convert `Drill` to a `ScriptableObject` so drills are editor-authored. Not needed now; static catalog is simplest and robust.

---

## 7. `TrainingSession` refactor (serializable, ID-based)

```csharp
[System.Serializable]
public class TrainingSession
{
    public DrillId Drill;
    public Player.Position? TargetPosition;     // positional only
    public List<int> SelectedPlayerIds = new(); // positional only

    [JsonIgnore] public Drill Definition => DrillCatalog.Get(Drill);
    [JsonIgnore] public TrainingType Type => Definition.Type;
    [JsonIgnore] public List<Player> SelectedPlayers =>
        SelectedPlayerIds.Select(id => PersonManager.Instance.GetPlayer(id))
                         .Where(p => p != null).ToList();

    public void Execute(Team team) { /* routes by Type, see §5.3 + §8 */ }
}
```
- Removes all direct `Player` references → safe to embed in `GameState`.
- `Execute()`:
  - **Boost types** (Technical/Mental/Physical): build affected stats, decay the rest, across `team.Players`.
  - **Tactical**: existing tactic-familiarity logic, but over `team.Players` (+ decay all boost).
  - **Social**: existing morale logic over `team.Players` (+ decay all boost).
  - **Positional**: per selected player, run the positional roll (§8) (+ decay all boost).

---

## 8. Positional progression (per spec)

### 8.1 Data
Add to `Player`:
```csharp
public Dictionary<Player.Position,int> PositionProgress = new();
```

### 8.2 Rule
On a Positional session, for each selected player (max 5) and the chosen `pos`:
```csharp
public void TickPositionalRoll(Position pos)
{
    EnsurePositions();
    PositionStrength cur = RawStats.Positions.TryGetValue(pos, out var s) ? s : PositionStrength.None;
    if (cur >= PositionStrength.Natural) return;            // capped

    if (Random.value < 0.5f)                                 // 50% chance per session
    {
        int counter = PositionProgress.TryGetValue(pos, out var c) ? c + 1 : 1;
        int threshold = ProgressThreshold(cur);
        if (counter >= threshold)
        {
            RawStats.Positions[pos] = cur + 1;               // level up (permanent)
            PositionProgress[pos] = 0;                        // reset
        }
        else PositionProgress[pos] = counter;
    }
}

static int ProgressThreshold(PositionStrength from) => from switch
{
    PositionStrength.None => 2,   // None → Poor
    PositionStrength.Poor => 3,   // Poor → Okay
    PositionStrength.Okay => 4,   // Okay → Good
    PositionStrength.Good => 8,   // Good → Natural  (the "jump")
    _ => int.MaxValue
};
```
- `Player.ImprovePositionalStrength` (old continuous model) is removed/replaced.
- `TrainingSession` enforces the **max-5** selection (validated in UI and defensively in `Execute`).

---

## 9. Persistence

### 9.1 `GameState`
Add:
```csharp
public TrainingSession CurrentTraining;
```

### 9.2 `SaveManager`
- `CollectGameState()`: `CurrentTraining = TrainingManager.Instance.CurrentSession`.
- `RestoreGameState()`: `TrainingManager.Instance.SetTraining(state.CurrentTraining)` (null-safe). Player IDs inside resolve lazily via `SelectedPlayers` (players already registered by `TeamConverter` during load).

### 9.3 Player fields
- `Boost` (int[18]) and `PositionProgress` (dict) serialize automatically inside `TeamConverter`'s inline player serialization.
- **Old-save safety:** add `EnsureBoost()` / `EnsurePositions()` guards (allocate if null/short) called at the top of the stats pipeline and the positional roll, so pre-Boost saves don't NRE.

---

## 10. Scheduling & "next session"

Add to `ScheduleManager`:
```csharp
public DateTime? GetNextTrainingDay()
{
    DateTime today = CalenderManager.Instance.CurrentDay.Date;
    for (int i = 0; i < SCHEDULE_AHEAD_DAYS; i++)
    {
        DateTime d = today.AddDays(i);
        if (schedule.TryGetValue(d, out var e) && e.Type == ScheduleEntryType.Training)
            return d;
    }
    return null;
}
```
- Surfaced on the training page ("Next session: Wed 12 Aug").
- **Future per-day scheduling:** because execution is driven solely by `OnTrainingDay` + `CurrentSession`, a future scheduler can simply assign different `TrainingSession`s per date (e.g. a `Dictionary<DateTime,TrainingSession>` consulted in `ExecuteTraining`) without touching Boost/positional logic. Keep `ExecuteTraining` reading from a single "session for today" accessor to make this swap trivial.

---

## 11. UI plan

### 11.1 What I implement (controllers / logic)
- **`TrainingPageUI` rework**
  - Build the drill list from `DrillCatalog.All`, grouped/colour-coded by `TrainingType`.
  - Selection state + **highlight the currently-set drill** (since it persists).
  - **Info panel** populated on select: drill name, type badge, affected stats, plain-English effect, and current **squad-average Boost** for those stats.
  - **"Set Training"** button → `TrainingManager.SetTraining(...)` (no instant execute). Confirmation toast/log.
  - **Next-session label** from `ScheduleManager.GetNextTrainingDay()`.
- **Positional sub-panel controller**
  - Player list with **up-to-5 multi-select** (rejects the 6th with feedback).
  - Position dropdown (`Player.Position`).
  - Writes `TargetPosition` + `SelectedPlayerIds` into the session.

### 11.2 What you build in-editor (nicer by hand) — checklist
I'll provide exact `SerializeField` names; you create + wire:
- [ ] Info panel layout: `header` (TMP), `typeBadge` (Image + TMP label), `affectedStatsRow` (container + small stat-chip prefab), `effectText` (TMP), `boostBars` (optional small bars), `nextSessionLabel` (TMP).
- [ ] **Set Training** button + a "currently set" indicator.
- [ ] Positional panel: a `ScrollRect` + **player-row prefab** (toggle + name + position text), and a `TMP_Dropdown` for the target position.
- [ ] Type badge colour swatches (Technical/Mental/Physical/Tactical/Social/Positional).
- [ ] Drill-button prefab (if restyling the existing one).

---

## 12. File-by-file changes

**New files**
- `Assets/_Scripts/DataModels/Drill.cs` — `DrillId` enum, `Drill` class, `DrillCatalog` static.

**Edited files**
- `Assets/_Scripts/DataModels/Player.cs`
  - Add `int[] Boost`, `Dictionary<Position,int> PositionProgress`.
  - Add `GetBoostedRawStats()`, route `GetStatsFor()` + `GetStats()` through it.
  - Add `EnsureBoost()`, `EnsurePositions()`, `TickPositionalRoll(pos)`, `ProgressThreshold(...)`.
  - Remove old `ImprovePositionalStrength` (or repoint it).
- `Assets/_Scripts/DataModels/TrainingSession.cs`
  - New serializable shape (`DrillId`, `TargetPosition?`, `List<int> SelectedPlayerIds`, `[JsonIgnore]` resolvers).
  - Rewrite `Execute()` for whole-squad Boost build/decay, positional roll, keep tactic/social.
- `Assets/_Scripts/Controllers/TrainingManager.cs`
  - `GetDrills()` → `DrillCatalog.All`; helper `BuildSession(DrillId, pos?, ids)`.
  - Keep `SetTraining` / `ExecuteTraining`; default to a sensible drill if none set.
- `Assets/_Scripts/Controllers/ScheduleManager.cs`
  - Add `GetNextTrainingDay()`.
- `Assets/_Scripts/Serialization/GameState.cs`
  - Add `TrainingSession CurrentTraining`.
- `Assets/_Scripts/Controllers/SaveManager.cs`
  - Collect + restore `CurrentTraining`.
- `Assets/_Scripts/UI/UIPages/TrainingPageUI.cs`
  - Full rework per §11.1 (+ new `SerializeField`s for you to wire).

---

## 13. Execution stages (order of work)

1. **Boost layer on `Player`** (data + `GetBoostedRawStats` + ensure-guards). *Compiles, no behaviour change yet.*
2. **`Drill` + `DrillCatalog`** (port all drills).
3. **`TrainingSession` refactor** (ID-based, serializable) + `TrainingManager` catalog wiring.
4. **Boost build/decay execution** over whole squad.
5. **Positional progression** (`TickPositionalRoll`, thresholds) + remove old model.
6. **Persistence** (`GameState` + `SaveManager`).
7. **`ScheduleManager.GetNextTrainingDay()`**.
8. **`TrainingPageUI` rework** + positional sub-panel controller.
9. **Editor checklist** handed to you for the UI prefabs/wiring.

---

## 14. Tunables (single place, easy to adjust)
| Constant | Default | Meaning |
|---|---|---|
| `MAX_BOOST` | 15 | Cap per stat above actual |
| `BUILD_PER_SESSION` | 1 | Boost gained on trained stats |
| `DECAY_PER_SESSION` | 1 | Boost lost on untrained stats |
| Positional roll chance | 0.5 | Per-session chance counter increments |
| Thresholds | 2 / 3 / 4 / 8 | None→Poor / Poor→Okay / Okay→Good / Good→Natural |
| Max positional selection | 5 | Players per positional session |

---

## 15. Verification checklist (in-editor, after execution)
- [ ] New game: set a Boost drill → advance to a training day → affected stats' Boost rises by 1 for the **whole squad**; other stats' Boost decays.
- [ ] Boost caps at +15 and never pushes effective stat over 100.
- [ ] Switch drills → previously trained stat's Boost fades over subsequent sessions.
- [ ] Positional: select ≤5 players + a position → over several sessions, counters rise ~50% of the time, strength levels up at 2/3/4/8, counter resets, caps at Natural.
- [ ] "Next session" label matches the next Wednesday/training entry.
- [ ] Save → reload: `CurrentTraining` (incl. positional selection) restored and keeps firing; `Boost` and `PositionProgress` survive.
- [ ] Old save (pre-Boost) loads without NRE (ensure-guards).
- [ ] Setting training does **not** execute immediately; it executes on the next training day.

---

*Cannot compile/run Unity here, so each stage is written defensively and the round-trip + UI wiring need in-editor verification.*
