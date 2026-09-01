# Team Talk Rework — plan (v2: dressing-room chatter + individual talks)

> ✅ **IMPLEMENTED (code complete, editor wiring pending).** `TeamTalkChatter` (the pure line generator),
> `PlayerMoraleBoxUI` speech bubbles + click-to-select, `TeamTalkController.TalkToPlayer` + `MaxIndividualTalks`, and
> `TeamTalkUI`'s pre/post/individual phases all exist now. This doc remains the **design rationale**; for the
> **editor wiring** follow [`HALFTIME_TEAM_TALK_REWORK_BUILD.md`](HALFTIME_TEAM_TALK_REWORK_BUILD.md) (built on the
> squad-talk wiring in [`MATCH_SIM_AND_TEAM_TALK_BUILD.md`](MATCH_SIM_AND_TEAM_TALK_BUILD.md)). One deviation from the
> plan below: player selection is by **clicking the morale box** (not `PlayerPickerPopup`), which suits the arch.

## The vision

Half-time becomes a **read-the-room** puzzle. You don't get told who's struggling — you *infer* it from what
players say (and don't say), which is deliberately **ambiguous**: coloured by personality, the result, what was
expected, their morale, and whether they've hidden a **slump** (`Player.InSlump`, §6 of PROJECT_OVERVIEW / the
morale model). Then you spend a **limited** number of one-on-one talks on whoever you judge needs it — a gamble,
because the same reaction can mean very different things.

The whole skill loop: **read the pre-talk mood → pick a squad tone → read the fuller post-talk reactions → spend
scarce individual talks on the players you think you've diagnosed.** Get the reads right and you rescue slumps and
fire the right people up; misread and you waste talks, or pick a tone that flattens the room.

The morale mechanics it leans on (slumps, blunders, `MoraleEffects.SlumpRecoveryChance`) are already implemented.
Everything below is new.

---

## 1. Half-time flow

1. **Open the talk.** The squad arch shows (existing `TeamTalkUI`). Each box can carry a **speech bubble**.
2. **Pre-talk chatter.** Before you choose anything, **a few** players have a bubble (most are silent — there's
   little to react to yet). This is your first read of the dressing room.
3. **Pick one squad tone** (existing `ResponseManager`: Praise / Encourage / Inspire / Galvanise / Persuade /
   Challenge / Rage / Deflect). Squad-wide morale swing via the existing reaction path.
4. **Post-talk reactions.** Now **most** players show a bubble — there's a lot more to react to (your tone + how it
   landed + the result). Boxes also flash the existing reaction colour/±delta.
5. **Individual talks.** You may now single out up to **`MaxIndividualTalks`** players (a serialized variable) for a
   private word — pick a player, pick a tone, they react (and a personal word is a *stronger* chance to lift a
   slump than the squad talk). A counter shows talks remaining.
6. **Resume the second half.**

> "Talk to a player once" from the brief → the **squad tone is your one collective talk**; the individual phase is
> the new, capped follow-up. Default `MaxIndividualTalks` ~2 (tune per difficulty).

---

## 2. What drives a reaction (the factors)

A player's line is a lossy, in-character *view* onto their hidden state. Inputs (`TalkContext`):

- **Personality** (the voice — §6).
- **Phase** — PreTalk / PostTalk / Individual (drives how likely they are to speak, and about what).
- **Result** — goal difference from your team's view (`CurrentGoalDifferenceForMyTeam`).
- **Expectation** — result **vs what was expected** of this fixture (over/on-par/under-performing). Derived from
  pre-match relative strength (overall rating gap → expected margin) compared to the actual scoreline. A 0-0 is
  *great* against the league leaders and *dismal* against the bottom club — same scoreline, opposite mood.
- **Morale bands** — Mood and Passion each vs the player's ideal (WellBelow / Below / Near / Above / WellAbove).
- **Rut** — `InSlump` (the hidden bit you're trying to detect).
- **Tone + reaction** — for Post/Individual: the tone you chose and how they took it (`Event.Reaction` from the
  existing table). `recoveredFromSlump` if the talk just snapped them out.

**Optional extra factors to enrich later (v2+):** the player's own half — did they score / miss a sitter / get
booked (from the match's per-player shots & fouls); **fatigue**; a **chemistry** clash with a nearby team-mate
([[chemistry]]); **starter vs sub**; **momentum** (were you winning and got pegged back?). Each is just another
tag on the line pools.

---

## 3. The chatter generator (pure logic)

New pure class `TeamTalkChatter` (in `Assets/_Scripts/TeamTalk/`), **no morale side effects** — it only turns state
into a string, derived on read:

```csharp
public enum ChatterPhase { PreTalk, PostTalk, Individual }

public struct TalkContext {
    public Person.PersonalityType personality;
    public ChatterPhase phase;
    public int goalDifference;              // + = winning
    public Expectation expectation;         // Overperforming / OnPar / Underperforming
    public Morale morale;                   // mood/passion vs ideal
    public bool inRut;
    public Event.Response tone;             // Post/Individual only
    public Event.Reaction reaction;         // Post/Individual only
    public bool recovered;                  // just lifted from a slump
}

public static string Line(TalkContext ctx);   // "" == silence (itself a signal, or noise)
public static bool   Speaks(TalkContext ctx); // silence probability by personality × phase × state
```

**Structure (house "coarse groups" style, with deliberate reuse):**
- Band the inputs (reaction → Positive/Neutral/Negative; result → Winning/Level/Losing; expectation → 3 bands;
  morale → the deviation bands). Author against the **bands**, not raw numbers — ~a few dozen lines per
  personality, not thousands.
- Lines live in **shared pools** tagged with the contexts they may appear in; multiple (personality, state) combos
  draw from the same pool. That's what creates overlap → ambiguity (§5) and keeps authoring small.
- **Silence is first-class.** `Speaks()` gives each personality a base talk-rate per phase (low pre-talk, higher
  post-talk), pushed up or down by state. Some personalities are near-silent by default (Shy, Lazy) so their
  silence carries no info; others are near-always-vocal (Driven, Cocky, Silly) so *their* silence screams.

---

## 4. The one rule: read the *deviation*, not the line

A savvy manager reads **"is this player behaving unlike their own baseline, given the context?"** — never a line in
isolation. The generator is built to make the same observable mean different things:

| Observable | Could mean… |
|---|---|
| **Silence** | Shy/Lazy baseline (no info) · Calm baseline · a normally-vocal type in a **rut** (big tell) · anyone who hated your tone and withdrew |
| **Anger / lashing out** | Aggressive fired-up (*healthy* for him — low-mood/high-passion is his ideal) · Aggressive in despair (rut) · Cocky ego-wound · anyone who took a Rage badly |
| **A joke** | Silly baseline · Silly masking a rut with humour |
| **Over-eager / agitated** | normal for Aggressive/Driven · **Calm** showing fire = over-pumped/rattled = warning |
| **Speaking up unexpectedly** | Shy/Lazy who are genuinely *lifted* (rare, positive) — the inverse tell |
| **Fatalism / "what's the point"** | Cautious baseline worry · a real rut in anyone |

So there's no lookup that says "in a rut." You weigh personality baseline × context × how far off-baseline they
are — and you'll still be wrong sometimes. That uncertainty is the point, and it's why individual talks are scarce.

---

## 5. Personality permutations (the content)

For each: **voice**, **pre-talk**, **post-talk by how they took your tone**, **in a rut**, and **the tell & the
trap**. Lines are examples from each personality's pools (the generator picks weighted-random among those matching
the context). Ideals shown as (mood/passion).

### Aggressive (25/95) — blunt, confrontational, high-octane
- **Pre:** losing/under → "This is embarrassing. Sort it out." · winning → "Don't you DARE let up."
- **Post +** (loved a Rage/Galvanise/Challenge): "NOW we're talking. Let's go." · bangs the wall.
- **Post −** (hated a soft Praise while losing): "Praise? We're getting battered — wake UP." *(lashes at you)*
- **Rut:** turns destructive — "Useless, all of you." — or, tellingly, goes **cold and silent**.
- **Tell/trap:** his anger is usually *good* (it's his ideal state). The rut sign is **defeatism** ("what's the
  point") or **silence**, not demand ("sort it out"). Easy to misread fire as trouble.

### Calm (90/5) — measured, brief, unflappable
- **Pre:** "All good. We stick to the plan." · often just a nod (silence).
- **Post +:** "Good. Nothing changes — we keep going."
- **Post −** (you raged): "…there's no need for that." *(mild, disappointed)*
- **Rut (mood below 90):** flat "Yeah. Fine." — nearly identical to his normal calm. **Red herring.**
- **Tell/trap:** his ideal passion is **5**, so **any visible fire/agitation is off-baseline** → over-pumped or
  rattled (a warning). Distress, though, hides perfectly behind his calm.

### Cautious (75/25) — hedging, anxious, risk-averse
- **Pre:** winning → "See it out. Don't chase it." · losing → "We're too open, this could get worse."
- **Post +** (Persuade/Deflect): "Okay. If we're disciplined, we're fine."
- **Post −** (Inspire/Galvanise gung-ho): "Attack more? That's how we concede…"
- **Rut:** catastrophising or silent — "I don't know if I can turn this around."
- **Tell/trap:** he *always* sounds worried (reuse the same anxious pool), so trait-worry vs rut-worry is genuinely
  hard. Watch for fatalism beyond his usual fretting.

### Cocky (95/80) — arrogant, credit-taking, dismissive
- **Pre:** winning → "Told you. Men against boys." · losing → "Relax, I'll sort it. Watch."
- **Post +** (Praise/Inspire): "Finally, someone gets it." *(smug)*
- **Post −** (Rage/Challenge wounds the ego): "Don't blame me for THIS lot." *(throws team-mates under the bus)* or sulks.
- **Rut (ego bruised):** uncharacteristic doubt/silence — "…maybe I'm just not feeling it."
- **Tell/trap:** a Cocky player expressing **doubt** is a huge tell. But his blaming others might be plain arrogance,
  not a rut — the deflection is shared between "fine and cocky" and "wounded."

### Driven (80/80) — motivational, team-first, relentless
- **Pre:** "Come on, lads — we go again, every second." · under → "This isn't us. Heads up, we fix it now."
- **Post +:** rallies the room — "You heard the gaffer — let's GO!"
- **Post −** (a limp/soft tone when he wanted fire): "…is that it?" *(frustrated)*
- **Rut:** the brief's example — **says nothing**, or a hollow "yeah, come on" with no conviction.
- **Tell/trap:** a quiet Driven player is the classic slump tell. **Trap:** cruising 4-0 up he might be quiet because
  the job's done, not because he's down — a false positive when you're winning comfortably.

### Kind (100/50) — supportive, empathetic, others-focused
- **Pre:** "Chin up, everyone — we're in this together." · consoles a team-mate: "Forget the miss, next one."
- **Post +:** "That's lovely, gaffer. Come on, boys."
- **Post −** (you raged at the team): "There's no need to be so hard on them." *(defends others)*
- **Rut (mood below 100 — needs to be very happy):** he **stops encouraging others**; the *absence* of his usual
  support is the tell.
- **Tell/trap:** so relentlessly nice that a rut just reads as "a bit quieter than usual" — subtle.

### Lazy (25/95) — indifferent, dry, wants it over
- **Pre:** "Yeah, whatever, we'll be alright." · losing → "…are we done yet?"
- **Post +:** understated even when lifted — "Sure. Fine."
- **Post −:** "Ugh, do we have to."
- **Rut:** even more checked-out / silent — but he's **baseline** checked-out, so it's near-invisible. **Red herring
  (shares the silence pool with Shy).**
- **Tell/trap:** inverted — if a Lazy player says something genuinely **engaged**, *that's* the anomaly (he's up).

### Shy (100/70) — quiet, avoids attention, mumbles
- **Pre:** usually **silent**.
- **Post +** (a strong Amazing reaction, or just recovered): a rare "…thanks, gaffer." / "…we can do this."
- **Post −** (you raged): flinches, goes smaller, silent.
- **Rut:** silent — **identical to baseline → no signal.** The archetypal red herring.
- **Tell/trap (inverted):** silence tells you nothing about a Shy player; him **speaking at all** tells you he's in
  a good place. (The brief's own example.)

### Silly (60/60) — jokes, deflects, lightens the mood
- **Pre:** "We're basically Brazil, lads." · losing → "On the bright side, it can't get worse, right?"
- **Post +:** "Aye aye, gaffer!" *(playful)*
- **Post −:** a nervous deflecting joke.
- **Rut:** the **jokes stop** — a flat or silent Silly player is the tell.
- **Tell/trap:** a joke while **losing** might be him fine-and-coping *or* masking a rut behind humour (same
  coping-joke pool serves both) — you can't tell from the quip alone.

### Smart (70/80) — analytical, tactical, calm authority
- **Pre:** "We've got their full-back — overload that side." · under → "We're second to everything. It's effort, not tactics."
- **Post +** (Persuade / a substantive tone): "Agreed. That's the adjustment."
- **Post −** (Rage with no substance): "…shouting doesn't change the midfield, gaffer." *(pushes back)*
- **Rut:** stops reading the game — uncharacteristic vagueness/silence, "I don't know."
- **Tell/trap:** a Smart player with **no read** on the match is off-baseline. But his critical pushback is normal
  (he questions everything), so "disagrees with you" ≠ "in a rut."

---

## 6. Individual talks

- After the squad tone, up to **`MaxIndividualTalks`** (serialized on `TeamTalkController`, default ~2) private
  words. **Reuse the discussion system**: `PlayerPickerPopup`/box-click to choose the player, the shared
  `ResponseManager` for the tone, the existing reaction table + `Person.NewMorale` for the effect.
- A personal word is a **stronger** slump-lifter than the blanket talk — reuse `MoraleEffects.SlumpRecoveryChance`
  with a personal multiplier (e.g. ×1.3, capped). Their `Individual` chatter line gives you feedback on how it landed.
- **Trade-off (the design rule):** talks are scarce and the tone can *backfire* (a Rage to the wrong personality
  dents them). Under the ambiguity of §4/§5 you'll sometimes spend one on a player who was fine, or read a red
  herring and miss a real slump. That risk is the mechanic.

---

## 7. Implementation steps

1. **`TeamTalkChatter` (pure)** + `TalkContext`/`ChatterPhase`/`Expectation` — the pools & rules (§3–§5). Unit-testable.
2. **Expectation** — a small helper: pre-match expected GD from relative team strength vs the live scoreline → band.
3. **`PlayerMoraleBoxUI`** — add a `TMP_Text` bubble child + `ShowBubble(string)` (hide when empty), reuse
   `TeamTalkReactions.ReactionColour` for tint.
4. **`TeamTalkUI`** —
   - on `Open()`: build the arch, then populate **pre-talk** bubbles (`ChatterPhase.PreTalk`).
   - after `DeliverTalk`: populate **post-talk** bubbles from each `PlayerReaction` (+ existing colour/delta flash).
   - then enter **individual-talk mode**: clickable boxes, a remaining-talks counter, a response popup per pick,
     apply via a new `TeamTalkController.TalkToPlayer(player, tone)`, show the `Individual` bubble.
5. **`TeamTalkController`** — `MaxIndividualTalks` + `IndividualTalksUsed` (reset in `ResetForMatch`); `TalkToPlayer`
   (single-player reaction + boosted rut recovery). Keep the one squad tone per match.
6. **`PlayerReaction`** already carries `wasInSlump`/`recoveredFromSlump`; thread them into the post-talk context.

### Editor wiring (for the eventual `*_BUILD.md`)
- `PlayerMoraleBox` prefab: a small child `TMP_Text` "Bubble" (+ optional bubble background image that toggles with
  non-empty text); make the box a Button for individual-talk selection.
- Half-time / team-talk panel: a **talks-remaining** label, and a per-player response popup (reuse the discussion
  `ResponseManager`).

---

## 8. Tuning / authoring notes
- **The feature *is* the pools.** Keep ~⅓ of personalities as silence/red-herring archetypes (Shy, Lazy, Calm) and
  ~⅓ as loud tells (Driven, Cocky, Silly) so reads are inferable, not certain. Deliberately share pools across
  states to create overlap.
- Talk-rates: PreTalk low (most silent), PostTalk high; per-personality base ± state.
- `MaxIndividualTalks`, the personal recovery multiplier, and `SlumpRecoveryChance` are the difficulty dials.
- Chatter never touches morale numbers — it's a read-only view, so it can't drift from the actual mechanics.
- Keep one squad tone per match (`ResetForMatch` at kickoff) so the collective read-and-choose stays a real
  decision with a real downside.
