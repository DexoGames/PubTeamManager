using System.Collections.Generic;
using System.Linq;

/// <summary>The stat a leaderboard ranks by. Each applies to both players and teams.</summary>
public enum StatCategory
{
    Goals,
    Assists,
    Shots,
    Saves,
    YellowCards,
    RedCards,
    CleanSheets,    // matches the team conceded 0 (keeper for players)
    GoalsConceded,  // goals let in (keeper for players)
    ShotsOnTarget,  // shots that forced a save or scored
    OwnGoals,       // put into their own net (the unlucky scorer / their team)
    BigMisses       // a clear chance (high xG) that wasn't scored
}

/// <summary>One ranked row — exactly one of <see cref="player"/> / <see cref="team"/> is set.</summary>
public class StatRanking
{
    public Player player;
    public Team team;
    public int value;
}

/// <summary>
/// Builds ranked "top N" stat tables from played fixtures — the data behind the stat widgets. Scope is just the
/// set of fixtures you pass in: one <see cref="Competition.Fixtures"/> for a single tournament, or several unioned.
/// Goals/assists come from the persisted goal log (survive archiving); shots/saves come from the full shot log
/// (trimmed on archived matches by <see cref="Fixture.SlimForArchive"/>, so those read 0 for old seasons).
/// </summary>
public static class StatLeaderboards
{
    public static string Label(StatCategory cat) => cat switch
    {
        StatCategory.Goals       => "Goals",
        StatCategory.Assists     => "Assists",
        StatCategory.Shots       => "Shots",
        StatCategory.Saves       => "Saves",
        StatCategory.YellowCards   => "Yellow Cards",
        StatCategory.RedCards      => "Red Cards",
        StatCategory.CleanSheets   => "Clean Sheets",
        StatCategory.GoalsConceded => "Goals Conceded",
        StatCategory.ShotsOnTarget => "Shots on Target",
        StatCategory.OwnGoals      => "Own Goals",
        StatCategory.BigMisses     => "Big Misses",
        _                          => cat.ToString()
    };

    /// <summary>A shot counts as a "big miss" when it was a clear chance (xG at/above this) but didn't go in.
    /// Public so highlights or commentary can flag the same chances. Tune to taste — chances range ~0.02–0.8.</summary>
    public const float BigMissXg = 0.5f;

    /// <summary>A clear-cut chance (high xG) that wasn't scored — saved, off target or hit the woodwork.</summary>
    public static bool IsBigMiss(Match.Shot s)
        => s.type != ShotType.Own_Goal && s.result != ShotOutcome.Goal && s.xG >= BigMissXg;

    /// <summary>Top players for a stat across the given fixtures. Pass <paramref name="onlyTeam"/> to restrict to
    /// one club's players (e.g. the My Team page).</summary>
    public static List<StatRanking> TopPlayers(IEnumerable<Fixture> fixtures, StatCategory cat, int topN, Team onlyTeam = null)
    {
        var tally = new Dictionary<Player, int>();
        void Add(Player p, int n = 1)
        {
            if (p == null) return;
            if (onlyTeam != null && p.Team != onlyTeam) return;
            tally.TryGetValue(p, out int v);
            tally[p] = v + n;
        }

        foreach (var f in PlayedFixtures(fixtures))
        {
            TallyPlayers(f.Result.home, f.Result.away, cat, Add);
            TallyPlayers(f.Result.away, f.Result.home, cat, Add);
        }

        return tally
            .OrderByDescending(kv => kv.Value)
            .Take(topN)
            .Select(kv => new StatRanking { player = kv.Key, value = kv.Value })
            .ToList();
    }

    /// <summary>Top teams for a stat across the given fixtures.</summary>
    public static List<StatRanking> TopTeams(IEnumerable<Fixture> fixtures, StatCategory cat, int topN)
    {
        var tally = new Dictionary<Team, int>();
        void Add(Team t, int n = 1)
        {
            if (t == null || n == 0) return;
            tally.TryGetValue(t, out int v);
            tally[t] = v + n;
        }

        foreach (var f in PlayedFixtures(fixtures))
        {
            TallyTeam(f.Result.home, f.Result.away, cat, Add);
            TallyTeam(f.Result.away, f.Result.home, cat, Add);
        }

        return tally
            .OrderByDescending(kv => kv.Value)
            .Take(topN)
            .Select(kv => new StatRanking { team = kv.Key, value = kv.Value })
            .ToList();
    }

    // ————————————————————— internals —————————————————————

    private static IEnumerable<Fixture> PlayedFixtures(IEnumerable<Fixture> fixtures)
        => (fixtures ?? Enumerable.Empty<Fixture>()).Where(f => f != null && f.BeenPlayed);

    private static IEnumerable<Match.Shot> Goals(Match.TeamStats s) => s.goals ?? Enumerable.Empty<Match.Shot>();
    private static IEnumerable<Match.Shot> Shots(Match.TeamStats s) => s.shots ?? Enumerable.Empty<Match.Shot>();
    private static IEnumerable<Match.Foul> Fouls(Match.TeamStats s) => s.fouls ?? Enumerable.Empty<Match.Foul>();

    private static void TallyPlayers(Match.TeamStats side, Match.TeamStats opponent, StatCategory cat, System.Action<Player, int> add)
    {
        switch (cat)
        {
            case StatCategory.Goals:
                foreach (var g in Goals(side)) if (g.type != ShotType.Own_Goal) add(g.shooter, 1);
                break;
            case StatCategory.Assists:
                foreach (var g in Goals(side)) if (g.assister != null) add(g.assister, 1);
                break;
            case StatCategory.Shots:
                foreach (var sh in Shots(side)) if (sh.type != ShotType.Own_Goal) add(sh.shooter, 1);
                break;
            case StatCategory.Saves: // the keeper belongs to the defending side, so this credits the right player
                foreach (var sh in Shots(side)) if (sh.result == ShotOutcome.Saved) add(sh.keeper, 1);
                break;
            case StatCategory.YellowCards:
                foreach (var fl in Fouls(side)) if (fl.card == Card.Yellow) add(fl.offender, 1);
                break;
            case StatCategory.RedCards:
                foreach (var fl in Fouls(side)) if (IsRed(fl.card)) add(fl.offender, 1);
                break;
            case StatCategory.ShotsOnTarget:
                foreach (var sh in Shots(side)) if (OnTarget(sh)) add(sh.shooter, 1);
                break;
            case StatCategory.BigMisses:
                foreach (var sh in Shots(side)) if (IsBigMiss(sh)) add(sh.shooter, 1);
                break;
            case StatCategory.OwnGoals: // the unlucky scorer (recorded on the benefiting side's goal log)
                foreach (var g in Goals(side)) if (g.type == ShotType.Own_Goal) add(g.shooter, 1);
                break;
            case StatCategory.CleanSheets: // the keeper who played, credited when the opponent failed to score
                if (!Goals(opponent).Any()) add(side.keeper, 1);
                break;
            case StatCategory.GoalsConceded:
                int conceded = Goals(opponent).Count();
                if (conceded > 0) add(side.keeper, conceded);
                break;
        }
    }

    private static bool IsRed(Card c) => c == Card.Red || c == Card.RedAndSuspension;
    private static bool OnTarget(Match.Shot s) => s.type != ShotType.Own_Goal && (s.result == ShotOutcome.Goal || s.result == ShotOutcome.Saved);

    private static void TallyTeam(Match.TeamStats side, Match.TeamStats opponent, StatCategory cat, System.Action<Team, int> add)
    {
        switch (cat)
        {
            case StatCategory.Goals:
                add(side.team, Goals(side).Count());
                break;
            case StatCategory.Assists:
                add(side.team, Goals(side).Count(g => g.assister != null));
                break;
            case StatCategory.Shots:
                add(side.team, Shots(side).Count(sh => sh.type != ShotType.Own_Goal));
                break;
            case StatCategory.Saves: // saves credited to the defending team (the keeper's team)
                foreach (var sh in Shots(side))
                    if (sh.result == ShotOutcome.Saved && sh.keeper != null) add(sh.keeper.Team, 1);
                break;
            case StatCategory.YellowCards:
                add(side.team, Fouls(side).Count(fl => fl.card == Card.Yellow));
                break;
            case StatCategory.RedCards:
                add(side.team, Fouls(side).Count(fl => IsRed(fl.card)));
                break;
            case StatCategory.ShotsOnTarget:
                add(side.team, Shots(side).Count(OnTarget));
                break;
            case StatCategory.BigMisses:
                add(side.team, Shots(side).Count(IsBigMiss));
                break;
            case StatCategory.CleanSheets:
                if (!Goals(opponent).Any()) add(side.team, 1);
                break;
            case StatCategory.GoalsConceded:
                add(side.team, Goals(opponent).Count());
                break;
            case StatCategory.OwnGoals: // the team that put it in their own net (the unlucky scorer's team)
                foreach (var g in Goals(side))
                    if (g.type == ShotType.Own_Goal && g.shooter != null) add(g.shooter.Team, 1);
                break;
        }
    }
}
