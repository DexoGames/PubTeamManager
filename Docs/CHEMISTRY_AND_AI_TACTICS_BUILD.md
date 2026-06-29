# Chemistry & CPU penalty exemptions — build / wiring notes

Two systems. **The C# is complete and compiles.** There's almost **no scene wiring** — the only editor step is
confirming three new ScriptableObject assets imported. Everything else is data-driven.

---

## 1. CPU penalty exemptions

**The idea (replaces the old "AI auto-simplifies its tactic" approach).** Several "technical" penalties exist to
punish the **human** for mismanagement they have *tools* to avoid. The AI doesn't use those tools, so applying the
same penalties to it just makes AI sides randomly bad — and the game too easy. Rather than have the AI cleverly
work around them, **CPU-controlled teams are simply exempt** from them. It doesn't matter that the AI isn't
"really" executing the tactic — only the visible result matters.

**Infrastructure.** `Team.IsCpuControlled` (and `IsHumanControlled`) — true for every team except the human's
club. It's **false in headless tools with no human team** (e.g. `TacticLab`), so balancing analysis still sees the
full mechanics. Backed by the null-safe `TeamManager.HumanTeam`. Adding a new exemption is a one-liner: gate the
penalty site on `team.IsCpuControlled`.

**Exemptions applied:**

| Penalty | Where it's gated | Why the human, not the AI, should eat it |
|---|---|---|
| Complexity / squad-IQ cut | `Tactic.ShouldApplyComplexityPenalty` | Human dodges it by picking smart players or a simpler tactic. |
| Morale stat penalty | `Player.GetStats` (skips `MoraleModifier`) | Human manages morale via team talks / discussions; AI never does. |
| Unfamiliarity early mistakes | `MatchEngine.StartingPhase` (CPU treated as fully familiar) | Human drills familiarity through training; the EXTRA mistakes from a low-familiarity AI side are dropped (base mistake rate still applies to all). |

**No scene wiring required.** All three run inside the existing match/stat path.

### The three new instruction assets — now just normal player options

`KeepItSimple` / `HoldPositions` / `CutOutRisks` were originally authored as complexity-*reducers* for the AI.
That AI logic is gone, but the assets remain as ordinary tactical instructions — they auto-appear in the player's
tactics screen (the UI loads every instruction in `Resources/Tactics/Instructions`) and each carries a real
trade-off, so they're useful **player** content (a way for a human with a dim squad to cut Complexity):

| Asset | tacticName | Complexity | Other mods (the trade-off) |
|---|---|---|---|
| `KeepItSimple.asset` | Keep It Simple | **−15** | Creativity −10, Threat −5, Stability +8 |
| `HoldPositions.asset` | Hold Your Positions | **−12** | Security +6, Stability +5, Creativity −8, AttackingWidth −5 |
| `CutOutRisks.asset` | Cut Out the Risks | **−10** | Stability +6, Creativity −6, Threat −6 |

**EDITOR STEP — verify import:** confirm the three show up as **TacticInstruction** in the Project window. If any
shows as a broken/Missing-script asset, right-click → Reimport. **If you'd rather not keep them at all, just delete
the six files** (`.asset` + `.asset.meta`) — nothing in code references them by name anymore.

### Further exemptions considered (NOT applied — your call)
Each is a one-liner against `Team.IsCpuControlled` if you want it:
- **Off-position penalty** — only bites a CPU side when an injury auto-sub forces someone out of position; arguably
  realistic, so left on. Could floor it for CPU if depleted AI teams collapse too hard.
- **Reliance weakness-amplification** — minor for CPU (it auto-binds the reliance to its *best* eligible player), so
  the downside is already small; left on.
- **Negative chemistry** (see §2) — mild and symmetric across the league, so it doesn't systematically weaken the
  AI; left on. Could exempt CPU from sub-Neutral links only if you want AI form to be steadier still.

---

## 2. Player chemistry

**What it does.** Every pair of players has a hidden `ChemistryLevel`:

| Level | Meaning | In-match effect (per affected stat) |
|---|---|---|
| `BadBlood` | very awkward (rarest) | **−10** |
| `Frosty` | awkward | **−5** |
| `Neutral` | the common case (~74%) | none |
| `InSync` | complementary | **+5** |

The level is **derived deterministically** by hashing the two players' PersonIDs (the same trick `KitColors` uses
for kits) — symmetric, stable across save/load, **nothing to serialize**, most pairs Neutral.

It only matters **in a match**, and only between two **starters in linked positions** (`Chemistry.ArePositionsLinked`:
same role, or adjacency like LB/LM, LB/LW, RW/ST, AM/CM, DM/CM, CB/CB…). Such a pair shifts a deterministic subset
(3) of both players' **combination stats** — Passing / Teamwork / Positioning / Creativity / Composure — by the
amount above. Resolved once at kickoff (`Tactic.ComputeChemistry`, cached per slot via `ChemistryDelta`) and folded
into effective ability in `PlayerExtensions.AverageStats`.

**Trade-off (per the design rule):** chemistry is hidden and you don't choose it. To *get* a boost you must field
the two players next to each other in linked roles; to *avoid* a clash you must split clashing players up — either
way it constrains selection, costing you freedom elsewhere. The extra tier is on the **negative** side on purpose
(downside can bite harder than the upside rewards).

**No scene wiring required.** It runs entirely inside the existing match path.

### Tuning (all consts at the top of `Assets/_Scripts/DataModels/Chemistry.cs`)
- `BAD_BLOOD_CHANCE 0.04`, `FROSTY_CHANCE 0.11`, `IN_SYNC_CHANCE 0.11` — the rest is Neutral. Raise/lower for more
  or fewer special links.
- `STAT_STEP 5` — magnitude per level (so BadBlood = −10).
- `AFFECTED_STAT_COUNT 3` — how many of the 5 link-stats each pairing moves ("some, not all").
- `LinkStats[]` — which stats chemistry touches.
- `ArePositionsLinked` adjacency table — which position pairs count as "playing together".

### Optional UI (future, not built)
There's no chemistry display yet. When you want one, `Player.GetChemistryWith(other)` returns the level and
`Chemistry.Label(level)` gives a short string ("Bad Blood" / "Frosty" / "In Sync"). A natural home is the
formation / squad screen: when a player is selected, tint or badge the team-mates he's currently **linked with**
(use `Chemistry.ArePositionsLinked` against the current formation slots to show only the ones that actually apply).
Pure renderer over existing logic — no new game state needed.
