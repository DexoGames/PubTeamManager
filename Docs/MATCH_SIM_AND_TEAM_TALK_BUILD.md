# Match Sim & Team Talk — UI build guide

**Authoritative build doc for the match screen and the half-time team-talk overlay.** Written from scratch,
assuming nothing is built. Where an older doc disagrees with this one, **this one wins**:

| Older doc | Status |
|---|---|
| `TEAM_TALK_UI.md` | Superseded by Part B. Still correct on the *reaction model*; its wiring is thinner and predates the visibility fix. |
| `INTERVIEW_INMATCH_TEAMTALK_BUILD.md` §2 | **Obsolete** — describes the abandoned minigame talk (`CreateRandom`/`ApplyResult`). Ignore for wiring; §1/§3 (interviews) still stand. |
| `TEAM_TALK_REWORK_PLAN.md` | **Now implemented** (`TeamTalkChatter` speech bubbles + capped individual talks). This Part B is still the base squad-talk wiring; the rework's extra wiring is in `HALFTIME_TEAM_TALK_REWORK_BUILD.md`. |
| `NEW_SYSTEMS_BUILD.md` §3–4 | Summary only; defers here. |
| `WIRING_RECRUITMENT_HALFTIME_SIM.md` §2–3 | Superseded by Parts A/C. §1 (recruitment) still stands. |

For **what's already in `Menu.unity` right now** and what's genuinely left to do, jump to the [Appendix](#appendix--current-scene-state).

---

## 0. Conventions you need before touching anything

- **`UIPage` shape.** Every page is a `XxxPageUI : UIPage` sitting on a GameObject whose **child index 0 is the
  content root** — `UIPage.Elements => transform.GetChild(0)`. `Show()` activates child-0 and calls `Setup()` on
  every `UIObject` beneath it; `Hide()` deactivates child-0. **The page GameObject itself always stays active**
  (its `Awake` must run to register `Instance`), so never disable the page root in the scene — disable `Elements`.
- **Page must exist at startup.** `UIManager.Setup()` caches each page's `Instance`. A page added at runtime is invisible to the manager.
- **Child-0 name.** Call it `Elements` for consistency with the other pages.
- **"Hook OnClick → `X.Method`"** = in the Button's OnClick list, press `+`, drag in the GameObject carrying that
  component, and pick the method. No-arg unless the entry says otherwise.
- **Auto-labelled buttons.** Where noted, code writes the caption via `GetComponentInChildren<TextMeshProUGUI>()`,
  so the button **must** have a TMP child. Don't hand-type those captions — they get overwritten.
- **Sibling order = render order** in uGUI. Later sibling draws on top. This matters for the two overlays.
- **Inspector field order below matches declaration order**, which is what you'll actually see in the Inspector.
  Unity's display names drop the leading `_` and title-case the rest (`_halfTimePanel` → **Half Time Panel**).

### Managers that must already be in the scene
One (or several) always-active GameObject(s) carrying: `GameManager`, `UIManager`, `TeamManager`, `FixturesManager`,
`ScheduleManager`, `CalenderManager`, `PersonManager`, `IdManager`, `SaveManager`, `EventsManager`,
`RecruitmentManager`, `InterviewManager`, **`TeamTalkController`**, `TrainingManager`, `LoadingOverlay`.

`TeamTalkController` is a plain singleton MonoBehaviour with three tuning fields (`maxMoodSwing`, `maxPassionSwing`,
`flopThreshold`) — those only feed the **dormant** minigame path, so leave them at defaults. Its
`ResetForMatch()` is called automatically at kickoff.

---

## 1. Prefabs to build

Four prefabs. Build these first; both parts reference them.

### 1a. `MatchEvent` — one commentary row
`Assets/Prefabs/Rows/MatchEvent.prefab`

```
MatchEvent                    Image  ← the row background (tinted to the team colour)
                              CanvasGroup
                              OnStartUI          (fade+scale in; delay ~0)
                              MatchEventUI
├── Minute   (TMP)
└── Text     (TMP)
```

`MatchEventUI` fields — **note the misspelled third field, that's in the code, not a typo here**:

| Inspector | Assign |
|---|---|
| **Text** (`_text`) | the `Text` TMP child — the commentary line |
| **Minute** (`_minute`) | the `Minute` TMP child |
| **Backgorund** (`backgorund`) | the row's own `Image` |

`SetText(team, str, minute)` sets the background to `team.TeamColor` and picks black/white text by luminance, so
**don't** hand-colour the TMPs. `OnStartUI` + `CanvasGroup` are required together (`[RequireComponent]`) — during
fast-forward `MatchEventUI.InstantShow()` disables the animation so rows appear instantly.

Layout: full width, fixed height (~40–56 px), Horizontal Layout Group with Minute on the left.

### 1b. `PlayerMoraleBox` — one squad-member tile
`Assets/Prefabs/PlayerMoraleBox.prefab`

Keep it **~56–70 px wide**: a 21–25-man squad has to fit across the arch (spacing ≈ `arcWidth / (squad − 1)`,
so ≈ 67 px at the default 1600).

```
PlayerMoraleBox               Image  ← background (gets tinted by the reaction)
                              PlayerMoraleBoxUI
├── Name       (TMP)          surname, small, centred
├── MoodBar    (Image)        Image Type = Filled, Fill Method = Horizontal
├── PassionBar (Image)        Image Type = Filled
├── Morale     (TMP)          numeric 0–100
└── Reaction   (TMP)          the "+8" / "−5" flash
```

| Inspector | Assign | Required? |
|---|---|---|
| **Name Text** | `Name` | optional |
| **Mood Fill** | `MoodBar` — **must** be `Image Type = Filled` | optional |
| **Passion Fill** | `PassionBar` — also Filled | optional |
| **Morale Text** | `Morale` | optional |
| **Reaction Text** | `Reaction` | optional |
| **Background** | the root `Image` | optional |

Every field is genuinely optional — `PlayerMoraleBoxUI` null-checks all six, so a name + Mood Fill is a valid
minimal tile. **Mood Fill is auto-coloured** green/amber/red by score; **Background is overwritten** with the
reaction colour after a talk, so whatever you set is just the pre-talk resting look.

### 1c. `ResponseButton` — one tone choice
`Assets/Prefabs/ResponseButton.prefab` — a variant of `Base UI/Button`, **shared** by the discussion page and the
team talk.

```
ResponseButton                Button          OnClick → ResponseButtonUI.OnClick
                              ResponseButtonUI
└── Label (TMP)
```

| Inspector (`ResponseButtonUI`) | Assign |
|---|---|
| **Text** | optional — falls back to the first TMP in the children |
| **Button** | optional — falls back to the `Button` on this object |
| **Picked Tint** | colour for the response you actually chose once the row locks (default grey) |

The label is written from the response name at spawn, so don't hand-type it. **The Button's OnClick must hold
exactly one entry → `ResponseButtonUI.OnClick`** — never add a second listener in code (that was the old
team-talk bug). Size for 8 across a row (~120–160 px wide).

### 1d. Half-time / touchline buttons
Plain `Base UI/Button` variants. The **Advance** and **Pause** buttons need a TMP child (code writes their captions).

---

# Part A — Match Simulation page

## A1. Hierarchy

```
Pages
└── Match Simulation Page                      ← MatchSimPageUI     [ALWAYS ACTIVE]
    └── Elements                               ← child 0            [inactive in scene]
        ├── Scoreboard                         ← FixtureUI
        │   ├── HomeTeam   (TMP)
        │   ├── HomeScore  (TMP)
        │   ├── AwayTeam   (TMP)
        │   ├── AwayScore  (TMP)
        │   └── Date       (TMP)
        ├── Timer          (TMP)                 "12'" / "Full Time"
        ├── EventFeed      (Scroll View)
        │   └── Viewport
        │       └── Content                    ← _eventsContainer
        ├── TouchlineControls
        │   ├── MentalityLabel      (TMP)      ← _mentalityText
        │   ├── MoreDefensiveButton
        │   ├── MoreAttackingButton
        │   └── MentalitySlider     (optional)
        ├── MatchControls
        │   ├── AdvanceButton   (+ TMP child)
        │   └── PauseButton     (+ TMP child)
        ├── HalfTimePanel                      ← _halfTimePanel     [inactive in scene]
        │   ├── EditTacticsButton
        │   └── TeamTalkButton
        └── TeamTalkPanel                      ← _teamTalkPanel     [inactive] — Part B
```

**Ordering rules that matter:**
- `Elements` must be **child 0** of the page root. Nothing else may take that slot.
- `HalfTimePanel` and `TeamTalkPanel` are the **last two siblings**, in that order — they're overlays and must
  draw above the feed and controls.
- `Content` under the scroll view needs **Vertical Layout Group** + **Content Size Fitter** (Vertical Fit =
  Preferred Size). Rows are `Instantiate`d into it and it's wiped by `Game.ClearContainer` at kickoff.

## A2. `MatchSimPageUI` fields (in Inspector order)

| # | Inspector | Assign | Notes |
|---|---|---|---|
| 1 | **Fixture UI** | `Scoreboard` | the `FixtureUI` component |
| 2 | **Timer Text** | `Timer` | shows `12'`, or `Full Time` once played |
| 3 | **Advance Button** | `AdvanceButton` | needs a TMP child |
| 4 | **Pause Button** | `PauseButton` | needs a TMP child |
| — | *— header: In-match tactics —* | | |
| 5 | **Mentality Text** | `MentalityLabel` | code drives its **colour alpha** too (see A5) |
| 6 | **Half Time Panel** | `HalfTimePanel` | GameObject, not a component |
| 7 | **Team Talk Panel** | `TeamTalkPanel` | **REQUIRED** — the `TeamTalkUI` component. See Part C |
| 8 | **Events Container** | `Content` | the Transform rows spawn into |
| 9 | **Match Event Prefab** | `Rows/MatchEvent.prefab` | §1a |

`FixtureUI` itself takes five public TMPs: **Home Team, Home Score, Away Team, Away Score, Date**.

> ⚠️ **Team Talk Panel is a newly added field.** `MatchSimPageUI.OnShow` calls `_teamTalkPanel.Close()`, so if you
> leave it empty you'll get a `NullReferenceException` the moment a match starts.

## A3. OnClick hooks

| Button | Hook |
|---|---|
| AdvanceButton | `MatchSimPageUI.Advance` |
| PauseButton | `MatchSimPageUI.Pause` |
| MoreAttackingButton | `MatchSimPageUI.MoreAttacking` |
| MoreDefensiveButton | `MatchSimPageUI.MoreDefensive` |
| EditTacticsButton | `MatchSimPageUI.OpenHalfTimeTactics` |
| TeamTalkButton | `TeamTalkUI.Open` |

**Optional mentality slider:** set **Whole Numbers = true, Min 0, Max 6**. In OnValueChanged drag the
`Match Simulation Page` object and pick **`SetMentalityInMatch (float)`** from the **Dynamic float** section
(top group), *not* the static one — the static entry would send a fixed baked value.

**Launch:** nothing on this page. The home "Next Day / Play" button → `GameManager.AdvanceOrPlay`, which detects a
pending fixture and calls `UIManager.ShowMatchSimPage(fixture)`.

## A4. What code drives — do not wire these

- **Advance button caption**, per state: `Begin Game` → `Sim To Half Time` → `Begin Second Half` →
  `Sim To Full Time` → `Return Home`. Pause toggles `Pause`/`Resume`.
- **Half-time panel visibility.** `UpdateHalfTimePanel()` sets it active **only** in the `HalfTime` state. Never
  toggle it by hand or from a button. (It's revealed by `EndHalf()` when the half genuinely ends — including
  during fast-forward, which is why it no longer pops up early.)
- **Row spawning, colours, and the fade-in.**
- **Nav bar.** `OnShow` calls `ShowNavigationButtons(false)`; full time re-enables it. `UIManager.Back()` is a
  no-op while `Tactic.InMatch`, so the Back button can't escape a live match.

## A5. The mentality label's two states

A touchline shout is **queued, not instant** — it goes live only once the minute in progress has finished
simulating (`Tactic.PendingMentalityShift` → `ApplyPendingMentality()`). The label reflects this:

- **Full opacity** — displayed mentality is live.
- **50% alpha** (`PENDING_MENTALITY_ALPHA`) — you've called for a change that hasn't taken effect yet.

Code writes `_mentalityText.color`'s alpha directly, so **pick an opaque colour** on the TMP and don't put a
`CanvasGroup` with its own alpha on it — you'll mask the cue. The matching "Touchline shout" feed row is printed
at the moment it actually bites, tagged with the minute that just finished.

At half-time there's no minute ticking, so changes apply immediately and print **no** feed row.

---

# Part B — Team Talk overlay

Pick one tone; the whole squad reacts through the **same** reaction path as a 1-on-1 discussion, with severity
taken from the half-time scoreline. One talk per match.

## B1. Hierarchy

```
TeamTalkPanel                              ← TeamTalkUI      [INACTIVE in scene]
├── Backdrop                (Image)          full-rect, opaque — this is now the page background
├── TitleText               (TMP)          ← titleText
├── SituationText           (TMP)          ← situationText
├── BoxContainer            (RectTransform)← boxContainer     ⚠ NO layout group
├── FlavourText             (TMP)          ← flavourText
├── OverallText             (TMP)          ← overallText
├── ResponseRow                           ← ResponseManager  [Horizontal Layout Group]
└── CloseButton                            → TeamTalkUI.Close
```

**Starts inactive** — `Open()` activates it.

**Set stretch anchors on the panel root**: Anchor Min `(0,0)`, Anchor Max `(1,1)`, all offsets `0`. It owns the
entire match screen while open (see B4), so a fixed size would leave dead margins on other resolutions.

### The two containers behave oppositely — this is the #1 thing to get right

- **`BoxContainer`: must NOT have a Layout Group.** `BuildSquadArch()` sets each box's `anchorMin`/`anchorMax`/
  `pivot`/`anchoredPosition` by hand. A Horizontal or Grid Layout Group will fight it and flatten the arch.
  Anchor the container itself to **top-centre**, sized to the usable width.
- **`ResponseRow`: SHOULD have a Horizontal Layout Group** (+ Content Size Fitter if you want it to hug). Those
  buttons are plain instantiated children with no manual positioning.

`Game.ClearContainer(boxContainer)` runs on every `Open()`, so anything you park inside BoxContainer in the editor
gets destroyed at runtime. Leave it empty.

## B2. `TeamTalkUI` fields (in Inspector order)

| # | Inspector | Assign / value |
|---|---|---|
| — | *— header: Squad arch —* | |
| 1 | **Box Prefab** | `PlayerMoraleBox.prefab` (§1b) |
| 2 | **Box Container** | `BoxContainer` |
| 3 | **Arc Width** | `1600` — total horizontal spread in px (≈ usable width) |
| 4 | **Arc Depth** | `130` — how far the ends dip. Small = shallow `∩` |
| 5 | **Top Margin** | `40` — gap from container top down to the centre (highest) box |
| — | *— header: Response buttons —* | |
| 6 | **Response Manager** | the `ResponseManager` on `ResponseRow` (§B2a) |
| — | *— header: Feedback —* | |
| 7 | **Title Text** | `TitleText` |
| 8 | **Situation Text** | `SituationText` |
| 9 | **Flavour Text** | `FlavourText` |
| 10 | **Overall Text** | `OverallText` |

**None of these are optional** — `TeamTalkUI` doesn't null-check, so a blank field is an NRE on `Open()`.

## B2a. `ResponseManager` — the shared tone row

The **same component the discussion page uses**. `TeamTalkUI` no longer builds its own buttons; it just points at
this and listens. Put it on `ResponseRow`:

| Inspector | Assign / value |
|---|---|
| **Response Button Prefab** | `ResponseButton.prefab` (§1c) |
| **Container** | `ResponseRow`'s own transform (or a child, if you want padding) |
| **Dialogue** | **leave empty** here — the team talk prints its own flavour line via `FlavourText` |
| **Responses** | 8 entries, soft → harsh: `Praise, Encourage, Inspire, Galvanise, Persuade, Challenge, Rage, Deflect` |
| **Lock After Pick** | ✅ ticked — one response per talk |
| **On Response Chosen** | **leave empty** — `TeamTalkUI` subscribes in code |

**Responses** is a filter *and* an order. Leave it **empty** to offer every `Event.Response` in enum order (which
is what the discussion page does). Setting it here is what gives the talk its soft→harsh reading order.

## B3. What each text shows

| Field | Example | When |
|---|---|---|
| Title Text | `Half-Time Team Talk` → `Team Talk — delivered` | on open / after delivering |
| Situation Text | `Half-time — 1 up — Pleasant` | on open |
| Flavour Text | `"Brilliant out there — keep doing exactly that."` | after delivering |
| Overall Text | `The dressing room is buzzing! (14 lifted, 3 unhappy)` | after delivering |

Situation Text is your read on the room **before** committing — it's the whole skill of the feature, so give it
prominence near the title.

## B4. The talk takes over the whole screen

Opening the talk switches **everything else on the match screen off** — scoreboard, feed, controls, half-time panel
— so the arch has the full width for a 20+ man squad. Closing puts back **exactly** what was showing before.

This is automatic: `Open()`/`Close()` call `MatchSimPageUI.SetMatchContentVisible(false/true)`, which walks
`Elements`' children, skips the team-talk panel, and remembers what it switched off. Nothing to wire, and no state
knowledge needed on either side — the half-time panel returns because it was on when you opened, not because
anything special-cases it.

Consequences for how you build the panel:
- **Give it a real opaque background.** There's nothing behind it any more, so a semi-transparent backdrop just
  shows the empty canvas.
- **Anything you want visible during the talk must live inside the panel.** A scoreline readout, for instance,
  has to be its own element here — the match screen's `Scoreboard` is off.
- **Don't nest it inside another element you'd want hidden**; it must be a direct child of `Elements`.

## B5. Behaviour notes

- **One talk per match.** After delivering, `ResponseManager` locks the row and tints the tone you picked;
  `TeamTalkController.Used` resets at the next kickoff. Closing *without* delivering leaves it available.
- **Boxes and buttons are both rebuilt every `Open()`.** Reopening a talk you've already delivered re-locks the
  fresh row (`RefreshState`), so the choice stays visible but unusable.
- **`Open()` reads the live scoreline** via `MatchSimPageUI.Instance.CurrentGoalDifferenceForMyTeam()`, so the
  match page must be in the scene. No extra wiring.
- **Severity mapping** (`TeamTalkReactions.SeverityFromScore`): `+4 → Momentous`, `+2/3 → Uplifting`,
  `+1 → Pleasant`, `0 → Irrelevant`, `−1 → Unfortunate`, `−2/3 → Pressing`, `−4 → Dire`.
- **The trade-off:** a talk that lands lifts mood/passion *and* can snap players out of a slump; one that misses
  actively flattens them. Rage while 3-down fires up your Aggressive/Driven players and crushes the timid ones.
- **Tuning lives in two places only:** per-personality reactions in `EventsManager.CreateReactionTable()` (shared
  with 1-on-1 discussions — edit once, both change) and the thresholds above.

---

# Part C — Visibility model, and why the panel wouldn't auto-close

## C1. How page hiding actually works

`UIManager.HideAllUI()` is a **hardcoded list** of the eleven `UIPage` singletons, each getting `.Hide()`
(which deactivates that page's child-0 `Elements`). There is **no generic scan of the `Pages` GameObject** —
`Pages` is purely an organisational parent in the hierarchy. Dropping something under it grants it nothing.

`TeamTalkUI` is deliberately **not** a `UIPage`. It's a modal overlay on the match screen that owns its own
visibility through `Open()`/`Close()` (`gameObject.SetActive`). So `HideAllUI()` has never known it exists.

## C2. So: yes, it was a real problem — two causes

Parenting it directly under `Pages` as a sibling of the pages meant:

1. **Nothing ever hides it.** It's not in `HideAllUI()`'s list, so navigating to any other page left it floating
   on top, swallowing clicks via its backdrop.
2. **It was saved active** in the scene, so it was up from the moment the game started.

## C3. The fix

**In the scene:**
1. **Reparent** `Team Talk UI` from `Pages` to `Match Simulation Page > Elements`, as the **last child** (after
   `HalfTimePanel`). Now the page's own `Hide()` takes it down with everything else.
2. **Set it inactive** (uncheck the top-left checkbox).
3. Assign it to **Team Talk Panel** on `MatchSimPageUI` (field 7 in A2).

**In code (already done):** deactivating a parent doesn't clear a child's own `activeSelf`, so an open panel would
pop straight back on `ResumeDisplay()`. `MatchSimPageUI` now explicitly closes it at the three moments that matter:

| Where | Why |
|---|---|
| `OnShow` | fresh match — never inherit a panel left open from last time |
| `OpenHalfTimeTactics()` | you're leaving for the tactics page |
| `Advance()`, `HalfTime` case | half-time is over; the talk window has closed |

That combination means it's covered whether you leave via navigation, via the second-half whistle, or via the
panel's own Close button.

> **Rule of thumb for future overlays:** if it's a *destination* the player navigates to, make it a `UIPage` and
> add it to `HideAllUI()`. If it's a *modal over one page* (like this), keep it a plain MonoBehaviour, park it
> inside that page's `Elements`, and have the owning page close it on its exit paths. Don't put it under `Pages`
> — that's the one option that gets you neither.

---

# Part C2 — Reusing `ResponseManager` on a new screen

`ResponseManager` + `ResponseButtonUI` + `ResponseButton.prefab` are the **one** response-row implementation.
Any future screen that asks the manager to pick a tone should drive it rather than spawning its own buttons.

To add it anywhere:
1. Make a container object with a Horizontal (or Grid) Layout Group, add **`ResponseManager`**.
2. Assign **Response Button Prefab** → `ResponseButton.prefab`, and **Container** → where buttons should spawn.
3. Optionally set **Responses** (subset + order; empty = all), **Dialogue** (a TMP it fills with the spoken line),
   and **Lock After Pick**.
4. Handle the pick — either hook **On Response Chosen** in the Inspector (dynamic `Event.Response`), or
   `AddListener` in code.
5. Call `SpawnButtons()` when your screen opens.

| Member | Use |
|---|---|
| `SpawnButtons()` | (re)build the row; clears the dialogue line and resets `HasPicked` |
| `OnResponseChosen` | `UnityEvent<Event.Response>` — the pick |
| `SetInteractable(bool)` | grey out / restore without marking a pick |
| `HasPicked` | has a response been chosen since the last `SpawnButtons()` |
| `ResponseToDialogue(r)` | static; the 1-on-1 flavour line for a response |

The two current consumers subscribe **in code** (`DiscussionPageUI.Awake`, `TeamTalkUI.Open`), so neither needs
an Inspector entry in **On Response Chosen** — leave it empty for both or you'll double-handle the pick.

> **Never add a click listener to a spawned button in code.** The prefab's Button already routes to
> `ResponseButtonUI.OnClick` → `ResponseManager.ButtonPressed`. Adding a second one is what broke the team talk.

---

# Part D — Test checklist

1. **Boot** → home page. No team-talk panel visible anywhere. *(Catches C2.)*
2. **Start a match** → nav buttons gone, `Begin Game` showing, feed empty, scoreline `0–0`.
3. **Play the first half** → rows fade in with minute + team colour; timer keeps up.
4. **Click More Attacking mid-minute** → label changes immediately and goes **translucent**; no feed row yet.
   Next minute it snaps to **full opacity** and the "Touchline shout" row appears, tagged with the minute that
   just finished (matching the rows around it — not one ahead).
5. **`Sim To Half Time`** → rows appear instantly, **half-time panel stays hidden until the sim actually reaches
   half-time**, then appears.
6. **Team Talk** → the rest of the match screen switches **off**; arch of boxes across the full width, situation
   line reads the scoreline, 8 tone buttons.
7. **Deliver** → boxes flash colours + deltas, flavour + verdict lines fill, buttons go dead, title reads
   *"Team Talk — delivered"*.
8. **Close** → match screen comes back **exactly** as it was, half-time panel included.
   Then **Edit Tactics → Resume Match** → back at half-time, same score, talk panel **not** reopened.
9. **Change mentality at half-time** → label updates, **no feed row printed**.
10. **Begin Second Half** → team-talk panel closed, half-time panel gone.
11. **Full Time** → `Return Home`; home page shows with **no** leftover match/talk UI. Autosave fires.
12. **Next match** → team talk is available again (one per match resets at kickoff).

---

# Appendix — current scene state

What's actually in `Menu.unity` today, so you don't rebuild working assets:

**Done ✅**
- `Assets/Prefabs/Rows/MatchEvent.prefab` — fully wired (`MatchEventUI` + `OnStartUI` + `CanvasGroup`).
- `Assets/Prefabs/Matchca.prefab` — this **is** the player morale box; all six `PlayerMoraleBoxUI` fields are
  assigned and it's already referenced by `TeamTalkUI.boxPrefab`. **Rename it to `PlayerMoraleBox`** — the current
  name is why it's hard to find.
- `Assets/Prefabs/ResponseButton.prefab` — a `Base UI/Button` variant, ready to use.
- `Match Simulation Page` — hierarchy built; fields 1–6, 8, 9 all assigned.

- `Team Talk UI` — parented under `Match Simulation Page > Elements`, saved inactive, assigned to
  `MatchSimPageUI.Team Talk Panel`; arch + feedback fields all assigned.

**Left to do ⚠️**
1. **Add a `ResponseManager`** to the team-talk panel's response row and assign it to `TeamTalkUI.Response
   Manager` (B2a). The old `Response Button Prefab` / `Response Button Container` / `Responses` fields are gone —
   `Responses` moves onto the `ResponseManager`. Until this is assigned, `Open()` will NRE.
2. Set the panel root to **stretch anchors** (`0,0`–`1,1`, zero offsets) — it's currently a fixed 1946×848 anchored
   top-left, which won't adapt to other resolutions now that it owns the full screen (B1).
3. Give it an **opaque** background — the match screen behind it is switched off during a talk (B4).
4. Confirm the half-time panel's **Team Talk** button is hooked to `TeamTalkUI.Open`, and that the panel has a
   Close button hooked to `TeamTalkUI.Close`.
5. On the **discussion page's** `ResponseManager`, clear the now-removed `Discussion Page` field reference if Unity
   leaves a stale entry — `DiscussionPageUI` subscribes in code now. No other change needed there.

## Dormant code — don't wire it

`TeamTalkMinigame`, `GerrymanderGame`, `BalanceGame`, and `TeamTalkController.CreateRandom`/`ApplyResult` are the
**abandoned** minigame talk. They still compile and `TeamTalkController` still carries their three tuning fields,
but nothing calls them. (`TeamTalkChatter` — the per-personality speech-bubble generator — now **exists** and is
wired via `HALFTIME_TEAM_TALK_REWORK_BUILD.md`; it is *not* dormant.)

`Tactic.CanMakeStructuralChange` (`!InMatch || IsHalfTime`) is defined but **not enforced** — during open play the
tactics page simply isn't reachable. If you ever add a mid-play tactics shortcut, gate the formation/instruction
handlers on it.
