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
/// Generates interview answers based on player stats and personality.
/// Personality affects honesty and self-perception.
/// </summary>
public static class InterviewAnswerGenerator
{
    /// <summary>
    /// Generate an answer for the given question from this player.
    /// Returns the answer text and optionally the "true" value for comparison.
    /// </summary>
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
            InterviewQuestionType.HandleCriticism => AnswerHandleCriticism(player),
            InterviewQuestionType.WorkEthic => AnswerWorkEthic(player),
            InterviewQuestionType.BigGameMentality => AnswerBigGameMentality(player),
            InterviewQuestionType.Leadership => AnswerLeadership(player),
            _ => new InterviewAnswer("I'm not sure what to say about that.", null, null)
        };
    }

    private static InterviewAnswer AnswerAboutStat(Player player, PlayerStat stat)
    {
        int actualValue = player.RawStats.GetStat(stat);
        int perceivedValue = GetPerceivedStatValue(player, stat, actualValue);
        string rating = StatValueToDescription(perceivedValue);
        
        string response = player.Personality switch
        {
            PersonalityType.Cocky => $"My {stat.ToString().ToLower()}? It's {rating}. One of my best attributes, honestly.",
            PersonalityType.Shy => $"Um, I think my {stat.ToString().ToLower()} is... {rating}? I'm not sure.",
            PersonalityType.Aggressive => $"My {stat.ToString().ToLower()} is {rating}. I'll prove it on the pitch.",
            PersonalityType.Kind => $"I'd say my {stat.ToString().ToLower()} is {rating}. I'm always trying to improve though!",
            PersonalityType.Lazy => $"Eh, my {stat.ToString().ToLower()}? It's {rating}, I guess.",
            PersonalityType.Driven => $"My {stat.ToString().ToLower()} is currently {rating}, but I'm working hard to improve it every day.",
            PersonalityType.Smart => $"Objectively speaking, my {stat.ToString().ToLower()} is {rating}.",
            PersonalityType.Silly => $"Haha, my {stat.ToString().ToLower()}? Let's just say it's {rating}!",
            PersonalityType.Cautious => $"I'd carefully estimate my {stat.ToString().ToLower()} at {rating}.",
            PersonalityType.Calm => $"My {stat.ToString().ToLower()} is {rating}. Nothing special, but consistent.",
            _ => $"My {stat.ToString().ToLower()} is {rating}."
        };

        return new InterviewAnswer(response, actualValue, perceivedValue);
    }

    private static InterviewAnswer AnswerBiggestStrength(Player player)
    {
        // Find actual best stat
        PlayerStat actualBest = GetBestStat(player);
        int actualBestValue = player.RawStats.GetStat(actualBest);
        
        // Personality might affect what they claim
        PlayerStat claimedBest = GetPerceivedBestStat(player);
        
        string response = player.Personality switch
        {
            PersonalityType.Cocky => $"Where do I even start? I'd say my {claimedBest.ToString().ToLower()} is world-class.",
            PersonalityType.Shy => $"I think... maybe my {claimedBest.ToString().ToLower()}? People have said it's decent.",
            PersonalityType.Aggressive => $"My {claimedBest.ToString().ToLower()}. I dominate with it.",
            PersonalityType.Kind => $"I've been told my {claimedBest.ToString().ToLower()} is pretty good!",
            PersonalityType.Lazy => $"Probably my {claimedBest.ToString().ToLower()}. It comes naturally.",
            PersonalityType.Driven => $"I take pride in my {claimedBest.ToString().ToLower()}. I've worked incredibly hard on it.",
            PersonalityType.Smart => $"Statistically, my {claimedBest.ToString().ToLower()} is my strongest attribute.",
            PersonalityType.Silly => $"Easy! My {claimedBest.ToString().ToLower()}! Watch me show off sometime!",
            PersonalityType.Cautious => $"I'd say my {claimedBest.ToString().ToLower()}, though I try not to rely on just one thing.",
            PersonalityType.Calm => $"Definitely my {claimedBest.ToString().ToLower()}. It's reliable.",
            _ => $"My {claimedBest.ToString().ToLower()} is my biggest strength."
        };

        return new InterviewAnswer(response, actualBest.ToString(), claimedBest.ToString());
    }

    private static InterviewAnswer AnswerBiggestWeakness(Player player)
    {
        // Find actual worst stat
        PlayerStat actualWorst = GetWorstStat(player);
        int actualWorstValue = player.RawStats.GetStat(actualWorst);
        
        // Personality affects honesty about weakness
        PlayerStat claimedWorst = GetPerceivedWorstStat(player);
        
        string response = player.Personality switch
        {
            PersonalityType.Cocky => $"Weakness? I mean, maybe my {claimedWorst.ToString().ToLower()}, but it's still better than most.",
            PersonalityType.Shy => $"Oh, there's a lot... but I guess my {claimedWorst.ToString().ToLower()} could use work.",
            PersonalityType.Aggressive => $"I don't have weaknesses. Fine... {claimedWorst.ToString().ToLower()}, but I make up for it.",
            PersonalityType.Kind => $"I'm trying to improve my {claimedWorst.ToString().ToLower()}. It's a work in progress!",
            PersonalityType.Lazy => $"Probably {claimedWorst.ToString().ToLower()}... too much effort to fix it though.",
            PersonalityType.Driven => $"My {claimedWorst.ToString().ToLower()} needs improvement. I'm already working on a plan to fix it.",
            PersonalityType.Smart => $"I'm aware my {claimedWorst.ToString().ToLower()} is below average. I've analyzed it thoroughly.",
            PersonalityType.Silly => $"Haha, probably {claimedWorst.ToString().ToLower()}! But who's counting?",
            PersonalityType.Cautious => $"I'd say my {claimedWorst.ToString().ToLower()}. I'm careful not to put myself in situations that expose it.",
            PersonalityType.Calm => $"My {claimedWorst.ToString().ToLower()} isn't great, but I manage.",
            _ => $"My {claimedWorst.ToString().ToLower()} is probably my weakest area."
        };

        return new InterviewAnswer(response, actualWorst.ToString(), claimedWorst.ToString());
    }

    private static InterviewAnswer AnswerBestPersonalityFit(Player player)
    {
        PersonalityType bestFit = player.GetBestCompatiblePersonality();
        
        string response = player.Personality switch
        {
            PersonalityType.Cocky => $"People who appreciate greatness. {bestFit} types usually get me.",
            PersonalityType.Shy => $"I like working with {bestFit.ToString().ToLower()} people. They make me comfortable.",
            PersonalityType.Aggressive => $"{bestFit} teammates. They match my energy or calm me down when needed.",
            PersonalityType.Kind => $"I get along with everyone! But {bestFit.ToString().ToLower()} people are wonderful.",
            PersonalityType.Lazy => $"Eh, {bestFit.ToString().ToLower()} people are chill. No drama.",
            PersonalityType.Driven => $"{bestFit} individuals who share my work ethic and ambition.",
            PersonalityType.Smart => $"I work best with {bestFit.ToString().ToLower()} teammates. Good synergy.",
            PersonalityType.Silly => $"{bestFit} people! They're fun to be around!",
            PersonalityType.Cautious => $"I prefer {bestFit.ToString().ToLower()} teammates. Predictable and reliable.",
            PersonalityType.Calm => $"{bestFit} personalities. We understand each other.",
            _ => $"I work well with {bestFit.ToString().ToLower()} people."
        };

        return new InterviewAnswer(response, bestFit.ToString(), bestFit.ToString());
    }

    private static InterviewAnswer AnswerWorstPersonalityFit(Player player)
    {
        PersonalityType worstFit = player.GetWorstCompatiblePersonality();
        
        string response = player.Personality switch
        {
            PersonalityType.Cocky => $"People who can't keep up. {worstFit} types drag me down.",
            PersonalityType.Shy => $"I struggle with {worstFit.ToString().ToLower()} people... they're intimidating.",
            PersonalityType.Aggressive => $"{worstFit} people annoy me. We clash constantly.",
            PersonalityType.Kind => $"I try to get along with everyone, but {worstFit.ToString().ToLower()} people can be... challenging.",
            PersonalityType.Lazy => $"{worstFit} types are exhausting. Too intense.",
            PersonalityType.Driven => $"{worstFit} personalities frustrate me. Different values.",
            PersonalityType.Smart => $"{worstFit} people. Our approaches don't align.",
            PersonalityType.Silly => $"{worstFit} people don't get my humor. Their loss!",
            PersonalityType.Cautious => $"{worstFit} personalities make me nervous. Too unpredictable.",
            PersonalityType.Calm => $"I find {worstFit.ToString().ToLower()} types can be difficult.",
            _ => $"I struggle with {worstFit.ToString().ToLower()} personalities."
        };

        return new InterviewAnswer(response, worstFit.ToString(), worstFit.ToString());
    }

    private static InterviewAnswer AnswerPreferredPosition(Player player)
    {
        Player.Position bestPosition = player.RawStats.Positions
            .OrderByDescending(p => p.Value)
            .First().Key;
        
        string response = player.Personality switch
        {
            PersonalityType.Cocky => $"Put me at {bestPosition}. That's where I shine brightest.",
            PersonalityType.Shy => $"I usually play {bestPosition}... if that's okay with you.",
            PersonalityType.Aggressive => $"{bestPosition}. That's where I can make the biggest impact.",
            PersonalityType.Kind => $"I'm flexible, but I'm probably best at {bestPosition}.",
            PersonalityType.Lazy => $"{bestPosition}. It's what I know.",
            PersonalityType.Driven => $"{bestPosition} is where I've focused my development.",
            PersonalityType.Smart => $"Based on my skillset, {bestPosition} is optimal.",
            PersonalityType.Silly => $"{bestPosition}! It's where the magic happens!",
            PersonalityType.Cautious => $"I'm most comfortable at {bestPosition}.",
            PersonalityType.Calm => $"{bestPosition} suits me well.",
            _ => $"I prefer playing {bestPosition}."
        };

        return new InterviewAnswer(response, bestPosition.ToString(), bestPosition.ToString());
    }

    private static InterviewAnswer AnswerCareerGoals(Player player)
    {
        string response = player.Personality switch
        {
            PersonalityType.Cocky => "I'm going to be the best. Multiple trophies, individual awards - the whole package.",
            PersonalityType.Shy => "I just want to play well and help the team... hopefully win something nice.",
            PersonalityType.Aggressive => "I want to dominate. Be feared by every opponent.",
            PersonalityType.Kind => "I want to inspire young players and make a positive impact on the game.",
            PersonalityType.Lazy => "Honestly? A steady career, good paycheck, retire comfortably.",
            PersonalityType.Driven => "I want to maximize my potential. Championships, records, legacy.",
            PersonalityType.Smart => "Calculated progression through top leagues, then transition to coaching or management.",
            PersonalityType.Silly => "Have fun, score some bangers, maybe get a viral celebration!",
            PersonalityType.Cautious => "Steady improvement, avoiding injuries, long sustainable career.",
            PersonalityType.Calm => "Consistent performance, win trophies, be remembered as reliable.",
            _ => "I want to be the best I can be."
        };

        return new InterviewAnswer(response, null, null);
    }

    // ————————————————————— Personality-probing answers (return personality CLUES) —————————————————————
    // Each of these narrows the hidden personality to a small candidate set. The answer TEXT is unique
    // per personality (their own voice), while the CLUE is the group of personalities that would answer
    // in that style — intersect several and you can pin the personality down.

    private static InterviewAnswer AnswerHandleCriticism(Player player)
    {
        string response = player.Personality switch
        {
            PersonalityType.Aggressive => "Honestly? It winds me up. I back myself and I don't like being told I'm wrong.",
            PersonalityType.Cocky => "I hear it — but nine times out of ten I'm right and the gaffer comes round to my way.",
            PersonalityType.Calm => "I take it on the chin. It's just information; I use it and move on.",
            PersonalityType.Smart => "I welcome it, as long as it's specific. Feedback is data — I act on it.",
            PersonalityType.Driven => "I love it. Tell me what's wrong and I'll work twice as hard to fix it.",
            PersonalityType.Cautious => "I listen carefully. I'd rather hear it early than make the same mistake twice.",
            PersonalityType.Shy => "It... gets to me a bit, if I'm honest. But I really do want to get it right.",
            PersonalityType.Kind => "I take it to heart, because I never want to let the lads down.",
            PersonalityType.Lazy => "Eh. In one ear, out the other. No point stressing over it.",
            PersonalityType.Silly => "Ha! I just nod along and crack a joke. Keeps it light!",
            _ => "I try to take it well."
        };
        return new InterviewAnswer(response, player.Personality.ToString(), null, CriticismGroup(player.Personality));
    }

    private static InterviewAnswer AnswerWorkEthic(Player player)
    {
        string response = player.Personality switch
        {
            PersonalityType.Driven => "Day off? I'll still be in the gym or watching clips of my marker.",
            PersonalityType.Smart => "I review my numbers and rest deliberately — recovery is part of the work.",
            PersonalityType.Cautious => "I do my stretches, eat right, get an early night. I don't cut corners.",
            PersonalityType.Aggressive => "I train. Sitting still does my head in — I'd rather be grafting.",
            PersonalityType.Lazy => "Sofa, telly, maybe a nap. Or two. Got to recharge, haven't you?",
            PersonalityType.Silly => "Mate, gaming all day and a kebab. Living the dream!",
            PersonalityType.Cocky => "I don't need extra sessions — I'm already better than most. I relax.",
            PersonalityType.Calm => "A quiet one. Walk the dog, see family, switch off properly.",
            PersonalityType.Kind => "I usually help out — see my nan, do a bit for the local club.",
            PersonalityType.Shy => "Nothing flash. Stay in, read, keep myself to myself really.",
            _ => "A bit of rest, a bit of training."
        };
        return new InterviewAnswer(response, player.Personality.ToString(), null, WorkEthicGroup(player.Personality));
    }

    private static InterviewAnswer AnswerBigGameMentality(Player player)
    {
        string response = player.Personality switch
        {
            PersonalityType.Cocky => "Big crowd, big stage? That's MY stage. Bring it on.",
            PersonalityType.Aggressive => "I love it. The bigger the game, the more I want to hurt them.",
            PersonalityType.Driven => "That's what all the work is for. I can't wait for those games.",
            PersonalityType.Calm => "Same as any other game. I keep my head and do my job.",
            PersonalityType.Smart => "I prepare the same way, so the occasion doesn't change my decisions.",
            PersonalityType.Kind => "Exciting! I just want to do the lads and the fans proud.",
            PersonalityType.Shy => "A bit nervous, to be honest... but the nerves keep me sharp.",
            PersonalityType.Cautious => "I do worry about it. I just try to focus on what I can control.",
            PersonalityType.Silly => "Big game, five-a-side, whatever — I'm just there for a laugh!",
            PersonalityType.Lazy => "It's only a game, innit? I don't really get worked up.",
            _ => "I just try to play my game."
        };
        return new InterviewAnswer(response, player.Personality.ToString(), null, BigGameGroup(player.Personality));
    }

    private static InterviewAnswer AnswerLeadership(Player player)
    {
        string response = player.Personality switch
        {
            PersonalityType.Aggressive => "Loud. I organise, I demand, and I'll let you know if you're slacking.",
            PersonalityType.Cocky => "I'm the main voice — lads look to me, and rightly so.",
            PersonalityType.Driven => "I lead by setting the standard, and I'll drag others up to it.",
            PersonalityType.Calm => "I'm not loud. I let my game do the talking and stay steady.",
            PersonalityType.Smart => "I talk when it matters — a word in the right ear, the right time.",
            PersonalityType.Cautious => "I'm measured. I'd rather quietly organise than shout the odds.",
            PersonalityType.Kind => "I'm the one picking lads up, keeping spirits up when it's tough.",
            PersonalityType.Silly => "I'm the joker! I keep everyone loose — morale's my department.",
            PersonalityType.Shy => "I keep myself to myself, really. I'm not one for big speeches.",
            PersonalityType.Lazy => "I'm pretty quiet. Don't really go in for all the shouting.",
            _ => "I do my bit in the dressing room."
        };
        return new InterviewAnswer(response, player.Personality.ToString(), null, LeadershipGroup(player.Personality));
    }

    private static PersonalityType[] CriticismGroup(PersonalityType p)
    {
        var defensive = new[] { PersonalityType.Aggressive, PersonalityType.Cocky };
        var sensitive = new[] { PersonalityType.Shy, PersonalityType.Kind };
        var dismissive = new[] { PersonalityType.Lazy, PersonalityType.Silly };
        var receptive = new[] { PersonalityType.Calm, PersonalityType.Smart, PersonalityType.Driven, PersonalityType.Cautious };
        if (Array.IndexOf(defensive, p) >= 0) return defensive;
        if (Array.IndexOf(sensitive, p) >= 0) return sensitive;
        if (Array.IndexOf(dismissive, p) >= 0) return dismissive;
        return receptive;
    }

    private static PersonalityType[] WorkEthicGroup(PersonalityType p)
    {
        var grafters = new[] { PersonalityType.Driven, PersonalityType.Smart, PersonalityType.Cautious, PersonalityType.Aggressive };
        var coasters = new[] { PersonalityType.Lazy, PersonalityType.Silly, PersonalityType.Cocky };
        var balanced = new[] { PersonalityType.Calm, PersonalityType.Kind, PersonalityType.Shy };
        if (Array.IndexOf(grafters, p) >= 0) return grafters;
        if (Array.IndexOf(coasters, p) >= 0) return coasters;
        return balanced;
    }

    private static PersonalityType[] BigGameGroup(PersonalityType p)
    {
        var thrive = new[] { PersonalityType.Cocky, PersonalityType.Aggressive, PersonalityType.Driven };
        var composed = new[] { PersonalityType.Calm, PersonalityType.Smart, PersonalityType.Kind };
        var nervy = new[] { PersonalityType.Shy, PersonalityType.Cautious };
        var loose = new[] { PersonalityType.Silly, PersonalityType.Lazy };
        if (Array.IndexOf(thrive, p) >= 0) return thrive;
        if (Array.IndexOf(composed, p) >= 0) return composed;
        if (Array.IndexOf(nervy, p) >= 0) return nervy;
        return loose;
    }

    private static PersonalityType[] LeadershipGroup(PersonalityType p)
    {
        var vocal = new[] { PersonalityType.Aggressive, PersonalityType.Cocky, PersonalityType.Driven };
        var byExample = new[] { PersonalityType.Calm, PersonalityType.Smart, PersonalityType.Cautious };
        var morale = new[] { PersonalityType.Kind, PersonalityType.Silly };
        var quiet = new[] { PersonalityType.Shy, PersonalityType.Lazy };
        if (Array.IndexOf(vocal, p) >= 0) return vocal;
        if (Array.IndexOf(byExample, p) >= 0) return byExample;
        if (Array.IndexOf(morale, p) >= 0) return morale;
        return quiet;
    }

    #region Helper Methods

    /// <summary>
    /// Get what the player perceives their stat value to be (affected by personality).
    /// </summary>
    private static int GetPerceivedStatValue(Player player, PlayerStat stat, int actualValue)
    {
        int modifier = player.Personality switch
        {
            PersonalityType.Cocky => UnityEngine.Random.Range(10, 25),    // Overestimates
            PersonalityType.Shy => UnityEngine.Random.Range(-20, -5),     // Underestimates
            PersonalityType.Aggressive => UnityEngine.Random.Range(5, 15),
            PersonalityType.Kind => UnityEngine.Random.Range(-5, 5),      // Honest
            PersonalityType.Lazy => UnityEngine.Random.Range(-10, 10),    // Doesn't care enough to be accurate
            PersonalityType.Driven => UnityEngine.Random.Range(-5, 5),    // Self-aware
            PersonalityType.Smart => UnityEngine.Random.Range(-3, 3),     // Most accurate
            PersonalityType.Silly => UnityEngine.Random.Range(-15, 15),   // Random
            PersonalityType.Cautious => UnityEngine.Random.Range(-15, 0), // Conservative estimate
            PersonalityType.Calm => UnityEngine.Random.Range(-5, 5),      // Reasonable
            _ => 0
        };

        return Mathf.Clamp(actualValue + modifier, 0, 100);
    }

    private static PlayerStat GetBestStat(Player player)
    {
        PlayerStat best = PlayerStat.Shooting;
        int bestValue = 0;

        for (int i = 0; i < Player.SKILL_NO; i++)
        {
            int value = player.RawStats.Skills[i];
            if (value > bestValue)
            {
                bestValue = value;
                best = (PlayerStat)i;
            }
        }
        return best;
    }

    private static PlayerStat GetWorstStat(Player player)
    {
        PlayerStat worst = PlayerStat.Shooting;
        int worstValue = 101;

        for (int i = 0; i < Player.SKILL_NO; i++)
        {
            int value = player.RawStats.Skills[i];
            if (value < worstValue)
            {
                worstValue = value;
                worst = (PlayerStat)i;
            }
        }
        return worst;
    }

    /// <summary>
    /// What the player thinks is their best stat (personality affects this).
    /// </summary>
    private static PlayerStat GetPerceivedBestStat(Player player)
    {
        // Cocky players might not pick their actual best - they pick something flashy
        if (player.Personality == PersonalityType.Cocky)
        {
            PlayerStat[] flashyStats = { PlayerStat.Shooting, PlayerStat.Dribbling, PlayerStat.Pace, PlayerStat.Creativity };
            return flashyStats[UnityEngine.Random.Range(0, flashyStats.Length)];
        }

        // Shy players won't show off their best — they name something middling instead.
        if (player.Personality == PersonalityType.Shy)
        {
            var sorted = new List<(PlayerStat stat, int value)>();
            for (int i = 0; i < Player.SKILL_NO; i++)
                sorted.Add(((PlayerStat)i, player.RawStats.Skills[i]));
            sorted = sorted.OrderBy(s => s.value).ToList();
            int mid = Mathf.Clamp(sorted.Count / 2, 0, sorted.Count - 1);
            return sorted[mid].stat;
        }

        // Smart and Driven players are accurate
        if (player.Personality == PersonalityType.Smart || player.Personality == PersonalityType.Driven)
        {
            return GetBestStat(player);
        }

        // Most others are reasonably accurate
        return GetBestStat(player);
    }

    /// <summary>
    /// What the player thinks is their worst stat (personality affects this).
    /// </summary>
    private static PlayerStat GetPerceivedWorstStat(Player player)
    {
        PlayerStat actualWorst = GetWorstStat(player);

        // Cocky players avoid admitting their actual worst - pick something less bad
        if (player.Personality == PersonalityType.Cocky)
        {
            // Find second or third worst instead
            var sortedStats = new List<(PlayerStat stat, int value)>();
            for (int i = 0; i < Player.SKILL_NO; i++)
            {
                sortedStats.Add(((PlayerStat)i, player.RawStats.Skills[i]));
            }
            sortedStats = sortedStats.OrderBy(s => s.value).ToList();
            
            // Pick 2nd or 3rd worst (index 1 or 2)
            int index = UnityEngine.Random.Range(1, 3);
            return sortedStats[Mathf.Min(index, sortedStats.Count - 1)].stat;
        }

        // Aggressive players also deflect
        if (player.Personality == PersonalityType.Aggressive)
        {
            // Pick a mental stat instead of their actual weakness
            PlayerStat[] deflectTo = { PlayerStat.Teamwork, PlayerStat.Intelligence };
            return deflectTo[UnityEngine.Random.Range(0, deflectTo.Length)];
        }

        // Smart players are honest
        if (player.Personality == PersonalityType.Smart || player.Personality == PersonalityType.Driven)
        {
            return actualWorst;
        }

        return actualWorst;
    }

    private static string StatValueToDescription(int value)
    {
        return value switch
        {
            >= 90 => "world-class",
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

    #endregion
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
    /// For personality-probing questions: the set of personalities consistent with this answer. The
    /// interview manager intersects these across questions to narrow down the hidden personality.
    /// Null for non-personality questions.
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
