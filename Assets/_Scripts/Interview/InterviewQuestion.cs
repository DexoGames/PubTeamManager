using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Person;
using static Player.Position;

/// <summary>
/// Defines the types of questions that can be asked during an interview.
/// </summary>
public enum InterviewQuestionType
{
    // Stat-specific questions
    AskAboutStat,

    // General questions
    BiggestStrength,
    BiggestWeakness,

    // Personality/social questions
    BestPersonalityFit,
    WorstPersonalityFit,

    // Position questions
    PreferredPosition,

    // Ambition questions
    CareerGoals,

    // Comparison question — uses the player picker to choose any squad member to compare against
    CompareToPlayer,

    // ————— Personality-probing questions (give CLUES that narrow down the hidden personality) —————
    HandleCriticism,    // reveals how defensive/receptive/dismissive they are
    WorkEthic,          // reveals conscientiousness (grafter vs coaster)
    BigGameMentality,   // reveals composure under pressure
    Leadership          // reveals how outgoing / dominant they are
}

/// <summary>
/// Represents an interview question and generates appropriate responses based on personality.
/// </summary>
public class InterviewQuestion
{
    public InterviewQuestionType Type { get; private set; }
    public PlayerStat? TargetStat { get; private set; }
    public string QuestionText { get; private set; }

    public InterviewQuestion(InterviewQuestionType type, PlayerStat? targetStat = null)
    {
        Type = type;
        TargetStat = targetStat;
        QuestionText = GenerateQuestionText();
    }

    private string GenerateQuestionText()
    {
        return Type switch
        {
            InterviewQuestionType.AskAboutStat when TargetStat.HasValue =>
                $"How would you rate your {TargetStat.Value.ToString().ToLower()}?",
            InterviewQuestionType.BiggestStrength =>
                "What would you say is your biggest strength?",
            InterviewQuestionType.BiggestWeakness =>
                "What would you say is your biggest weakness?",
            InterviewQuestionType.BestPersonalityFit =>
                "What kind of teammates do you work best with?",
            InterviewQuestionType.WorstPersonalityFit =>
                "What kind of personalities do you struggle to work with?",
            InterviewQuestionType.PreferredPosition =>
                "What position do you prefer to play?",
            InterviewQuestionType.CareerGoals =>
                "What are your career ambitions?",
            InterviewQuestionType.CompareToPlayer =>
                "How do you rate yourself against another player?",
            InterviewQuestionType.HandleCriticism =>
                "How do you take it when the gaffer gets on your back?",
            InterviewQuestionType.WorkEthic =>
                "What does a day off look like for you?",
            InterviewQuestionType.BigGameMentality =>
                "How do you feel walking out for a massive game?",
            InterviewQuestionType.Leadership =>
                "What sort of voice are you in the dressing room?",
            _ => "Tell me about yourself."
        };
    }
}

/// <summary>
/// Generates interview answers. Each question sorts the 10 personalities into a small number of response
/// "buckets" (~3), and the answer TEXT is keyed on the bucket — so different personalities can give the same
/// answer to one question. Crucially the bucketing DIFFERS per question, and every answer returns its bucket as
/// the personality clue, so asking several questions and intersecting the buckets narrows the hidden personality.
///
/// Ability questions add shape-based variation on top: whether one stat/position is a clear standout or one of
/// many, with the threshold for "standout" shifted by the personality bucket (a boastful type calls something
/// their standout readily; a modest type rarely does).
/// </summary>
public static class InterviewAnswerGenerator
{
    public static InterviewAnswer GenerateAnswer(Player player, InterviewQuestion question)
    {
        return question.Type switch
        {
            InterviewQuestionType.AskAboutStat => AnswerAboutStat(player, question.TargetStat.Value),
            InterviewQuestionType.BiggestStrength => AnswerBiggestStrength(player),
            InterviewQuestionType.BiggestWeakness => AnswerBiggestWeakness(player),
            InterviewQuestionType.BestPersonalityFit => AnswerBestPersonalityFit(player),
            InterviewQuestionType.WorstPersonalityFit => AnswerWorstPersonalityFit(player),
            InterviewQuestionType.PreferredPosition => AnswerPreferredPosition(player),
            InterviewQuestionType.CareerGoals => AnswerCareerGoals(player),
            // CompareToPlayer needs a chosen rival, so it's generated via GenerateComparisonAnswer (after the
            // player picker) rather than here; this is just a safe fallback.
            InterviewQuestionType.CompareToPlayer => new InterviewAnswer("Compared to who, boss?", null, null),
            InterviewQuestionType.HandleCriticism => AnswerHandleCriticism(player),
            InterviewQuestionType.WorkEthic => AnswerWorkEthic(player),
            InterviewQuestionType.BigGameMentality => AnswerBigGameMentality(player),
            InterviewQuestionType.Leadership => AnswerLeadership(player),
            _ => new InterviewAnswer("I'm not sure what to say about that.", null, null)
        };
    }

    /// <summary>
    /// Comparison answer (the rival is chosen via the player picker). Scores ability as the signed square root of
    /// each per-stat difference, summed — so an all-round gap reads big while no single stat can dominate. The
    /// personality bucket biases where the boundary sits (a cocky type rates themselves up, a modest one down) and
    /// doubles as the clue. Bands run: on another level → an edge → about the same → an edge to them → miles off.
    /// </summary>
    public static InterviewAnswer GenerateComparisonAnswer(Player me, Player other)
    {
        if (me == null || other == null) return new InterviewAnswer("Compared to who, boss?", null, null);

        float total = 0f;
        for (int i = 0; i < Player.SKILL_NO; i++)
        {
            int diff = me.RawStats.Skills[i] - other.RawStats.Skills[i];
            total += Mathf.Sign(diff) * Mathf.Sqrt(Mathf.Abs(diff));
        }

        var (bucket, group) = Bucket(me.Personality,
            G(PersonalityType.Cocky, PersonalityType.Aggressive, PersonalityType.Driven),                  // talk themselves up
            G(PersonalityType.Shy, PersonalityType.Kind, PersonalityType.Cautious, PersonalityType.Calm),  // play it down
            G(PersonalityType.Smart, PersonalityType.Lazy, PersonalityType.Silly));                        // call it straight

        float bias = bucket == 0 ? 12f : bucket == 1 ? -12f : 0f;
        float perceived = total + bias;

        string them = other.FullName;
        int band = perceived >= 45f ? 2 : perceived >= 18f ? 1 : perceived > -18f ? 0 : perceived > -45f ? -1 : -2;

        string response = band switch
        {
            2  => $"Honestly? I'm miles better than {them}. Not close.",
            1  => $"I'd back myself over {them} — I've got the edge.",
            0  => $"Me and {them}? About the same, I reckon.",
            -1 => $"{them}'s got a bit on me, in fairness.",
            _  => $"{them}? They're on another level to me, I'll be honest."
        };
        return new InterviewAnswer(response, Mathf.RoundToInt(total), Mathf.RoundToInt(perceived), group);
    }

    // ————————————————————— bucketing —————————————————————

    /// <summary>Finds which response bucket a personality falls in. Returns the index and the bucket's members
    /// (the members double as the personality clue). Buckets should cover all 10 personalities.</summary>
    private static (int index, PersonalityType[] group) Bucket(PersonalityType p, params PersonalityType[][] buckets)
    {
        for (int i = 0; i < buckets.Length; i++)
            if (Array.IndexOf(buckets[i], p) >= 0) return (i, buckets[i]);
        return (buckets.Length - 1, buckets[buckets.Length - 1]); // fallback (shouldn't happen if buckets are complete)
    }

    private static PersonalityType[] G(params PersonalityType[] members) => members;

    // ————————————————————— ability questions (bucket × stat-shape) —————————————————————

    private static InterviewAnswer AnswerAboutStat(Player player, PlayerStat stat)
    {
        // Bucketed by self-rating bias: over-raters / under-raters / honest.
        var (i, group) = Bucket(player.Personality,
            G(PersonalityType.Cocky, PersonalityType.Aggressive, PersonalityType.Silly),
            G(PersonalityType.Shy, PersonalityType.Cautious, PersonalityType.Kind),
            G(PersonalityType.Smart, PersonalityType.Driven, PersonalityType.Calm, PersonalityType.Lazy));

        int actual = player.RawStats.GetStat(stat);
        int bias = i == 0 ? UnityEngine.Random.Range(8, 18)
                 : i == 1 ? UnityEngine.Random.Range(-18, -6)
                          : UnityEngine.Random.Range(-4, 5);
        int perceived = Mathf.Clamp(actual + bias, 0, 100);
        string rating = StatValueToDescription(perceived);
        string name = stat.ToString().ToLower();

        string response = i switch
        {
            0 => $"My {name}? {rating}. One of the best bits of my game, that.",
            1 => $"My {name} is... {rating}, I'd say. I don't like to talk myself up.",
            _ => $"I'd put my {name} at about {rating}. Sounds right to me."
        };
        return new InterviewAnswer(response, actual, perceived, group);
    }

    private static InterviewAnswer AnswerBiggestStrength(Player player)
    {
        // Bucketed by bravado: boastful / humble / matter-of-fact.
        var (i, group) = Bucket(player.Personality,
            G(PersonalityType.Cocky, PersonalityType.Aggressive, PersonalityType.Driven),
            G(PersonalityType.Shy, PersonalityType.Kind, PersonalityType.Cautious, PersonalityType.Calm),
            G(PersonalityType.Smart, PersonalityType.Lazy, PersonalityType.Silly));

        PlayerStat best = GetBestStat(player);
        string name = best.ToString().ToLower();

        // "By far and away their best" vs "one of many" — and the bar for calling it a standout shifts by bucket
        // (boastful claims it easily, humble rarely, matter-of-fact only when it's genuinely dominant).
        int threshold = i == 0 ? 6 : i == 2 ? 12 : 20;
        bool standout = TopGap(player) >= threshold;

        string response = (i, standout) switch
        {
            (0, true)  => $"Easy! My {name}. It's miles ahead of anything else I do.",
            (0, false) => $"My {name}, for sure. Though honestly I'm dangerous all over the pitch.",
            (1, true)  => $"People are kind about my {name}. I suppose it's my main thing.",
            (1, false) => $"I wouldn't single one out... my {name}, maybe? I just try to do a bit of everything.",
            (2, true)  => $"My {name} is clearly my strongest area.",
            _          => $"Probably my {name}, but I'm fairly even across the board."
        };
        return new InterviewAnswer(response, best.ToString(), best.ToString(), group);
    }

    private static InterviewAnswer AnswerBiggestWeakness(Player player)
    {
        // Bucketed by honesty about flaws: deflect / owns it / shrugs it off.
        var (i, group) = Bucket(player.Personality,
            G(PersonalityType.Cocky, PersonalityType.Aggressive),
            G(PersonalityType.Shy, PersonalityType.Kind, PersonalityType.Cautious, PersonalityType.Driven, PersonalityType.Calm),
            G(PersonalityType.Lazy, PersonalityType.Silly, PersonalityType.Smart));

        PlayerStat worst = GetWorstStat(player);
        string name = worst.ToString().ToLower();
        bool glaring = player.RawStats.Skills.Min() < 40; // a genuine hole vs just their lowest of a good set

        string response = (i, glaring) switch
        {
            (0, true)  => $"Weakness? If pushed... my {name}. But I more than make up for it elsewhere.",
            (0, false) => "Weakness? None spring to mind.",
            (1, true)  => $"My {name}, hands down. I know it's a problem and I'm working hard on it.",
            (1, false) => $"My {name} could be sharper, but there's nothing I'd call a real flaw.",
            (2, true)  => $"Statistically it's my {name}. I just play around it.",
            _          => $"Maybe my {name}? Nothing major though."
        };
        return new InterviewAnswer(response, worst.ToString(), worst.ToString(), group);
    }

    private static InterviewAnswer AnswerPreferredPosition(Player player)
    {
        // Bucketed by assertiveness about their role: demanding / flexible / reserved.
        var (i, group) = Bucket(player.Personality,
            G(PersonalityType.Cocky, PersonalityType.Aggressive),
            G(PersonalityType.Kind, PersonalityType.Calm, PersonalityType.Lazy, PersonalityType.Silly, PersonalityType.Smart),
            G(PersonalityType.Shy, PersonalityType.Cautious, PersonalityType.Driven));

        Player.Position best = BestPositionByStrength(player);
        string pos = Player.LongPosition(best);
        bool versatile = CountStrongPositions(player) >= 3; // can genuinely do a few roles

        string response = (i, versatile) switch
        {
            (0, true)  => $"Play me anywhere across the line. But I'm at my best at {pos}.",
            (0, false) => $"{pos}. That's my position, don't be playing me out of it.",
            (1, true)  => $"Happy wherever you need me! I can fill in all over — though {pos} suits me best.",
            (1, false) => $"I'll play where you ask, but I'm really a {pos}.",
            (2, true)  => $"I can cover a few roles. On balance {pos} is probably my strongest.",
            _          => $"I'm a {pos}. That's where I'm most comfortable."
        };
        return new InterviewAnswer(response, best.ToString(), best.ToString(), group);
    }

    private static InterviewAnswer AnswerCareerGoals(Player player)
    {
        // Bucketed by ambition: hungry / steady / just-here-for-fun.
        var (i, group) = Bucket(player.Personality,
            G(PersonalityType.Cocky, PersonalityType.Aggressive, PersonalityType.Driven),
            G(PersonalityType.Calm, PersonalityType.Cautious, PersonalityType.Kind, PersonalityType.Shy),
            G(PersonalityType.Lazy, PersonalityType.Silly, PersonalityType.Smart));

        string response = i switch
        {
            0 => "I want to win everything! Trophies, awards, the lot. Not here to make up numbers.",
            1 => "Play well, stay fit, win a few things with a good group. A solid career'll do me.",
            _ => "Enjoy my football, score a few worldies, have a laugh. Whatever comes, comes."
        };
        return new InterviewAnswer(response, null, null, group);
    }

    private static InterviewAnswer AnswerBestPersonalityFit(Player player)
    {
        // Bucketed by warmth: gets-on-with-anyone / picky / easy-going.
        var (i, group) = Bucket(player.Personality,
            G(PersonalityType.Kind, PersonalityType.Calm, PersonalityType.Silly, PersonalityType.Cocky),
            G(PersonalityType.Aggressive, PersonalityType.Smart),
            G(PersonalityType.Shy, PersonalityType.Lazy, PersonalityType.Driven, PersonalityType.Cautious));

        string fit = player.GetBestCompatiblePersonality().ToString().ToLower();
        string response = i switch
        {
            0 => $"I get on with everyone! Me, {fit} types especially. Good people.",
            1 => $"I work best with {fit} sorts. The rest can be hard work, if I'm honest.",
            _ => $"{fit} teammates suit me. Easy to play alongside."
        };
        return new InterviewAnswer(response, fit, fit, group);
    }

    private static InterviewAnswer AnswerWorstPersonalityFit(Player player)
    {
        // Same warmth bucketing as BestFit (consistent voice), different content.
        var (i, group) = Bucket(player.Personality,
            G(PersonalityType.Kind, PersonalityType.Calm, PersonalityType.Silly, PersonalityType.Cocky),
            G(PersonalityType.Aggressive, PersonalityType.Smart),
            G(PersonalityType.Shy, PersonalityType.Lazy, PersonalityType.Driven, PersonalityType.Cautious));

        string clash = player.GetWorstCompatiblePersonality().ToString().ToLower();
        string response = i switch
        {
            0 => $"I try with everyone, but {clash} types can be a bit much.",
            1 => $"{clash} people. We just don't see eye to eye.",
            _ => $"I keep clear of {clash} sorts where I can. Too much friction."
        };
        return new InterviewAnswer(response, clash, clash, group);
    }

    // ————————————————————— personality-probe questions (pure bucket clue) —————————————————————

    private static InterviewAnswer AnswerHandleCriticism(Player player)
    {
        var (i, group) = Bucket(player.Personality,
            G(PersonalityType.Aggressive, PersonalityType.Cocky),                                                  // defensive
            G(PersonalityType.Shy, PersonalityType.Kind),                                                          // sensitive
            G(PersonalityType.Lazy, PersonalityType.Silly),                                                        // dismissive
            G(PersonalityType.Calm, PersonalityType.Smart, PersonalityType.Driven, PersonalityType.Cautious));     // receptive

        string response = i switch
        {
            0 => "I usually back myself.",
            1 => "It... gets to me a bit, if I'm honest. But I really do want to get it right.",
            2 => "I mean... I'll hear them out, but there's no point stressing over it.",
            _ => "I welcome it, as long as it's specific. Tell me what's wrong and I'll fix it."
        };
        return new InterviewAnswer(response, player.Personality.ToString(), null, group);
    }

    private static InterviewAnswer AnswerWorkEthic(Player player)
    {
        var (i, group) = Bucket(player.Personality,
            G(PersonalityType.Driven, PersonalityType.Smart, PersonalityType.Aggressive), // grafters
            G(PersonalityType.Lazy, PersonalityType.Silly, PersonalityType.Cocky),                                  // coasters
            G(PersonalityType.Calm, PersonalityType.Kind, PersonalityType.Shy, PersonalityType.Cautious));                                    // balanced

        string response = i switch
        {
            0 => "Day off? I'll still get a session in or watch clips. Can't sit still.",
            1 => "Sofa, telly, maybe a kebab. Got to recharge, haven't you?",
            _ => "A quiet one! Family, early night, look after myself properly."
        };
        return new InterviewAnswer(response, player.Personality.ToString(), null, group);
    }

    private static InterviewAnswer AnswerBigGameMentality(Player player)
    {
        var (i, group) = Bucket(player.Personality,
            G(PersonalityType.Cocky, PersonalityType.Aggressive, PersonalityType.Driven), // thrive
            G(PersonalityType.Calm, PersonalityType.Smart, PersonalityType.Kind),         // composed
            G(PersonalityType.Shy, PersonalityType.Cautious),                             // nervy
            G(PersonalityType.Silly, PersonalityType.Lazy));                              // loose

        string response = i switch
        {
            0 => "The bigger the game, the more I want it.",
            1 => "Same as any other game. I keep my head and do my job.",
            2 => "A bit nervous, to be honest... but the nerves keep me sharp!",
            _ => "Big game, five-a-side, whatever. I'm here to have fun!"
        };
        return new InterviewAnswer(response, player.Personality.ToString(), null, group);
    }

    private static InterviewAnswer AnswerLeadership(Player player)
    {
        var (i, group) = Bucket(player.Personality,
            G(PersonalityType.Aggressive, PersonalityType.Cocky, PersonalityType.Driven), // vocal
            G(PersonalityType.Calm, PersonalityType.Smart, PersonalityType.Cautious),     // by example
            G(PersonalityType.Kind, PersonalityType.Silly),                               // morale
            G(PersonalityType.Shy, PersonalityType.Lazy));                                // quiet

        string response = i switch
        {
            0 => "Loud. I organise, I demand, and I'll let you know if you're slacking.",
            1 => "I talk when it matters. A word in the right ear at the right time.",
            2 => "I'm the one keeping spirits up, having a laugh, picking the lads up.",
            _ => "I keep myself to myself, really. Not one for big speeches."
        };
        return new InterviewAnswer(response, player.Personality.ToString(), null, group);
    }

    // ————————————————————— stat / position helpers —————————————————————

    /// <summary>Gap between the best skill and the third-best — large = one clear standout, small = several near the top.</summary>
    private static int TopGap(Player player)
    {
        int[] s = (int[])player.RawStats.Skills.Clone();
        Array.Sort(s);
        Array.Reverse(s); // descending
        if (s.Length >= 3) return s[0] - s[2];
        if (s.Length >= 2) return s[0] - s[1];
        return 0;
    }

    private static PlayerStat GetBestStat(Player player)
    {
        PlayerStat best = PlayerStat.Shooting;
        int bestValue = -1;
        for (int i = 0; i < Player.SKILL_NO; i++)
            if (player.RawStats.Skills[i] > bestValue) { bestValue = player.RawStats.Skills[i]; best = (PlayerStat)i; }
        return best;
    }

    private static PlayerStat GetWorstStat(Player player)
    {
        PlayerStat worst = PlayerStat.Shooting;
        int worstValue = 101;
        for (int i = 0; i < Player.SKILL_NO; i++)
            if (player.RawStats.Skills[i] < worstValue) { worstValue = player.RawStats.Skills[i]; worst = (PlayerStat)i; }
        return worst;
    }

    private static Player.Position BestPositionByStrength(Player player)
    {
        Player.Position best = Player.Position.CM;
        int bestValue = -1;
        foreach (var kv in player.RawStats.Positions)
            if ((int)kv.Value > bestValue) { bestValue = (int)kv.Value; best = kv.Key; }
        return best;
    }

    private static int CountStrongPositions(Player player)
    {
        int count = 0;
        foreach (var kv in player.RawStats.Positions)
            if (kv.Value >= Player.PositionStrength.Good) count++;
        return count;
    }

    private static string StatValueToDescription(int value) => value switch
    {
        >= 90 => "amazing",
        >= 80 => "excellent",
        >= 70 => "very good",
        >= 60 => "good",
        >= 50 => "decent",
        >= 40 => "average",
        >= 30 => "below average",
        >= 20 => "poor",
        _ => "weak"
    };
}

/// <summary>
/// Represents an answer to an interview question.
/// </summary>
public class InterviewAnswer
{
    public string ResponseText { get; private set; }
    public object ActualValue { get; private set; }
    public object PerceivedValue { get; private set; }

    /// <summary>
    /// The set of personalities consistent with this answer's response bucket. Every answer sets it (the bucketing
    /// differs per question), and the interview manager intersects them across questions to narrow the hidden
    /// personality. Null only for the fallback answer.
    /// </summary>
    public PersonalityType[] PossiblePersonalities { get; private set; }

    /// <summary>
    /// Whether the player's perception matches reality.
    /// </summary>
    public bool IsAccurate => ActualValue?.ToString() == PerceivedValue?.ToString();

    public InterviewAnswer(string responseText, object actualValue, object perceivedValue,
                           PersonalityType[] possiblePersonalities = null)
    {
        ResponseText = responseText;
        ActualValue = actualValue;
        PerceivedValue = perceivedValue;
        PossiblePersonalities = possiblePersonalities;
    }
}
