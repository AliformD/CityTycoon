using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;

namespace CityTycoon.Engine.Events;

/// <summary>
/// How one finished trade worked out (decision #150): the rent each side later collected
/// from what it got in the trade. For a color property that means the whole group, because
/// finishing a group makes every piece of it earn more (double rent, then houses). <see cref="TradeOffer.FromId"/> is whoever wrote
/// the final terms (the proposer, or the last player to counter).
/// </summary>
/// <param name="LogIndex">Where the <see cref="TradeCompleted"/> event is in the log.</param>
/// <param name="FromRent">Rent <c>Offer.FromId</c> collected afterwards on what they got (whole groups for color properties).</param>
/// <param name="ToRent">The same for <c>Offer.ToId</c>.</param>
/// <param name="Counters">How many counter-offers it took.</param>
public sealed record TradeOutcome(int LogIndex, int Round, TradeOffer Offer, int Counters, int FromRent, int ToRent);

/// <summary>
/// A report card for every trade in a game, worked out from the game log alone (like
/// <see cref="GameHistory"/>), so it works on any save and needs nothing new stored.
/// </summary>
public static class TradeReport
{
    public static IReadOnlyList<TradeOutcome> Trades(IReadOnlyList<GameEvent> log, GameContent content)
    {
        var outcomes = new List<TradeOutcome>();
        int round = 0;
        int counters = 0;
        for (int i = 0; i < log.Count; i++)
        {
            switch (log[i])
            {
                case TurnStarted t:
                    round = t.Round;
                    break;
                case TradeProposed:
                    counters = 0;
                    break;
                case TradeCountered:
                    counters++;
                    break;
                case TradeCompleted done:
                    TradeOffer offer = done.Offer;
                    outcomes.Add(new TradeOutcome(i, round, offer, counters,
                        RentAfter(log, i, offer.FromId, WithGroups(content, offer.GetProperties)),
                        RentAfter(log, i, offer.ToId, WithGroups(content, offer.GiveProperties))));
                    break;
            }
        }
        return outcomes;
    }

    /// <summary>The properties, plus the rest of the color group each one belongs to.</summary>
    private static HashSet<string> WithGroups(GameContent content, IReadOnlyList<string> propertyIds) =>
        propertyIds.SelectMany(id => content.Property(id).Group is string group ? content.GroupMembers(group) : [id]).ToHashSet();

    /// <summary>Rent the player collected on these properties after the log entry at <paramref name="from"/>.</summary>
    private static int RentAfter(IReadOnlyList<GameEvent> log, int from, int playerId, HashSet<string> propertyIds)
    {
        int total = 0;
        for (int i = from + 1; i < log.Count; i++)
        {
            if (log[i] is RentPaid r && r.ToId == playerId && propertyIds.Contains(r.PropertyId))
                total += r.Amount;
        }
        return total;
    }
}
