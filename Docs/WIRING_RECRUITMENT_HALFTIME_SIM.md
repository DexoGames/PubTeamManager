# Recruitment & Interview — wiring reference

**Status: wired.** `DialogueUI` (on `Dialogue.prefab`) has all five fields assigned, `InterviewManager ▸ dialogue`
points at it, all seven `RecruitmentPageUI` fields are assigned, the four action buttons are hooked, and the nav
button calls `UIManager.ShowRecruitment`.

Two gaps remain, both tracked elsewhere:

| Gap | Doc |
|---|---|
| Three question types have no button (`CareerGoals`, `HandleCriticism`, `BigGameMentality`) — and the page can now spawn the full set from code instead | [`UI_AUDIT_AND_LAYOUT_PLAN.md`](UI_AUDIT_AND_LAYOUT_PLAN.md) §5.1 |
| Release-before-hire flow (hitting the 25-man cap is a dead end) | [`NEW_SYSTEMS_BUILD.md`](NEW_SYSTEMS_BUILD.md) §3 |

> The match-sim and half-time sections that used to live here are superseded by
> [`MATCH_SIM_AND_TEAM_TALK_BUILD.md`](MATCH_SIM_AND_TEAM_TALK_BUILD.md).

---

## Automatic — do not wire

- **Interview days.** `ScheduleManager` fires `OnInterviewDay` → `RecruitmentManager.NotifyInterviewDay()` (sets
  `InterviewDay`, tops up the 30-strong `FreeAgentPool`). Subscribed on both new game and load. Gating is
  `CanInterviewToday` (interview day, and not already used today); `RecruitmentPageUI` shows a "No Interviews Today"
  message otherwise, now naming the next interview day.
- **Answer routing.** `InterviewManager.StartInterview` calls `dialogue.Setup(...)`; each question calls
  `dialogue.UpdateDialogue(...)`. Personality narrowing is internal.
- **Testing:** tick `RecruitmentManager.debugEndlessInterviews` to make every day an interview day with unlimited
  sessions.

## Field reference

### `DialogueUI` (on `Dialogue.prefab`, one instance under the Recruitment page)
| Field | What |
|---|---|
| `descriptionText` | TMP — the candidate/context blurb |
| `personNameText` | TMP — candidate name (rich-text link) |
| `contentsText` | TMP — the spoken answer line |
| `extraInfoText` | TMP — morale-feedback line (toggled by code) |
| `face` | `Image` — the morale face (colour/sprite set by code) |

### `RecruitmentPageUI`
| Field | What |
|---|---|
| `headerText` | TMP — "Interview Day — N Candidates" / "No Interviews Today" |
| `candidateInfoText` | TMP — current candidate / squad-full / session messages |
| `questionsRemainingText` | TMP — "Questions remaining: N / Personality: X / Y" |
| `interviewButton` | Button → OnClick `RecruitmentPageUI.OnInterviewClicked` |
| `hireButton` | Button → OnClick `RecruitmentPageUI.OnHireClicked` |
| `rejectButton` | Button → OnClick `RecruitmentPageUI.OnRejectClicked` |
| `skipButton` | Button → OnClick `RecruitmentPageUI.OnSkipClicked` |
| **`questionContainer`** | **required** — the layout group the question row spawns into |
| **`questionButtonPrefab`** | **required** — `InterviewButton.prefab` |
| `statQuestions` | which stats may be asked about, in order; **leave empty = all 20** |

The page builds its question row from `InterviewManager.GetGeneralQuestions()` / `GetPersonalityQuestions()` /
`GetStatQuestions()` + the comparison question, so it can never drift from `InterviewQuestionType`.
**Delete the 16 hand-placed buttons** — the row is spawned now, and leaving them gives you both sets.

### `InterviewQuestionButton` (on `InterviewButton.prefab`)
| Field | What |
|---|---|
| `questionType` | the `InterviewQuestionType` |
| `stat` | a `PlayerStat` — **only** read when `questionType == AskAboutStat` |
| `label` | TMP on the button — auto-filled with the question text |

OnClick → `InterviewQuestionButton.Ask` (already set on `InterviewButton.prefab`). The Button component is read off
the same object, so there's nothing to assign for it. Buttons grey themselves out when there's no live interview or
all five questions are spent.

## How the interview plays

Every question keys its answer on a ~3-way **response bucket** (different bucketing per question), and **every**
answer narrows the hidden personality by intersection — shown in the questions-remaining text via
`InterviewManager.NarrowedPersonalitiesText()`, e.g. *"Personality: Cocky / Aggressive"*. Ability answers also vary
by **stat shape** (clear standout vs all-rounder), so they reveal ability *and* a personality clue. Max 5 questions
per candidate, one hire per session.

`CompareToPlayer` is the odd one out: it opens the shared `PlayerPickerPopup` first, and only consumes a question
once a squad member is chosen (cancelling costs nothing).
