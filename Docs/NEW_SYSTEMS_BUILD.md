# New Systems — remaining wiring & tuning reference

Covers the tactics overhaul (mentality, reliances, familiarity, complexity), in-match changes, injuries/
suspensions/death, and interview day.

**Status: mostly built.** `InjuryManager` is on the managers object, all four tactics-page widgets
(`MentalitySelectorUI`, `FamiliaritySlider`, `TacticInfoUI`, `ComplexitySlider`) are placed, the half-time panel is
wired, the shared `PlayerPickerPopup` prefab is in Resources, three reliance instructions are authored, and the
training slots exist.

> **Design rule baked into all of it:** every edge carries a matching risk. A reliance amplifies a player's
> weaknesses as well as his strengths; complex tactics need intelligent players; changing your setup burns
> Familiarity; a botched team talk *lowers* morale.

---

## Remaining work

### 1. 🟠 `InjuryManager ▸ Illness Event` is unassigned

`injuryEvent`, `deathEvent` and `notificationPrefab` are all assigned; **`illnessEvent` is null**. `RaiseEvent`
null-checks and returns, so illnesses fire and silently generate **no inbox event and no notification** — the whole
category is invisible to the player.

Create an `IllnessEvent` via **Assets → Create → EventType** under `Resources/Events/` and assign it:

| Field | Suggested |
|---|---|
| tag | Special |
| description | `<1> has come down ill and will miss some time.` |
| severity | Unfortunate |
| moodChange | −3 |
| noAffected | 1 |

Keep `moodChange` small (0 to −3): `InjuryManager.ApplyInjuryMorale` already applies a severity-scaled shock
(a **Knock** nicks the player's own mood; **Standard/Hamstring** hits him more with a small squad ripple; an **ACL**
hits hard and visibly dents the squad; a **death** rocks the team). The EventType's own `moodChange` stacks on top,
so a large value double-counts the victim.

### 2. 🟠 Availability isn't shown anywhere in the squad UI

`Player` exposes `IsAvailable`, `IsInjured`, `IsSuspended`, `IsDeceased` and `AvailabilityStatus` (e.g.
`"Injured (Hamstring, back ~12 Jun)"`, `"Suspended (2)"`, `"Deceased"`) — nothing reads them.

Extend `SquadStatusWidget.Setup()` (which already flags fatigue and low morale) to list unavailable players via
`team.AvailablePlayers` / `team.UnavailableStarters`, and/or add a TMP to `PlayerRowUI` reading
`player.AvailabilityStatus`, tinted red when `!player.IsAvailable`.

> Note the widget itself still isn't on the Home page — see [`HOME_SCREEN_BUILD.md`](HOME_SCREEN_BUILD.md).
>
> Lineups auto-fix regardless: `Team.EnsureAvailableLineup()` runs before every match and benches anyone who can't
> play, swapping in the best-rated available sub. Deceased players stay in the squad list until released — a
> "Release" affordance would be worth adding.

### 3. 🟠 Release-before-hire flow

Squad cap is `RecruitmentManager.MAX_SQUAD_SIZE` (25). When full, **Hire** is blocked and the page prints a
"release a player first" message — with no way to do it from that page. It's a dead end.

`RecruitmentManager.HirePlayerReplacing(candidate, playerToRelease)` exists and is called by nothing. Reuse the
shared picker: list `TeamManager.Instance.MyTeam.Players` through `PlayerPickerPopup.Show`, then call it. That's
the whole feature.

### 4. ⚪ Optional: "More Attacking / More Defensive" buttons

`MatchSimPageUI.MoreAttacking()` / `MoreDefensive()` are hooked nowhere — the in-match **mentality slider** covers
the same ground and is wired. Add the two buttons only if you want one-tap nudges alongside the slider.

### Also outstanding, tracked elsewhere

| Item | Doc |
|---|---|
| `TacticsPageUI.ResumeMatch` hook (half-time is a one-way trip) | `MATCH_SIM_AND_TEAM_TALK_BUILD.md` |
| Team-talk Close button, second `ResponseManager`, bubble background | `MATCH_SIM_AND_TEAM_TALK_BUILD.md`, `HALFTIME_TEAM_TALK_REWORK_BUILD.md` |
| Three missing interview questions; 5th training slot | `UI_AUDIT_AND_LAYOUT_PLAN.md` §8 |

---

## Authoring reference — reliances

A **reliance is a property of a `TacticInstruction` asset**, not a separate component. Three are authored so far
(`BuildupAround` → Defenders, `FocusWideOn` → WidePlayers, `FreeRole` → MidfieldAndAttack); this is how to add more.

An instruction *becomes* a reliance simply by filling in **Reliance → Eligible Groups** — there's no flag
(`hasReliance` is derived: any group ⇒ reliance, none ⇒ plain instruction). Under **Reliance** set:

- **Eligible Groups** (`PlayerGroup[]`) — does three things: **filters the picker**, **auto-disables** the
  instruction if the player later moves out of all of them, and **scopes which phases of play the boost applies
  to**. The engine resolves position-band phases (build-up ≈ defenders, finishing ≈ attackers, transitions ≈ the
  mid+def / mid+att unions), and the reliance only amplifies its stats in a phase whose band sits *within* these
  groups. So a `Defenders`-only reliance lifts the build-up but not the finish; pick `MidfieldAndAttack` /
  `Outfield` for one felt further up. (`Goalkeeper` has no match phase — a keeper-only reliance won't bite.)
- **Stats** — which of the reliant player's stats count more toward the team (Passing/Creativity for a playmaker;
  Heading/Strength for a target man).
- **Multiplier** — extra weight on those stats (`1` = he counts double for them, `2` = triple).
- **Familiarity Penalty** (0–1) — how hard *changing* the reliant player (a sub/swap) dents familiarity. `0` = a
  minor reliance; higher = the instruction must be re-drilled.
- Add normal `statModifications` too (e.g. +Complexity) so leaning on a player also raises the squad-IQ demand.

Then add the asset to `TacticGridLayout.tactics` like any instruction. Toggling it on opens the picker (eligible
starters only); the toggle then reads "Name: Surname". The binding is by **squad slot**, so it follows substitutions
and survives formation changes. `Tactic.RefreshReliances()` runs on every tactic/lineup/formation change and at
match start to auto-bind AI/default reliances, apply the familiarity penalty when the slot's player changes, and
auto-disable any reliance whose player has moved into a disallowed group.

> Old assets still carry a stale `reliances: []` array from a previous schema (the field is now singular,
> `reliance`). Unity ignores it; it'll disappear the next time each asset is re-saved.

## Authoring reference — instructions & synergies

New instructions: **Assets → Create → Tactic → Instruction** in `Resources/Tactics/Instructions/`, set
`statModifications` / `tacticDependencies` / `incompatibleInstructions`, then add to `TacticGridLayout.tactics`.

**Synergies (`complementaryInstructions`)** — the positive mirror of `incompatibleInstructions`. Each entry is a
**partner instruction** + a list of **tactic-stat perks**. When *both* the owner and the partner are active, the
owner's perks are added (step "1b" of `RecalculateStats`, after base instruction stats, before mentality). It's
**one-directional** — list the synergy on both assets if you want each to perk the other. Only `FocusWideOn`
currently authors one, so this is largely untapped content.

Keep the design rule in mind: a perk is an upside, so pair it with an authored downside — e.g. High Press + High
Line gains Threat, but the High Line already carries a Security cost.

## Reference — the shared player picker

`PlayerPickerPopup` is **instantiated on demand** from `Assets/Resources/UI/PlayerPickerPopUp.prefab` — no scene
object. `Show(candidates, onPicked, title, describe, onCancelled)` builds it, parents it to the root canvas, forces
`sortingOrder = short.MaxValue` with its own `GraphicRaycaster` so its clicks win over page sub-canvases, and
destroys it on pick/cancel.

> Minor: the const is `PrefabResourcePath = "UI/PlayerPickerPopup"` but the asset is `PlayerPickerPopUp.prefab`
> (capital U). `Resources.Load` tolerates this in-editor; rename one of them before shipping to a case-sensitive
> platform.

Used by the reliance picker, the training slots, and the interview comparison question. Any new "choose one player"
flow should call it rather than building another list.

---

## Tuning cheat-sheet (where the numbers live)

| Lever | File | Notes |
|---|---|---|
| Mentality stat shifts | `Tactic.ApplyMentality` | per-step Threat/Security/etc. |
| Formation → tactic stats (base) + Tempo | each formation asset's `FormStats` (`Resources/Formations/Usable/*.asset`), consumed by `Tactic.ApplyFormationBase` | formation base-replaces Complexity/Intensity/Control/Threat/Security; `Tempo` drives Build-vs-direct in `MatchEngine.StartingPhase` (high = direct) |
| Familiarity learn rate (≈3 months → 100%) | `Tactic.FAMILIARITY_LEARN_RATE` (0.12) | raise = faster |
| Formation's share of Familiarity (≈40% drop on a fresh switch) | `Tactic.FORMATION_FAMILIARITY_SHARE` (0.4) | rest comes from instructions |
| Familiarity → mistakes | `MatchEngine.StartingPhase` | `mistakeThreshold` |
| Intelligence bar (= Complexity) & penalty | `Tactic.IntelligenceThreshold` / `ShouldApplyComplexityPenalty` / `ComplexityPenaltyFraction` (`INTELLIGENCE_PENALTY_PER_POINT` 0.05, cap 0.9) | penalty only if the **XI average** is below the bar; proportional cut ≈50% at 10 below, applied to `Tactic.ComplexityAffectedStats` |
| Reliance (groups, stats, multiplier, familiarity penalty) | the `reliance` on a `TacticInstruction` | see the authoring reference above |
| Instruction synergies | `TacticInstruction.complementaryInstructions` | perks granted when a named partner is also active |
| Foul/card/injury rates | `MatchEngine.TryFoul/DecideCard/DecideInjury` | |
| Injury durations | `Player.InjuryDurationDays` | |
| Injury morale impact (per severity) | `InjuryManager.ApplyInjuryMorale` | victim vs teammate bands |
| Off-pitch injury/death rate | `InjuryManager.dailyMisfortuneChance` (0.02) + the severity split in `RollOffPitchMisfortune` | |
| Yellow-card ban threshold | `Player.YELLOW_SUSPENSION_THRESHOLD` (5) | |
| Team-talk reactions / severity | `EventsManager.CreateReactionTable` (shared with discussions) + `TeamTalkReactions.SeverityFromScore` | |
| Interview answer buckets / compare bands | `InterviewAnswerGenerator` (per-`AnswerX` bucket arrays; `GenerateComparisonAnswer` ±18/±45 bands, ±12 bias) | |
| Big-miss threshold | `StatLeaderboards.BigMissXg` (0.5) | xG at/above which an unscored shot is a "big miss" |
| Interview cadence | `ScheduleManager.INTERVIEW_INTERVAL` (14) | fortnightly, fixed cadence, its own day |
| Squad cap | `RecruitmentManager.MAX_SQUAD_SIZE` (25) | |
| Endless-interview debug | `RecruitmentManager.debugEndlessInterviews` | every day becomes an interview day, unlimited sessions |

## Test checklist

- **Tactics:** change mentality → stat sliders + `TacticInfoUI` move. Toggle an instruction on/off repeatedly →
  Familiarity dips while the new state differs from the team's habit and **recovers when you toggle back** (it must
  not keep dropping). Familiarity climbs over matches/training, ~3 months to ~100%.
- Add instructions → Complexity rises → `ComplexitySlider` turns red and the warning label appears only when the
  XI's **average** intelligence is below the bar; a smart XI clears it even with one dim player.
- **Reliance:** toggle `BuildupAround` → picker opens filtered to eligible starters → pick → toggle reads
  "Name: Surname" and Complexity rises. Cancel the picker → toggle flips back off.
- **In-match:** mentality slider → change takes effect next minute with a feed line. Half-time → Edit Tactics →
  structural change → Resume Match → second half uses it, no restart. *(Blocked until the ResumeMatch hook is made.)*
- **Injuries:** watch for foul/booking/injury lines in the feed; a knock barely dents morale, a long injury hits the
  squad. Check a booked/injured player's `AvailabilityStatus`; confirm a suspended player is benched next match.
  Advance several days → expect the occasional off-pitch injury notification. *(Illness stays silent until §1.)*
- **Interviews:** on an interview day → ask personality questions → watch "Personality: X / Y" narrow. Fill the
  squad to 25 → Hire is blocked. *(Only 2 of the 4 personality probes have buttons — see `UI_AUDIT` §5.1.)*
- **Training:** pick a Positional drill → slots appear; click an empty slot → picker shows the squad minus already-
  slotted players with each one's strength/rating at the chosen position; X clears a slot; "Set Training" saves.
