using UnityEngine;

/// <summary>
/// Live-feed highlight for a foul: a booking, a sending-off, and/or an injury to the fouled player.
/// Broadcast by the match engine when <see cref="MatchEngine"/> rolls a foul.
/// </summary>
public class FoulHighlight : Highlight
{
    public override float Duration => Card == Card.None ? 0.7f : 1.3f;

    public Player Offender { get; set; }
    public Player Victim { get; set; }
    public Card Card { get; set; }
    public InjuryType Injury { get; set; }

    public FoulHighlight(Team team, Minute minute, Player offender, Player victim, Card card, InjuryType injury)
        : base(team, minute)
    {
        Offender = offender;
        Victim = victim;
        Card = card;
        Injury = injury;
    }

    public override string Describe()
    {
        string off = Offender != null ? Offender.Surname : "A player";
        string vic = Victim != null ? Victim.Surname : "an opponent";

        string line;
        switch (Card)
        {
            case Card.Yellow:
                line = $"Yellow card — {off} is booked for a foul on {vic}";
                break;
            case Card.Red:
            case Card.RedAndSuspension:
                line = $"RED CARD! {off} is sent off for a foul on {vic}";
                break;
            default:
                line = $"Free kick — {off} fouls {vic}";
                break;
        }

        if (Injury == InjuryType.Death)
            line += $", and {vic} is stretchered off in a terrible state";
        else if (Injury != InjuryType.None)
            line += $", and {vic} stays down injured ({Injury})";

        return line + ".";
    }
}
