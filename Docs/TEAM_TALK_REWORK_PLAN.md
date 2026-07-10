# Team Talk Rework — plan

Goal: turn the half-time team talk into a "read the room" moment. After you speak, **every player shows a little
reaction line**, and those lines — flavoured by personality **and** whether the player is secretly in a **slump**
(see the morale model in `PROJECT_OVERVIEW.md` §6 / `MoraleEffects`) — let you *infer* who's struggling without it
ever being spelled out. The talk also has a chance to lift slumping players back out.

This is the **plan**; the morale mechanics it builds on (slumps, blunders, `SlumpRecoveryChance`) are already
implemented. None of the below is built yet.

---

## What already exists
- **Squad-wide tone talk** — `TeamTalkController.DeliverTalk(response, severity)`: you pick one `Event.Response`
  (Praise / Rage / Encourage / …), every squad member reacts via `EventsManager.ReactionTable` (keyed on
  personality), severity comes from the half-time scoreline. Returns `List<PlayerReaction>`.
- **`PlayerReaction`** now carries `wasInSlump` / `recoveredFromSlump` (added for this rework).
- **Slump recovery** — `DeliverTalk` already rolls `MoraleEffects.SlumpRecoveryChance(reaction)` per slumping
  player, so a well-received talk snaps some of them out. ✅ working today.
- **`TeamTalkUI`** lays every player out as a morale box along a shallow arch and flashes how each took the talk
  (`PlayerMoraleBoxUI`). This is what we extend.

---

## The core new piece: personality- & slump-aware reaction lines

A **pure logic** class — propose `TeamTalkChatter` (in `Assets/_Scripts/TeamTalk/`) — that maps a reaction to a
short spoken line:

```csharp
public static string Line(Person.PersonalityType personality, Event.Reaction reaction, bool inSlump)
```

Design rules (consistent with the house "coarse groups" style):
- **Band the reaction** into Positive (Good/Great/Amazing) / Neutral / Negative (Poor/Bad/Terrible) — so the table
  is ~3 bands × 10 personalities, not 70 bespoke lines.
- **Two variants per cell:** a *normal* line and a *slump* line. The slump line is usually **silence** (`""`) or a
  flat, withdrawn mutter — the player has gone into their shell.
- **It must not be obvious.** The reveal is *relative*: a normally-vocal type going quiet is the tell.
  - **Driven** normal + positive → `"Come on, lads!"`; **in a slump** → `""` (says nothing — out of character → a clue).
  - **Cocky** normal → `"Told you. Easy."`; slump → a muted `"…yeah."`.
  - **Shy** → `""` in almost every case, slump or not → **no signal** (you can't read him — intentional noise).
  - **Calm** → short and even either way → weak signal.
  - **Aggressive** normal + negative → `"Sort it OUT!"`; slump → `""`.
  So a sharp player learns to spot "X always pipes up and he's silent today" — but red herrings (Shy, Calm) keep it
  from being a reliable readout. Author the full 10×3×2 table in `TeamTalkChatter` with this spirit.

Return value contract: empty string ⇒ the box shows no speech bubble (just the morale face/flash), which is itself
information.

---

## Half-time flow — one decision to make

The brief says *"you can talk to an individual player once in the half time"*, and *"after making your talk, every
player should have a reaction box"*. Two ways to reconcile "individual talk" with "everyone reacts":

**Option A (recommended) — squad talk + the room reacts.** Keep the single squad-wide tone talk (one per
half-time, as now). After it lands, show the reaction line on **every** player's box. Simplest, reuses
`DeliverTalk` as-is, and still delivers the whole "read the room / spot the slumps" loop. "Individual" then just
means you get **one** talk.

**Option B — pick one player, whole room overhears.** Add a player-pick step (reuse `PlayerPickerPopup`) before the
tone: the chosen player gets the **full** morale swing + slump-recovery roll; everyone else gets a **muted** swing
(e.g. ×0.3) but still shows a reaction line (they react to the mood). More faithful to "talk to an individual", more
UI, and needs `DeliverTalk` split into "target gets full / others muted".

Recommend **A** for v1 (smallest change, full payoff), with **B** as a follow-up if you want the targeted-talk
texture. The reaction-box system below is identical either way.

---

## Implementation steps

1. **`TeamTalkChatter` (pure)** — the table above. Unit-testable, no scene refs. *(code)*
2. **`PlayerReaction`** already has `wasInSlump`/`recoveredFromSlump`; pass them through so the box can pick the
   slump line and (optionally) a subtle "lifted" flash when `recoveredFromSlump`. *(done for the data; UI reads it)*
3. **`PlayerMoraleBoxUI`** — add a small `TMP_Text` speech-line child. New method `ShowReaction(PlayerReaction r)`:
   set the line from `TeamTalkChatter.Line(r.player.Personality, r.reaction, r.wasInSlump)`, tint via
   `TeamTalkReactions.ReactionColour(r.reaction)`, hide the bubble when the line is empty. *(code + editor)*
4. **`TeamTalkUI`** — after `DeliverTalk`, iterate the returned reactions and call `ShowReaction` on each box
   (staggered for readability). Keep the existing morale-box arch. *(code + editor)*
5. **Flow / once-per-half-time** — `TeamTalkController.Used` already enforces one talk. Wire the half-time panel
   button(s) (`MatchSimPageUI._halfTimePanel`) to the talk UI. If Option B: insert the `PlayerPickerPopup` step and
   add `DeliverTalk(response, severity, Player target, float bystanderScale)`. *(code + editor)*
6. **Recovery feedback (optional)** — when `recoveredFromSlump`, a quiet positive flash on that box (don't label it
   "recovered from slump" — keep it non-obvious).

### Editor wiring (for the eventual `*_BUILD.md`)
- `PlayerMoraleBoxUI` prefab: add a child `TMP_Text` "ReactionLine" (small, under the face), serialized field on the
  component; a bubble background image that toggles with non-empty text.
- If Option B: a "Pick a player" button on the half-time panel that opens the shared `PlayerPickerPopup`.

---

## Tuning / authoring notes
- The whole readability of the feature lives in the `TeamTalkChatter` table — keep ~1/3 of personalities as
  "always quiet" or "always chatty" red herrings so slumps are *inferable, not obvious*.
- Slump-recovery odds are in `MoraleEffects.SlumpRecoveryChance` (Amazing 90% → Good 45% → Neutral 15% → none).
- Slump entry odds / blunder odds are atop `MoraleEffects` — see `PROJECT_OVERVIEW.md` §6.
- Keep the talk **one per match** (`TeamTalkController.ResetForMatch` at kickoff) so reading the room and choosing a
  tone stays a real decision with a real downside (a bad tone flattens the squad / fails to lift slumps).
