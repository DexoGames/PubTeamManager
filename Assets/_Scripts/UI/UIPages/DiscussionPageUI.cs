using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DiscussionPageUI : UIPage
{
    [SerializeField] DialogueUI dialogue;
    [SerializeField] ResponseManager responseManager;

    Person person;
    Event thisEvent;
    DiscussionContext discussionContext;

    public static DiscussionPageUI Instance { get; private set; }

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
            responseManager.OnResponseChosen.AddListener(Response);
        }
    }


    protected override void OnShow(Event @event, Person person)
    {
        base.OnShow(@event, person);

        thisEvent = @event;
        this.person = person;

        discussionContext = new DiscussionContext(@event, person);
        dialogue.Setup(discussionContext);
        string promise = PromiseLabel(@event.type.concern);
        if (promise == null) responseManager.SpawnButtons();
        else responseManager.SpawnButtons(new Dictionary<Event.Response, string>
        {
            { Event.Response.Encourage, promise },
            { Event.Response.Deflect, "Make no promise" }
        });
    }

    static string PromiseLabel(ClubConcern concern)
    {
        switch (concern)
        {
            case ClubConcern.PlayingTime: return "Start next match";
            case ClubConcern.Workload: return "Rest next match";
            case ClubConcern.Tactics: return "Simplify next match";
            default: return null;
        }
    }

    public void Response(Event.Response response)
    {
        if (!EventsManager.Instance.Events.Contains(thisEvent)) return;

        bool promised = thisEvent.type.concern != ClubConcern.None && response == Event.Response.Encourage;
        if (promised)
        {
            var player = (Player)person;
            player.Selection.Promise = thisEvent.type.concern;
            player.Selection.PromisedComplexity = player.Team.Tactic.Complexity;
        }

        Event.Reaction reaction = EventsManager.Instance.ReactionTable[(response, person.Personality)];
        reaction = Event.ReactionSeverityChange(response, reaction, thisEvent.type.severity);

        (int, int) moraleChange = person.NewMorale(thisEvent.type.moodChange, reaction, thisEvent.type.severity);
        dialogue.UpdateDialogue(ReactionToDialogue(reaction));

        dialogue.UpdateExtraInfo(promised ? "Promise agreed · " + PromiseLabel(thisEvent.type.concern) : InfoAboutResponse(moraleChange));

        EventsManager.Instance.Events.Remove(thisEvent);

        // Persist the morale change + event removal.
        SaveManager.Instance.SaveCore();
    }

    public static string ReactionToDialogue(Event.Reaction reaction)
    {
        switch (reaction)
        {
            case Event.Reaction.Terrible:
                return "Boss, I don't know what to say. Is there something wrong with you?!?";
            case Event.Reaction.Bad:
                return "Boss, what are you on about?!";
            case Event.Reaction.Poor:
                return "Ummm, okay??";
            case Event.Reaction.Neutral:
                return "Sure boss.";
            case Event.Reaction.Good:
                return "Cheers boss.";
            case Event.Reaction.Great:
                return "Thanks boss!";
            case Event.Reaction.Amazing:
                return "Boss, you are absolutely correct!!";
        }
        return "Okay.";
    }

    public static string InfoAboutResponse((int, int) moraleChange)
    {
        int moodChange = moraleChange.Item1;
        int passionChange = moraleChange.Item2;

        int distance = Mathf.Abs(moodChange) + Mathf.Abs(passionChange);
        string much = distance > 10 ? "much " : "";

        if (Mathf.Abs(moodChange) <= 2 && Mathf.Abs(passionChange) <= 2) { }

        else if (moodChange > 0 && passionChange > 0)
        {
            return $"They seemed to get {much}more excited.";
        }
        else if (moodChange > 0 && passionChange < 0)
        {
            return $"They seemed to get {much}more relaxed.";
        }
        else if (moodChange < 0 && passionChange > 0)
        {
            return $"They seemed to get {much}more annoyed.";
        }
        else if (moodChange < 0 && passionChange < 0)
        {
            return $"They seemed to get {much}more upset.";
        }

        return "There seemed to be no change in morale.";
    }
}