using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;

namespace CityTycoon.AI;

/// <summary>
/// One line of a bot's private notebook (decision #150): what it was thinking when it offered,
/// accepted, declined or countered a trade. Bots keep these for the people building the game,
/// not for the other players: they hold the bot's limit, which would spoil the haggling.
/// So they never go in the game log or a <see cref="Engine.Views.PlayerView"/>. They are saved
/// with the game, written to a file by the Sim, and shown in the game log download (a developer
/// tool in Settings). Writing them never changes what the bot does.
/// </summary>
/// <param name="LogIndex">How long the game log was when the bot decided, so the note can be put in the right place.</param>
/// <param name="Kind">"Offered" (the bot started the trade) or "Answered" (it was asked).</param>
/// <param name="Offer">The trade as it was written by whoever made it.</param>
/// <param name="Choice">"Offer", "Accept", "Decline" or "Counter".</param>
/// <param name="Counter">The bot's counter-offer, if it sent one.</param>
/// <param name="Basic">Cash, properties and cards at face value, from the bot's side, if the trade went through as offered.</param>
/// <param name="Mine">What the color groups the trade finishes for the bot are worth to it.</param>
/// <param name="Theirs">What the groups it finishes for the other player cost the bot.</param>
/// <param name="Limit">The most cash the bot would pay for these items (negative: the least it must be paid).</param>
public sealed record BotNote(
    int LogIndex,
    int Round,
    int BotId,
    string Level,
    string Personality,
    string Kind,
    TradeOffer Offer,
    string Choice,
    TradeOffer? Counter,
    string? Why,
    int Cash,
    double Basic,
    double Mine,
    double Theirs,
    int Limit)
{
    /// <summary>Basic + Mine − Theirs: how many dollars better off the bot thought it would be.</summary>
    public double Gain => Basic + Mine - Theirs;

    /// <summary>A few plain lines, for the game log download.</summary>
    public string Describe(Func<int, string> playerName, Func<string, string> spaceName, Func<string, string>? cardName = null)
    {
        static string Money(double dollars) => (Math.Round(dollars) < 0 ? "-$" : "$") + Math.Abs(Math.Round(dollars)).ToString("N0");
        int other = Offer.FromId == BotId ? Offer.ToId : Offer.FromId;
        string trade = EventDescriber.DescribeTrade(Offer, spaceName, cardName);
        string what = Kind == "Offered"
            ? $"offered {playerName(other)}: {trade}"
            : $"was offered by {playerName(other)}: {trade}";
        string limit = Limit >= 0 ? $"would pay up to {Money(Limit)}" : $"wants at least {Money(-Limit)}";
        var lines = new List<string>
        {
            $"[Bot notes] {playerName(BotId)} ({Level}, {Personality}, {Money(Cash)} cash) {what}",
            $"    Sums: face value {Money(Basic)}, sets it gets {Money(Mine)}, sets {playerName(other)} gets {Money(-Theirs)}, total {Money(Gain)}. For these items it {limit}.",
            $"    Choice: {Choice}" + (Counter != null ? $" with {EventDescriber.DescribeTrade(Counter, spaceName, cardName)}" : "")
                + (string.IsNullOrEmpty(Why) ? "" : $". Said: \"{Why}\""),
        };
        return string.Join("\n", lines);
    }
}
