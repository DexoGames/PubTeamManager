using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Event;
using static EventType;

public class EventsManager : MonoBehaviour
{
    public static EventsManager Instance { get; private set; }

    public List<Event> Events = new List<Event>();
    [HideInInspector] public Dictionary<(Response, Player.PersonalityType), Reaction> ReactionTable { get;  private set; }
    [SerializeField] Notification notificationPrefab;
    [SerializeField] EventType winEvent;
    [SerializeField] EventType loseEvent;

    EventType[] randomEvents;

    public void Awake()
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

    void Start()
    {
        CalenderManager.Instance.NewDay.AddListener(CheckEvents);
        CalenderManager.Instance.NewDay.AddListener(AddRandomEvent);

        randomEvents = Resources.LoadAll<EventType>("Events/Random");

        ReactionTable = CreateReactionTable();
    }

    public Dictionary<(Response, Player.PersonalityType), Reaction> CreateReactionTable()
    {
        Dictionary<(Response, Player.PersonalityType), Reaction> reactionTable = new Dictionary<(Response, Person.PersonalityType), Reaction>();

        void Add(Response response, Player.PersonalityType personality, Reaction reaction)
        {
            reactionTable.Add((response, personality), reaction);
        }

        Add(Response.Praise, Person.PersonalityType.Aggressive, Reaction.Poor);
        Add(Response.Praise, Person.PersonalityType.Calm, Reaction.Good);
        Add(Response.Praise, Person.PersonalityType.Cautious, Reaction.Good);
        Add(Response.Praise, Person.PersonalityType.Cocky, Reaction.Amazing);
        Add(Response.Praise, Person.PersonalityType.Driven, Reaction.Good);
        Add(Response.Praise, Person.PersonalityType.Kind, Reaction.Good);
        Add(Response.Praise, Person.PersonalityType.Lazy, Reaction.Neutral);
        Add(Response.Praise, Person.PersonalityType.Shy, Reaction.Poor);
        Add(Response.Praise, Person.PersonalityType.Silly, Reaction.Neutral);
        Add(Response.Praise, Person.PersonalityType.Smart, Reaction.Poor);

        Add(Response.Encourage, Person.PersonalityType.Aggressive, Reaction.Bad);
        Add(Response.Encourage, Person.PersonalityType.Calm, Reaction.Good);
        Add(Response.Encourage, Person.PersonalityType.Cautious, Reaction.Great);
        Add(Response.Encourage, Person.PersonalityType.Cocky, Reaction.Bad);
        Add(Response.Encourage, Person.PersonalityType.Driven, Reaction.Good);
        Add(Response.Encourage, Person.PersonalityType.Kind, Reaction.Great);
        Add(Response.Encourage, Person.PersonalityType.Lazy, Reaction.Neutral);
        Add(Response.Encourage, Person.PersonalityType.Shy, Reaction.Amazing);
        Add(Response.Encourage, Person.PersonalityType.Silly, Reaction.Poor);
        Add(Response.Encourage, Person.PersonalityType.Smart, Reaction.Poor);

        Add(Response.Challenge, Person.PersonalityType.Aggressive, Reaction.Terrible);
        Add(Response.Challenge, Person.PersonalityType.Calm, Reaction.Good);
        Add(Response.Challenge, Person.PersonalityType.Cautious, Reaction.Bad);
        Add(Response.Challenge, Person.PersonalityType.Cocky, Reaction.Terrible);
        Add(Response.Challenge, Person.PersonalityType.Driven, Reaction.Amazing);
        Add(Response.Challenge, Person.PersonalityType.Kind, Reaction.Poor);
        Add(Response.Challenge, Person.PersonalityType.Lazy, Reaction.Poor);
        Add(Response.Challenge, Person.PersonalityType.Shy, Reaction.Poor);
        Add(Response.Challenge, Person.PersonalityType.Silly, Reaction.Bad);
        Add(Response.Challenge, Person.PersonalityType.Smart, Reaction.Good);

        Add(Response.Persuade, Person.PersonalityType.Aggressive, Reaction.Bad);
        Add(Response.Persuade, Person.PersonalityType.Calm, Reaction.Great);
        Add(Response.Persuade, Person.PersonalityType.Cautious, Reaction.Neutral);
        Add(Response.Persuade, Person.PersonalityType.Cocky, Reaction.Bad);
        Add(Response.Persuade, Person.PersonalityType.Driven, Reaction.Good);
        Add(Response.Persuade, Person.PersonalityType.Kind, Reaction.Good);
        Add(Response.Persuade, Person.PersonalityType.Lazy, Reaction.Bad);
        Add(Response.Persuade, Person.PersonalityType.Shy, Reaction.Good);
        Add(Response.Persuade, Person.PersonalityType.Silly, Reaction.Bad);
        Add(Response.Persuade, Person.PersonalityType.Smart, Reaction.Great);

        Add(Response.Inspire, Person.PersonalityType.Aggressive, Reaction.Poor);
        Add(Response.Inspire, Person.PersonalityType.Calm, Reaction.Good);
        Add(Response.Inspire, Person.PersonalityType.Cautious, Reaction.Poor);
        Add(Response.Inspire, Person.PersonalityType.Cocky, Reaction.Good);
        Add(Response.Inspire, Person.PersonalityType.Driven, Reaction.Great);
        Add(Response.Inspire, Person.PersonalityType.Kind, Reaction.Amazing);
        Add(Response.Inspire, Person.PersonalityType.Lazy, Reaction.Poor);
        Add(Response.Inspire, Person.PersonalityType.Shy, Reaction.Neutral);
        Add(Response.Inspire, Person.PersonalityType.Silly, Reaction.Good);
        Add(Response.Inspire, Person.PersonalityType.Smart, Reaction.Great);

        Add(Response.Galvanise, Person.PersonalityType.Aggressive, Reaction.Great);
        Add(Response.Galvanise, Person.PersonalityType.Calm, Reaction.Bad);
        Add(Response.Galvanise, Person.PersonalityType.Cautious, Reaction.Poor);
        Add(Response.Galvanise, Person.PersonalityType.Cocky, Reaction.Great);
        Add(Response.Galvanise, Person.PersonalityType.Driven, Reaction.Great);
        Add(Response.Galvanise, Person.PersonalityType.Kind, Reaction.Bad);
        Add(Response.Galvanise, Person.PersonalityType.Lazy, Reaction.Poor);
        Add(Response.Galvanise, Person.PersonalityType.Shy, Reaction.Bad);
        Add(Response.Galvanise, Person.PersonalityType.Silly, Reaction.Great);
        Add(Response.Galvanise, Person.PersonalityType.Smart, Reaction.Poor);

        Add(Response.Rage, Person.PersonalityType.Aggressive, Reaction.Great);
        Add(Response.Rage, Person.PersonalityType.Calm, Reaction.Bad);
        Add(Response.Rage, Person.PersonalityType.Cautious, Reaction.Bad);
        Add(Response.Rage, Person.PersonalityType.Cocky, Reaction.Bad);
        Add(Response.Rage, Person.PersonalityType.Driven, Reaction.Poor);
        Add(Response.Rage, Person.PersonalityType.Kind, Reaction.Terrible);
        Add(Response.Rage, Person.PersonalityType.Lazy, Reaction.Amazing);
        Add(Response.Rage, Person.PersonalityType.Shy, Reaction.Terrible);
        Add(Response.Rage, Person.PersonalityType.Silly, Reaction.Bad);
        Add(Response.Rage, Person.PersonalityType.Smart, Reaction.Bad);

        Add(Response.Deflect, Person.PersonalityType.Aggressive, Reaction.Neutral);
        Add(Response.Deflect, Person.PersonalityType.Calm, Reaction.Poor);
        Add(Response.Deflect, Person.PersonalityType.Cautious, Reaction.Good);
        Add(Response.Deflect, Person.PersonalityType.Cocky, Reaction.Great);
        Add(Response.Deflect, Person.PersonalityType.Driven, Reaction.Terrible);
        Add(Response.Deflect, Person.PersonalityType.Kind, Reaction.Neutral);
        Add(Response.Deflect, Person.PersonalityType.Lazy, Reaction.Great);
        Add(Response.Deflect, Person.PersonalityType.Shy, Reaction.Poor);
        Add(Response.Deflect, Person.PersonalityType.Silly, Reaction.Poor);
        Add(Response.Deflect, Person.PersonalityType.Smart, Reaction.Bad);

        return reactionTable;
    }

    public EventType PickRandomEvent(EventType[] allTypes)
    {
        List<float> odds = new List<float>();
        float cumulativeSum = 0f;

        foreach (EventType t in allTypes)
        {
            cumulativeSum += t.odds;
            odds.Add(cumulativeSum);
        }

        float rand = Random.Range(0, cumulativeSum);

        for (int i = 0; i < allTypes.Length; i++)
        {
            if (rand <= odds[i])
            {
                return allTypes[i];
            }
        }

        return allTypes[allTypes.Length - 1];
    }


    public void AddEvent(Event _event)
    {
        Events.Add(_event);

        foreach(Person p in _event.affected)
        {
            p.Morale.Mood += _event.type.moodChange;
        }
    }

    public void AddWinEvent(Team winner, Team loser)
    {
        Event winEvent = new Event(this.winEvent, TeamManager.Instance.MyTeam.Players.ToList<Person>(), CalenderManager.Instance.CurrentDay, new List<string>{ loser.Name });

        AddEvent(winEvent);
    }
    public void AddLoseEvent(Team winner, Team loser)
    {
        Event loseEvent = new Event(this.loseEvent, TeamManager.Instance.MyTeam.Players.ToList<Person>(), CalenderManager.Instance.CurrentDay, new List<string> { winner.Name });

        AddEvent(loseEvent);
    }

    public void AddRandomEvent(System.DateTime date)
    {
        int rand = Random.Range(0, 5);
        if (rand > 1) return;

        EventType[] allTypes = randomEvents;
        Person[] allPlayers = TeamManager.Instance.MyTeam.Players.ToArray();
        allPlayers.Shuffle();

        EventType randomType = PickRandomEvent(allTypes);

        Event randomEvent = new Event(randomType, allPlayers.Take(randomType.noAffected).ToList(), date);
        AddEvent(randomEvent);

        if (randomEvent.type.severity == Severity.Irrelevant || randomEvent.type.severity == Severity.Pleasant || randomEvent.type.severity == Severity.Unfortunate) return;

        Notification notification = Instantiate(notificationPrefab, HomePageUI.Instance.Elements);
        notification.Setup(randomEvent, randomEvent.affected[0]);
    }

    public void CheckEvents(System.DateTime date)
    {
        for(int i = 0; i < Events.Count; i++)
        {
            Event e = Events[i];

            int days = (date - e.date).Days;
            if(days > 7)
            {
                Events.Remove(e);
                e = null;
                i--;
            }
            else
            {
                return;
            }
        }
    }
}