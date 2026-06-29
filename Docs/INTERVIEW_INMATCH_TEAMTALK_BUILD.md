# Deep dive: In-match changes, Half-time team talks, Interview day

An in-depth, code-grounded build guide for three of the systems summarised in `NEW_SYSTEMS_BUILD.md` (§3, §4/5,
§5 there). For each: **what's already coded vs what you build**, the exact classes/methods/serialized fields, the
data flow, and the gotchas. Class/method names below are the real ones in `Assets/_Scripts`.

---

## 1. In-match tactical changes

**The model.** The live match screen is `MatchSimPageUI` (a singleton `UIPage`) running a state machine:
`PreKickoff → FirstHalf → HalfTime → SecondHalf → FullTime`. The fact that makes everything trivial: **the engine
re-reads the tactic every simulated minute** (`Match.SimulateMinute` reads `Tactic.EffectiveMentality`/stats fresh),
so any change lands at the **start of the next minute** with no event plumbing.

Two tiers, enforced conceptually by `Tactic.CanMakeStructuralChange => !InMatch || IsHalfTime`:

### Tier A — shouts, anytime (open play). Mentality only.
- `MoreAttacking()` / `MoreDefensive()` → `ShiftMentality(±1)` → `Tactic.ShiftMentalityInMatch(delta)` (clamps
  `InMatchMentalityShift`, recalcs stats, returns the new `EffectiveMentality`, prints a touchline-shout line).
- `SetMentalityInMatch(int 0–6)` jumps straight to a level.
- **Temporary**: `Tactic.BeginMatch()` zeroes the shift at kickoff, `EndMatch()` clears it at full time — your
  *saved* tactic is never changed by a shout.

### Tier B — structural, half-time only.
At `EndHalf(First)`: state → `HalfTime`, `Tactic.IsHalfTime = true`, and the half-time panel auto-shows. Flow:
**Edit Tactics** → `MatchSimPageUI.OpenHalfTimeTactics()` → `TacticsPageUI.EnterReturnToMatchMode()` +
`UIManager.ShowTactics()`. The tactics page then reveals a Resume button → `TacticsPageUI.ResumeMatch()` →
`UIManager.ResumeMatchFromTactics()` → `MatchSimPageUI.ResumeDisplay()`, which re-shows the match screen **in the
same preserved state — never restarting**. Pressing Advance ("Begin Second Half") sets `IsHalfTime = false`.

### What you wire (hooks already exist on `MatchSimPageUI`)
- Two Buttons → `MoreAttacking()` / `MoreDefensive()`; optional 0–6 whole-number Slider → `SetMentalityInMatch((int)value)`.
- A TMP → the **Mentality Text** field (`_mentalityText`) — shows the live mentality label.
- A panel GameObject → the **Half Time Panel** field (`_halfTimePanel`) — auto-shows only at half-time. Inside it:
  an **Edit Tactics** button → `OpenHalfTimeTactics()` and a **Team Talk** button (system 2).
- On `TacticsPageUI`: a Resume button GameObject → the **resumeMatchButton** field; its OnClick → `ResumeMatch()`.

### Gotchas
- `CanMakeStructuralChange` is **defined but not consulted anywhere** — during a match the tactics page is only
  reachable at half-time, so it's belt-and-suspenders. If you ever expose tactics mid-play, gate the
  instruction/formation handlers on that flag.
- Familiarity advances for both teams inside `FinaliseResult` (a match counts as a drilling "session").

---

## 2. Half-time team talks

**The split:** the *logic* is done and unit-testable; you build the *renderer*.
- `TeamTalkMinigame` (abstract): `Title`, `Instructions`, `Score` (0–1), `IsComplete`.
- `TeamTalkController` (singleton on the managers object): `CreateRandom()` → a `GerrymanderGame` or `BalanceGame`;
  `ApplyResult(game)` applies the morale swing and returns a summary line; `Used` guards one-per-match;
  `ResetForMatch()` is already called in `MatchSimPageUI.SimMatch()` at kickoff.

### The trade-off (already coded)
`ApplyResult` computes `t = (score − flopThreshold) / (1 − flopThreshold)`, clamped `[−1,1]`, then
`mood = maxMoodSwing·t`, `passion = maxPassionSwing·t` applied to **every** squad member. A score **below**
`flopThreshold` (0.4) goes negative → morale **drops**. Tunables on the controller: `maxMoodSwing` (10),
`maxPassionSwing` (8), `flopThreshold` (0.4).

### What you build — a `TeamTalkUI` MonoBehaviour + prefab
Opened from the half-time panel's **Team Talk** button:
```csharp
TeamTalkMinigame game = TeamTalkController.Instance.CreateRandom(); // show game.Title + game.Instructions
// render the state, let the player edit it (below), enable Done only when game.IsComplete
string summary = TeamTalkController.Instance.ApplyResult(game);     // applies the swing, returns a line
// show summary, close the panel
```
- **GerrymanderGame ("Win the Room")** — a `Size×Size` grid (6×6, six zones of six). `Friendly[x,y]` = your colour
  (paint cells in two colours). Player paints a zone index per cell → `AssignCell(x,y,zone)`. Validate with
  `IsValidPartition(out string err)` (equal-size + 4-connected); enable Done on `IsComplete`. Show `DistrictsWon()`
  / `DistrictCount`. The board is ~45% yours on purpose, so winning the majority needs clever borders.
- **BalanceGame ("Steady the Ship")** — `Weights[]` bags (4), each placed at a signed distance ±`MaxDistance` (5)
  → `Place(i, dist)`. Show `NetTorque()` live (target 0). `IsComplete` when all placed; `Score = 1 − |torque|/maxTorque`.

### Gotchas
- `BalanceGame.IsComplete` treats `0` as "not placed", so a bag genuinely centred at 0 is illegal — force each onto
  a non-zero distance (default ±1, or require a drag).
- `Score` is `0` while invalid/incomplete, and a sub-threshold score **hurts** morale — disable Done until
  `IsComplete` so the player commits deliberately.
- Disable the Team Talk button when `TeamTalkController.Used` is true (one talk per match).
- Add microgames by subclassing `TeamTalkMinigame` and extending `CreateRandom()`.

---

## 3. Interview day

**Mostly wired** — `RecruitmentPageUI` already runs the whole candidate cycle; you assemble the prefab + question buttons.

### The pieces
- `RecruitmentManager` (singleton): gating (`IsInterviewDay`, `CanInterviewToday` = interview day & not used today),
  pool (`StartInterviewSession()` → `CurrentCandidates`, 5), squad cap (`MAX_SQUAD_SIZE` = 25, `SquadSize`,
  `IsSquadFull`), and `HirePlayer` / `HirePlayerReplacing(newPlayer, toRelease)`.
- `InterviewManager` (singleton): `StartInterview(player)` resets `NarrowedPersonalities` to *all* and calls
  `dialogue.Setup(...)`; `AskQuestion` / `AskAboutStat` generate a personality-flavoured answer, **intersect** the
  personality clue-set, push text to the dialogue; `QuestionsRemaining` (max 5), `NarrowedPersonalitiesText()`,
  `HireInterviewee()`, `RejectInterviewee()`.
- `RecruitmentPageUI` (singleton page): `StartSession` (shows "No Interviews Today" when gated), `OnInterviewClicked`
  → `StartInterview`, `OnHireClicked` (squad-cap message when full), `OnRejectClicked`, `OnSkipClicked`,
  `OnQuestionAsked` → refreshes "Questions remaining: N / Personality: X / Y".
- `InterviewQuestionButton`: one per question Button — set **Question Type** (+ **Stat** for `AskAboutStat`), assign a
  TMP label (auto-fills the question text in `OnEnable`), OnClick → `Ask()`.

### What you wire in Unity
1. The recruitment page: assign `headerText`, `candidateInfoText`, `questionsRemainingText`, and the four Buttons
   (`interviewButton → OnInterviewClicked`, `hireButton → OnHireClicked`, `rejectButton → OnRejectClicked`,
   `skipButton → OnSkipClicked`).
2. A **DialogueUI** for answers, assigned to `InterviewManager`'s `dialogue` field — answers stream there automatically.
3. A grid of question Buttons each with `InterviewQuestionButton → Ask()`. Suggested mix: `BiggestStrength`,
   `BiggestWeakness`, a few `AskAboutStat` (Shooting/Pace/Passing/Strength), and the four probes: `HandleCriticism`,
   `WorkEthic`, `BigGameMentality`, `Leadership`.
4. `RecruitmentManager` + `InterviewManager` on the managers object.

### The actual game (the interesting bit)
Each question sorts the 10 personalities into ~3 **response buckets**, and the answer text is keyed on the bucket —
so different personalities give the same answer to a given question, and you can't read personality off one reply.
**Crucially the bucketing differs per question**, and *every* answer returns its bucket as a clue, so the manager
**intersects the buckets across the questions you ask** to narrow the hidden personality (e.g. `HandleCriticism`
splits defensive/sensitive/dismissive/receptive, while `WorkEthic` splits grafter/coaster/balanced — Cocky and
Aggressive share the first bucket but split on the second). Ability questions do double duty: they reveal a stat AND
a coarse personality clue, plus **shape-based variation** — a *clear standout* stat vs *one of many*, a *specialist*
vs a *versatile* player — with the "standout" threshold shifted by the bucket (a boastful type claims a standout
readily, a modest one rarely). With only **5 questions per candidate**, which questions you spend is the decision:
the bucketings are in `InterviewAnswerGenerator` (one set of buckets per `AnswerX` method).

### Squad cap / release-before-hire
When `IsSquadFull`, `OnHireClicked` shows "release a player first." Two routes:
- Release via your squad screen, then re-press Hire (what the page assumes), **or**
- Build a one-step picker over `TeamManager.Instance.MyTeam.Players` → `RecruitmentManager.HirePlayerReplacing(candidate, toRelease)`.

### Gotchas
- Gating: only on a scheduled interview day, once/day. The schedule's `OnInterviewDay` tops up the pool (subscribed on
  new game + load). When gated, `StartSession` shows the message and disables the buttons.
- Answers reflect personality, so stat answers are **noisy** (perceived vs actual) — the probes are the reliable read.
- Max 5 questions per candidate (`MAX_QUESTIONS`). Plan the mix.

---

## Team Talk UI — now built (reuses the discussion system)
The team-talk renderer now exists as a squad-wide version of the **1-on-1 discussion**: every squad member shows as a
morale box along a shallow arch, and you pick one `Event.Response` (Praise / Encourage / Challenge / Persuade /
Inspire / Galvanise / Rage / Deflect) that hits everyone. Each player's reaction comes from the **same**
`EventsManager.ReactionTable` (keyed on personality) and the same `Person.NewMorale`, with the **severity derived
from the half-time scoreline** (`TeamTalkReactions.SeverityFromScore`: 1-up → Pleasant, 3-down → Pressing, …). So
there's no parallel morale maths — discussions and team talks share one tuning source. Scripts: `TeamTalkReactions`
(score→severity + display), `TeamTalkController.DeliverTalk(response, severity)`, `PlayerMoraleBoxUI`, `TeamTalkUI`;
`MatchSimPageUI.CurrentGoalDifferenceForMyTeam()` feeds the scoreline. **Full editor wiring is in `TEAM_TALK_UI.md`.**

So all three systems are now code-complete; what's left is editor wiring (prefabs, panel layout, button hooks).
