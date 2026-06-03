using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Handles saving and loading of game state via JSON serialization.
/// Uses GameState as the root container — all models serialize themselves
/// via Newtonsoft.Json attributes and custom converters.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string DEFAULT_SLOT = "autosave";
    private const string SAVE_EXTENSION = ".json";

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

    /// <summary>
    /// Saves the current game state to a named slot.
    /// </summary>
    public void Save(string slotName = null)
    {
        slotName = slotName ?? DEFAULT_SLOT;
        string path = GetSavePath(slotName);

        try
        {
            GameState state = CollectGameState();
            string json = JsonConvert.SerializeObject(state, GetSerializerSettings());
            File.WriteAllText(path, json);
            Debug.Log($"[Save] Game saved to {path} — Teams:{state.Teams.Count} Comps:{state.Competitions.Count} Series:{state.CompetitionSeries.Count} FreeAgents:{state.FreeAgents.Count} | NextIds P:{state.NextPersonId} T:{state.NextTeamId} C:{state.NextCompetitionId} F:{state.NextFixtureId} S:{state.NextSeriesId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Failed to save: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// Loads game state from a named slot.
    /// </summary>
    public bool Load(string slotName = null)
    {
        slotName = slotName ?? DEFAULT_SLOT;
        string path = GetSavePath(slotName);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[Save] No save file found at {path}");
            return false;
        }

        try
        {
            // Clear runtime registries so references resolve against fresh state.
            // (Teams/persons are registered during deserialization by their converters.)
            PersonManager.Instance.Clear();
            TeamManager.Instance.ClearLookup();
            FixturesManager.Instance.ClearCompetitionState();

            string json = File.ReadAllText(path);
            GameState state = JsonConvert.DeserializeObject<GameState>(json, GetSerializerSettings());
            RestoreGameState(state);
            Debug.Log($"[Save] Game loaded from {path}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Failed to load: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Checks if a save file exists for the given slot.
    /// </summary>
    public bool HasSave(string slotName = null)
    {
        slotName = slotName ?? DEFAULT_SLOT;
        return File.Exists(GetSavePath(slotName));
    }

    /// <summary>
    /// Deletes the save file for the given slot. Used when starting a new game.
    /// </summary>
    public void DeleteSave(string slotName = null)
    {
        slotName = slotName ?? DEFAULT_SLOT;
        string path = GetSavePath(slotName);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[Save] Deleted save at {path}");
        }
    }

    /// <summary>
    /// Auto-save — called during day advances.
    /// </summary>
    public void AutoSave()
    {
        Save(DEFAULT_SLOT);
    }

    private string GetSavePath(string slotName)
    {
        return Path.Combine(Application.persistentDataPath, slotName + SAVE_EXTENSION);
    }

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

    // ————————————————————— COLLECT —————————————————————

    /// <summary>
    /// Collects all game state into a GameState object.
    /// Models serialize themselves via JSON attributes — no DTO conversion needed.
    /// </summary>
    private GameState CollectGameState()
    {
        return new GameState
        {
            CurrentDay = CalenderManager.Instance.CurrentDay,
            PlayerTeamId = TeamManager.Instance.MyTeam.TeamId,
            NextPersonId = IdManager.Instance.NextPersonId,
            NextTeamId = IdManager.Instance.NextTeamId,
            NextCompetitionId = IdManager.Instance.NextCompetitionId,
            NextFixtureId = IdManager.Instance.NextFixtureId,
            NextSeriesId = IdManager.Instance.NextSeriesId,
            Teams = TeamManager.Instance.GetAllTeams(),
            Competitions = FixturesManager.Instance.Competitions,
            CompetitionSeries = FixturesManager.Instance.CompetitionSeries,
            FreeAgents = RecruitmentManager.Instance?.FreeAgentPool ?? new List<Player>(),
            Events = EventsManager.Instance?.Events ?? new List<Event>()
        };
    }

    // ————————————————————— RESTORE —————————————————————

    /// <summary>
    /// Restores full game state from a deserialized GameState.
    /// Teams and Players are already rebuilt by TeamConverter during deserialization.
    /// This method wires up the remaining cross-references and rebuilds indices.
    /// </summary>
    private void RestoreGameState(GameState state)
    {
        // 0. Re-seed ID allocators so entities created after load don't collide.
        IdManager.Instance.SeedFromState(
            state.NextPersonId, state.NextTeamId, state.NextCompetitionId,
            state.NextFixtureId, state.NextSeriesId);

        // 1. Date
        CalenderManager.Instance.SetCurrentDay(state.CurrentDay);

        // 2. Teams — already deserialized by TeamConverter (Players, Manager, Tactic rebuilt)
        TeamManager.Instance.RestoreTeamsFromState(state.Teams, state.PlayerTeamId);

        // 3. Competitions — deserialize with fixture references
        RestoreCompetitions(state);

        // 4. Free agents
        if (RecruitmentManager.Instance != null && state.FreeAgents != null)
        {
            RecruitmentManager.Instance.RestoreFreeAgentsFromState(state.FreeAgents);
        }

        // 5. Events
        if (EventsManager.Instance != null && state.Events != null)
        {
            EventsManager.Instance.Events = state.Events;
            foreach (var evt in state.Events)
                evt.OnAfterDeserialize();
        }

        // 6. Schedule
        if (ScheduleManager.Instance != null)
        {
            ScheduleManager.Instance.GenerateSchedule(state.CurrentDay);
        }

        // 7. Wire up schedule events
        if (ScheduleManager.Instance != null)
        {
            ScheduleManager.Instance.OnTrainingDay += () => TrainingManager.Instance?.ExecuteTraining();
        }

        Debug.Log($"[Save] Full restore complete. Date: {state.CurrentDay}, Teams: {state.Teams.Count}, Competitions: {state.Competitions.Count}");
    }

    /// <summary>
    /// Restores competitions — Fixtures need special handling because Competition.Fixtures 
    /// is [JsonIgnore] (Fixture references Competition which would be circular).
    /// Competitions serialize their fixtures separately in GameState.
    /// </summary>
    private void RestoreCompetitions(GameState state)
    {
        // State was already cleared at the start of Load(); restore series lineages first.
        FixturesManager.Instance.RestoreSeries(state.CompetitionSeries);

        List<Fixture> allRestoredFixtures = new List<Fixture>();
        Dictionary<string, League> leaguesByName = new Dictionary<string, League>();

        foreach (var comp in state.Competitions)
        {
            // Wire fixture → competition back-reference (Fixture.Competition is [JsonIgnore])
            foreach (var fixture in comp.Fixtures)
            {
                fixture.Competition = comp;
            }

            // Index competition (preserves restored Id/SeriesId/SeasonYear)
            FixturesManager.Instance.IndexRestoredCompetition(comp);
            allRestoredFixtures.AddRange(comp.Fixtures);

            if (comp is League league)
            {
                league.OnAfterDeserialize();
                // Only the active (current-season) league of each name needs promo/releg links.
                if (!league.IsComplete)
                    leaguesByName[league.Name] = league;
            }
            else if (comp is Cup cup)
            {
                cup.OnAfterDeserialize();
            }
        }

        // Link promotion/relegation
        LeagueTemplate[] templates = UnityEngine.Resources.LoadAll<LeagueTemplate>("Competitions/Leagues");
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

        // Register all fixtures in FixturesManager lookup
        FixturesManager.Instance.RegisterFixtures(allRestoredFixtures);

        Debug.Log($"[Restore] Restored {state.Competitions.Count} competitions, {allRestoredFixtures.Count} fixtures");
    }
}
