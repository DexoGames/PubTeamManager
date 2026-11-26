using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Saves/loads the game across multiple files within a per-slot folder:
///   &lt;slot&gt;/core.json          — cross-season state (teams, players, events, training, lineage, season index)
///   &lt;slot&gt;/season_&lt;year&gt;.json — one per season (competitions + fixtures, inline)
///
/// Only the core file + the current season file are rewritten each save; past-season files
/// are written once when a season is archived (and their non-player matches are slimmed),
/// so autosave cost stays roughly constant as years pass.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string DEFAULT_SLOT = "autosave";
    private const string CORE_FILE = "core.json";
    private const string SEASON_PREFIX = "season_";
    private const string SAVE_EXTENSION = ".json";

    /// <summary>The slot the live game reads/writes (archival writes here too).</summary>
    string activeSlot = DEFAULT_SLOT;
    string liveSeasonFile;

    static void WriteAtomically(string path, string contents, string backup = null)
    {
        string pending = path + ".pending";
        File.WriteAllText(pending, contents);
        if (File.Exists(path)) File.Replace(pending, path, backup);
        else File.Move(pending, path);
    }

    public void PreserveCareer()
    {
        string directory = GetSlotDir(activeSlot);
        if (Directory.Exists(directory))
            Directory.Move(directory, directory + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this.gameObject);
        else
            Instance = this;
    }

    // ————————————————————— paths —————————————————————

    private string GetSlotDir(string slot) => Path.Combine(Application.persistentDataPath, slot);
    private string GetCorePath(string slot) => Path.Combine(GetSlotDir(slot), CORE_FILE);
    private string GetSeasonPath(string slot, int year) => Path.Combine(GetSlotDir(slot), $"{SEASON_PREFIX}{year}{SAVE_EXTENSION}");
    private void EnsureSlotDir(string slot) => Directory.CreateDirectory(GetSlotDir(slot));

    private JsonSerializerSettings GetSerializerSettings()
    {
        return new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented
        };
    }

    // ————————————————————— public API —————————————————————

    /// <summary>Saves the core file + the current season file to the slot folder.</summary>
    public void Save(string slotName = null)
    {
        slotName = slotName ?? DEFAULT_SLOT;
        activeSlot = slotName;

        try
        {
            EnsureSlotDir(slotName);
            var settings = GetSerializerSettings();

            int year = FixturesManager.Instance.CurrentSeasonYear;
            string first = $"live_{year}_a.json";
            string destination = liveSeasonFile == first ? $"live_{year}_b.json" : first;
            var season = new SeasonSaveState { Year = year, Competitions = FixturesManager.Instance.GetCurrentSeasonCompetitions() };
            WriteAtomically(Path.Combine(GetSlotDir(slotName), destination), JsonConvert.SerializeObject(season, settings));

            CoreSaveState core = CollectCoreState();
            core.LiveSeasonFile = destination;
            string path = GetCorePath(slotName);
            WriteAtomically(path, JsonConvert.SerializeObject(core, settings), path + ".bak");
            liveSeasonFile = destination;

            Debug.Log($"[Save] Saved core + season {year} → {GetSlotDir(slotName)} (Teams:{core.Teams.Count}, FreeAgents:{core.FreeAgents.Count}, Seasons on disk:{core.Seasons.Count})");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Failed to save: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>Loads the core file + the current season file from the slot folder.</summary>
    public bool Load(string slotName = null)
    {
        slotName = slotName ?? DEFAULT_SLOT;
        activeSlot = slotName;

        string path = GetCorePath(slotName);
        if (TryLoad(slotName, path)) return true;
        if (!TryLoad(slotName, path + ".bak")) return false;
        // Keep the verified manifest in place before the next save rotates its backup.
        WriteAtomically(path, File.ReadAllText(path + ".bak"));
        return true;
    }

    bool TryLoad(string slotName, string corePath)
    {
        if (!File.Exists(corePath))
        {
            Debug.LogWarning($"[Save] No save found at {corePath}");
            return false;
        }

        try
        {
            // Clear runtime registries so references resolve against fresh state.
            PersonManager.Instance.Clear();
            TeamManager.Instance.ClearLookup();
            FixturesManager.Instance.ClearCompetitionState();

            var settings = GetSerializerSettings();

            // 1. Core (teams/players register during this deserialize, so season refs resolve next).
            CoreSaveState core = JsonConvert.DeserializeObject<CoreSaveState>(File.ReadAllText(corePath), settings);
            RestoreCoreState(core);

            // 2. Current season's competitions.
            liveSeasonFile = core.LiveSeasonFile;
            string seasonPath = string.IsNullOrEmpty(liveSeasonFile)
                ? GetSeasonPath(slotName, core.CurrentSeasonYear)
                : Path.Combine(GetSlotDir(slotName), Path.GetFileName(liveSeasonFile));
            var season = JsonConvert.DeserializeObject<SeasonSaveState>(File.ReadAllText(seasonPath), settings);
            if (season == null || season.Year != core.CurrentSeasonYear || season.Competitions.Count == 0)
                throw new InvalidDataException("The current season is incomplete.");

            RestoreSeason(season);

            // 3. Wiring that depends on competitions being present.
            FinishRestore(core);

            Debug.Log($"[Save] Loaded from {GetSlotDir(slotName)} — Date:{core.CurrentDay:d}, CurrentSeason:{core.CurrentSeasonYear}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Failed to load: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    public bool HasSave(string slotName = null) => Directory.Exists(GetSlotDir(slotName ?? DEFAULT_SLOT));

    public void AutoSave() => Save(DEFAULT_SLOT);

    /// <summary>
    /// Writes ONLY the core file — fast. Use for action-triggered saves (training, tactics,
    /// event responses) that change cross-season data but not the current season's fixtures.
    /// Falls back to a full save if the current season's file doesn't exist yet, so core and
    /// season files never drift out of sync.
    /// </summary>
    public void SaveCore(string slotName = null)
    {
        slotName = slotName ?? activeSlot;
        if (!IsGameInProgress()) return;

        // Keep core + season consistent: if no season file exists yet, do a full save instead.
        if (string.IsNullOrEmpty(liveSeasonFile) || !File.Exists(Path.Combine(GetSlotDir(slotName), liveSeasonFile)))
        {
            Save(slotName);
            return;
        }

        try
        {
            EnsureSlotDir(slotName);
            CoreSaveState core = CollectCoreState();
            string path = GetCorePath(slotName);
            WriteAtomically(path, JsonConvert.SerializeObject(core, GetSerializerSettings()), path + ".bak");
            Debug.Log($"[Save] Core saved → {GetSlotDir(slotName)}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Failed to save core: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>True once a game is actually running (teams spawned), so we never write an empty save.</summary>
    private bool IsGameInProgress() =>
        TeamManager.Instance != null
        && TeamManager.Instance.GetAllTeams() != null
        && TeamManager.Instance.GetAllTeams().Count > 0
        && FixturesManager.Instance != null;

    /// <summary>Deletes a save slot (folder + any legacy single-file save).</summary>
    public void DeleteSave(string slotName = null)
    {
        slotName = slotName ?? DEFAULT_SLOT;

        string dir = GetSlotDir(slotName);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);

        string legacy = Path.Combine(Application.persistentDataPath, slotName + SAVE_EXTENSION);
        if (File.Exists(legacy)) File.Delete(legacy);

        Debug.Log($"[Save] Deleted save '{slotName}'");
    }

    // ————————————————————— archival (called at season rollover) —————————————————————

    /// <summary>
    /// Archives a finished season to its own file. Matches not involving the player's team are
    /// slimmed (fouls/possession dropped; score, scorers and starting XIs kept). The player's
    /// own matches keep full detail.
    /// </summary>
    public bool ArchiveSeason(int year, List<Competition> comps)
    {
        try
        {
            int club = TeamManager.Instance.MyTeam.TeamId;
            var archive = comps.ConvertAll(competition => competition.CopyForArchive(club));
            EnsureSlotDir(activeSlot);
            WriteSeasonFile(activeSlot, year, archive, GetSerializerSettings());
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Failed to archive season {year}: {e.Message}");
            return false;
        }
    }

    // ————————————————————— history loading (for a future UI) —————————————————————

    /// <summary>
    /// Loads a past season's competitions for viewing, registering them in the FixturesManager
    /// viewing cache (resolvable by ID) without merging into live state. Call
    /// FixturesManager.ClearViewedSeason() when the history view closes.
    /// </summary>
    public List<Competition> LoadSeasonForViewing(int year)
    {
        SeasonSaveState season = ReadSeasonFile(activeSlot, year, GetSerializerSettings());
        if (season == null) return new List<Competition>();

        foreach (var comp in season.Competitions)
        {
            foreach (var fx in comp.Fixtures) fx.Competition = comp;
            if (comp is League league) league.OnAfterDeserialize();
            else if (comp is Cup cup) cup.OnAfterDeserialize();
        }

        FixturesManager.Instance.RegisterViewedSeason(season.Competitions);
        return season.Competitions;
    }

    // ————————————————————— file IO helpers —————————————————————

    private void WriteSeasonFile(string slot, int year, List<Competition> comps, JsonSerializerSettings settings)
    {
        var season = new SeasonSaveState { Year = year, Competitions = comps ?? new List<Competition>() };
        WriteAtomically(GetSeasonPath(slot, year), JsonConvert.SerializeObject(season, settings));
    }

    private SeasonSaveState ReadSeasonFile(string slot, int year, JsonSerializerSettings settings)
    {
        string path = GetSeasonPath(slot, year);
        if (!File.Exists(path)) return null;
        return JsonConvert.DeserializeObject<SeasonSaveState>(File.ReadAllText(path), settings);
    }

    // ————————————————————— collect —————————————————————

    private CoreSaveState CollectCoreState()
    {
        return new CoreSaveState
        {
            CurrentDay = CalenderManager.Instance.CurrentDay,
            InterviewEpoch = ScheduleManager.Instance.InterviewEpoch,
            Recruitment = RecruitmentManager.Instance.CaptureState(),
            FormerPlayers = RecruitmentManager.Instance.FormerPlayers,
            LiveSeasonFile = liveSeasonFile,
            PlayerTeamId = TeamManager.Instance.MyTeam.TeamId,
            NextPersonId = IdManager.Instance.NextPersonId,
            NextTeamId = IdManager.Instance.NextTeamId,
            NextCompetitionId = IdManager.Instance.NextCompetitionId,
            NextFixtureId = IdManager.Instance.NextFixtureId,
            NextSeriesId = IdManager.Instance.NextSeriesId,
            Teams = TeamManager.Instance.GetAllTeams(),
            FreeAgents = RecruitmentManager.Instance?.FreeAgentPool ?? new List<Player>(),
            Events = EventsManager.Instance?.Events ?? new List<Event>(),
            CurrentTraining = TrainingManager.Instance?.CurrentSession,
            CompetitionSeries = FixturesManager.Instance.CompetitionSeries,
            CurrentSeasonYear = FixturesManager.Instance.CurrentSeasonYear,
            Seasons = FixturesManager.Instance.Seasons
        };
    }

    // ————————————————————— restore —————————————————————

    private void RestoreCoreState(CoreSaveState core)
    {
        // ID allocators first so post-load entities don't collide.
        IdManager.Instance.SeedFromState(
            core.NextPersonId, core.NextTeamId, core.NextCompetitionId,
            core.NextFixtureId, core.NextSeriesId);

        CalenderManager.Instance.SetCurrentDay(core.CurrentDay);

        // Teams were registered during core deserialization (TeamConverter); finalise the list.
        TeamManager.Instance.RestoreTeamsFromState(core.Teams, core.PlayerTeamId);

        // Lineage + season index (so we know which season files exist).
        FixturesManager.Instance.RestoreSeries(core.CompetitionSeries);
        FixturesManager.Instance.RestoreSeasonIndex(core.Seasons, core.CurrentSeasonYear);

        if (RecruitmentManager.Instance != null && core.FreeAgents != null)
            RecruitmentManager.Instance.RestoreFreeAgentsFromState(core.FreeAgents);

        RecruitmentManager.Instance.FormerPlayers.Clear();
        RecruitmentManager.Instance.FormerPlayers.AddRange(core.FormerPlayers);
        foreach (Player player in core.FormerPlayers) PersonManager.Instance.RegisterExisting(player);

        if (EventsManager.Instance != null && core.Events != null)
        {
            EventsManager.Instance.Events = core.Events;
            foreach (var evt in core.Events)
                evt.OnAfterDeserialize();
        }

        if (TrainingManager.Instance != null && core.CurrentTraining != null)
            TrainingManager.Instance.SetTraining(core.CurrentTraining);
    }

    /// <summary>Restores a single season's competitions into live state.</summary>
    private void RestoreSeason(SeasonSaveState season)
    {
        List<Fixture> allRestoredFixtures = new List<Fixture>();
        Dictionary<string, League> activeLeagues = new Dictionary<string, League>();

        foreach (var comp in season.Competitions)
        {
            foreach (var fixture in comp.Fixtures)
                fixture.Competition = comp;

            FixturesManager.Instance.IndexRestoredCompetition(comp);
            allRestoredFixtures.AddRange(comp.Fixtures);

            if (comp is League league)
            {
                league.OnAfterDeserialize();
                activeLeagues[league.Name] = league;
            }
            else if (comp is Cup cup)
            {
                cup.OnAfterDeserialize();
            }
        }

        LinkPromotionRelegation(activeLeagues);
        FixturesManager.Instance.RegisterFixtures(allRestoredFixtures);

        Debug.Log($"[Restore] Restored season {season.Year}: {season.Competitions.Count} competitions, {allRestoredFixtures.Count} fixtures");
    }

    /// <summary>Re-links league promotion/relegation pointers from the editor templates.</summary>
    private void LinkPromotionRelegation(Dictionary<string, League> leaguesByName)
    {
        LeagueTemplate[] templates = Resources.LoadAll<LeagueTemplate>("Competitions/Leagues");
        foreach (var template in templates)
        {
            if (template.PromotionLeagueTemplate != null &&
                leaguesByName.ContainsKey(template.LeagueName) &&
                leaguesByName.ContainsKey(template.PromotionLeagueTemplate.LeagueName))
            {
                leaguesByName[template.LeagueName].PromotionLeague =
                    leaguesByName[template.PromotionLeagueTemplate.LeagueName];
            }
            if (template.RelegationLeagueTemplate != null &&
                leaguesByName.ContainsKey(template.LeagueName) &&
                leaguesByName.ContainsKey(template.RelegationLeagueTemplate.LeagueName))
            {
                leaguesByName[template.LeagueName].RelegationLeague =
                    leaguesByName[template.RelegationLeagueTemplate.LeagueName];
            }
        }
    }

    /// <summary>Post-restore wiring that needs competitions present.</summary>
    private void FinishRestore(CoreSaveState core)
    {
        if (ScheduleManager.Instance != null)
        {
            ScheduleManager.Instance.InterviewEpoch = core.InterviewEpoch == default ? new DateTime(2024, 8, 1) : core.InterviewEpoch;
            ScheduleManager.Instance.GenerateSchedule(core.CurrentDay);
            ScheduleManager.Instance.OnTrainingDay += () => TrainingManager.Instance?.ExecuteTraining();
            ScheduleManager.Instance.OnInterviewDay += () => RecruitmentManager.Instance?.NotifyInterviewDay();

            // If the save was made on an interview day, make interviewing available immediately.
            if (ScheduleManager.Instance.GetTodaysEntry().Type == ScheduleEntryType.Interview)
                RecruitmentManager.Instance?.NotifyInterviewDay();
        }

        RecruitmentManager.Instance.RestoreSession(core.Recruitment);
        Debug.Log("[Save] Full restore complete.");
    }
}
