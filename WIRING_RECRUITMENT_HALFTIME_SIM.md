# Wiring guide — Recruitment/Interview, Half-time, Match Sim

Exact editor wiring (serialized fields, OnClick hooks, hierarchy). All logic/scheduling is already in code — this is
only Inspector work. Conventions used throughout:

- **UIPage hierarchy:** every page is a `XxxPageUI : UIPage` on a GameObject whose **child index 0 is the content
  root** (`Elements => transform.GetChild(0)`). `Show()` activates child-0 and calls `Setup()` on every `UIObject`
  under it; `Hide()` deactivates child-0. The page GameObject itself stays active. Each page sets its `Instance` in
  `Awake`, and `UIManager.Setup()` caches them — so **the page must exist in the scene at startup**.
- **Button label auto-set:** where noted, code writes the label via `GetComponentInChildren<TextMeshProUGUI>()`, so the
  button needs a child TMP.
- "Hook OnClick → `X.Method()`" means: in the Button's OnClick list, drag the GameObject holding that component and
  select the method (no-arg unless stated).

## 0. Managers that must be in the scene (singletons)
One GameObject (or several) carrying: `GameManager`, `UIManager`, `TeamManager`, `FixturesManager`,
`ScheduleManager`, `CalenderManager`, `PersonManager`, `IdManager`, `SaveManager`, `EventsManager`,
`RecruitmentManager`, `InterviewManager`, `TeamTalkController`, `TrainingManager`, `LoadingOverlay`.
`GameManager.SetupGame()`/`Start()` calls `RefreshPool()`, subscribes `ScheduleManager.OnInterviewDay →
RecruitmentManager.NotifyInterviewDay`, and calls `UIManager.Setup()` (which caches all page `Instance`s and shows
the home page). **No manual scheduling wiring needed.**

---

# 1. Recruitment / Interview

### Code that's automatic (do NOT wire)
- Interview days: `ScheduleManager` fires `OnInterviewDay` → `RecruitmentManager.NotifyInterviewDay()` (sets
  `InterviewDay`, tops up the 30-strong `FreeAgentPool`). Gating via `CanInterviewToday` (interview day & not used).
- Answer routing: `InterviewManager.StartInterview` calls `dialogue.Setup(...)`; each question calls
  `dialogue.UpdateDialogue(...)`. Personality narrowing is internal.

### 1a. DialogueUI (the Q&A panel) — `DialogueUI`
Build a panel with the answer display and add `DialogueUI`. Assign:
| Field | What |
|---|---|
| `descriptionText` | TMP — the candidate/context blurb |
| `personNameText` | TMP — candidate name (rich-text link) |
| `contentsText` | TMP — the spoken answer line |
| `extraInfoText` | TMP — morale-feedback line (toggled on/off by code) |
| `face` | `Image` — the morale face (colour/sprite set by code) |

### 1b. InterviewManager
On the managers object. Assign **`dialogue`** → the §1a `DialogueUI`. (That's its only field; `MAX_QUESTIONS = 5`.)

### 1c. Recruitment page — `RecruitmentPageUI` (a `UIPage`)
Page GameObject with child-0 Elements; add `RecruitmentPageUI` (it sets `Instance` in Awake). Assign:
| Field | What |
|---|---|
| `headerText` | TMP — "Interview Day — N Candidates" / "No Interviews Today" |
| `candidateInfoText` | TMP — current candidate / squad-full / session messages |
| `questionsRemainingText` | TMP — "Questions remaining: N / Personality: X / Y" |
| `interviewButton` | Button → OnClick `RecruitmentPageUI.OnInterviewClicked` |
| `hireButton` | Button → OnClick `RecruitmentPageUI.OnHireClicked` |
| `rejectButton` | Button → OnClick `RecruitmentPageUI.OnRejectClicked` |
| `skipButton` | Button → OnClick `RecruitmentPageUI.OnSkipClicked` |

Place the §1a DialogueUI under this page's Elements too, so it shows during the interview. (`OnShow` → `StartSession`
runs automatically when the page is shown.)

### 1d. Question buttons — `InterviewQuestionButton`
A row/grid of Buttons under the page's Elements. On **each** Button add `InterviewQuestionButton` and set:
| Field | What |
|---|---|
| `questionType` | the `InterviewQuestionType` (e.g. `BiggestStrength`, `AskAboutStat`, `HandleCriticism`) |
| `stat` | a `PlayerStat` — **only** read when `questionType == AskAboutStat` |
| `label` | TMP on the button — auto-filled with the question text in `OnEnable` |

Hook each Button's OnClick → `InterviewQuestionButton.Ask` (the component on that same button). Suggested set:
`BiggestStrength`, `BiggestWeakness`, 3–4 `AskAboutStat` (Shooting/Pace/Passing/Strength), and the four probes
(`HandleCriticism`, `WorkEthic`, `BigGameMentality`, `Leadership`). `Ask()` is a no-op until an interview starts, so
they can always be visible.

### 1e. Navigation to the page
A nav Button (home/menu) → OnClick `UIManager.ShowRecruitment` (drag the `UIManager` object). Squad-full hiring is
handled in `OnHireClicked` (shows a "release someone" message); to add a one-click replace, call
`RecruitmentManager.HirePlayerReplacing(candidate, toRelease)` from a release picker over `MyTeam.Players`.

---

# 2. Match Sim screen — `MatchSimPageUI`

### Code that's automatic
- Launch: the home "Next Day / Play" button → `GameManager.AdvanceOrPlay()` → if there's a pending player fixture
  today, `UIManager.ShowMatchSimPage(fixture)`. So **the only nav wiring is that one button** (see §2c).
- The Advance/Pause button **labels** are set by code per state ("Begin Game" → "Sim To Half Time" → "Begin Second
  Half" → … → "Return Home"). The engine re-reads the tactic each minute, so in-match changes need no event plumbing.

### 2a. The page itself (`MatchSimPageUI`, a `UIPage`)
Page GameObject, child-0 Elements, add `MatchSimPageUI` (sets `Instance`). Assign:
| Field | What |
|---|---|
| `_fixtureUI` | `FixtureUI` showing the scoreline (`SetFixtureText(fixture, true)`) |
| `_timerText` | TMP — minute / "Full Time" |
| `_advanceButton` | Button (needs a child TMP for its auto-label) → OnClick `MatchSimPageUI.Advance` |
| `_pauseButton` | Button (child TMP) → OnClick `MatchSimPageUI.Pause` |
| `_eventsContainer` | Transform — the commentary feed list (Vertical Layout Group + Content Size Fitter, in a Scroll View) |
| `_matchEventPrefab` | a `MatchEventUI` row prefab (`SetText(team, text, minute)`) |
| `_mentalityText` | TMP — live mentality label (§2b) |
| `_halfTimePanel` | GameObject — auto-shown only at half-time (§3) |

### 2b. In-match shouts (open play)
- **More Attacking** Button → OnClick `MatchSimPageUI.MoreAttacking`.
- **More Defensive** Button → OnClick `MatchSimPageUI.MoreDefensive`.
- *(optional)* a **Slider** with **Whole Numbers = true, Min Value 0, Max Value 6**. In its OnValueChanged, drag the
  `MatchSimPageUI` object and pick **`SetMentalityInMatch (float)`** from the **dynamic** section (there's now a float
  overload that rounds to a level, so it binds directly — pick the dynamic float entry, not the static one).
  (Shouts are temporary; the live mentality resets to your base mentality at full time.)

### 2c. Launch button (home page)
The home "Next Day / Play Match" Button → OnClick `GameManager.AdvanceOrPlay`. That single method both advances days
and launches the player's match when due; the sim returns to the home page at full time and autosaves.

---

# 3. Half-time (team talk + structural tactics)

### Code that's automatic
- At the end of the first half, `MatchSimPageUI.EndHalf(First)` sets state `HalfTime`, flips
  `Tactic.IsHalfTime = true`, and `UpdateHalfTimePanel()` activates `_halfTimePanel`. Leaving half-time (Advance)
  sets `IsHalfTime = false`. `ResumeDisplay()` re-enters the match in the same state — never restarts.

### 3a. The Half-Time Panel (assigned to `_halfTimePanel` in §2a)
A child panel of the match screen, holding two buttons:
- **Edit Tactics** Button → OnClick `MatchSimPageUI.OpenHalfTimeTactics` (it guards on `state == HalfTime`, calls
  `TacticsPageUI.EnterReturnToMatchMode()` then `UIManager.ShowTactics()`).
- **Team Talk** Button → OnClick `TeamTalkUI.Open` (the team-talk panel; full build in `TEAM_TALK_UI.md`).

You don't toggle the panel's visibility — code does (active only in the `HalfTime` state).

### 3b. Tactics page resume hook — `TacticsPageUI`
On the tactics page object, assign **`resumeMatchButton`** → a "Resume Match" Button GameObject. Hook that button's
OnClick → `TacticsPageUI.ResumeMatch`. The button auto-shows only when the page was opened from half-time
(`EnterReturnToMatchMode` set it); `ResumeMatch` calls `UIManager.ResumeMatchFromTactics()` →
`MatchSimPageUI.ResumeDisplay()`. Structural edits (formation/instructions) are allowed because `IsHalfTime` is set.

### 3c. Flow (no code needed)
First half ends → half-time panel appears → **Edit Tactics** (full tactics page, structural edits allowed) →
**Resume Match** → back on the match screen at half-time → **Team Talk** (optional, once) → **Begin Second Half**
continues the same match.

> Note: `Tactic.CanMakeStructuralChange` (`!InMatch || IsHalfTime`) is defined but **not enforced** anywhere — during
> open play the tactics page simply isn't reachable. If you ever add a mid-play tactics shortcut, gate the
> instruction/formation handlers on that property.
