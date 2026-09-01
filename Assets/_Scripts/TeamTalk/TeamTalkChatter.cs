using UnityEngine;

/// <summary>
/// Which moment in the team-talk flow a line is spoken at — drives how likely a player is to speak, and about what.
/// </summary>
public enum ChatterPhase { PreTalk, PostTalk, Individual }

/// <summary>Result vs what was expected of this fixture (from pre-match relative strength). A 0-0 is Overperforming
/// against the league leaders and Underperforming against the bottom club — same scoreline, opposite mood.</summary>
public enum Expectation { Overperforming, OnPar, Underperforming }

/// <summary>
/// Everything the chatter generator needs to voice one player's half-time line. It's a lossy, in-character VIEW onto
/// the player's hidden state — never a direct read-out (see <see cref="TeamTalkChatter"/>).
/// </summary>
public struct TalkContext
{
    public Person.PersonalityType personality;
    public ChatterPhase phase;
    public int goalDifference;      // + = the player's team is winning
    public Expectation expectation;
    public Morale morale;           // mood/passion vs this player's ideal
    public bool inRut;              // Player.InSlump — the hidden thing you're trying to detect
    public Event.Response tone;     // Post / Individual only: the tone you chose
    public Event.Reaction reaction; // Post / Individual only: how they took it
    public bool recovered;          // the talk just snapped them out of a slump
}

/// <summary>
/// Turns a player's hidden half-time state into an in-character dressing-room line — a READ-ONLY view, with no morale
/// side effects (the actual swing is applied by <see cref="TeamTalkController"/>). It's the skill of the feature:
/// you infer who's struggling from what players say (and DON'T say), deliberately ambiguously — the same observable
/// (silence, anger, a joke) means different things for different personalities in different situations.
///
/// The rule the generator is built around: read the DEVIATION from a player's own baseline given the context, never
/// a line in isolation. So lines are banded (not per-point), silence is first-class (some personalities are silent
/// by default → their silence carries no info; loud ones going quiet screams), and pools are deliberately SHARED
/// across states so the same line can't be a certain tell. See TEAM_TALK_REWORK_PLAN.md.
/// </summary>
public static class TeamTalkChatter
{
    // ————————————————————— speech rates —————————————————————
    // Base chance a personality speaks at all, per phase. Pre-talk is sparse (little to react to yet — a few speak);
    // post-talk is busy (most speak). Loud archetypes are near-always vocal; the near-silent ones carry no info when
    // quiet. State then pushes these up/down in Speaks().
    struct Rate { public float pre, post; public Rate(float pre, float post) { this.pre = pre; this.post = post; } }

    static Rate BaseRate(Person.PersonalityType p) => p switch
    {
        Person.PersonalityType.Aggressive => new Rate(0.35f, 0.90f),
        Person.PersonalityType.Calm       => new Rate(0.12f, 0.55f),
        Person.PersonalityType.Cautious   => new Rate(0.25f, 0.75f),
        Person.PersonalityType.Cocky      => new Rate(0.40f, 0.90f),
        Person.PersonalityType.Driven     => new Rate(0.40f, 0.92f),
        Person.PersonalityType.Kind       => new Rate(0.28f, 0.85f),
        Person.PersonalityType.Lazy       => new Rate(0.10f, 0.40f),
        Person.PersonalityType.Shy        => new Rate(0.05f, 0.30f),
        Person.PersonalityType.Silly      => new Rate(0.40f, 0.90f),
        Person.PersonalityType.Smart      => new Rate(0.30f, 0.80f),
        _                                 => new Rate(0.30f, 0.70f)
    };

    /// <summary>How much a rut suppresses speech — but ONLY for the normally-expressive types, whose withdrawal is
    /// the tell. The already-quiet ones (Shy/Lazy/Calm/Cautious) are left alone: their silence means nothing.</summary>
    const float RUT_SILENCE_FACTOR = 0.4f;

    static bool NormallyVocal(Person.PersonalityType p) =>
        p == Person.PersonalityType.Aggressive || p == Person.PersonalityType.Cocky ||
        p == Person.PersonalityType.Driven     || p == Person.PersonalityType.Silly  ||
        p == Person.PersonalityType.Kind       || p == Person.PersonalityType.Smart;

    /// <summary>
    /// Chance this player says anything at all. Silence is a real signal, so this is deliberately noisy: a rut mutes
    /// the loud types (a tell), a fresh recovery loosens the quiet ones (the inverse tell), a strong reaction and an
    /// under-performing scoreline get people talking. You single a player out in an Individual talk, so they always
    /// answer there.
    /// </summary>
    public static bool Speaks(TalkContext ctx)
    {
        if (ctx.phase == ChatterPhase.Individual) return true;

        Rate rate = BaseRate(ctx.personality);
        float chance = ctx.phase == ChatterPhase.PreTalk ? rate.pre : rate.post;

        if (ctx.inRut && NormallyVocal(ctx.personality)) chance *= RUT_SILENCE_FACTOR;

        // A just-lifted player opens up — biggest anomaly on the normally-silent types (them speaking = they're up).
        if (ctx.recovered) chance = Mathf.Max(chance, NormallyVocal(ctx.personality) ? 0.85f : 0.7f);

        // Post-talk: a reaction they really felt (either way) makes them more likely to voice it.
        if (ctx.phase == ChatterPhase.PostTalk)
        {
            int intensity = Mathf.Abs((int)ctx.reaction - (int)Event.Reaction.Neutral);
            chance += 0.08f * intensity;
        }

        if (ctx.expectation == Expectation.Underperforming) chance += 0.1f;

        return Random.value < Mathf.Clamp01(chance);
    }

    // ————————————————————— shared pools (deliberate overlap → ambiguity) —————————————————————
    // Reused across personalities/states so the same observable can't be a certain diagnosis.

    static readonly string[] Defeatist =
    {
        "What's the point.", "We're not turning this around.", "Same as always. Hopeless.", "I've got nothing left."
    };
    static readonly string[] AnxiousPool =
    {
        "This could slip away from us.", "We're leaving too many gaps.", "I don't like how this is going.",
        "One mistake and we're punished, watch."
    };
    static readonly string[] CopingJoke =
    {
        "On the bright side, it can't get worse… right?", "We're basically Brazil, lads.",
        "Anyone else fancy a pint at full time?", "I meant to do that, honestly."
    };
    static readonly string[] FlatMutter = { "Yeah.", "Fine.", "Sure.", "Whatever you say, boss." };

    // ————————————————————— banding helpers —————————————————————

    enum ResultBand { Winning, Level, Losing }
    static ResultBand Result(int gd) => gd > 0 ? ResultBand.Winning : gd < 0 ? ResultBand.Losing : ResultBand.Level;

    enum ReactionBand { Negative, Neutral, Positive }
    static ReactionBand Took(Event.Reaction r) =>
        r < Event.Reaction.Neutral ? ReactionBand.Negative :
        r > Event.Reaction.Neutral ? ReactionBand.Positive : ReactionBand.Neutral;

    static string Pick(params string[] options)
    {
        if (options == null || options.Length == 0) return "";
        return options[Random.Range(0, options.Length)];
    }

    /// <summary>
    /// The in-character line for this context, or "" for silence (itself a signal — or just noise). Each personality
    /// owns its own tells: what a rut looks like, what a recovery sounds like, and where it hides behind its baseline.
    /// </summary>
    public static string Line(TalkContext ctx)
    {
        if (!Speaks(ctx)) return "";

        return ctx.personality switch
        {
            Person.PersonalityType.Aggressive => Aggressive(ctx),
            Person.PersonalityType.Calm       => Calm(ctx),
            Person.PersonalityType.Cautious    => Cautious(ctx),
            Person.PersonalityType.Cocky       => Cocky(ctx),
            Person.PersonalityType.Driven      => Driven(ctx),
            Person.PersonalityType.Kind        => Kind(ctx),
            Person.PersonalityType.Lazy        => Lazy(ctx),
            Person.PersonalityType.Shy         => Shy(ctx),
            Person.PersonalityType.Silly       => Silly(ctx),
            Person.PersonalityType.Smart       => Smart(ctx),
            _                                  => ""
        };
    }

    // ————————————————————— per-personality voices —————————————————————
    // Post/Individual lines lean on how they TOOK the tone (ReactionBand). Pre lines lean on the scoreline/expectation.
    // The rut and recovery branches are the meat — that's where each personality's tell (or red herring) lives.

    static string Aggressive(TalkContext ctx)
    {
        // His anger is his IDEAL state (25/95) — fire is usually GOOD. The rut tell is defeatism or going cold, NOT demand.
        if (ctx.recovered) return Pick("Right. That's more like it. LET'S GO.", "Enough sulking. We go again.");
        if (ctx.inRut) return Pick(Concat(Defeatist, new[] { "Useless, all of you.", "I'm done shouting. No point." }));

        if (ctx.phase == ChatterPhase.PreTalk)
            return Result(ctx.goalDifference) switch
            {
                ResultBand.Winning => Pick("Don't you DARE let up.", "Kill it off. No mercy."),
                ResultBand.Losing  => Pick("This is embarrassing. Sort it out.", "Wake UP. We're getting bullied."),
                _                  => Pick("Not good enough. More.", "Come on — grab this game.")
            };

        return Took(ctx.reaction) switch
        {
            ReactionBand.Positive => Pick("NOW we're talking. Let's go.", "YES. That's what I wanted to hear."),
            ReactionBand.Negative => Pick("Praise? We're getting battered — wake UP.", "Soft. That won't fire anyone up."),
            _                     => Pick("Fine. Whatever. Let's just play.", "Alright. Talk's cheap — prove it.")
        };
    }

    static string Calm(TalkContext ctx)
    {
        // Ideal 90/5 — his tell is INVERTED: any visible fire is off-baseline (over-pumped/rattled). Distress hides
        // perfectly behind the calm, so a rut here is a near-invisible red herring (falls through to his normal line).
        bool fired = ctx.morale.Passion > ctx.morale.IdealPassion + 20;
        if (fired) return Pick("I'm… wound up. Not like me, this.", "Bit on edge, if I'm honest.");

        if (ctx.recovered) return Pick("Good. Clear head again.", "Right. Back to normal.");

        if (ctx.phase == ChatterPhase.PreTalk)
            return Pick("All good. We stick to the plan.", "No panic. Same again.", "Steady. It's fine.");

        return Took(ctx.reaction) switch
        {
            ReactionBand.Positive => Pick("Good. Nothing changes — we keep going.", "Agreed. Calm and steady."),
            ReactionBand.Negative => Pick("…there's no need for that.", "That won't help anyone, boss."),
            _                     => Pick("Understood.", "Fine by me.")
        };
    }

    static string Cautious(TalkContext ctx)
    {
        // Always sounds worried (75/25) — trait-worry vs rut-worry is genuinely hard. Reuses the shared anxious pool
        // so you can't separate "his usual fretting" from a real rut except by degree (fatalism = beyond his normal).
        if (ctx.recovered) return Pick("Okay. Maybe we're alright after all.", "Right. Let's just be sensible.");
        if (ctx.inRut) return Pick(Concat(Defeatist, new[] { "I don't know if I can turn this around.", "It's slipping and I can't stop it." }));

        if (ctx.phase == ChatterPhase.PreTalk)
            return Result(ctx.goalDifference) switch
            {
                ResultBand.Winning => Pick("See it out. Don't chase it.", "Don't get greedy — hold what we've got."),
                ResultBand.Losing  => Pick("We're too open, this could get worse.", Pick(AnxiousPool)),
                _                  => Pick(AnxiousPool)
            };

        return Took(ctx.reaction) switch
        {
            ReactionBand.Positive => Pick("Okay. If we're disciplined, we're fine.", "Alright — carefully, though."),
            ReactionBand.Negative => Pick("Attack more? That's how we concede…", "That feels risky, boss."),
            _                     => Pick(AnxiousPool)
        };
    }

    static string Cocky(TalkContext ctx)
    {
        // 95/80 — DOUBT is the huge tell (wildly off-baseline). But blaming team-mates is shared between "fine and
        // arrogant" and "wounded", so deflection alone doesn't diagnose him.
        if (ctx.recovered) return Pick("Obviously I've sorted myself out. You're welcome.", "Knew I'd click. Watch me now.");
        if (ctx.inRut) return Pick("…maybe I'm just not feeling it.", "Dunno. Not my day, whatever.", "…leave me out of it.");

        if (ctx.phase == ChatterPhase.PreTalk)
            return Result(ctx.goalDifference) switch
            {
                ResultBand.Winning => Pick("Told you. Men against boys.", "This is too easy for me."),
                ResultBand.Losing  => Pick("Relax, I'll sort it. Watch.", "Give ME the ball and it's fixed."),
                _                  => Pick("Get it to me and we win this.", "I'm the difference here, trust me.")
            };

        return Took(ctx.reaction) switch
        {
            ReactionBand.Positive => Pick("Finally, someone gets it.", "Correct. About time."),
            ReactionBand.Negative => Pick("Don't blame me for THIS lot.", "Me? Look at the others, boss."),
            _                     => Pick("Yeah yeah. I've got it.", "Sure. I'll carry us, as usual.")
        };
    }

    static string Driven(TalkContext ctx)
    {
        // 80/80, always rallying — a QUIET Driven player is the classic slump tell. Trap: cruising 4-0 up he might
        // just be calm because the job's done (a false positive when you're winning comfortably).
        if (ctx.recovered) return Pick("Right — heads up, we go again. Every second.", "That's it. Come ON, lads!");
        if (ctx.inRut) return Pick(Concat(Defeatist, new[] { "…yeah. Come on.", "…we go again, I guess." })); // hollow, no conviction

        if (ctx.phase == ChatterPhase.PreTalk)
            return Result(ctx.goalDifference) switch
            {
                ResultBand.Winning => Pick("Don't drop off now — bury them.", "We keep going till the whistle."),
                ResultBand.Losing  => Pick("This isn't us. Heads up, we fix it now.", "We've got 45 minutes. Let's earn it."),
                _                  => Pick("Come on, lads — we go again, every second.", "Level's not enough. Push.")
            };

        return Took(ctx.reaction) switch
        {
            ReactionBand.Positive => Pick("You heard the gaffer — let's GO!", "That's the message. Everyone, up!"),
            ReactionBand.Negative => Pick("…is that it?", "I wanted more from that, boss."),
            _                     => Pick("Alright. Let's back it up out there.", "Fine — actions now, not words.")
        };
    }

    static string Kind(TalkContext ctx)
    {
        // 100/50, relentlessly supportive — a rut shows as the ABSENCE of his usual encouragement (he goes quiet or
        // stops lifting others). So subtle it just reads as "a bit quieter than usual".
        if (ctx.recovered) return Pick("Right, come on everyone — together, yeah?", "There we go. Chin up, all of us.");
        if (ctx.inRut) return Pick("…", "Sorry, I've not got much to give right now.", "…I'll try, lads.");

        if (ctx.phase == ChatterPhase.PreTalk)
            return Result(ctx.goalDifference) switch
            {
                ResultBand.Losing => Pick("Chin up, everyone — we're in this together.", "Forget the miss, next one goes in."),
                _                 => Pick("Great effort out there, boys.", "Proud of this lot. Keep it up.")
            };

        return Took(ctx.reaction) switch
        {
            ReactionBand.Positive => Pick("That's lovely, gaffer. Come on, boys.", "Nice one — you heard him, together now."),
            ReactionBand.Negative => Pick("There's no need to be so hard on them.", "Ease up on the lads, boss — they're trying."),
            _                     => Pick("Come on, we've got each other.", "Heads up, everyone.")
        };
    }

    static string Lazy(TalkContext ctx)
    {
        // 25/95, baseline checked-out — his rut is near-invisible (shares the silence pool with Shy). INVERTED tell:
        // if a Lazy player says something genuinely engaged, THAT'S the anomaly.
        if (ctx.recovered) return Pick("Actually… yeah, go on. Let's have it.", "Fine, I'm in. Properly this time.");
        if (ctx.inRut) return Pick(FlatMutter); // indistinguishable from his baseline

        if (ctx.phase == ChatterPhase.PreTalk)
            return Result(ctx.goalDifference) switch
            {
                ResultBand.Losing => Pick("…are we done yet?", "Yeah, whatever, we'll be alright."),
                _                 => Pick("Yeah, whatever, we'll be alright.", "Can we just play the second half.")
            };

        return Took(ctx.reaction) switch
        {
            ReactionBand.Positive => Pick("Sure. Fine.", "Yeah, alright, good speech."),
            ReactionBand.Negative => Pick("Ugh, do we have to.", "Alright, alright, calm down."),
            _                     => Pick(FlatMutter)
        };
    }

    static string Shy(TalkContext ctx)
    {
        // 100/70, usually silent — his rut is IDENTICAL to baseline (Speaks() usually returns false anyway), the
        // archetypal red herring. INVERTED tell: him speaking at all means he's in a good place.
        if (ctx.recovered) return Pick("…thanks, gaffer.", "…we can do this.");
        if (ctx.inRut) return Pick(FlatMutter); // if he speaks at all, nothing to read

        if (ctx.phase == ChatterPhase.PreTalk) return Pick("…", "…okay.");

        return Took(ctx.reaction) switch
        {
            ReactionBand.Positive => Pick("…thanks, gaffer.", "…I'll do my best."),
            ReactionBand.Negative => Pick("…", "…sorry."),
            _                     => Pick("…okay.", "…")
        };
    }

    static string Silly(TalkContext ctx)
    {
        // 60/60, jokes to cope — a rut is the jokes STOPPING (flat/silent). Trap: a joke while LOSING is shared
        // between "fine and coping" and "masking a rut", so the quip alone tells you nothing.
        if (ctx.recovered) return Pick("Right, comeback's ON. I've decided.", "Okay okay, serious face now. Ish.");
        if (ctx.inRut) return Pick(Concat(FlatMutter, new[] { "…not really in the mood, boss.", "…" }));

        if (ctx.phase == ChatterPhase.PreTalk)
            return Result(ctx.goalDifference) switch
            {
                ResultBand.Losing => Pick(CopingJoke),
                _                 => Pick("We're basically Brazil, lads.", Pick(CopingJoke))
            };

        return Took(ctx.reaction) switch
        {
            ReactionBand.Positive => Pick("Aye aye, gaffer!", "Love it. To the moon, boys!"),
            ReactionBand.Negative => Pick(CopingJoke), // nervous deflecting joke — same pool as coping
            _                     => Pick("Noted, boss. Ish.", Pick(CopingJoke))
        };
    }

    static string Smart(TalkContext ctx)
    {
        // 70/80, always reading the game — a rut shows as NO read (uncharacteristic vagueness). Trap: his critical
        // pushback is normal (he questions everything), so "disagrees with you" ≠ "in a rut".
        if (ctx.recovered) return Pick("Right — I see it now. Overload their left, we win this.", "Okay. Back on it. Here's the plan.");
        if (ctx.inRut) return Pick("I don't know. I can't read it today.", "…no idea what's wrong, honestly.", "Nothing's clicking. Can't see it.");

        if (ctx.phase == ChatterPhase.PreTalk)
            return ctx.expectation == Expectation.Underperforming
                ? Pick("We're second to everything. It's effort, not tactics.", "We're losing the midfield battle — that's the game.")
                : Pick("We've got their full-back — overload that side.", "Their line's high. Play in behind.");

        return Took(ctx.reaction) switch
        {
            ReactionBand.Positive => Pick("Agreed. That's the adjustment.", "Yes — that's the fix. Good."),
            ReactionBand.Negative => Pick("…shouting doesn't change the midfield, gaffer.", "That's not the problem, though, is it."),
            _                     => Pick("Maybe. I'd tweak the press, personally.", "We'll see. I'd do it differently.")
        };
    }

    static string[] Concat(string[] a, string[] b)
    {
        var r = new string[a.Length + b.Length];
        a.CopyTo(r, 0);
        b.CopyTo(r, a.Length);
        return r;
    }

    // ————————————————————— expectation —————————————————————

    /// <summary>Points to shift the expected margin by one goal (rating gap → expected goals). Tunable difficulty knob.</summary>
    public const float RATING_POINTS_PER_GOAL = 8f;
    /// <summary>How far from the expected margin (in goals) counts as genuinely over/under-performing rather than on-par.</summary>
    public const float EXPECTATION_BAND = 1.25f;

    /// <summary>Bands the live scoreline against what pre-match strength said to expect. The caller supplies the
    /// expected goal difference (from relative team strength); we compare it to the actual margin.</summary>
    public static Expectation ExpectationFromScore(int goalDifference, float expectedGoalDifference)
    {
        float delta = goalDifference - expectedGoalDifference;
        if (delta >= EXPECTATION_BAND) return Expectation.Overperforming;
        if (delta <= -EXPECTATION_BAND) return Expectation.Underperforming;
        return Expectation.OnPar;
    }

    /// <summary>A short read on how the result compares to expectation — for the situation line above the arch.</summary>
    public static string ExpectationText(Expectation e) => e switch
    {
        Expectation.Overperforming  => "punching above your weight",
        Expectation.Underperforming => "below where you should be",
        _                           => "about as expected"
    };
}
