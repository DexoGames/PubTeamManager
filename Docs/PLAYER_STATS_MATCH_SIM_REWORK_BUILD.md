# Player Stats + Match Phase Rework — Build and Tuning Guide

Status: implemented in code. No new scene object, prefab, serialized reference, or Inspector hook is required.

## 1. Player stat schema

Players now have 20 skills, shown in this order:

1. Technical: Shooting, Passing, Tackling, Technique, Dribbling, Crossing, Heading.
2. Mental: Positioning, Creativity, Intelligence, Marking, Aggression, Composure, Teamwork.
3. Physical: Strength, Agility, Pace, Stamina, Jumping, Durability.

Height remains a separately stored 0–100 body measurement and is not in the skill array or training Boost array.
`Player.HeightToCm()` still maps it to 155–198 cm for display.

`PlayerStat` values are serialized into Unity assets, so the pre-existing skill IDs remain unchanged. Technique and
Marking occupy the two new skill slots (18 and 19); Height moved from enum value 18 to 20. `SkillDisplayOrder` gives
the human-facing order above instead of relying on enum IDs.

### Old-save migration

`Player.EnsureStatData()` expands an old 18-value `RawStats.Skills` array to 20 the first time it is loaded/read.
It copies every old value without remapping, then deterministically seeds:

- Technique = 40% Dribbling + 35% Passing + 15% Shooting + 10% Agility.
- Marking = 40% Positioning + 35% Tackling + 15% Intelligence + 10% Teamwork.

Legacy Boost arrays are expanded with the original 18 boosts preserved and zero Boost on the two new skills. The
migration runs explicitly in `TeamConverter` and `RecruitmentManager.RestoreFreeAgentsFromState`, with a defensive
guard inside the player stat pipeline as well.

The three authored tactic dependencies that meant Height (`LowBlock`, `LobIt`, `HoofItLong`) now serialize value
20. All other serialized `PlayerStat` values retain their original meaning.

## 2. Generated player profiles

Generation is correlated, but only at creation time. No permanent links exist between stats after the player has
been made.

The generator builds:

- one lightly bell-shaped overall aptitude;
- technical, mental and physical domain aptitudes that partly inherit it;
- a still-dominant independent roll for every skill;
- one simultaneous relationship pass over the untouched seed values.

Because the relationship pass is simultaneous, the relation list order cannot cascade or repeatedly smooth a
profile. A stat is pulled at most 42% toward the weighted average of its neighbours. The strongest links are
Technique↔Dribbling, Positioning↔Marking, Stamina↔Durability, Heading↔Jumping, Pace↔Agility and
Marking↔Tackling. Weaker links connect related but distinct abilities such as Creativity↔Passing,
Shooting↔Composure and Strength↔Durability. Personality modifiers run afterwards and can deliberately create an
exception to the generated shape.

Generation tuning lives together in `Player.GenerateCorrelatedSkills` and `GenerationRelations`.

## 3. Derived ratings

The UI/team-summary composites were updated so both new skills matter:

- Attacking centres Shooting, then Technique, Composure, Dribbling, Positioning, Creativity and movement/aerial
  support.
- Midfield centres Passing, then Technique, Intelligence, Creativity, Teamwork and Positioning.
- Defending centres Marking, then Positioning, Tackling, Intelligence and physical/aerial support.
- Mental is the rounded average of all seven mental skills.
- Physical is the rounded average of the six physical skills; Height is deliberately not treated as ability.
- Goalkeeping is still derived because there is no keeper-only skill. It uses Positioning and Agility most, then
  Composure, Jumping, Intelligence, Height and Technique.

Shot execution also uses Technique. Headers combine Heading, Jumping, Height, Strength and Positioning; solo and
stylish attempts use the appropriate Technique/Dribbling/Agility mix. Assist selection now combines Passing,
Creativity, Technique, Crossing and Teamwork.

## 4. Group aggregation

`SquadStatProfile` is now the single effective-group seam. Before collecting samples it applies the existing
off-position, Boost, morale, complexity, chemistry and reliance rules. Reliance still adds weight only to its named
stats and only in eligible phase bands.

Each phase can then choose one of six reductions independently per stat:

| Mode | Formula | Intended use |
|---|---|---|
| Mean | reliance-weighted arithmetic mean | genuinely additive effort or a neutral baseline |
| Cohesive | 60% weighted mean + 40% weighted median | organised/unit behaviours; resists one freak outlier |
| Star-influenced | 65% Cohesive + 35% maximum | one elite creator, distributor or runner can change a move |
| Weak-link-influenced | 65% Cohesive + 35% minimum | one pressable defender or slow recovery runner can be targeted |
| Best-pair-influenced | 55% Cohesive + 45% best-two mean | actions usually performed by a small specialist partnership |
| Weakest-pair-influenced | 55% Cohesive + 45% worst-two mean | future use for systems needing more than one failure point |

The ordinary `AverageStats()` API remains for UI/team summaries, but delegates to this same profile and returns its
weighted arithmetic mean. There is no duplicate complexity/chemistry/reliance implementation.

## 5. Phase responsibilities

Abbreviations below: C = Cohesive, M = Mean, S = Star-influenced, W = Weak-link-influenced, B = Best-pair-influenced.
Numbers are within-side weights and therefore sum to approximately 1 per side.

### Build

- Ball side: goalkeeper + defence. Passing .26 S, Technique .20 W, Composure .17 W, Intelligence .14 C,
  Teamwork .10 C, Positioning .08 C, Agility .05 M.
- Pressing side: midfield + attack. Positioning .20 C, Marking .18 C, Intelligence .15 C, Pace .14 S,
  Stamina .10 C, Aggression .09 M, Tackling .08 B, Teamwork .06 C.
- Meaning: the best distributor can open the press, but its weakest receiver/composure point remains targetable.

### Progress

- Ball side: midfield. Passing .23 C, Technique .18 C, Intelligence .14 C, Positioning .12 C, Teamwork .10 C,
  Dribbling .09 S, Creativity .08 S, Composure .06 W.
- Defending side: midfield. Marking .21 C, Positioning .19 C, Intelligence .15 C, Tackling .14 B,
  Teamwork .11 C, Agility .08 C, Stamina .07 C, Aggression .05 M.
- Meaning: circulation is a unit task, while one carrier or inventive midfielder can advance it.

### Probe

- Ball side: midfield + attack. Creativity .20 S, Intelligence .16 C, Technique .15 C, Passing .13 C,
  Composure .11 C, Positioning .10 B, Dribbling .08 B, Crossing .04 S, Teamwork .03 C.
- Defending side: defence. Marking .22 C, Positioning .21 C, Intelligence .15 C, Teamwork .13 C,
  Composure .09 C, Tackling .08 B, Agility .05 C, Heading .04 C, Aggression .03 M.
- Meaning: a chief creator matters, but the move still needs enough collective execution around the box.

### Advance

- Ball side: midfield + attack. Pace .21 S, Dribbling .16 B, Technique .14 C, Passing .12 C,
  Creativity .11 S, Crossing .09 S, Agility .08 B, Teamwork .05 C, Stamina .04 C.
- Defending side: defence + midfield. Pace .21 W, Positioning .19 C, Marking .17 C, Tackling .14 B,
  Agility .10 W, Teamwork .08 C, Stamina .06 C, Strength .05 C.
- Meaning: this is the explicit fastest-attacker versus slowest/least-agile recovery defender matchup.

### Penetrate

- Ball side: midfield + attack. Technique .15 C, Dribbling .15 B, Creativity .14 S, Passing .11 C,
  Crossing .10 S, Positioning .10 B, Composure .09 C, Pace .07 S, Heading .05 B, Teamwork .04 C.
- Defending side: defence. Marking .19 C, Positioning .18 C, Tackling .15 B, Strength .10 C,
  Agility .10 C, Teamwork .09 C, Composure .06 C, Jumping .05 B, Heading .05 B, Height .03 B.
- Meaning: final-ball variety meets both ground defending and the back line's best aerial partnership.

### Counter

- Breakout side: defence + midfield. Passing .20 S, Technique .15 C, Intelligence .14 C, Creativity .13 S,
  Pace .12 S, Dribbling .10 B, Composure .08 W, Teamwork .05 C, Stamina .03 C.
- Counter-press side: midfield + attack. Pace .20 W, Marking .18 C, Positioning .16 C, Tackling .13 B,
  Stamina .11 C, Aggression .08 M, Teamwork .08 C, Intelligence .06 C.
- Meaning: one excellent release pass can start the break; one slow presser can leave the exit open.

### Break

- Running side: attack. Pace .23 S, Dribbling .16 B, Technique .14 C, Composure .12 C, Passing .10 C,
  Creativity .09 S, Positioning .08 B, Agility .05 B, Stamina .03 C.
- Recovery side: defence. Pace .23 W, Positioning .19 C, Marking .17 W, Tackling .13 B, Composure .08 W,
  Strength .07 C, Agility .06 W, Teamwork .04 C, Heading .03 C.
- Meaning: the quickest attacking outlet has leverage, while the weakest recovery defender can be isolated.

## 6. Resolution corrections

- Phase ability/tactic aggregates remain floats until resolution instead of being truncated to integers.
- Stability-driven randomness is now symmetric: it uses the two teams' average Stability. Previously the home and
  away terms had different divisors, creating an unintended home/away asymmetry.
- A failed phase now measures distance below `FailThreshold` correctly (`FailThreshold - score`). The previous
  `score + FailThreshold` expression exaggerated every failure once the threshold was negative.
- The debug phase name for Penetrate is now `Penetrate`, not `Strike`.

## 7. Connected systems

- Two catalog drills were added without renumbering persisted `DrillId`s: Technique Sessions and Marking Practice.
- Low Mood now affects Technique and Marking. Durability and Height remain immune to momentary form/morale changes.
- Tactical complexity can penalise Marking, because it is a mental execution skill; Intelligence remains the gate
  and is not recursively penalised.
- Chemistry can now select Technique or Marking among its combination-stat effects.
- Player details and interview stat questions use `SkillDisplayOrder`.

## 8. Editor verification checklist

- Start a new game and inspect several players: all 20 skills appear in the intended category order; Height remains
  in its existing separate label.
- Load a pre-rework save: no existing values move; Technique/Marking are plausible; training Boost remains intact.
- Confirm Technique Sessions and Marking Practice appear automatically in the training list and build/decay Boost.
- Compare an otherwise equal attack with one very fast attacker: Advance/Break performance should improve, but not
  as much as making the whole unit fast.
- Compare a defence with one very slow defender: Advance/Break defence should deteriorate more than its plain mean.
- Give one player an extreme Teamwork/Positioning value: cohesive phase values should move less than a plain mean.
- Run `TacticLab` with paired seeds before final balance tuning; phase weights are first-pass design values, not a
  claim of final goal-rate calibration.
