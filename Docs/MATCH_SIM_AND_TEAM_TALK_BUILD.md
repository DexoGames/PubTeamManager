# Match Sim & Team Talk — build guide

The match screen and the half-time team-talk overlay. **Status: built.** Both prefabs exist
(`Rows/MatchEvent.prefab`, `Matchca.prefab` = the morale box, `ResponseButton.prefab`, `Responses.prefab`), the
`Match Simulation Page` hierarchy is complete, every `MatchSimPageUI` field is assigned, and the Advance / Pause /
Edit Tactics / Team Talk hooks are all in place.

What's left is in [§Remaining work](#remaining-work). Everything else here is **reference** — the rules that
govern the screen, kept because they're easy to break and hard to re-derive.

The rework's extra wiring (speech bubbles, individual talks) is
[`HALFTIME_TEAM_TALK_REWORK_BUILD.md`](HALFTIME_TEAM_TALK_REWORK_BUILD.md). Scene-level layout problems on both
screens are [`UI_AUDIT_AND_LAYOUT_PLAN.md`](UI_AUDIT_AND_LAYOUT_PLAN.md) §3–§4.

---

## Remaining work

### 1. 🔴 The team talk has no way out — soft-locks the match

`TeamTalkUI.Open()` calls `SetMatchContentVisible(false)`, which disables **every sibling under `Elements`** —
including the `Buttons` column holding Advance, Pause, Tactics and Team Talk. The only three callers of `Close()`
are those disabled buttons plus "a Close button on the panel", and **no such button exists in the scene**.

**Fix:** add a `Base UI/Button.prefab` child to `Team Talk UI` named `CloseButton` (label "Back to the match"),
OnClick → drag **Team Talk UI** → `TeamTalkUI.Close`.

### 2. 🔴 Half-time "Edit Tactics" is a one-way trip

`Canvas > Constants > ResumeGame_Btn` is already assigned to `TacticsPageUI ▸ Resume Match Button` and is switched
on correctly at half-time — but its **OnClick list is empty**.

**Fix:** OnClick → drag the **Tactics Page** object → `TacticsPageUI.ResumeMatch`.

### 3. 🟠 The panel is transparent and fixed-size

- No `Image` on `Team Talk UI`, yet the whole match screen behind it is switched off — the talk floats over bare
  canvas. Give it an **opaque** background.
- Its rect is a fixed **1946×848 anchored top-left**. Set **stretch anchors** (`0,0`–`1,1`, zero offsets) so it
  adapts to other resolutions now that it owns the full screen. (Its parent HLG overrides those numbers at runtime
  anyway, which is its own reason to fix this — see `UI_AUDIT` §3.6 for the full panel rebuild.)

---

## Conventions (reference)

- **`UIPage` shape.** Every page is a `XxxPageUI : UIPage` on a GameObject whose **child index 0 is the content
  root** — `UIPage.Elements => transform.GetChild(0)`. `Show()` activates child-0 and calls `Setup()` on every
  `UIObject` beneath it; `Hide()` deactivates child-0. **The page GameObject itself always stays active** (its
  `Awake` must run to register `Instance`), so never disable a page root in the scene — disable `Elements`.
- **Pages must exist at startup.** `UIManager.Setup()` caches each page's `Instance` once.
- **Auto-labelled buttons.** Advance/Pause captions are written by code via `GetComponentInChildren<TextMeshProUGUI>()`,
  so those buttons **must** have a TMP child. Don't hand-type those captions — they get overwritten.
- **Sibling order = render order.** Later sibling draws on top. This matters for the overlay.
- Unity's Inspector drops the leading `_` and title-cases the rest (`_halfTimePanel` → **Half Time Panel**).

## What code drives — never wire these

| Behaviour | Owner |
|---|---|
| Advance/Pause button captions per state | `SetAdvanceButton` / `SetPauseButton` |
| Half-time panel visibility | `UpdateHalfTimePanel` (active only in the `HalfTime` state) |
| Mentality label text + its translucent "queued" state | `UpdateMentalityUI` |
| Match feed rows | `PrintEvent` → `MatchEventUI.SetText` (row colour = team colour) |
| Team-talk boxes and tone buttons | rebuilt on every `TeamTalkUI.Open()` |
| Half-time structural-change window | `Tactic.IsHalfTime`, set in `EndHalf` / cleared in `Advance` |

## The mentality label's two states

A shout during open play is **queued**, not immediate — the engine finishes the current minute first
(`Tactic.PendingMentalityShift`). The label shows the *pending* value at **50% alpha** while it waits, snaps to
full alpha when it goes live, and only then prints the "Touchline shout" feed row, tagged with the minute that just
finished. Half-time changes go live immediately and print **no** feed row (there's no match being played to shout
into). If you see a shout row appear instantly at half-time, that contract has been broken.

## The talk takes over the whole screen

`SetMatchContentVisible(false)` walks the **direct children of `Elements`**, deactivates every one that is
currently active (skipping the talk panel), and remembers exactly which it touched. Restoring re-activates only
those. So a panel that was already hidden stays hidden, and the half-time panel comes back correctly — without this
method knowing anything about the state machine.

Consequences to respect:
- Anything that must stay visible during a talk has to be **inside** the talk panel.
- Don't nest the talk panel inside another element you'd want hidden; it must be a **direct child of `Elements`**.

## Why the panel lives where it does

`UIManager.HideAllUI()` is a **hardcoded list** of the `UIPage` singletons — there is no generic scan of the
`Pages` GameObject, which is purely an organisational parent. `TeamTalkUI` is deliberately **not** a `UIPage`; it's
a modal over one page that owns its visibility through `Open()`/`Close()`.

It therefore lives under `Match Simulation Page > Elements` as the **last child**, saved **inactive**, so the
page's own `Hide()` takes it down. And because deactivating a parent doesn't clear a child's `activeSelf`, an open
panel would pop straight back on `ResumeDisplay()` — so `MatchSimPageUI` explicitly closes it at three moments:

| Where | Why |
|---|---|
| `OnShow` | fresh match — never inherit a panel left open from last time |
| `OpenHalfTimeTactics()` | you're leaving for the tactics page |
| `Advance()`, `HalfTime` case | half-time is over; the talk window has closed |

> **Rule of thumb for future overlays:** if it's a *destination* the player navigates to, make it a `UIPage` and add
> it to `HideAllUI()`. If it's a *modal over one page*, keep it a plain MonoBehaviour, park it inside that page's
> `Elements`, and have the owning page close it on its exit paths. Don't put it under `Pages` — that's the one
> option that gets you neither.

## Team-talk behaviour notes

- **One talk per match.** After delivering, `ResponseManager` locks the row and tints the tone you picked;
  `TeamTalkController.Used` resets at the next kickoff. Closing *without* delivering leaves it available.
- **Boxes and buttons are rebuilt every `Open()`.** Reopening a delivered talk re-locks the fresh row
  (`RefreshState`), so the choice stays visible but unusable.
- **Severity mapping** (`TeamTalkReactions.SeverityFromScore`): `+4 → Momentous`, `+2/3 → Uplifting`,
  `+1 → Pleasant`, `0 → Irrelevant`, `−1 → Unfortunate`, `−2/3 → Pressing`, `−4 → Dire`.
- **The trade-off:** a talk that lands lifts mood/passion *and* can snap players out of a slump; one that misses
  actively flattens them. Rage while 3-down fires up your Aggressive/Driven players and crushes the timid ones.
- **Tuning lives in two places only:** per-personality reactions in `EventsManager.CreateReactionTable()` (shared
  with 1-on-1 discussions — edit once, both change) and the severity thresholds above.

## Reusing `ResponseManager` on a new screen

`ResponseManager` + `ResponseButtonUI` + `ResponseButton.prefab` are the **one** response-row implementation. Any
future screen that asks the manager to pick a tone should drive it rather than spawning its own buttons.
`Responses.prefab` is a ready-made instance (row + `ManagerDialogue`) — drop it in.

1. Container object with a Horizontal (or Grid) Layout Group, add **`ResponseManager`**.
2. Assign **Response Button Prefab** → `ResponseButton.prefab`, **Container** → where buttons should spawn.
3. Optionally set **Responses** (subset + order; empty = all), **Dialogue** (a TMP it fills with the spoken line),
   **Lock After Pick**.
4. Handle the pick — hook **On Response Chosen** in the Inspector, *or* `AddListener` in code.
5. Call `SpawnButtons()` when your screen opens.

| Member | Use |
|---|---|
| `SpawnButtons()` | (re)build the row; clears the dialogue line and resets `HasPicked` |
| `OnResponseChosen` | `UnityEvent<Event.Response>` — the pick |
| `SetInteractable(bool)` | grey out / restore without marking a pick |
| `HasPicked` | has a response been chosen since the last `SpawnButtons()` |
| `ResponseToDialogue(r)` | static; the 1-on-1 flavour line for a response |

Both current consumers subscribe **in code** (`DiscussionPageUI.Awake`, `TeamTalkUI.Open`), so neither needs an
Inspector entry in **On Response Chosen** — leave it empty for both or you'll double-handle the pick.

> **Never add a click listener to a spawned button in code.** The prefab's Button already routes to
> `ResponseButtonUI.OnClick` → `ResponseManager.ButtonPressed`. Adding a second one is what broke the team talk.
>
> **Never point two fields at one `ResponseManager`.** That is the current team-talk bug — see
> `HALFTIME_TEAM_TALK_REWORK_BUILD.md` §1.

## Test checklist

1. **Boot** → home page, no team-talk panel visible anywhere.
2. **Start a match** → nav buttons gone, `Begin Game` showing, feed empty, scoreline `0–0`.
3. **Play the first half** → rows fade in with minute + team colour; timer keeps up.
4. **Click More Attacking mid-minute** → label changes immediately and goes **translucent**; no feed row yet. Next
   minute it snaps to full opacity and the "Touchline shout" row appears, tagged with the minute that just
   finished — not one ahead.
5. **`Sim To Half Time`** → rows appear instantly; the half-time panel stays hidden until the sim actually reaches
   half-time, then appears.
6. **Team Talk** → the rest of the match screen switches off; squad grid across the full width, situation line
   reads the scoreline, 8 tone buttons.
7. **Deliver** → boxes flash colours + deltas, flavour + verdict lines fill, buttons go dead, title reads
   *"Team Talk — delivered"*.
8. **Close** → match screen comes back **exactly** as it was, half-time panel included. Then **Edit Tactics →
   Resume Match** → back at half-time, same score, talk panel **not** reopened.
9. **Change mentality at half-time** → label updates, **no feed row printed**.
10. **Begin Second Half** → team-talk panel closed, half-time panel gone.
11. **Full Time** → `Return Home`; home page shows with no leftover match/talk UI. Autosave fires.
12. **Next match** → the team talk is available again.

> Steps 6→8 cannot currently be completed — see [Remaining work](#remaining-work) items 1 and 2.

## Dormant code — don't wire it

`TeamTalkMinigame`, `GerrymanderGame`, `BalanceGame`, and `TeamTalkController.CreateRandom`/`ApplyResult` are the
**abandoned** minigame talk. They still compile and `TeamTalkController` still carries their three tuning fields
(`maxMoodSwing`, `maxPassionSwing`, `flopThreshold`), but nothing calls them.

`Tactic.CanMakeStructuralChange` (`!InMatch || IsHalfTime`) is defined but **not enforced** — during open play the
tactics page simply isn't reachable. If you ever add a mid-play tactics shortcut, gate the formation/instruction
handlers on it.
