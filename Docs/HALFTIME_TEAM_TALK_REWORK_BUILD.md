# Half-Time Team Talk rework — remaining editor work

Editor wiring for the team-talk **rework**: per-player speech bubbles (a *read-the-room* puzzle) and a capped
**individual-talk** phase after the squad tone. Design rationale is
[`TEAM_TALK_REWORK_PLAN.md`](TEAM_TALK_REWORK_PLAN.md); the base squad talk is
[`MATCH_SIM_AND_TEAM_TALK_BUILD.md`](MATCH_SIM_AND_TEAM_TALK_BUILD.md).

**Status:** all C# ships. `PlayerMoraleBoxUI` has its Bubble Text and Select Button assigned,
`TeamTalkController`'s two tuning fields are live, and `TeamTalkUI`'s `individualPanel`,
`individualTargetText` and `talksRemainingText` are all assigned. **Four wiring problems remain**, and until
they're fixed the individual-talk phase is broken and the bubbles have no bubble.

> Every `[SerializeField]` on `PlayerMoraleBoxUI` and `TeamTalkUI` is now **required** — the scripts no longer
> null-check inspector references, so anything left unassigned throws instead of silently doing nothing.

---

## What the player is supposed to see

1. **Open the talk** → a few players have a speech bubble (most silent — little to react to yet). Your first read.
2. **Pick one squad tone** → the squad reacts; boxes flash ±delta **and now most players speak** (post-talk lines
   coloured by how they took it).
3. **Individual phase** → a "Private words left: N" counter appears and boxes become **clickable**. Click a player →
   a small popup ("A word with *Surname*") with tone buttons → they react, a stronger chance to lift a slump, and
   their bubble updates. Counter ticks down; at 0 the boxes stop responding.
4. **Begin Second Half** as before.

The bubbles are a **deliberately ambiguous** view of hidden state (personality × scoreline × expectation × morale ×
hidden slump). Reading them is the skill; individual talks are scarce, so misreading wastes one. That's the trade-off.

---

## 1. 🔴 The two response rows are the same object

`TeamTalkUI ▸ Response Manager` and `▸ Individual Response Manager` both point at fileID `1414398093` — the single
`Responses` prefab instance parented to the talk panel. The code assumes two separate rows, so today:

- `Deliver` and `DeliverIndividual` are both subscribed to one `OnResponseChosen`.
- `EnterIndividualPhase()` locks the squad row, then `OnBoxClicked` calls `SpawnButtons()` on that **same** row and
  un-locks it.
- The private-word tone buttons spawn into the **squad row at the bottom of the panel**, not inside the popup. The
  popup shows a name and nothing else.

**Fix:** drop a **second** instance of `Assets/Prefabs/Interview/Responses.prefab` inside `Team Talk UI > Individual`, and
repoint **Individual Response Manager** at the `ResponseManager` on *that* instance.

| Its Inspector | Value |
|---|---|
| Response Button Prefab | `ResponseButton.prefab` |
| Container | the new instance's own transform |
| Dialogue | leave empty |
| Responses | 8 entries soft→harsh (same as the squad row), or empty for all |
| Lock After Pick | ✅ (the popup rebuilds the row on each open anyway) |
| **On Response Chosen** | **leave empty** — `TeamTalkUI` subscribes in code (`DeliverIndividual`) |

> Assign **both** Individual Panel *and* Individual Response Manager or the phase stays off (the squad talk still
> works). That graceful degradation is why this reads as "half working" rather than throwing.

## 2. 🔴 No Cancel button on the popup

`TeamTalkUI.CancelIndividual()` exists and is hooked nowhere. Add a `Base UI/Button.prefab` named `CancelButton`
inside `Individual`, OnClick → drag **Team Talk UI** → `TeamTalkUI.CancelIndividual`.

While you're there: `Individual` currently stretches `0,0`–`1,1` with **no `Image`**, so it neither dims the squad
behind it nor blocks raycasts — clicks land on the morale boxes underneath and re-target `individualTarget`
mid-popup. Give it a dim `Image` (≈ `0,0,0,0.6`, **Raycast Target on**) and size it as a real centred dialog.

## 3. 🟠 "Private words left" is inside the popup it advertises

`talksRemainingText` (`Texty (5)`) is a child of `Individual`, but `UpdateTalksRemaining()` runs from
`EnterIndividualPhase()` — the moment the squad talk lands, while the popup is **closed**. So the counter is only
ever visible once you've already committed to spending one, and the "Deliver a team talk first" prompt is never seen.

It is also positioned at exactly `(17.99, 0)` — the same pixel as `individualTargetText` *and* `overallText`.

**Fix:** move `TalksRemainingText` out of `Individual` and onto the panel's header row, next to the title. It's a
persistent HUD read-out, not popup chrome. (Covered by the panel rebuild in
[`UI_AUDIT_AND_LAYOUT_PLAN.md`](UI_AUDIT_AND_LAYOUT_PLAN.md) §3.6.)

## 4. 🔴 The speech bubble has no bubble — and now it throws

`PlayerMoraleBoxUI ▸ Bubble Background` is unassigned on `HalfTimePlayer.prefab` (the morale box). Only `bubbleText`
is wired, so the chatter — the entire mechanic — renders as bare text over the box, and `ShowBubble(line, tint)`'s
reaction tint has nothing to tint. **`PlayerMoraleBoxUI` no longer null-guards its references** (house style: an
unwired field should fail loudly), so opening the team talk now throws a `NullReferenceException` in `ShowBubble`
until this is assigned. Assign it, or say the word and the field comes out of the script.

**Fix:** add a `BubbleBG` (Image) under the box's Bubble object and assign it. A 200×150 grid cell can't hold a box
*and* a legible line for 25 players, so either drop the bubble to 1–2 lines with TMP auto-size + `Ellipsis`
overflow, or (recommended) raise the cell to ~240×190 and let the squad grid scroll.

Wiring notes for the box, if you rebuild it:
- The **Select Button** should target a graphic that *isn't* the reaction-tinted `background` (or set its Transition
  to **None**), otherwise its disabled/hover tint fights the reaction colour. A transparent full-rect Image child as
  the target graphic is cleanest.
- **Do not** add an OnClick entry — `PlayerMoraleBoxUI` wires the listener in code (it needs the player reference).
- **Don't** hand-type `BubbleText` — code writes it, and blanks it for silence.

---

## Driven by code — nothing to wire

- **Expectation read.** The situation line reads e.g. *"Half-time — level — Irrelevant · punching above your
  weight"*. The over/under-performing half comes from `MatchSimPageUI.ExpectedGoalDifferenceForMyTeam()`
  (starting-XI strength gap) vs the live score.
- **Pre-talk / post-talk / individual bubbles** — `TeamTalkUI` fills them via `TeamTalkChatter`.
- **Selectable boxes / counter** — enabled when the squad tone is delivered, disabled at 0 talks.
- **Squad grid layout** — `BuildSquadGrid()` just instantiates into `boxContainer`; the Grid Layout Group does the
  placement. (The old arch-placement fields `arcWidth`/`arcDepth`/`topMargin` have been removed from the script.)

## Test checklist

1. **Open the talk** → a *handful* of boxes have bubbles; the situation line shows the score band **and** an
   over/under-performing phrase.
2. **Deliver a tone** → boxes flash ±delta, most now show a coloured bubble, "Private words left: 2" appears
   **on the panel header** (not hidden in the popup).
3. **Click a box** → popup "A word with *Name*" **with tone buttons inside it**. **Cancel** → popup closes, counter
   unchanged.
4. **Click a box → pick a tone** → that box reacts + its bubble updates; counter → 1. Repeat → 0; boxes stop
   responding.
5. A **normally-loud** player (Driven/Cocky/Silly) gone **quiet** post-talk is your slump tell; a **Shy/Lazy**
   player *speaking* is the inverse tell. Reads, not certainties — you'll misjudge some. That's intended.
6. **Begin Second Half** → panel closes; next match both the squad tone and the individual talks reset.

## Tuning cheat-sheet

| Knob | Where | Effect |
|---|---|---|
| `maxIndividualTalks` | `TeamTalkController` (Inspector) | how forgiving the read-the-room puzzle is |
| `individualRecoveryMultiplier` | `TeamTalkController` (Inspector) | payoff of a correctly-diagnosed private word |
| `RATING_POINTS_PER_GOAL`, `EXPECTATION_BAND` | `TeamTalkChatter` (consts) | how strong a strength gap must be to read as over/under-performing |
| base speech rates, `RUT_SILENCE_FACTOR` | `TeamTalkChatter` (consts) | how sparse pre-talk is / how loudly a rut shows as silence |
| the line pools | `TeamTalkChatter` (per-personality methods) | **the feature itself** — voices, tells and red herrings |
| `SlumpRecoveryChance` | `MoraleEffects` | base chance any talk lifts a slump |
| reaction table | `EventsManager.CreateReactionTable()` | which tone lands for which personality (shared with 1-on-1s) |

> `maxMoodSwing` / `maxPassionSwing` / `flopThreshold` on `TeamTalkController` feed only the **dormant** minigame
> path — they do nothing for the shipping talk. Leave them alone (or delete them with the minigame classes).
