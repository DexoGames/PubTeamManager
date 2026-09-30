using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

/// <summary>
/// Lineage that links every season's instance of the same competition across years.
/// One series ("Premier Division", "Papa Johns Cup") owns the list of per-season
/// Competition instances, enabling history browsing (all seasons of a competition).
///
/// Anchored to an editor blueprint by TemplateName so new seasons attach to the
/// correct series. Serialized inline in GameState.
/// </summary>
[System.Serializable]
public class CompetitionSeries
{
    public int Id;
    public string Name;

    /// <summary>Editor blueprint anchor (LeagueTemplate name, or cup name).</summary>
    public string TemplateName;

    /// <summary>Competition.Id of each season's instance, in chronological order.</summary>
    public List<int> SeasonCompetitionIds = new List<int>();

    public CompetitionSeries() { }

    public CompetitionSeries(int id, string name, string templateName)
    {
        Id = id;
        Name = name;
        TemplateName = templateName;
    }

    public void AddSeason(Competition competition)
    {
        if (competition == null || SeasonCompetitionIds.Contains(competition.Id)) return;
        SeasonCompetitionIds.Add(competition.Id);
    }

    /// <summary>Resolves the season instances to live Competitions, in chronological order.</summary>
    [JsonIgnore]
    public List<Competition> Seasons =>
        SeasonCompetitionIds
            .Select(id => FixturesManager.Instance.GetCompetition(id))
            .Where(c => c != null)
            .ToList();
}
