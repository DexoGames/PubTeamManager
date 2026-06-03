using UnityEngine;

/// <summary>
/// Central allocator for stable, per-type entity IDs.
/// Every persistent entity (Person, Team, Competition, Fixture, CompetitionSeries)
/// draws its ID here so IDs are unique within their type and never recycle a live ID,
/// even across save/load cycles.
///
/// Counters are persisted in GameState and re-seeded on load via SeedFromState() so
/// entities created after a load continue past the highest restored ID.
/// </summary>
public class IdManager : MonoBehaviour
{
    public static IdManager Instance { get; private set; }

    public int NextPersonId { get; private set; }
    public int NextTeamId { get; private set; }
    public int NextCompetitionId { get; private set; }
    public int NextFixtureId { get; private set; }
    public int NextSeriesId { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public int AllocatePersonId() => NextPersonId++;
    public int AllocateTeamId() => NextTeamId++;
    public int AllocateCompetitionId() => NextCompetitionId++;
    public int AllocateFixtureId() => NextFixtureId++;
    public int AllocateSeriesId() => NextSeriesId++;

    /// <summary>
    /// Restores counters from a loaded GameState so new allocations don't collide
    /// with restored entities. Call before any post-load allocation.
    /// </summary>
    public void SeedFromState(int nextPerson, int nextTeam, int nextCompetition, int nextFixture, int nextSeries)
    {
        NextPersonId = nextPerson;
        NextTeamId = nextTeam;
        NextCompetitionId = nextCompetition;
        NextFixtureId = nextFixture;
        NextSeriesId = nextSeries;

        Debug.Log($"[Ids] Seeded from save — Person:{NextPersonId} Team:{NextTeamId} Comp:{NextCompetitionId} Fixture:{NextFixtureId} Series:{NextSeriesId}");
    }
}
