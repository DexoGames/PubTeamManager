# Team Talk UI — build guide

The half-time team talk **reuses the exact 1-on-1 player-discussion system**, just broadcast to the whole squad.
Every squad member shows as a little morale box along a shallow arch across the top of the screen; you pick one
**response** — the same `Event.Response` options a discussion uses (**Praise, Encourage, Challenge, Persuade,
Inspire, Galvanise, Rage, Deflect**) — and it hits everyone. Each player's reaction comes from the shared
`EventsManager.ReactionTable` keyed on their personality, adjusted by a **severity taken from the half-time
scoreline**, and applied via the same `Person.NewMorale`. The boxes flash how it landed; an overall verdict sums it
up. One talk per match.

## Scripts (all written — this is editor wiring)
| Script | Role |
|---|---|
| `TeamTalkReactions` | Score→severity mapping + display helpers (labels, flavour lines, colours, summary). No reaction table of its own — it defers to the discussion system. |
| `TeamTalkController.DeliverTalk(response, severity)` | For every squad player: `ReactionTable[(response, personality)]` → `Event.ReactionSeverityChange` → `Person.NewMorale`. Marks the talk used, returns the reactions. |
| `PlayerMoraleBoxUI` | One squad-member tile (name + morale bar(s); flashes the ±delta and a reaction tint). |
| `TeamTalkUI` | The screen: arch layout, response buttons, reads the scoreline, delivers, shows feedback. |

## The reaction model (shared with discussions — no duplicate logic)
- **Who reacts how:** `EventsManager.CreateReactionTable()` — the same `(Event.Response, Personality) → Event.Reaction`
  table the 1-on-1 discussions use. Edit reactions there and both systems change together. (e.g. Rage → Aggressive =
  Great, Shy = Terrible, Lazy = Amazing.)
- **Severity from the score:** `TeamTalkReactions.SeverityFromScore(goalDifference)` maps the player team's half-time
  margin to `EventType.Severity`: `+1 → Pleasant`, `+2/3 → Uplifting`, `+4 → Momentous`, `0 → Irrelevant`,
  `−1 → Unfortunate`, `−2/3 → Pressing`, `−4 → Dire`. Severity then bends each reaction via
  `Event.ReactionSeverityChange` (e.g. *Challenge/Persuade* backfire when you're behind; *Praise* lands better when ahead).
- **Morale applied:** `Person.NewMorale(0, reaction, severity)` — the identical call a discussion makes (mutates +
  clamps mood/passion, returns the delta). So there's no separate team-talk morale maths to keep in sync.

Net effect: the same response reads the room differently. *Rage* while 3-down (Pressing) is a gamble that fires up
your Aggressive/Driven players and crushes the timid ones; *Praise* while 1-up (Pleasant) is safe but flat.

## 1. Player box prefab — `PlayerMoraleBox`
A small tile (keep it ~56–70 px wide so a 21–25-man squad fits the arch). Add `PlayerMoraleBoxUI` and assign any of:
- **Name Text** (TMP) — shows the surname.
- **Mood Fill** (an `Image` set to *Image Type = Filled*) — the morale bar; auto-coloured green/amber/red.
- **Passion Fill** (optional second filled `Image`).
- **Morale Text** (optional numeric).
- **Reaction Text** (optional TMP) — flashes "+8" / "−5" after the talk.
- **Background** (optional `Image`) — tinted by the reaction (green = Great/Amazing, red = Bad/Terrible).
All are optional; a name + Mood Fill is enough.

## 2. Response button prefab
A `Button` with a child **TMP label** (the label text is set automatically to the response name). No script needed.

## 3. The Team Talk panel — `TeamTalkUI`
1. Create a full-screen panel GameObject (start it **inactive**), add `TeamTalkUI`.
2. **Squad arch:** assign **Box Prefab** (§1) and **Box Container** — a `RectTransform` anchored to the **top-centre**
   of the screen. Tune **Arc Width** (~ usable screen width, e.g. 1600), **Arc Depth** (how far the ends dip — small
   for a *very shallow* ∩, e.g. 120), **Top Margin** (gap from the top edge to the centre box). Boxes spread evenly
   left→right; the middle sits highest and the ends dip → the shallow "∩".
3. **Response buttons:** assign **Response Button Prefab** (§2) and **Response Button Container** (give it a Horizontal
   Layout Group). The eight entries in the **Responses** list are built into buttons automatically (reorder/trim to taste).
4. **Feedback:** assign **Title Text**, **Situation Text** (shows e.g. "Half-time — 1 up — Pleasant"), **Flavour Text**
   (the line you "said"), and **Overall Text** (the verdict).
5. Add a **Close/Done** button on the panel → hook its OnClick to `TeamTalkUI.Close()`.

## 4. Hook it into half-time
On the match screen's **Half-Time Panel** (the `_halfTimePanel` on `MatchSimPageUI`), the **Team Talk** button's
OnClick → `TeamTalkUI.Open()`. `Open()` reads the live scoreline via `MatchSimPageUI.CurrentGoalDifferenceForMyTeam()`
to set the severity, so no extra wiring is needed. (`TeamTalkController` lives on the managers object and is reset at
kickoff by `MatchSimPageUI.SimMatch()`, so the one-talk-per-match guard just works.)

## Behaviour & notes
- **One per match:** `DeliverTalk` sets `TeamTalkController.Used`; the response buttons disable after you deliver and
  re-enable next match. Close without delivering and you can come back to it.
- **Visual feedback:** every box flashes its reaction colour + ±delta and updates its bar; the overall line reads e.g.
  *"The dressing room is buzzing! (14 lifted, 3 unhappy)"*.
- **Tuning lives in two places:** the per-personality reactions in `EventsManager.CreateReactionTable()` (shared with
  discussions) and the score→severity thresholds in `TeamTalkReactions.SeverityFromScore`.
- The microgame classes (`GerrymanderGame`, `BalanceGame`, `TeamTalkController.CreateRandom/ApplyResult`) are left in
  place but unused; delete or revive later.
