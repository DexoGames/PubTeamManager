# Half-Time Team Talk — dressing-room chatter + individual talks (build guide)

Editor wiring for the team-talk **rework**: per-player speech bubbles (a *read-the-room* puzzle) and a capped
**individual-talk** phase after the squad tone. The design is [`TEAM_TALK_REWORK_PLAN.md`](TEAM_TALK_REWORK_PLAN.md);
this doc is what to add **in the scene**. It builds on the already-wired squad talk in
[`MATCH_SIM_AND_TEAM_TALK_BUILD.md`](MATCH_SIM_AND_TEAM_TALK_BUILD.md) Part B — do that first; nothing here replaces it.

**All the C# is done and drop-in.** New/changed scripts:
`TeamTalkChatter` (new, pure), `TeamTalkController` (+ individual talks), `PlayerMoraleBoxUI` (+ bubble + click),
`TeamTalkUI` (+ pre/post bubbles + individual phase), `MatchSimPageUI` (+ `ExpectedGoalDifferenceForMyTeam`).

> **Graceful degradation:** the squad talk works with **none** of this wired. The individual-talk phase only turns on
> once you assign **both** the Individual Panel and the Individual Response Manager on `TeamTalkUI` (§3). The bubbles
> only appear if `PlayerMoraleBoxUI` has a Bubble Text (§1). So you can wire it in stages.

---

## What the player now sees

1. **Open the talk** → a few players have a speech bubble (most silent — little to react to yet). Your first read.
2. **Pick one squad tone** (existing response row) → the squad reacts; boxes flash ±delta **and now most players
   speak** (post-talk lines coloured by how they took it).
3. **Individual phase** → a "Private words left: N" counter appears and boxes become **clickable**. Click a player →
   a small popup ("A word with *Surname*") with the tone buttons → they react, a stronger chance to lift a slump,
   and their bubble updates. Counter ticks down; at 0 the boxes stop responding.
4. **Begin Second Half** as before.

The bubbles are a **deliberately ambiguous** view of hidden state (personality × scoreline × expectation × morale ×
hidden slump). Reading them is the skill; individual talks are scarce, so misreading wastes one. That's the trade-off.

---

## 1. `PlayerMoraleBox` prefab (`Assets/Prefabs/Matchca.prefab`)

Already carries `PlayerMoraleBoxUI` with its six original fields assigned. **Add three things** (all optional — the
box degrades to today's behaviour without them, but the bubble is the whole point of the rework):

```
PlayerMoraleBox                 Image  ← background (tinted by the reaction, as now)
                                Button          ← NEW: makes the box selectable in the individual phase
                                PlayerMoraleBoxUI
├── Name       (TMP)
├── MoodBar    (Image, Filled)
├── PassionBar (Image, Filled)
├── Morale     (TMP)
├── Reaction   (TMP)             the "+8" / "−5" flash
└── Bubble                       ← NEW: the speech bubble
    ├── BubbleBG   (Image)       optional backing, toggled + tinted with the line
    └── BubbleText (TMP)         the spoken line
```

New `PlayerMoraleBoxUI` fields (Inspector, under the added headers):

| Header | Inspector | Assign | Notes |
|---|---|---|---|
| Chatter | **Bubble Text** | `BubbleText` | the spoken line; auto-hidden when the player is silent |
| Chatter | **Bubble Background** | `BubbleBG` | optional; shown/hidden with the line, tinted by the reaction |
| Individual talk | **Select Button** | the `Button` on the root | optional; enables click-to-single-out |

Wiring notes:
- The bubble sits **above** the box (the arch dips at the ends, so leave headroom at the top of the arch container —
  see Arc Depth / Top Margin in Part B). It's a child of the box, so it rides along at each arch position. Give
  `BubbleText` **auto-size** or a fixed small font; keep it narrow (boxes are ~67 px apart).
- **Don't** hand-type `BubbleText` — code writes it (and blanks it for silence).
- The **Select Button** should target a graphic that *isn't* the reaction-tinted `background` (or set its Transition
  to **None**), otherwise its disabled/hover tint fights the reaction colour. A transparent full-rect Image child as
  the button's Target Graphic is cleanest. **Do not** add an OnClick entry — `PlayerMoraleBoxUI` wires the listener
  in code (it needs the player reference).

---

## 2. `TeamTalkController` (managers object)

Two new tuning fields under an **Individual talks** header — defaults are sensible, adjust for difficulty:

| Inspector | Default | Meaning |
|---|---|---|
| **Max Individual Talks** | `2` | private words allowed per match after the squad tone |
| **Individual Recovery Multiplier** | `1.3` | a personal word is this-times better at snapping a player out of a slump |

`ResetForMatch()` (already called at kickoff) now also resets the individual-talk count. Nothing else to wire.

---

## 3. `TeamTalkUI` — the individual-talk popup

Add a **child popup** to the existing `TeamTalkPanel` (from Part B) and four field assignments. Everything is a
direct child of the panel so it's hidden/restored with it.

```
TeamTalkPanel                              ← TeamTalkUI   [INACTIVE in scene]
├── Backdrop / TitleText / SituationText / BoxContainer / FlavourText / OverallText   (existing, Part B)
├── ResponseRow                            ← ResponseManager  (existing squad tone row, Part B)
├── TalksRemaining        (TMP)            ← NEW  "Private words left: 2"
├── IndividualPanel                        ← NEW  [set INACTIVE — code shows it on a box click]
│   ├── TargetText        (TMP)            ← NEW  "A word with Smith"
│   ├── IndividualResponseRow              ← NEW  a SECOND ResponseManager  [Horizontal Layout Group]
│   └── CancelButton                        → TeamTalkUI.CancelIndividual
└── CloseButton                             → TeamTalkUI.Close   (existing)
```

New `TeamTalkUI` fields, under **"Individual talks (optional…)"**:

| # | Inspector | Assign |
|---|---|---|
| 1 | **Individual Panel** | `IndividualPanel` GameObject (leave inactive in the scene) |
| 2 | **Individual Response Manager** | the `ResponseManager` on `IndividualResponseRow` (§3a) |
| 3 | **Individual Target Text** | `TargetText` |
| 4 | **Talks Remaining Text** | `TalksRemaining` |

> Assign **both** Individual Panel *and* Individual Response Manager or the phase stays off (the squad talk still
> works). Target/Talks-Remaining texts are optional niceties.

### 3a. The second `ResponseManager` (`IndividualResponseRow`)

Same component/prefab as the squad row — see `MATCH_SIM_AND_TEAM_TALK_BUILD.md` §B2a / Part C2. It's a **separate
instance** so the two rows don't interfere.

| Inspector | Value |
|---|---|
| **Response Button Prefab** | `ResponseButton.prefab` |
| **Container** | `IndividualResponseRow`'s own transform |
| **Dialogue** | leave empty |
| **Responses** | 8 entries soft→harsh (same as the squad row), or empty for all |
| **Lock After Pick** | ✅ (the popup rebuilds the row on each open anyway) |
| **On Response Chosen** | **leave empty** — `TeamTalkUI` subscribes in code (`DeliverIndividual`) |

### 3b. OnClick hooks (only these two are manual)

| Button | Hook |
|---|---|
| IndividualPanel → CancelButton | `TeamTalkUI.CancelIndividual` |
| (Team Talk button, half-time panel) | `TeamTalkUI.Open` — unchanged |

Box clicks, the squad tone pick, and the individual tone pick are all wired **in code** — don't add listeners for them.

---

## 4. No wiring needed (code drives it)

- **Expectation read.** The situation line now reads e.g. *"Half-time — level — Irrelevant · punching above your
  weight"*. The over/under-performing part comes from `MatchSimPageUI.ExpectedGoalDifferenceForMyTeam()` (starting-XI
  strength gap) vs the live score — nothing to assign.
- **Pre-talk / post-talk / individual bubbles** — `TeamTalkUI` fills them via `TeamTalkChatter`.
- **Selectable boxes / counter** — enabled by `TeamTalkUI` when the squad tone is delivered, disabled at 0 talks.

---

## 5. Test checklist (extends Part D)

1. **Open the talk** → a *handful* of boxes have bubbles; the situation line shows the score band **and** an
   over/under-performing phrase.
2. **Deliver a tone** → boxes flash ±delta and **most** now show a coloured bubble; "Private words left: 2" appears.
3. **Click a box** → popup "A word with *Name*" + tone buttons. **Cancel** → popup closes, counter unchanged.
4. **Click a box → pick a tone** → that box reacts + its bubble updates; counter → 1. Repeat → 0; boxes stop
   responding to clicks.
5. A **normally-loud** player (Driven/Cocky/Silly) who's gone **quiet** post-talk is your slump tell; a **Shy/Lazy**
   player *speaking* is the inverse tell. (Reads, not certainties — you'll misjudge some. That's intended.)
6. **Begin Second Half** → panel closes; **next match** the squad tone *and* the individual talks are available again.

---

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
