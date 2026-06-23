using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns player availability — injuries, suspensions and (very rarely) death. Two sources feed it:
///   • Off-pitch: a small daily roll can give a squad member an illness/injury, or — astronomically
///     rarely — kill them, making them permanently unavailable.
///   • On-pitch: after one of the player's matches, fouls recorded by the match engine are turned into
///     bookings (yellow accumulation / straight reds → suspensions) and injuries to the player's team.
///
/// Recovery is date-driven (injuries heal when their date passes); suspensions count down one per match
/// the team plays. The whole system is asset-optional: assign the EventType slots to route news through
/// the morale inbox + notifications, or leave them null to just apply morale and log.
/// </summary>
public class InjuryManager : MonoBehaviour
{
    public static InjuryManager Instance { get; private set; }

    [Header("Off-pitch misfortune")]
    [Tooltip("Daily chance that a squad member suffers an off-pitch injury or illness.")]
    [SerializeField, Range(0f, 0.2f)] private float dailyMisfortuneChance = 0.02f;

    [Header("Optional inbox integration (leave null to just log + apply morale)")]
    [SerializeField] private EventType injuryEvent;
    [SerializeField] private EventType illnessEvent;
    [SerializeField] private EventType deathEvent;
    [SerializeField] private Notification notificationPrefab;

    /// <summary>Recent injury/suspension headlines (newest first) for a news/squad UI.</summary>
    public List<string> RecentNews { get; private set; } = new List<string>();

    private const int MAX_NEWS = 30;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        // Add the daily listener WITHOUT touching the advance-response counter (same as EventsManager) —
        // only GameManager's listener is expected to call RespondToAdvance.
        if (CalenderManager.Instance != null)
            CalenderManager.Instance.NewDay.AddListener(OnNewDay);
    }

    // ————————————————————— daily tick —————————————————————

    private void OnNewDay(DateTime date)
    {
        Team me = TeamManager.Instance != null ? TeamManager.Instance.MyTeam : null;
        if (me == null) return;

        // 1. Heal anyone whose lay-off has elapsed.
        foreach (var p in me.Players) p.TickInjuryRecovery(date);

        // 2. Roll for an off-pitch misfortune.
        RollOffPitchMisfortune(me, date);
    }

    private void RollOffPitchMisfortune(Team team, DateTime date)
    {
        if (UnityEngine.Random.value > dailyMisfortuneChance) return;

        var fit = team.AvailablePlayers;
        if (fit.Count == 0) return;

        Player victim = fit[UnityEngine.Random.Range(0, fit.Count)];

        float s = UnityEngine.Random.value;
        InjuryType type;
        if (s < 0.55f) type = InjuryType.Knock;        // minor illness / niggle
        else if (s < 0.80f) type = InjuryType.Standard;
        else if (s < 0.94f) type = InjuryType.Hamstring;
        else if (s < 0.995f) type = InjuryType.ACL;
        else type = InjuryType.Death;                   // ~0.5% of misfortunes

        if (type == InjuryType.Death)
        {
            victim.ApplyInjury(InjuryType.Death, date);
            AnnounceDeath(victim);
            ApplyInjuryMorale(team, victim, InjuryType.Death);
        }
        else
        {
            victim.ApplyInjury(type, date);
            bool illness = type == InjuryType.Knock && UnityEngine.Random.value < 0.5f;
            AnnounceInjury(victim, type, illness);
            ApplyInjuryMorale(team, victim, type);
        }

        team.EnsureAvailableLineup();
    }

    // ————————————————————— post-match consequences —————————————————————

    /// <summary>
    /// Applies bookings, suspensions and injuries to the player's team from a completed fixture it played in.
    /// Suspensions are served first (the banned player sat THIS match out), then this match's new cards apply.
    /// </summary>
    public void ProcessMatchConsequences(Fixture fixture)
    {
        Team me = TeamManager.Instance != null ? TeamManager.Instance.MyTeam : null;
        if (me == null || fixture == null) return;
        if (!fixture.InvolvesTeam(me.TeamId)) return;

        DateTime today = CalenderManager.Instance != null ? CalenderManager.Instance.CurrentDay : DateTime.MinValue;

        bool meHome = fixture.HomeTeam == me;
        Match.TeamStats myStats = meHome ? fixture.Result.home : fixture.Result.away;
        Match.TeamStats oppStats = meHome ? fixture.Result.away : fixture.Result.home;

        // 1. Serve one match of any existing suspension (the banned players sat this one out).
        foreach (var p in me.Players) p.ServeOneSuspensionMatch();

        // 2. Cards my players picked up (they are the offenders in my fouls list).
        if (myStats.fouls != null)
        {
            foreach (var foul in myStats.fouls)
            {
                if (foul.offender == null || foul.offender.Team != me) continue;

                if (foul.card == Card.Yellow)
                {
                    if (foul.offender.AddYellowCard())
                        AddNews($"{foul.offender.FullName} is suspended after picking up {Player.YELLOW_SUSPENSION_THRESHOLD} yellow cards.");
                }
                else if (foul.card == Card.Red || foul.card == Card.RedAndSuspension)
                {
                    foul.offender.ApplyRedCard();
                    AddNews($"{foul.offender.FullName} is sent off and will be suspended.");
                }
            }
        }

        // 3. Injuries to my players (they are the victims in the opponent's fouls list).
        if (oppStats.fouls != null)
        {
            foreach (var foul in oppStats.fouls)
            {
                if (foul.victim == null || foul.victim.Team != me) continue;
                if (foul.injuryType == InjuryType.None) continue;

                if (foul.injuryType == InjuryType.Death)
                {
                    foul.victim.ApplyInjury(InjuryType.Death, today);
                    AnnounceDeath(foul.victim);
                    ApplyInjuryMorale(me, foul.victim, InjuryType.Death);
                }
                else
                {
                    foul.victim.ApplyInjury(foul.injuryType, today);
                    AnnounceInjury(foul.victim, foul.injuryType, false);
                    ApplyInjuryMorale(me, foul.victim, foul.injuryType);
                }
            }
        }

        me.EnsureAvailableLineup();
    }

    // ————————————————————— news / morale —————————————————————

    private void AnnounceInjury(Player victim, InjuryType type, bool illness)
    {
        string verb = illness ? "has fallen ill" : $"has picked up a {type} injury";
        string back = victim.InjuredUntil.HasValue ? $" (out until ~{CalenderManager.ShortDateWordsNoYear(victim.InjuredUntil.Value)})" : "";
        AddNews($"{victim.FullName} {verb}{back}.");
        RaiseEvent(illness ? illnessEvent : injuryEvent, victim);
    }

    private void AnnounceDeath(Player victim)
    {
        AddNews($"Tragic news: {victim.FullName} has passed away. The club is in mourning.");
        RaiseEvent(deathEvent, victim);
    }

    /// <summary>Routes news through the morale inbox + a notification when the EventType is assigned.</summary>
    private void RaiseEvent(EventType type, Player about)
    {
        if (type == null || EventsManager.Instance == null) return;

        var evt = new Event(type, new List<Person> { about },
                            CalenderManager.Instance != null ? CalenderManager.Instance.CurrentDay : DateTime.MinValue);
        EventsManager.Instance.AddEvent(evt);

        if (notificationPrefab != null && HomePageUI.Instance != null)
        {
            Notification n = Instantiate(notificationPrefab, HomePageUI.Instance.Elements);
            n.Setup(evt, about);
        }
    }

    /// <summary>
    /// Morale impact scaled by injury severity. A small knock only nicks the injured player's own mood; a
    /// long-term injury hits him hard and ripples a little through the squad; a death rocks the whole team.
    /// </summary>
    private void ApplyInjuryMorale(Team team, Player victim, InjuryType type)
    {
        int Range(int a, int b) => UnityEngine.Random.Range(a, b);

        // (victim mood, victim passion, teammate mood, teammate passion)
        int vMood = 0, vPass = 0, tMood = 0, tPass = 0;
        switch (type)
        {
            case InjuryType.Knock:     vMood = Range(2, 5);   vPass = 0;            tMood = 0;            tPass = 0; break;
            case InjuryType.Standard:  vMood = Range(5, 10);  vPass = Range(0, 3);  tMood = Range(0, 2);  tPass = 0; break;
            case InjuryType.Hamstring: vMood = Range(8, 14);  vPass = Range(2, 6);  tMood = Range(1, 3);  tPass = 0; break;
            case InjuryType.ACL:       vMood = Range(16, 26); vPass = Range(8, 16); tMood = Range(2, 5);  tPass = Range(0, 3); break;
            case InjuryType.Death:     vMood = 0;             vPass = 0;            tMood = Range(12, 22); tPass = Range(8, 16); break;
        }

        // The injured player themselves (skip for death — they're gone).
        if (type != InjuryType.Death && victim != null)
        {
            victim.Morale.Mood = Mathf.Clamp(victim.Morale.Mood - vMood, 0, 100);
            victim.Morale.Passion = Mathf.Clamp(victim.Morale.Passion - vPass, 0, 100);
        }

        // The rest of the squad — only for the heavier severities.
        if (tMood != 0 || tPass != 0)
        {
            foreach (var p in team.Players)
            {
                if (p == victim) continue;
                p.Morale.Mood = Mathf.Clamp(p.Morale.Mood - tMood, 0, 100);
                p.Morale.Passion = Mathf.Clamp(p.Morale.Passion - tPass, 0, 100);
            }
        }
    }

    private void AddNews(string line)
    {
        RecentNews.Insert(0, line);
        if (RecentNews.Count > MAX_NEWS) RecentNews.RemoveAt(RecentNews.Count - 1);
        Debug.Log($"[Injury] {line}");
    }
}
