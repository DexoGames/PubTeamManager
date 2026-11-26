# UI Audit & Layout Plan

For the current scene wiring, implemented career and match flows, and outstanding play checks, see [Career and match flows](CAREER_AND_MATCH_BUILD.md). The descriptions below record the earlier design and build state.


**Audit of `Menu.unity` as it stands today** (hierarchy, RectTransforms, layout components and every
`[SerializeField]` reference on the page scripts), plus a prioritised plan for fixing the layout, wiring and
structure. Focus pages: **Training**, **Recruitment**, **Match Sim** and especially the **half-time Team Talk**.

This audit reads the *scene*, not the docs. Where the existing build docs disagree with what's actually wired, this
one records reality — see [§7 Doc drift](#7--doc-drift).

Everything below is either **[SCENE]** (fix in the editor, no code) or **[CODE]** (drop-in script change).

> **Status:** every **[CODE]** item is now **done and in the working tree** — see §8 for the list. Everything left
> is **[SCENE]** editor work, laid out step by step in §8. For a whole-project sweep of *other* incomplete wiring,
> dead code and described-but-unbuilt features, see [`COMPLETENESS_AUDIT.md`](COMPLETENESS_AUDIT.md).

---

## 0. Method

Parsed `Assets/Scenes/Menu.unity` directly: GameObject/RectTransform graph, layout components with their settings,
prefab instances and their overrides, and the serialized field values of every page script. So the findings are what
the scene *actually* contains, not what the build docs describe.

---

## 1. What's structurally good — keep it

Worth stating up front so none of it gets "improved" away:

- **The page shell is sound.** `UIPage` + `UIManager.HideAllUI()`/`ShowX()` is a clean single-owner navigation
  model, one `Instance` per page cached at boot. Adding a page is cheap.
- **Canvas setup is correct.** Screen Space – Overlay, `Scale With Screen Size`, 1920×1080, Match 0.5. Nothing to
  change here.
- **Shared widgets are genuinely shared** — `ResponseManager`/`ResponseButtonUI` drives both the discussion page and
  the team talk; `PlayerPickerPopup` serves reliances, training slots and interview comparison; `PlayerSlotUI`,
  `StatWidget`, `Base UI/Panel 1`, `Base UI/Button`. This is the right instinct and the plan below leans on it.
- **The Home page is the model to copy.** `TopStrip` / `Lower` → `LeftColumn` + `OtherColumns` (HLG, ctrl+expand
  on), widgets as prefab variants. It's the only page that's fully layout-group driven and it's the only one that
  will survive a resolution change untouched. **Every fix below is "make this page look like Home".**

---

## 2. Cross-cutting problems

### 2.1 Two layout philosophies in one project — **[SCENE]**

| Page | Layout style |
|---|---|
| Home | Layout groups end-to-end ✅ |
| Match Sim `Elements` | HLG at top level, hand-placed inside ⚠️ |
| Training | HLG at top level, hand-anchored inside panels ⚠️ |
| Team Talk | 100% hand-placed ❌ |
| Recruitment | 100% hand-placed ❌ |

The hand-placed pages hold together at 1920×1080 and only there. Every text block is a fixed pixel rect
(`500×100`, `650×157`, `650.2×105.96`) positioned by `anchoredPosition` from the panel centre, so any content longer
than the authored string clips rather than reflows, and any aspect ratio other than 16:9 slides things apart.

**Fix:** convert the four bad panels to Vertical/Horizontal Layout Groups with `LayoutElement` on the children, the
same as Home. This is the single highest-value change in this document and it costs no code.

### 2.2 Placeholder naming — **[SCENE]**

`Texty`, `Texty (1)`…`Texty (5)`, `Text (TMP)`, `Text (TMP) (1)`, `Button`, `Button (1)`…, `Matchca`,
`InterviewButton (11)`. It is currently impossible to tell from the hierarchy which TMP is `situationText` and which
is `overallText` — you have to click each one and read the reference back off the page script. This is the reason
the Team Talk overlaps in §3 went unnoticed.

**Fix:** rename to the serialized field name (`SituationText`, `FlavourText`, `OverallText`, `TalksRemainingText`,
`HeaderText`, `CandidateInfoText`, …). Renaming does **not** break references — Unity tracks fileIDs, not names.

Special case: the player morale box prefab has already been renamed from `Matchca.prefab` to
**`Assets/Prefabs/TeamTalk/HalfTimePlayer.prefab`**. Its children (`Name`, `MoraleIcon`, `MoraleText`,
`ReactionText`, `Bubble/BubbleText`) still want the same renaming pass described above if not already done.

### 2.3 `using UnityEngine.UIElements;` in uGUI files — **[CODE]**

Present in `FormationInteractableUI.cs:9`, `PlayerDetailsUI.cs:5`, `TacticsPageUI.cs:8`. UI Toolkit defines its own
`Button`, `Image`, `Slider`, `Toggle`, `ScrollView`. These files compile today only because they happen not to name
those types unqualified. The moment someone adds `Button myButton;` to one of them they get an ambiguous-reference
error that reads like a mystery.

**Fix:** delete all three `using` lines (none of the three files uses a UI Toolkit type).

### 2.4 The Back button is dead — **[SCENE]**

`UIManager` maintains a full 50-deep navigation history (`PushNav`/`Back`/`CanGoBack`) and `BackButton.cs` is a
zero-wiring drop-in component. **Neither the component nor its guid appears anywhere in `Menu.unity` or in any
prefab.** So the entire back-stack is unreachable, and every page exit today goes through the top nav bar.

**Fix:** add a `Back` button to `Canvas > Constants` and put `BackButton` on it. No wiring — it auto-hooks its own
`onClick` and greys itself out via `CanGoBack`. Then add it to `UIManager.hideForMatchSim` so it disappears during a
match (`Back()` already no-ops mid-match, but a visibly dead button is worse than no button).

While you're in `Constants`: `hideForMatchSim` currently holds 7 of the 9 children — **`Exit` and `ResumeGame_Btn`
are not hidden during a match.** A live Exit button over the match screen is a footgun.

### 2.5 🔴 A UnityEvent still targets a deleted class — **[SCENE]**

The Competition page's prev/next week buttons both carry a persistent call with:

```
m_Target: {fileID: 0}
m_TargetAssemblyTypeName: FixtureListUI, Assembly-CSharp
m_MethodName: ChangeWeekFocus
```

`FixtureListUI` no longer exists — the class was renamed to `CompetitionPageUI` (which still declares
`ChangeWeekFocus(int)` at `CompetitionPageUI.cs:30`). A renamed MonoBehaviour does **not** carry its UnityEvent
subscribers across, so both buttons are inert and the Inspector shows a missing-method entry on each.

**Fix:** delete the dead entry on each button, then re-add: **+** → drag the **Competition Page** object →
`CompetitionPageUI.ChangeWeekFocus`, argument `-1` (prev) / `+1` (next).

> Worth a sweep whenever a UI class is renamed: Inspector-hooked methods are matched by **class name string**, so a
> rename silently unhooks every button that called into it. Only `ChangeWeekFocus` is affected today.

---

## 3. Half-time Team Talk — the worst page, and one blocker

`Match Simulation Page > Elements > Team Talk UI`. Current hierarchy:

```
Team Talk UI  [inactive]                    TeamTalkUI          1946×848, anchored TOP-LEFT, no background Image
├── Players                                 GridLayoutGroup     cell 200×150, top-stretch, fixed height 500
├── Texty                → titleText        TMP  650×157  @ (   0.00, -111.7)
├── Texty (1)            → situationText    TMP  650×157  @ (-689.10, -111.7)
├── Texty (2)            → flavourText      TMP  600×157  @ ( 731.79, -111.7)
├── Texty (3)            → overallText      TMP  650×106  @ (  17.99,    0.0)
├── Individual                              (no Image, full-screen stretch)
│   ├── Texty (4)        → individualTargetText  TMP 650×106 @ (17.99, 0.0)
│   └── Texty (5)        → talksRemainingText    TMP 650×106 @ (17.99, 0.0)
└── Responses            <Responses.prefab>  ResponseManager, bottom bar, height 100
```

### 3.1 🔴 BLOCKER — there is no way out of the team talk

`TeamTalkUI.Open()` calls `MatchSimPageUI.SetMatchContentVisible(false)`, which **disables every sibling under
`Elements`** — including the `Buttons` column that holds `Advance`, `Pause`, `Tactics` and `Team Talk`. The screen is
given over to the talk.

The three things that call `Close()` are:

| Caller | Reachable while the talk is open? |
|---|---|
| `MatchSimPageUI.Advance()` (HalfTime case) | ❌ the Advance button is disabled |
| `MatchSimPageUI.OpenHalfTimeTactics()` | ❌ the Tactics button is disabled |
| A Close/Done button on the panel | ❌ **no such button exists in the scene** |

So opening the team talk soft-locks the match. Nothing under `Team Talk UI` is a Button except the tone buttons
inside `Responses` and the morale boxes.

**Fix — [SCENE]:** add a **`CloseButton`** to the talk panel (bottom-right, `Base UI/Button` prefab, label "Back to
the match") and hook its OnClick → `TeamTalkUI.Close`.

### 3.2 🔴 `responseManager` and `individualResponseManager` are the *same* object

Both fields point at fileID `1414398093` — the single `Responses` prefab instance parented directly to
`Team Talk UI`. `TeamTalkUI` is written assuming two distinct rows:

- Both `Deliver` and `DeliverIndividual` end up subscribed to the same `OnResponseChosen`, so one click fires both.
  (It limps: `Deliver` early-returns on `TeamTalkController.Instance.Used`.)
- `EnterIndividualPhase()` calls `responseManager.SetInteractable(false)` to lock the squad row — then
  `OnBoxClicked` calls `individualResponseManager.SpawnButtons()`, which **rebuilds that same row**, un-locking it.
- The private-word tone buttons therefore spawn into the **squad row's container at the bottom of the panel**, not
  inside the `Individual` popup. The popup shows a name and nothing else.

**Fix — [SCENE]:** put a **second** `Responses.prefab` instance inside `Individual`, and point
`TeamTalkUI.individualResponseManager` at *that* one. (`Responses.prefab` is self-contained — its own
`ResponseManager` + container + `ManagerDialogue`. This is exactly the reuse the shared component was built for.)

### 3.3 🟠 Three texts stacked on the same pixel

`overallText`, `individualTargetText` and `talksRemainingText` are all `650.2×105.96` at `(17.99, 0)` — the panel
centre. Two of them are inside `Individual`, so the moment the private-word popup opens you get
"A word with Smith" drawn on top of "Private words left: 2" drawn on top of the squad verdict.

**Fix — [SCENE]:** covered by the §3.6 rebuild — the three lines belong in three different regions.

### 3.4 🟠 `talksRemainingText` is inside the popup it advertises

`UpdateTalksRemaining()` runs from `EnterIndividualPhase()` — i.e. the moment the squad talk lands, while the popup
is **closed**. So the "Private words left: N" counter is only ever visible once you've already committed to
spending one, and the "Deliver a team talk first" prompt is never seen at all.

**Fix — [SCENE]:** move `TalksRemainingText` out of `Individual` and onto the panel's header row, next to the title.
It's a persistent HUD read-out, not popup chrome.

### 3.5 🟠 `Individual` is a click-through, backdrop-less "modal"

It stretches `0,0`–`1,1` over the whole panel but has **no `Image`**, so it neither dims the squad behind it nor
blocks raycasts. Clicks land on the morale boxes underneath — and `OnBoxClicked` re-targets `individualTarget`
mid-popup. There is also no **Cancel** button, despite `TeamTalkUI.CancelIndividual()` existing for exactly that.

**Fix — [SCENE]:** give `Individual` a dim `Image` (`0,0,0,0.6`, Raycast Target **on**), size it as a real centred
dialog rather than a full-screen stretch, and add a `CancelButton` hooked to `TeamTalkUI.CancelIndividual`.

### 3.6 🟠 The panel has no background and no layout

- No `Image` on the root, yet the whole match screen behind it is switched off — the talk floats over bare canvas.
- Root rect is a fixed **1946×848 anchored top-left**, which is also self-contradictory: its parent `Elements` is a
  `HorizontalLayoutGroup` with Control Child Width/Height **on**, so those numbers are overwritten at runtime
  anyway.
- `Players` is a `GridLayoutGroup` with a **hand-set 500px height and no `ContentSizeFitter`**. It happens to fit a
  25-man squad (9 per row × 3 rows × 150 = 450) at 1920×1080. That is coincidence, not design — one wider cell, one
  extra squad slot, or one narrower screen and the last row lands on top of the text below it.
- `arcWidth` (2105), `arcDepth` (408) and `topMargin` (20) are still serialized on `TeamTalkUI`, but the arch
  placement code in `BuildSquadArch()` is **commented out** — the grid does the layout now. Dead inspector fields
  that read as live tuning knobs.

**Fix — [SCENE], target hierarchy:**

```
Team Talk UI                       Image (opaque)  +  VerticalLayoutGroup (ctrl W/H, padding 40, spacing 20)
├── Header                         HorizontalLayoutGroup   LayoutElement minHeight 90
│   ├── TitleText                  → titleText
│   ├── SituationText              → situationText
│   └── TalksRemainingText         → talksRemainingText        (moved out of Individual, §3.4)
├── SquadScroll                    ScrollRect (vertical only)  LayoutElement flexibleHeight 1
│   └── Viewport                   Image + Mask
│       └── Players                GridLayoutGroup + ContentSizeFitter (Vertical = Preferred)  → boxContainer
├── FlavourText                    → flavourText               LayoutElement minHeight 70
├── OverallText                    → overallText               LayoutElement minHeight 70
├── Responses                      <Responses.prefab>          LayoutElement minHeight 100   → responseManager
├── Footer                         HorizontalLayoutGroup       LayoutElement minHeight 110
│   └── CloseButton                <Base UI/Button>  OnClick → TeamTalkUI.Close              (§3.1)
└── Individual                     Image (dim backdrop, raycast on)  — centred dialog, starts INACTIVE
    ├── IndividualTargetText       → individualTargetText
    ├── Responses                  <Responses.prefab>          → individualResponseManager   (§3.2)
    └── CancelButton               <Base UI/Button>  OnClick → TeamTalkUI.CancelIndividual   (§3.5)
```

Root anchors → stretch `0,0`–`1,1`, zero offsets. Sibling order = render order, so `Individual` stays **last**.

**[CODE] cleanup once the above is wired:** delete `arcWidth`/`arcDepth`/`topMargin` and the commented-out arch
block from `TeamTalkUI`; delete the unused `PlayerMoraleBoxUI.MoraleColour` (superseded by
`player.GetMoraleColor()`).

### 3.7 🟡 The speech bubble has no bubble

`PlayerMoraleBoxUI.bubbleBackground` is **unassigned** on the morale box prefab; only `bubbleText` is wired. The
chatter — which is the entire read-the-room mechanic — renders as bare text over the box, and
`ShowBubble(line, tint)`'s reaction tint has nothing to tint.

Worse, a 200×150 grid cell can't hold a box *and* a legible line of dialogue for 25 players. Two options:

- **Cheap:** assign `BubbleBG`, drop the bubble to 1–2 lines with TMP auto-size and `Ellipsis` overflow, and show the
  full line on hover/click.
- **Better (recommended):** raise the cell to ~240×190 and let the grid scroll (per §3.6). Bubbles are the mechanic —
  give them the room.

---

## 4. Match Sim shell

`Match Simulation Page > Elements` (HLG, ctrl+expand both) holds three columns: `Buttons`, `Team Talk UI`,
`MainPanel`.

### 4.1 🟠 The `Buttons` column has no layout group — **[SCENE]**

Four items hand-anchored inside it:

| Child | Placement |
|---|---|
| `HalfTimePanel` | stretch, `sizeDelta.y = -275`, `pos.y = 137.5` — an invisible full-height container |
| `Mentality` (Slider) | anchored to column centre, `pos.y = -245` |
| `Advance` | bottom stretch, `sizeDelta.x = -300`, `pos.x = 150` |
| `Pause` | bottom-left, 280 wide |

At 1920×1080 these don't collide (Pause 0–280, Advance 300–right; half-time buttons 207–407 below the top;
mentality 245 below centre). But it's four independent fixed-pixel guesses sharing one rect, and the two half-time
buttons live at `y = -407` *inside a container whose height is `parentHeight − 275`* — change the page height and
they walk.

**Fix:** `VerticalLayoutGroup` on `Buttons` (ctrl width, spacing ~20, padding 20), children in order
`HalfTimePanel` → `Mentality` → `Pause` → `Advance`, each with a `LayoutElement` minHeight. `HalfTimePanel` gets its
own `HorizontalLayoutGroup` for the Tactics/Team Talk pair. Toggling `HalfTimePanel` then *reflows* the column
instead of leaving a hole.

### 4.2 🟡 Half-time state isn't legible — **[SCENE]**

At half-time the only signal is that two extra buttons appear in a column. The timer says `45'`, identical in
weight to every other minute. Add a `HalfTimeBanner` TMP inside `HalfTimePanel` ("HALF TIME — one team talk, then
kick off") so the state announces itself. Pure scene work; nothing in code needs to know.

### 4.3 🟡 `Mentality` slider has no tick labels — **[SCENE]**

`SetMentalitySliderInMatch(Slider)` rounds to 7 discrete levels but the slider renders as a continuous bar with a
single `Label` child that `_mentalityText` writes into. The 7 stops are invisible until you drag. Add a static
7-stop tick strip (a `HorizontalLayoutGroup` of small marks) behind the fill. `MentalitySelectorUI` already exists
in the project — check whether it can be reused here before building anything new.

### 4.4 🟡 `Team Talk UI` sits under `Pages`-adjacent content, not as an overlay — **[NOTE]**

It's a layout child of `Elements`, so `SetMatchContentVisible(false)` works by *disabling siblings* — the HLG then
gives the talk the whole row. It functions, and the existing build doc's "modal over one page" rule endorses it.
But it means the talk can never be shown *over* a dimmed match screen. If you ever want that (recommended once §3.6
gives it an opaque background), take it out of the HLG flow: keep it parented to `Elements` but add a `LayoutElement`
with **Ignore Layout** ticked and stretch it `0,0`–`1,1`. Then `SetMatchContentVisible` can stop disabling siblings
altogether.

### 4.5 🔴 BLOCKER — half-time "Edit Tactics" is a one-way trip

`TacticsPageUI.resumeMatchButton` is assigned (→ `Canvas > Constants > ResumeGame_Btn`, which starts inactive and is
switched on by `EnterReturnToMatchMode`). But that Button's **OnClick list is empty** — `m_PersistentCalls.m_Calls: []`
— and `TacticsPageUI.ResumeMatch` is hooked nowhere in the project.

So at half-time: **Edit Tactics** → the tactics page opens, the Resume button appears, clicking it does nothing.
`UIManager.Back()` also refuses to run mid-match by design, and the nav bar is hidden. The match is stranded.

`NEW_SYSTEMS_BUILD.md:188` and `WIRING_RECRUITMENT_HALFTIME_SIM.md` §3b both specify this hook; it was never made.

**Fix — [SCENE]:** `ResumeGame_Btn` → OnClick **+** → drag the **Tactics Page** object → `TacticsPageUI.ResumeMatch`.

---

## 5. Recruitment page

```
Recruitment Page > Elements  [no layout group anywhere]
├── Text (TMP)        → headerText              500×100 @ (0,  100)
├── Text (TMP) (1)    → candidateInfoText       500×100 @ (0,    0)
├── Text (TMP) (2)    → questionsRemainingText  500×100 @ (0, -100)
├── Button            → interviewButton  300×100  top-LEFT   @ ( 150,  -49.8)
├── Button (1)        → skipButton       300×100  top-LEFT   @ ( 473,  -49.8)
├── Button (2)        → rejectButton     300×100  top-RIGHT  @ (-497, -323.9)
├── Button (3)        → hireButton       300×100  top-RIGHT  @ (-179, -323.9)
├── InterviewButtons  GridLayoutGroup cell 220×90, bottom strip, fixed height 200
│   └── 16 × InterviewButton.prefab   (hand-placed, each with a questionType/stat override)
└── Dialogue          <Dialogue.prefab>
```

### 5.1 🔴 Three of the interview questions are missing — **[SCENE]**

The 16 hand-placed buttons carry these `questionType` overrides: `1, 2, 3, 4, 5, 7, 9, 11` (+ 8 buttons left on the
default `0 = AskAboutStat` with a `stat` override, and one with **no override at all**).

Against `InterviewQuestionType`, that means **three question types have no button**:

| Missing | Value | Why it matters |
|---|---|---|
| `CareerGoals` | 6 | a general question, simply absent |
| `HandleCriticism` | 8 | **personality clue** |
| `BigGameMentality` | 10 | **personality clue** |

`GetPersonalityQuestions()` lists four probes (`HandleCriticism`, `WorkEthic`, `BigGameMentality`, `Leadership`) and
only **two** are on the page. Narrowing the hidden personality is the whole point of the interview, and you have five
questions to spend — with half the probes missing, `NarrowedPersonalitiesText()` can rarely narrow far.

The one button with no override is also a duplicate: it defaults to `AskAboutStat` + `stat = 0` (Shooting), which
another button already covers.

**Fix:** add three buttons (`CareerGoals`, `HandleCriticism`, `BigGameMentality`) and repoint the no-override
duplicate at an unused stat. **[CODE] alternative (recommended, and cheaper to maintain):** spawn the question
buttons from `InterviewManager.GetStatQuestions()` / `GetGeneralQuestions()` / `GetPersonalityQuestions()` at
`OnShow`, the way `TrainingPageUI` spawns drills and `ResponseManager` spawns tones. Then the page can never drift
from the enum again, and it groups naturally into three tabs (Stats / General / Personality).

### 5.2 🟠 Buttons in two unrelated corners — **[SCENE]**

`interviewButton` and `skipButton` are top-left; `rejectButton` and `hireButton` are ~274px lower on the right. They
are a single decision set and `SetButtonsActive` / `ShowCurrentCandidate` toggle them together, so hiding two leaves
a gap in one corner and an orphan pair in the other.

**Fix:** one `ActionBar` (`HorizontalLayoutGroup`, bottom-right, spacing 20) holding `Interview | Hire | Reject |
Skip` in that order. Toggling then reflows.

### 5.3 🟠 No candidate context and no disabled states — **[SCENE] + [CODE]**

- `candidateInfoText` is a fixed `500×100` rect carrying up to five lines (the squad-full message is four). It
  clips. Give it a `ContentSizeFitter` inside a `VerticalLayoutGroup`.
- The page shows **name only** before you interview — no age, no position, no asking price. There's a perfectly good
  `PlayerDetailsUI` and `StatWidget` in the project; a compact candidate card would make Skip a real decision
  instead of a coin flip.
- **[CODE]** The 16 question buttons stay `interactable` when there's no live interview and after all 5 questions are
  spent — clicking just logs a warning. `RecruitmentPageUI.OnQuestionAsked()` already fires after every ask; have it
  set `interactable = InterviewManager.Instance.QuestionsRemaining > 0` across the grid, and disable the grid
  entirely outside an interview.
- **[CODE]** `interviewStarted` is never cleared by `OnHireClicked`, so a failed hire path can leave it stale. Minor,
  but it's one line.

### 5.4 🟡 The "no interviews today" state is a dead end — **[SCENE]**

`StartSession()` writes an explanatory line and calls `SetButtonsActive(false)`, leaving a page with one paragraph
and 16 live-looking question buttons. Hide `InterviewButtons` and `Dialogue` in that branch too, and show the date of
the next interview day (`ScheduleManager` already knows it — same call shape as
`GetNextTrainingDay()`).

---

## 6. Training page

```
Training Page > Elements
├── Header                    HorizontalLayoutGroup ✅
│   ├── HeaderText  ├── NextSessionText
└── Body                      (no layout group — manual 40/60 split, acceptable)
    ├── DrillScroll           ScrollRect  horizontal=1 AND vertical=1, both scrollbars
    │   └── Viewport > Content   GridLayoutGroup cell 150×100, height fixed 300, NO ContentSizeFitter
    └── RightColumn           HorizontalLayoutGroup
        ├── MainPanel         VerticalLayoutGroup ✅
        │   ├── CurrentDrillText  ├── InfoPanel  └── SetTrainingButton
        └── PositionalPanel   (no layout group)
            ├── PositionSelection (TMP_Dropdown)
            ├── SelectedCountText
            └── 4 × TrainingPlayerSlot.prefab
```

### 6.1 🔴 Only 4 of 5 positional slots exist — **[SCENE]**

`TrainingSession.MAX_POSITIONAL_PLAYERS = 5`; `TrainingPageUI.playerSlots` has **4** entries. `SetupSlots()` does
`Mathf.Min(playerSlots.Length, MAX)`, so it degrades silently — you simply can never train the fifth player, and
`selectedCountText` reads "4/5 selected" at full capacity, which looks like a bug to the player.

**Fix:** duplicate a `TrainingPlayerSlot` and add it to the `Player Slots` array.

### 6.2 🟠 The drill list's scroll is broken by construction — **[SCENE]**

`Content` has a `GridLayoutGroup` (cell 150×100) but a **hand-set `sizeDelta.y = 300` and no `ContentSizeFitter`**.
`ScrollRect` sizes its scrollbar from the content rect, so with 18 drills — 5 rows ≈ 500px of content declared as
300 — the scrollbar is lying, and any drill added past the current set renders outside the declared content.

Also: **`Horizontal` is ticked** on the ScrollRect and a horizontal scrollbar is wired, for a vertical drill list.

**Fix:** untick `Horizontal`, delete `Scrollbar Horizontal`, add `ContentSizeFitter` (Vertical = Preferred Size) to
`Content`. And widen the cell — 150px cannot show "Strength & Conditioning". A `VerticalLayoutGroup` list at full
column width suits 18 named drills better than a 4-column grid;
if you keep the grid, go to 2 columns at ~340 wide.

### 6.3 🟠 `InfoPanel` is three hand-anchored blocks — **[SCENE]**

`TitleRow` (top, 85), `AffectedStats` (centre, 200), `EffectText` (bottom, **100**). `EffectText` receives
`drill.Description + "\n\n" + GetEffectivenessDescription()` — comfortably 4–6 lines — in a fixed 100px rect.
It clips.

**Fix:** `VerticalLayoutGroup` on `InfoPanel` (ctrl width, spacing 12, padding 20) with `LayoutElement` minHeights on
the three children, and `EffectText` on flexibleHeight 1.

### 6.4 🟡 The positional panel doesn't show what you're comparing — **[SCENE]**

`SlotDescriber` already returns a colour-coded `Natural · 78 @ ST` line, but only *inside the picker popup*. Once a
slot is filled, `PlayerSlotUI.Refresh()` shows the name alone — so after picking you can't see whether your five
picks were a good fit without reopening the picker.

**Fix (CODE done, SCENE outstanding):** `PlayerSlotUI` now has a `subLabel` field and keeps the `describe` func
around after the pick, re-running it whenever the target position changes. **`TrainingPlayerSlot.prefab` still has
no second TMP**, so `subLabel` is unassigned — and the field is **required** (house style: inspector refs are not
null-checked), so the positional panel throws a `NullReferenceException` in `Refresh()` until you add it.
Add a second TMP under the slot (below the name) and assign it to `Sub Label`.

### 6.5 🟡 `PositionalPanel` has no layout group — **[SCENE]**

Dropdown at the top, count text at the top, four slots wherever they were dropped. `VerticalLayoutGroup`, spacing
12: `PositionDropdown` → `SelectedCountText` → the five slots.

---

## 7. Doc drift

| Doc | Status |
|---|---|
| `TRAINING_UI_LAYOUT.md` | **Deleted** — it described `playerListContainer` + `playerRowPrefab` (Toggle rows), fields that no longer exist. §6 of this doc is now the training page's reference. |
| `MATCH_SIM_AND_TEAM_TALK_BUILD.md` | **Rewritten.** Its old "left to do" list mixed done and outstanding items; it now opens with just the three that remain (Close button, ResumeMatch hook, opaque/stretched panel) and keeps the rest as reference. |
| `HALFTIME_TEAM_TALK_REWORK_BUILD.md` §3 | Correct as written, but the scene diverges: one shared `ResponseManager` instead of two (§3.2), and `BubbleBackground` unassigned (§3.7). |
| `WIRING_RECRUITMENT_HALFTIME_SIM.md` §1 | Still accurate for the fields; silent on which question buttons to place, which is how §5.1 happened. |

Fold these corrections in as the fixes land rather than leaving a fourth doc to reconcile.

---

## 8. The plan — status & step-by-step

### ✅ Done — [CODE] (already in the working tree)

| # | Change | File |
|---|---|---|
| 16 | Dropped the three `using UnityEngine.UIElements;` lines | `FormationInteractableUI`, `PlayerDetailsUI`, `TacticsPageUI` |
| 17 | Deleted dead `arcWidth`/`arcDepth`/`topMargin` + the commented-out arch block (`BuildSquadArch` → `BuildSquadGrid`), and the unused `squadTone` field | `TeamTalkUI` |
| 17 | Deleted the unused `MoraleColour` helper (superseded by `player.GetMoraleColor()`) | `PlayerMoraleBoxUI` |
| 14 | Question buttons can now be **spawned** from `InterviewManager.GetGeneralQuestions()` / `GetPersonalityQuestions()` / `GetStatQuestions()` + the comparison question, so the page can no longer drift from the enum. Adds `SetQuestion(InterviewQuestion)` | `RecruitmentPageUI`, `InterviewQuestionButton` |
| 15 | Question buttons grey out when there's no live interview or the five questions are spent (`RefreshInteractable`), applied to spawned **and** hand-placed buttons via `Elements.GetComponentsInChildren` | `RecruitmentPageUI`, `InterviewQuestionButton` |
| — | Skipping a candidate now ends the interview, and a successful hire clears `interviewStarted` | `RecruitmentPageUI` |
| — | "No interviews today" now names the next interview day. `GetNextTrainingDay()` generalised to `GetNextDay(ScheduleEntryType)` and kept as a wrapper (reuse, not a parallel method) | `ScheduleManager`, `RecruitmentPageUI` |
| 19 | `subLabel` on a slot keeps the picker's descriptor ("Natural · 78 @ ST") visible after the pick; re-runs when the target position changes | `PlayerSlotUI`, `TrainingPageUI` |

**#14 is opt-in and degrades gracefully:** leave `Question Container` / `Question Button Prefab` empty and the 16
hand-placed buttons keep working exactly as before — but they stay incomplete (§5.1), so wiring the spawner is the
recommended path.

---

### P0 — [SCENE] — three ways to strand the player mid-match (do these first)

**1. Team-talk Close button** (§3.1) — *without this, opening the team talk soft-locks the match.*
   - `Match Simulation Page > Elements > Team Talk UI` → add a `Base UI/Button.prefab` child named `CloseButton`,
     label it "Back to the match", anchor bottom-right.
   - OnClick **+** → drag the **Team Talk UI** object → `TeamTalkUI.Close`.

**2. Resume-match hook** (§4.5) — *without this, half-time "Edit Tactics" is a one-way trip.*
   - Select `Canvas > Constants > ResumeGame_Btn` (it already exists and is already assigned to
     `TacticsPageUI.resumeMatchButton`; only its OnClick is empty).
   - OnClick **+** → drag the **Tactics Page** object → `TacticsPageUI.ResumeMatch`.

**3. Competition-page week arrows** (§2.5) — *both currently point at a class that no longer exists.*
   - `Competition Page` → the prev/next week buttons. Their OnClick shows a missing-method entry targeting
     `FixtureListUI` (renamed to `CompetitionPageUI`).
   - Remove the broken entry, then **+** → drag the **Competition Page** object → `CompetitionPageUI.ChangeWeekFocus`,
     and set the int argument (`-1` on prev, `+1` on next).

### P0b — [SCENE] — mechanics that silently under-deliver

**4. Second `ResponseManager` for private words** (§3.2)
   - Inside `Team Talk UI > Individual`, drag in a **second** instance of `Assets/Prefabs/Interview/Responses.prefab`.
   - On `TeamTalkUI`, repoint **Individual Response Manager** at the `ResponseManager` on *that* new instance.
     (It currently points at the same object as **Response Manager** — the squad row.)
   - Leave its **On Response Chosen** empty; `TeamTalkUI` subscribes in code.

**5. Fifth training slot** (§6.1)
   - Duplicate a `TrainingPlayerSlot` under `Training Page > … > PositionalPanel` and add it to
     `TrainingPageUI ▸ Player Slots` (the array has 4; `MAX_POSITIONAL_PLAYERS` is 5).

**6. The three missing interview questions** (§5.1)
   - **Preferred:** on `RecruitmentPageUI`, assign **Question Container** → `InterviewButtons` and
     **Question Button Prefab** → `InterviewButton.prefab`, then **delete the 16 hand-placed buttons**. The row now
     builds itself from the enum. Optionally set **Stat Questions** to the subset you want (empty = all 20).
   - **Or, minimally:** add three `InterviewButton` prefabs set to `CareerGoals`, `HandleCriticism` and
     `BigGameMentality`, and repoint the one button with no override (currently a duplicate `AskAboutStat`/Shooting).

### P1 — [SCENE] — layout correctness

| # | Fix | Where |
|---|---|---|
| 7 | Rebuild `Team Talk UI` per the target hierarchy: VLG, opaque background, stretch anchors, squad grid in a ScrollRect with a `ContentSizeFitter`, `TalksRemainingText` moved to the header | §3.3–§3.6 |
| 8 | `Individual` → a real centred dialog: dim `Image` backdrop (raycast on) + `CancelButton` → `TeamTalkUI.CancelIndividual` | §3.5 |
| 9 | Assign `BubbleBackground` on the morale box; size the cell so a line of chatter fits | §3.7 |
| 10 | Training `DrillScroll`: untick Horizontal, delete the horizontal scrollbar, add `ContentSizeFitter` (Vertical = Preferred) to `Content`, widen the cells | §6.2 |
| 11 | Training `InfoPanel` + `PositionalPanel` → VerticalLayoutGroups | §6.3, §6.5 |
| 12 | Recruitment: one bottom-right `ActionBar` (Interview / Hire / Reject / Skip); `ContentSizeFitter` on `candidateInfoText` | §5.2, §5.3 |
| 13 | Match Sim `Buttons` column → VerticalLayoutGroup | §4.1 |
| 14 | Add a `SubLabel` TMP to `TrainingPlayerSlot.prefab` and assign it to `PlayerSlotUI ▸ Sub Label` (code is in) | §6.4 |

### P2 — [SCENE] — structure, clarity, hygiene

| # | Fix | Where |
|---|---|---|
| 15 | Rename `Matchca.prefab` → `PlayerMoraleBox`; rename every `Texty` / `Text (TMP)` / `Button (n)` to its serialized-field name | §2.2 |
| 16 | Put a `Back` button with `BackButton` in `Constants`; add `Exit` (and the new Back) to `UIManager.hideForMatchSim` | §2.4 |
| 17 | `HalfTimeBanner` text inside `HalfTimePanel`; mentality tick labels (check `MentalitySelectorUI` for reuse first) | §4.2, §4.3 |
| 18 | Team talk as a true stretch overlay (`LayoutElement ▸ Ignore Layout`) | §4.4 |

### Deliberately not on the list

- Rebuilding the Home page — it's the reference implementation.
- Touching the Canvas/CanvasScaler — correct as configured.
- Replacing `ResponseManager`, `PlayerPickerPopup` or `UIPage` — the shared-widget layer is doing its job; every fix
  above uses more of it, not less.
- Wiring `TeamTalkMinigame` / `GerrymanderGame` / `BalanceGame` — still dormant.
