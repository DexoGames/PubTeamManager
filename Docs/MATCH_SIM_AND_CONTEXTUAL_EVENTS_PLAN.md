# Match Simulation + Contextual Events — Future Plan

Status: design only. None of the systems in this file should be considered implemented by the player-stat/phase
rework. This is the recommended direction for the next prompt/pass.

The central recommendation is to make both matches and off-pitch events emerge from a shared record of what is
actually happening: who played, where, how well, under which tactic, in what physical/emotional state, and what the
manager previously did about it. Keep a small ambient-random layer for pub-league character, but stop using pure
random selection as the default source of drama.

## 1. Design rules

1. A player should usually be able to explain why an event became possible, even if its exact timing was random.
2. Randomness chooses among plausible outcomes; it should not invent a premise contradicted by the game state.
3. Every managerial option has a trade-off. Solving one person's concern can cost tactical strength, another
   player's minutes, manager credibility, squad harmony, training time or future flexibility.
4. Personality changes whether a concern is felt, voiced, delayed, framed publicly/privately and spread to others.
   It should not simply multiply every probability in the same direction.
5. Important events have memory, escalation and resolution. They are not disposable one-day text boxes.
6. The game should expose enough evidence for the manager to reason, while preserving uncertainty about hidden
   personality and relationships.
7. Pure logic remains testable outside Unity; ScriptableObjects author content, not core state transitions.

## 2. Further aggregation ideas worth experimenting with

The current `SquadStatProfile` supplies mean, median-resistant, max/min-influenced and top/bottom-pair shapes. The
following are candidates for A/B tests, not a request to add every formula. Prefer a small named vocabulary whose
gameplay meaning is legible.

### 2.1 Distribution reducers

- Trimmed mean: discard the highest and lowest sample before averaging. Good for large units where both extremes
  should be treated as noise; poor for a two-centre-back partnership where extremes matter.
- Winsorised mean: clamp extremes to the next sample rather than removing them. Retains headcount and is smoother
  when group sizes change between formations.
- Percentiles/quantiles: use P75 for "enough good runners" or P25 for "how targetable is this line". More stable
  than literal max/min and useful when a formation changes the group from three to five players.
- Generalised/power mean: a positive exponent rewards multiple high values; a negative exponent behaves like a
  bottleneck. One tunable family could replace several bespoke reducers, but names should hide the maths from UI.
- Geometric mean: punishes a very low component in a multi-stat action. Useful for a single player's "can execute
  this demanding action" check, less intuitive for a whole group.
- Harmonic mean: an even stronger bottleneck. Potential use for a coordinated offside line, but likely too harsh as
  a general team aggregate.
- Root-mean-square: makes high performers matter more without selecting the literal maximum. A smooth alternative
  for explosive pace/dribbling.
- Softmax/softmin: differentiable max/min with a temperature control. This is the best technical alternative if
  literal outliers make results jump too hard when one player is substituted.
- Median absolute deviation/variance term: reward reliable compact units and penalise a badly mismatched line.
  Variance must have context: variety can be good in attack and bad in coordinated defence.
- Threshold count: "how many players clear 65 Pace?" Sometimes coverage matters more than the average. This makes
  recruitment goals concrete, but hard thresholds need smoothing to avoid 64→65 cliffs.
- Top-k/bottom-k coverage: top two crossers, three recovery runners, weakest two markers. This scales better than a
  fixed max/min and maps naturally to roles.
- Capped star contribution: use the star only up to N points above the unit mean. Prevents one 99 from carrying a
  pub team while still making specialists valuable.
- Reliability-adjusted value: effective stat = expected value minus an uncertainty/error term derived from
  Composure, fatigue and familiarity. A brilliant but erratic player then has a wider result distribution rather
  than simply being worse.

### 2.2 Structure-aware reducers

- Role-weighted samples: the current position group can be refined into roles. The ball-playing centre-back's
  Passing should count more in Build, the covering centre-back's Pace more in recovery, without making positions
  hard-coded stat bonuses.
- Lane coverage: aggregate left/centre/right separately, then let the tactic choose or discover a lane. A weak left
  back becomes targetable instead of lowering every defensive possession equally.
- Partnership reducers: score actual linked pairs (CB+CB, full-back+winger, striker+AM) using chemistry and
  complementary stat profiles, then aggregate partnerships.
- Complementarity: reward one runner plus one passer rather than requiring both players to be identical all-rounders.
  Compute assignment quality across action roles, not just stat means.
- Bottleneck chain: a move can require secure receipt → decision → execution. The weakest stage limits the action,
  but overflow in an earlier phase can partially compensate.
- Redundancy: one elite creator is powerful until marked out; having a second reduces the opponent's ability to
  shut the route. This can be represented by top-two gap or alternative-lane quality.
- Matchup assignment: compare individual attacker/defender pairs in the chosen lane. Marking may determine whether
  the defender actually tracks the dangerous player; Positioning determines whether cover exists.
- Opponent-targeted weak link: do not automatically apply the minimum. Let Intelligence/Creativity and scouting
  determine how often the opposition identifies and attacks it.
- Tactical fit weighting: a stat matters in proportion to how often the selected tactic asks for its action. A high
  line requests more recovery Pace; a low block requests more Heading, Marking and sustained Composure.
- Dynamic participation: weight by touches, zones, role and current match state rather than static position group.
- Substitution continuity: use a short-lived partnership/familiarity penalty when the unit changes, then recover.

### 2.3 Recommended evaluation order

1. Calibrate the current six reducers and phase weights with `TacticLab` and distribution-focused synthetic teams.
2. Add softmax/softmin only if literal star/weak-link substitution swings are too sharp.
3. Add lane/partnership profiles before adding more mathematical reducers; spatial identity creates more gameplay
   than a ninth flavour of average.
4. Move to actor-vs-actor action resolution, at which point team aggregates become priors/support rather than the
   final result.

## 3. Match simulation improvements

### 3.1 Replace recursive phase calls with an explicit possession state

Keep the seven phase names initially, but run them through a non-recursive `PossessionState` loop:

```text
PossessionState
  teamInPossession
  phase
  zone (defensive third / middle / final third)
  lane (left / centre / right / half-space)
  ballCarrierId
  supportPlayerIds
  pressurePlayerIds
  transitionAge
  accumulatedAdvantage
  startReason (kickoff / regain / restart / mistake)
```

Benefits:

- a hard cap prevents pathological recursive chains;
- the highlight system can describe the same possession coherently;
- events such as a press beaten, switch of play or player mistake have real actors;
- fatigue/touches/actions can be recorded incrementally;
- replay/debug output can show why a shot occurred.

### 3.2 Add zones and lanes without building a physics engine

Use a small discrete pitch graph, not coordinates. Three longitudinal zones × three lanes, with optional half-spaces,
is enough to make width, overloads and weak flanks real.

- Formations seed occupancy/support in each cell.
- Roles and instructions change allowed transitions and participant weights.
- Defensive width redistributes coverage rather than being intrinsically good or bad.
- Attacking width creates outside routes but stretches central support and counter protection.
- Switching play compares the far-side receiver's Positioning/Technique against the defence's shifting
  Intelligence/Teamwork/Pace.
- A weak full-back can be specifically targeted, making lineup decisions visible in highlights and post-match data.

### 3.3 Resolve actions with actors and support

Each phase should select an action, actor, target and opposing actor before rolling:

```text
ActionAttempt
  actionType
  actor
  target/support
  directOpponent
  attackingSupportScore
  defensiveCoverScore
  executionDifficulty
  outcome
```

Examples:

- Short pass: actor Passing + Technique + Composure; receiver Positioning + Technique; press Marking +
  Positioning + Pace; nearby Teamwork modifies support.
- Carry: Dribbling + Technique + Agility against Tackling + Marking + Agility, with Pace affecting separation.
- Through ball: Passing + Creativity + Intelligence against Marking + Positioning + Intelligence; runner Pace +
  Positioning versus recovery Pace.
- Cross: Crossing + Technique under pressure; target Heading + Jumping + Strength + Height versus marker Marking +
  Heading + Jumping + Strength + Height.
- Press: Positioning + Stamina + Aggression + Teamwork; victim's Technique + Composure + Passing; poor Tackling
  raises foul risk rather than simply lowering pressure strength.
- Clearance/second ball: Heading/Strength/Positioning and nearby numerical support determine who receives next.

The group profile supplies support/cover and actor-selection priors. The actor duel supplies the decisive variance.
This makes specialists rewarding while preventing one player's maximum from being applied to every possession.

### 3.4 Make tactics change action frequency and risk

Tactics should alter what the side attempts, not merely add a second rating beside ability.

- Control: more short secure links, lower turnover distance, slower progression.
- Intensity/Tempo: quicker action selection and more transition opportunities, but higher fatigue and technical
  error under pressure.
- Creativity: more difficult/high-upside actions, with turnovers in worse shapes when they fail.
- Pressure: more press attempts and higher field position on regains, but space behind and stamina cost.
- Security: more cover players and safer rest defence, but fewer forward support options.
- Stability: narrower outcome variance and better shape retention, at the cost of fewer improvisational overloads.
- Width: reallocates lanes; an extreme width creates a real central/outside trade-off.
- Mentality: changes accepted risk, player numbers committed and response to score/time.

### 3.5 Chance construction and shot quality

Replace a phase-specific random xG range with a constructed chance record:

```text
ChanceContext
  zone + angle
  shotType
  defensivePressure
  nearestDefenderDistanceBand
  keeperSetness
  assistType
  bodyPart/aerial
  transitionSpeed
  shooterBalance
```

Base xG comes from location/shot type; the build-up modifies pressure, angle and keeper setness. Shooter skill should
primarily affect execution/on-target/placement, not retroactively turn a terrible location into a great chance.
Keeper skill then resolves save reach, positioning and handling. Rebounds become a possession state rather than a
new unrelated random shot.

### 3.6 Proper restarts and set pieces

Add dedicated states for corners, wide/deep free kicks, direct free kicks, penalties, throw-ins and goal kicks.

- Select takers from relevant Technique/Passing/Crossing/Shooting plus Composure.
- Select aerial targets and markers explicitly.
- Height is valuable but cannot replace Heading, Jumping, Strength or Positioning.
- Corners trade attacking numbers for counter vulnerability.
- Aggressive tackling and poor timing create free kicks/cards; tactical Fouling changes intent and field zone.
- Authored routines can later reuse the reliance/player-picker system.

### 3.7 Fatigue, durability and availability during matches

Implement `Fatigue` as action load rather than a flat clock:

- Sprint, press, repeated duel, knock, high intensity and playing out of position add load.
- Stamina slows accumulation; Durability reduces knock/injury consequences and recovery loss.
- Fatigue first widens execution variance, then lowers Pace/Agility/Technique/Composure selectively.
- Players can conserve energy depending on personality, score, role and tactic.
- A tired press loses synchronisation; one exhausted full-back becomes a discoverable weak link.
- Half-time and substitutions produce explicit recovery/continuity effects.

### 3.8 Cards, injuries and discipline as live constraints

- A booked player's aggression/tackling decision should change according to Intelligence, personality and manager
  instructions; cautious behaviour lowers red risk but makes them easier to attack.
- Opponents with Creativity/Intelligence can target booked or injured defenders.
- Minor knocks alter action selection/performance before the post-match injury is known.
- The manager should receive meaningful substitution prompts, not automatic optimal decisions.

### 3.9 Score, clock and opponent adaptation

Teams need a small decision policy based on score, time, competition context and manager/personality:

- leading late: choose between control, counter threat and deep security; each has a distinct failure mode;
- trailing late: commit players and accept rest-defence risk;
- two-legged ties/cup elimination context changes thresholds;
- CPU sides identify repeated lane success, a booked defender, tired players and tactical mismatches with imperfect
  accuracy based on manager Tactics/Intelligence;
- human changes create an adaptation delay, not instant omniscient countering.

### 3.10 Player attribution and history

Persist compact possession/chance summaries, not every internal roll forever:

- key pass, carry, duel, interception, mistake, press regain, chance created, expected threat, shot context;
- a short reason code and actor IDs for highlights and post-match narratives;
- full detail for the player's matches, slim aggregates for background matches, matching the existing archive rule;
- derived form and role-performance windows become inputs to events.

### 3.11 Calibration and observability

Add a `MatchTelemetry` result alongside goals:

- phase entries/successes by side;
- turnovers by zone/action/reason;
- shots/xG/shot quality distribution;
- actor selections and contribution concentration;
- star/weak-link aggregate values used;
- possession length and transition frequency;
- fouls/cards/injuries/fatigue load;
- tactic and ability components before randomness.

Extend `TacticLab` with controlled profiles: one superstar, one weak link, balanced XI, mismatched partnerships,
fast attack/slow defence, narrow/wide strengths and fatigued variants. Targets should include plausible goal rate,
home advantage (when deliberately added), favourite win rate, tactic effect size and diminishing superstar returns.

## 4. Contextual event system architecture

### 4.1 Separate definition, opportunity and runtime thread

Use three layers:

```text
EventDefinition (authored ScriptableObject)
  stable key, category, copy variants
  trigger query + scoring rules
  participant roles
  cooldown/once/escalation policy
  choices and effect templates
  follow-up definitions

EventOpportunity (pure transient candidate)
  definition key
  bound people/team/fixture/tactic facts
  relevance score + random weight
  evidence/reason codes

EventThread (saved runtime state)
  thread ID + definition key
  bound participant IDs
  stage/status
  created/last-advanced dates
  remembered facts and manager promises
  next eligible follow-up date
```

The existing `Event` can evolve into or wrap `EventThread`; do not create a parallel inbox/discussion system.
Existing `Event.Response`, reaction tables, `Person.NewMorale`, `DiscussionPageUI` and save flow remain reusable.

### 4.2 Build one daily `WorldContextSnapshot`

The snapshot is the read-only input to event generation. Candidate rules should not reach through singletons
independently and accidentally observe different moments.

Team facts:

- league/cup position, expectation, recent trend, streak, schedule congestion, days since match;
- last result and manner (late collapse, comeback, upset, heavy loss, clean sheet, repeated chance creation);
- tactical identity, complexity, familiarity, recent changes and formation/instruction version;
- squad size/position depth, average morale, mood spread, cliques/chemistry, leaders and unresolved threads;
- current training and recent training balance;
- manager response history, promises kept/broken and perceived consistency.

Player facts:

- availability, injury history, fatigue, Durability, age and recovery state;
- starts, minutes, bench/non-squad days and consecutive omissions over rolling 3/5/10-match windows;
- role/position used versus preferred/natural roles; lineup competition and depth-chart rank;
- match contribution/form, mistakes, cards, goals/assists/saves and expected-versus-actual performance;
- effective Intelligence versus tactic Complexity, familiarity and exposure to the tactic;
- training selected, recent Boost changes, positional retraining and improvement/stagnation;
- morale relative to personal ideals, trend and cause ledger;
- chemistry/relationship edges, previous event involvement and discussion outcomes;
- personality-derived behavioural axes and voice/silence history.

### 4.3 Eligibility first, probability second

Every definition follows this pipeline:

1. Hard eligibility: all facts required by the premise are true.
2. Suppression: no active duplicate, cooldown, one-shot key or contradictory thread exists.
3. Participant binding: choose plausible people using role-specific weights.
4. Relevance scoring: severity, recency, exposure and personality adjust urgency.
5. Daily budget: select a small number across categories so the inbox does not become noise.
6. Weighted randomness: decide timing among eligible candidates.
7. Thread creation: freeze the evidence used, so later text and effects remain coherent.

Recommended mixture:

- roughly 70% state-linked opportunities;
- roughly 20% hybrid opportunities requiring loose context but retaining random flavour;
- roughly 10% ambient pub-life events with only availability/cooldown checks.

Those ratios are tuning targets, not hard guarantees. A quiet stable week should be allowed to remain quiet.

### 4.4 Memory, cooldowns and deduplication

Use scoped memory keys rather than a single `hasHappened` flag:

```text
eventKey + playerId + tacticVersion
eventKey + playerId + season
eventKey + unordered playerPair
eventKey + injuryInstanceId
eventKey + promiseId
```

Policies:

- OnceEver: rare identity-defining events.
- OncePerSeason: concerns that can reasonably recur next year.
- OncePerContextVersion: tactical confusion can recur after a materially new tactic, not every week on the same one.
- CooldownDays/Matches: minor recurring concerns.
- Escalating: first concern → warning → demand → consequence, with one active thread.
- ResolvedUntilRegression: returns only if the tracked condition worsens beyond a hysteresis threshold.

Hysteresis prevents flicker: a complaint triggered at five straight omissions does not resolve after one substitute
appearance and retrigger immediately.

## 5. Worked example: confused by complex tactics

This should be the first contextual event implemented because it connects player Intelligence, tactics, selection,
personality, manager choices and match consequences.

### 5.1 Eligibility

- Player is available and has meaningful exposure: e.g. at least 90 minutes or two starts under the current tactic
  version in the last five matches. A non-playing reserve normally cannot diagnose match-plan confusion.
- `Tactic.IntelligenceThreshold - player.TacticalIntelligence()` exceeds a tunable concern margin.
- Current tactic complexity/familiarity has existed long enough to be experienced, unless a sudden large change
  creates an immediate concern.
- No active confusion thread for this player/tactic version.
- The player has not already resolved/accepted this exact tactic version.

### 5.2 Probability/relevance

```text
confusionSeverity = smoothstep(3, 18, intelligenceShortfall)
exposure          = clamp01(recentMinutes / targetMinutes)
unfamiliarity     = 1 - playerOrTeamFamiliarity
recentEvidence    = mistakes/poor-role-actions attributable to complexity
voice             = personality assertiveness × communication tendency

opportunityScore = confusionSeverity
                 × (0.45 + 0.55 × exposure)
                 × (0.60 + 0.40 × unfamiliarity)
                 × evidenceMultiplier
                 × voiceTimingMultiplier
```

Low-voice personalities do not become immune. They are more likely to produce indirect evidence first: withdrawn
morale, asking a teammate, training hesitation, or a delayed private admission after a mistake.

### 5.3 Choices and trade-offs

- Simplify the tactic: resolves/softens concern for several players and raises short-term execution, but changes the
  tactic, loses familiarity and sacrifices a tactical edge.
- Explain it privately: manager Communication/relationship check; success raises understanding/familiarity, failure
  hurts confidence or trust. Costs one limited discussion/attention slot.
- Assign a mentor: links the player to a smart/Teamwork-heavy teammate; can improve adaptation, but burdens the
  mentor and a bad chemistry pairing can worsen both moods.
- Change their role/bench them: removes exposure and immediate errors, but can create playing-time/role concerns.
- Challenge them to learn: Driven/Smart/Cocky may respond; Lazy/Shy/Cautious can react very differently. Failure
  increases pressure and later mistakes.
- Dismiss the concern: preserves tactical plan and authority short-term; creates a remembered grievance, raises
  mistake/slump likelihood and makes later public escalation possible.

### 5.4 Follow-up and resolution

- After N training sessions/minutes, reassess shortfall, familiarity and attributable mistakes.
- Success event: player says it has clicked; thread resolves and can grant a small familiarity/relationship effect,
  not a permanent Intelligence increase.
- Continued struggle: private concern escalates according to personality.
- Tactic version changes materially: close old thread as obsolete, retain how the manager treated them as relationship
  memory, and allow a new scoped opportunity only after real exposure.

## 6. Event families and state hooks

### 6.1 Playing time and selection

- Wants more starts after sustained benching; urgency depends on ambition/assertiveness and whether competitors are
  actually performing.
- Accepts impact-sub role, asks for a clearer role, requests a position change, resents being used out of position.
- Veteran worries about being phased out; youngster asks for a chance after training/form progress.
- Player admits they need rest, hides fatigue, or insists on playing through a knock.
- Consecutive omission group forms; one vocal player speaks for quieter teammates.
- Promise events: "start me in the next two", "use me at CM", "give me cup minutes" with explicit deadline and
  automatic kept/broken evaluation.

### 6.2 Tactics and role fit

- Confusion from complexity; frustration with an unsuitable role; excitement about a system that fits strengths.
- Defender fears a high line because of low Pace; winger dislikes lack of width; creator feels over-restricted;
  striker feels isolated; midfielder complains the press is unsynchronised.
- Smart/observant player identifies a repeated opponent pattern after match telemetry supports it.
- Reliant player feels pressure or status from being the focus; displaced teammate feels marginalised.
- Tactical change splits the squad: risk-seekers enjoy it, cautious players lose security.

Positive tactical events must carry risk: praise for a player-centric role can inflate Cocky behaviour, dependency
or teammate resentment; accepting feedback can cost familiarity or weaken another phase.

### 6.3 Performance and form

- Goal drought, finishing hot streak, repeated mistakes, keeper confidence, clean-sheet pride, unlucky xG run.
- Player wrongly blames themselves/another; evidence and personality alter accuracy.
- Teammate defends a struggling player, privately criticises them, offers training help or demands they be dropped.
- Big-game hero wants recognition; quiet contributor feels ignored; stat leader resents collective praise.
- Return-from-injury fear, rust or reckless overconfidence.

### 6.4 Training and development

- Enjoys/dislikes current drill based on needs/personality; asks to work on a weakness or protect a strength.
- Boost stagnation, overtraining/fatigue, positional retraining progress/frustration.
- Smart/Driven player volunteers to mentor; Lazy player cuts corners; Aggressive player overdoes contact work;
  Cautious player fears injury in high-load sessions.
- Group training improves bonds or exposes rivalry. Choosing social/training/tactical focus always has an
  opportunity cost through the existing decay/familiarity model.

### 6.5 Relationships and squad dynamics

- Chemistry becomes narratively visible through repeated linked actions/mistakes, not a random "they now hate each
  other" roll.
- Pair requests to play together, refuses blame, clashes over responsibility, reconciles after success.
- Clique inclusion/exclusion, newcomer integration, veteran/youngster friction, position-rival rivalry.
- Captain/leader challenges standards, mediates a dispute or abuses influence.
- One player's treatment causes observers with relevant empathy/loyalty/justice traits to react.

### 6.6 Discipline and conduct

- Repeated cards, dangerous aggression, retaliation after being provoked, apology/refusal, teammate intervention.
- Manager warning can lower card risk but aggression/duel intensity too; public punishment can satisfy cautious squad
  members while alienating loyal friends.
- Smart players may change behaviour before being asked; Cocky/Aggressive may interpret caution as weakness.

### 6.7 Health, fatigue and availability

- Concealed knock, rehab impatience, fear after severe injury, guilt about replacing an injured teammate.
- Congested schedule complaints; Durable/Driven players may underreport risk, Cautious players report early.
- Squad concern after ACL/death already uses severity; threads can add grief, leadership and return-to-play stages.

### 6.8 Results, competition and club trajectory

- Promotion pressure, relegation fear, cup excitement, upset belief, complacency after streaks, crisis after collapse.
- Reaction depends on expectations, match manner and personal involvement, not win/loss alone.
- Local rivalry/pub identity events, supporter pressure, team social after landmark results.
- Ambitious players worry if the club underperforms; loyal/kind players rally the group; cocky players make public
  claims that raise pressure.

### 6.9 Ambient and hybrid pub-life events

- Minor social mishaps, transport/work conflicts, pub interactions, local attention, equipment/weather oddities.
- Still gate on availability, date, prior occurrences and participants. A deceased/injured/absent player cannot be
  selected for contradictory physical comedy.
- Hybrid events use context lightly: a celebration is more likely after a landmark win; a late-night problem is
  likelier after a social activity and may affect next-day fatigue.

## 7. Personality behaviour model

The existing ten `PersonalityType`s remain the authored voice/archetype. Derive a `BehaviourProfile` from the type
plus small stable per-person variation so two Calm players are not clones. The variation can be generated once and
saved, or derived deterministically from `PersonID` if the axes never need progression.

Useful hidden axes:

- Assertiveness: chance to voice directly and escalate.
- Sociability: public/group versus private channels; contagion reach.
- Emotional volatility: reaction magnitude and decay speed.
- Ambition: sensitivity to minutes, role, club trajectory and recognition.
- Professionalism/work ethic: training, tactical adaptation and promise reliability.
- Risk appetite: response to creative/aggressive tactics, injury and challenges.
- Adaptability: reaction to role/tactic/teammate change.
- Empathy/loyalty: likelihood of reacting to another player's treatment.
- Ego/status need: praise, reliance and hierarchy sensitivity.
- Candour: accuracy/directness of what they tell the manager.
- Leadership: chance concerns represent a subgroup and propagate.
- Resilience: recovery after failure versus lingering thread memory.

Do not show these numbers directly. Express them through interview answers, chatter, timing, silence, choices and
repeated behaviour.

### 7.1 Aggressive

- Voices early, directly and often publicly; low threshold for unfairness, weak standards, losing duels and passive
  tactics.
- More likely to demand action, confront a blamed teammate or volunteer for a challenge.
- Responds to clear boundaries, direct challenges and accountability; may respect firm private criticism.
- Reacts badly to hedging, public humiliation or perceived cowardice. Dismissal can escalate quickly.
- Spreads intensity through vocal influence; can rally Driven/Cocky players or intimidate Shy/Cautious ones.
- Event risks: cards, training clashes, retaliation, high-line/press enthusiasm despite fatigue.

### 7.2 Calm

- Voices later and usually privately; a complaint from a Calm player is strong evidence the problem is sustained.
- Notices emotional instability, unnecessary tactical chaos and unfair treatment.
- Responds to reasoned explanation, consistency and de-escalation.
- Rarely creates a public crisis, but may quietly lose trust if promises are repeatedly broken.
- Stabilises squad mood and can mediate volatile teammates; low contagion amplitude but high credibility.
- Event risks: silence can hide a deep issue; may tolerate too much before a sudden firm boundary.

### 7.3 Cautious

- Raises risks, role uncertainty, injuries, high lines, complex instructions and schedule overload.
- More likely to ask questions or seek reassurance than issue demands; may speak before exposure if danger is obvious,
  but needs real exposure for tactical-confusion events.
- Responds to plans, safety nets, gradual change and specific evidence.
- Reacts badly to reckless challenges, vague "trust me" answers and public pressure.
- Can spread anxiety to Shy/Calm players but also identify genuine risks early.
- Event risks: avoids ambitious actions, overreports danger, confidence falls after one visible failure.

### 7.4 Cocky

- Voices status, recognition, role and playing-time concerns quickly; frames problems as the system failing them.
- Enjoys free roles, reliance and high-risk attacking responsibility, but may resent sharing spotlight.
- Responds to wagers/challenges, status rewards and praise paired with measurable expectations.
- Reacts badly to being ignored, benched without explanation or compared unfavourably in public.
- Can lift belief after upsets or create complacency/pressure with boasts; influence follows performance.
- Event risks: demands, blame shifting, dependency on praise, rivalry with another star.

### 7.5 Driven

- Raises standards, training quality, ambition and underperformance; often offers a solution with the complaint.
- Tolerates short-term benching or difficult tactics if given a credible development/competition plan.
- Responds to challenge, responsibility, honest targets and merit-based decisions.
- Reacts badly to broken promises, laziness being tolerated or directionless simplification.
- Strong positive/negative standards contagion; natural mentor/captain candidate.
- Event risks: overtraining, playing through injury, impatience with weaker/less professional teammates.

### 7.6 Kind

- More likely to voice another player's distress than their own; prefers private, conciliatory conversations.
- Sensitive to squad exclusion, harsh punishment, grief/injury and unfair blame.
- Responds to empathy, collective solutions and chances to support/mentor.
- Reacts badly when the manager sacrifices a vulnerable player purely for authority or dismisses group consequences.
- Strong supportive contagion and mediation, but may protect an underperformer too long.
- Event risks: takes on others' mood, avoids honest confrontation, becomes quietly disillusioned by cruelty.

### 7.7 Lazy

- Low chance to raise tactical confusion as a learning problem; more likely to call it unnecessary effort or avoid
  exposure. Complaints often appear when training/load/accountability rises.
- May be content with a bench role until status or convenience changes, so low minutes alone are a weaker trigger.
- Responds to easy concrete steps, immediate incentives, social pressure and tightly scoped responsibilities.
- Reacts badly to long explanations, extra individual work and moralising challenges.
- Low positive effort contagion; can normalise coasting among Silly/Cocky peers.
- Event risks: training shortcuts, concealed lack of preparation, fatigue complaints, unreliable promises.

### 7.8 Shy

- Very low direct voice probability; communicates through private delayed talks, a trusted teammate, performance
  withdrawal or silence changes.
- Sensitive to public criticism, sudden role exposure, complex tactics and group conflict.
- Responds to private reassurance, specific small steps, mentor support and time.
- Reacts badly to public demands, forced leadership or being used as an example.
- Low direct contagion, but Kind/Calm leaders may carry their concern; unresolved issues can become severe before
  reaching the manager.
- Event risks: hidden confusion, avoids admitting injury/error, confidence spirals after mistakes.

### 7.9 Silly

- Voices unpredictably and often indirectly through jokes; humour can reveal or conceal real tension.
- Likes novelty, social events and expressive tactics; dislikes repetitive seriousness and rigid roles.
- Responds to playful framing, short challenges and social solutions, but serious issues still need clear boundaries.
- Reacts badly to humourless overreaction or public embarrassment when a joke was a defence mechanism.
- Strong mood contagion in both directions: can defuse pressure or make complacency/lack of focus contagious.
- Event risks: distractions, misunderstood jokes, poor preparation, accidentally exposing another player's concern.

### 7.10 Smart

- Accurately identifies tactical/role problems and may wait for evidence before speaking. A Smart complaint should
  often contain useful but not infallible analysis.
- Less likely to be confused by complexity, more likely to object to contradiction, inefficiency or an exploitable
  mismatch.
- Responds to evidence, autonomy, explanation and being asked for input.
- Reacts badly to inconsistent reasoning, obvious spin, repeated ignored warnings or simplistic public challenges.
- High informational influence: teammates may trust their diagnosis even without high emotional leadership.
- Event risks: overthinking, impatience with poor communicators, faction forming around "the right way" to play.

### 7.11 Voice-channel selection

Choose how an eligible concern appears separately from whether it exists:

- Direct private talk.
- Public dressing-room statement.
- Teammate proxy (Kind/leader/friend).
- Group petition or clique concern.
- Behavioural tell (silence, training drop, mood change).
- Match action/highlight evidence followed by a later talk.
- Anonymous/ambiguous chatter before identity becomes clear.

This prevents Shy/Calm players from becoming eventless and Aggressive/Cocky players from owning every narrative.

## 8. Squad mood and relationship effects

### 8.1 Keep individual morale, add cause and topic

Do not replace `Morale`. Add a short bounded `MoraleCauseLedger` recording recent deltas:

```text
source key, topic, magnitude, date, responsible person/thread, expiry/decay
```

Topics can include Selection, Tactics, Trust, Results, Belonging, Health, Recognition and Discipline. Two players
with the same Mood/Passion can therefore generate different concerns and react differently to the same solution.

### 8.2 Propagation graph

When an effect may spread, calculate observers rather than applying a flat squad delta:

- chemistry/link proximity and friendship/clique membership;
- shared position/competition for minutes;
- personality empathy/loyalty/justice sensitivity;
- leader influence and credibility;
- whether the observer witnessed the event or heard it through someone;
- agreement with the topic and manager response;
- current mood susceptibility and event severity.

Positive and negative propagation should be asymmetric. Public praise of one player can inspire friends, annoy a
direct rival and mean nothing to a distant squad member. Public punishment can restore standards while damaging
trust among the punished player's allies.

### 8.3 Squad-level states derive from people

Derive, do not independently store, broad views such as:

- atmosphere (mean mood plus negative-tail weight);
- drive (passion plus Driven/Aggressive leadership);
- cohesion (chemistry graph, unresolved conflicts, teamwork and mood spread);
- confidence (recent evidence versus expectation, with personality filtering);
- trust in manager (promise/consistency ledger).

Events may read these values and change individuals/relationships; avoid a second free-floating "squad mood" number
that can contradict everyone in the squad.

## 9. Choices, consequences and promises

An `EventChoiceDefinition` should support effect bundles and conditional branches:

```text
immediate morale/topic effects
relationship/trust effects
tactic/training/selection mutation request
promise with deadline and success query
temporary behaviour modifier
follow-up schedule
public/private observer propagation
```

Outcome checks can reuse the existing personality reaction table as a baseline, then add context:

- relationship/trust with manager;
- accuracy of the player's claim;
- evidence strength;
- public versus private delivery;
- response consistency with earlier promises;
- manager/player communication-related stats if introduced;
- personality axes and current state.

No response is universally best. For example, promising minutes can immediately calm the player, but breaking it
should be worse than refusing honestly; simplifying tactics helps confused players while costing familiarity and
the reason the complex tactic was chosen.

## 10. Technical plan

### 10.1 Pure rule layer

Suggested plain C# types:

- `WorldContextSnapshot` and compact player/team/match fact records.
- `IEventRule` or serializable rule nodes for eligibility/scoring.
- `EventCandidateBuilder` for participant binding and evidence.
- `EventScheduler` for budgets, category diversity and weighted selection.
- `EventMemoryIndex` for O(1) scoped cooldown/active-thread checks.
- `EventResolver` for choices, conditional effects, propagation and promises.
- `EventThreadState`/`PromiseState` persisted in core save.
- `BehaviourProfile` derived from personality plus stable variation.
- `SquadInfluenceGraph` derived from chemistry, position links, leadership and active threads.

`EventsManager` remains the owning controller and adapts these pure services to the calendar/UI.

### 10.2 Authored content

Extend `EventType` gradually or introduce a referenced `ContextualEventDefinition` only if the old shape cannot be
migrated cleanly. Required authored fields:

- stable non-name key;
- category/tags and priority;
- participant role schemas;
- rule/scoring parameters;
- copy variants keyed by stage/personality channel/severity;
- cooldown/memory policy;
- choices, effects and follow-up links.

Prefer tokenised copy backed by frozen context facts. Never regenerate the premise on display and accidentally say
"five games benched" after the player has since started.

### 10.3 Save compatibility

- Existing one-off `Event`s load as simple stage-zero threads or remain supported through an adapter.
- Persist stable definition keys and participant IDs, never ScriptableObject object graphs.
- Persist only active threads, bounded cause ledgers, promises and compact memory timestamps/counters.
- Archive resolved history in a capped season log if it is needed for biography; do not let core save grow forever.
- Rebuild all derived indices/influence graphs after deserialize.

### 10.4 Debugging tools

Add an Event Lab alongside `TacticLab`:

- build a synthetic world snapshot;
- list every eligible definition, failed hard rule and score contribution;
- force-bind a player and preview all personality/channel copy;
- simulate 1,000 days to inspect event frequency, duplicates and category starvation;
- verify every choice has at least one downside or opportunity cost;
- show active memory keys/cooldowns and why an apparent duplicate was suppressed.

## 11. Implementation stages

### Stage 1 — evidence foundation

- Add rolling player participation/role/form/tactical-exposure records at fixture finalisation.
- Add tactic version/signature and material-change detection.
- Build `WorldContextSnapshot` and debug dump.
- No new UI yet.

### Stage 2 — thread/memory scheduler

- Add stable definition keys, scoped memory, cooldown, active thread and daily event budget.
- Route existing random/result events through the scheduler where possible.
- Preserve the current discussion UI/reaction system.

### Stage 3 — tactical confusion vertical slice

- Implement the complete worked example: eligibility, personality channel, choices, promise/follow-up, deduplication
  and squad observers.
- Validate all ten personality types and playing-time exposure edge cases.

### Stage 4 — playing time, role and performance families

- Add rolling-window rules, promises and escalation.
- Add cause-ledger topics and manager trust.
- Add proxy/group voice channels.

### Stage 5 — relationships and squad propagation

- Build the derived influence graph and observer effects.
- Add pair/clique threads, mediation and leadership.
- Expose only readable consequences, not hidden graph numbers.

### Stage 6 — match evidence upgrade

- Implement explicit `PossessionState`, actor/action attribution and compact telemetry.
- Feed real mistakes, role actions, fatigue and tactical outcomes into contextual events.
- Add spatial lanes, restarts and constructed chances incrementally.

## 12. Verification criteria

- The same player cannot repeat a resolved tactical-confusion complaint under the unchanged tactic context.
- A player with no meaningful minutes almost never raises match-plan confusion; a frequently used low-IQ player does.
- Every personality can surface the concern, but via observably different timing/channel/framing.
- Changing tactics materially permits a future new concern only after new exposure.
- A promise is evaluated from game facts and remembered after save/load.
- Public choices affect plausible observers differently; no blanket squad delta is used as a shortcut.
- Event frequency remains bounded during injury crises, bad runs and congested schedules.
- Ambient events still occur, but never bind impossible participants.
- Event Lab can state why any candidate was eligible, suppressed, selected and resolved.
- Match telemetry and event facts survive save/load/archive at the intended detail level.
