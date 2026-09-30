# Morale → stats: the maths

How a player's `Morale` (Mood + Passion, each 0–100) bends their in-match stats. All logic is in
`Assets/_Scripts/Utilities/Simulation/MoraleEffects.cs` and is applied in `Player.GetStats`. Every team is subject to it
(the AI included). Each player has a **personality-specific ideal** (`Person.IdealMorale`, ±5 random jitter at
creation) — morale is measured as the signed gap to that ideal, so "good morale" means *near your ideal*, not
"high numbers".

## Symbols
- `Pᵢ`, `Mᵢ` = ideal passion / mood. `P`, `M` = current passion / mood.
- Passion shortfall `Sₚ = max(0, Pᵢ − P)`; mood shortfall `S_m = max(0, Mᵢ − M)`.
- Over-ideal excess `E = max(0, M − Mᵢ) + max(0, P − Pᵢ)`.

## 1. Constant drop-off (every frame stats are read), only when BELOW ideal

```
passion drop per stat  fₚ = ceil(Sₚ / 3)     → applied to PassionStats (8)
mood drop    per stat  f_m = ceil(S_m / 5)    → applied to MoodStats   (11)  [in a slump, M is treated as 0]
```

Passion is the **stronger** drop (÷3); mood is **weaker** (÷5); both use `ceil`, so any shortfall ≥1 costs at
least −1. Two coherent groups (Height & Durability never move):

| Group | Driven by | Stats |
|---|---|---|
| **PassionStats** (physical / intensity) | Passion below ideal | Pace, Stamina, Strength, Jumping, Agility, Tackling, Heading, Aggression |
| **MoodStats** (technical / mental / confidence) | Mood below ideal | Passing, Shooting, Technique, Dribbling, Crossing, Positioning, Creativity, Intelligence, Marking, Composure, Teamwork |

Per-stat drop as a function of shortfall:

| shortfall | passion drop `ceil(÷3)` | mood drop `ceil(÷5)` |
|--:|--:|--:|
| 5  | −2 | −1 |
| 10 | −4 | −2 |
| 20 | −7 | −4 |
| 30 | −10 | −6 |
| 40 | −14 | −8 |
| 50 | −17 | −10 |
| 60 | −20 | −12 |
| 80 | −27 | −16 |

**Above** ideal in a dimension contributes **nothing** here — it feeds the blunder roll (§3) instead.

## 2. Slump (the persistent "rut")

While **mood is below ideal** and the player isn't already slumping, each minute:

```
slumpChance/min = min(0.02, S_m × 0.00023)      (× 0.5 in the second half)
```

A roll happens **once per `SimulateMinute`, ≈35×/half**, so it compounds: `P(slump by HT) = 1 − (1 − p)^35`.
The rate is **calibrated to the reference point "mood 20 below ideal ⇒ ≈15% in the first half"**:

| S_m (mood below ideal) | chance/min | P(slump by HT) |
|--:|--:|--:|
| 10 | 0.23% | ≈ 8% |
| **20** | **0.46%** | **≈ 15%** ← reference |
| 30 | 0.69% | ≈ 22% |
| 40 | 0.92% | ≈ 28% |
| 50 | 1.15% | ≈ 33% |
| ≥87 | 2.0% (cap) | ≈ 51% |

(The second half halves the per-minute chance, so a player who survived the first half is correspondingly safer.)

Once slumped, **mood is treated as 0** for the constant drop (so `f_m = floor(Mᵢ / 8)` — the full-ideal hit), for
the rest of the game. Recovery: **CPU teams clear all slumps at half-time** (their manager "sorts them out"); the
**human** clears them only via a half-time team talk — `MoraleEffects.SlumpRecoveryChance` per player by how well
they took it (Amazing 90% → Great 70% → Good 45% → Neutral 15% → none).

## 3. Blunder (one-minute collapse), the cost of being TOO happy/pumped

Rolled **every minute for every on-pitch player**:

```
blunderChance/min = lerp(0.01, 0.20, clamp01(E / 60))
```

When it fires, that player's stats are ×0.4 (a 60% collapse) **for that single minute** — a moment of
complacency/over-eagerness. ~68 rolls/game, so the aggregate matters:

| E (combined over-ideal) | chance/min | expected blunder-minutes/game | P(≥1/game) |
|--:|--:|--:|--:|
| 0 (at/below ideal) | 1.0% | 0.7 | ≈ 50% |
| 15 | 5.8% | 3.9 | ≈ 98% |
| 30 | 10.5% | 7.1 | ~100% |
| ≥60 | 20% (cap) | 13.6 | ~100% |

> Note the baseline: even a perfectly-in-form squad averages ~0.7 blunder-minutes per player per game (≈ half the
> XI has at least one). That's the intended "sprinkle of chaos", but if it feels too frequent, drop `BLUNDER_BASE`.

## 4. Worked examples (ideals shown without the ±5 jitter)

**A — Driven (ideal 80/80), M 60 / P 55.** Sₚ=25→**−9** to all PassionStats; S_m=20→**−4** to all MoodStats.
E=0 → blunder 1% (~0.7/game). Slump 0.46%/min → **≈15%/half** (the reference case). *Noticeably flat and low on
confidence, with a real-but-not-rife slump risk.*

**B — Calm (ideal 90/5), M 88 / P 55.** Passion is **50 above** ideal → no passion drop; mood ~at ideal → none.
E=50 → blunder **16.8%/min → ~11 blunder-minutes/game.** *On paper "calm and happy", but whipping a calm man up
to passion 55 makes him a liability — keep him serene.*

**C — Aggressive (ideal 25/95), M 55 / P 60.** Sₚ=35→**−12** to PassionStats; mood is 30 **above** ideal → no mood
drop. E=30 → blunder **10.5%/min (~7/game).** *Too content and under-pumped: he wants to be kept lean and angry —
low mood, high passion.*

**D — Smart (ideal 70/80), M 38 / P 76.** Sₚ=4→**−2**; S_m=32→**−7** to all MoodStats normally. Slump 0.74%/min →
~23%/half; **if he slumps, mood→0 → −14** to all 11 MoodStats (Passing/Technique/Creativity/Marking/…). *An
unhappy playmaker has a poor game; a slump makes it a shocker until a team talk lifts him.*

**E — Shy (ideal 100/70), M 70 / P 50.** Sₚ=20→**−7** PassionStats; S_m=30→**−6** MoodStats; slump 0.69%/min
(~22%/half). *Shy players have a very high mood ideal (100), so they're easily unhappy and slump-prone — they need
constant reassurance.*

**F — anyone at ideal.** No drops; blunder 1% (~0.7/game); no slump risk — peak form.

## 5. Tuning (all atop `MoraleEffects`)
`PASSION_DIVISOR 3`, `MOOD_DIVISOR 5` (drop = `ceil(shortfall/divisor)`) · `SLUMP_RISK_PER_POINT 0.00023`, `SLUMP_MAX_PER_MIN 0.02`,
`SLUMP_SECOND_HALF_FACTOR 0.5` (calibrated so mood-20-below ⇒ ≈15%/first half) · `BLUNDER_BASE 0.01`,
`BLUNDER_MAX 0.20`, `BLUNDER_EXCESS_FOR_MAX 60`, `BLUNDER_STAT_FACTOR 0.4` · affected stats = the `PassionStats` /
`MoodStats` arrays.
