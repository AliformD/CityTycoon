using System.Text;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI;

/// <summary>
/// The game log download (decision #150): every event of a game as a plain sentence, with the
/// bots' private notes slotted in where they made each choice, and a report card for every
/// trade at the end. It is a developer tool (Settings), meant to be read by people or sent to
/// Claude to look for bot mistakes, so it shows things the players could not see.
/// </summary>
public static class GameLogText
{
    /// <param name="view">A view of the game (the public one is fine: the log is the same for everyone).</param>
    /// <param name="seats">What sits in each seat, like "Person" or "Hard bot".</param>
    /// <param name="notes">Every bot's notebook, in any order.</param>
    public static string Write(PlayerView view, GameContent content, IReadOnlyDictionary<int, string> seats, IEnumerable<BotNote> notes)
    {
        string Name(int id) => view.Player(id).Name;
        string Space(string id) => content.Properties.TryGetValue(id, out var p) ? p.Name : view.Spaces.First(s => s.Id == id).Name;
        string Card(string id) => content.Cards.TryGetValue(id, out var c) ? c.Name : id;
        static string Money(int dollars) => (dollars < 0 ? "-$" : "$") + Math.Abs(dollars).ToString("N0");

        var text = new StringBuilder();
        text.AppendLine("City Tycoon game log");
        text.AppendLine(view.Mode == GameMode.Timed ? $"Timed game, {view.MaxRounds} rounds, reached round {view.Round}." : $"Classic game, reached round {view.Round}.");
        foreach (var p in view.Players)
            text.AppendLine($"  {p.Name}: {seats.GetValueOrDefault(p.Id, "?")}");
        text.AppendLine("Lines starting [Bot notes] are the bot's private thinking. Nobody saw them during the game.");
        text.AppendLine();

        var byIndex = notes.ToLookup(n => n.LogIndex);
        for (int i = 0; i <= view.Log.Count; i++)
        {
            // A note's LogIndex is how long the log was when the bot chose, so it goes just before that event.
            foreach (var note in byIndex[i])
                text.AppendLine(note.Describe(Name, Space, Card));
            if (i == view.Log.Count)
                break;
            string line = EventDescriber.Describe(view.Log[i], view, content);
            if (line != "")
                text.AppendLine(line);
        }

        var trades = TradeReport.Trades(view.Log, content);
        text.AppendLine();
        text.AppendLine($"Trade report card ({trades.Count} trade{(trades.Count == 1 ? "" : "s")})");
        if (trades.Count == 0)
            text.AppendLine("  No trades were made.");
        foreach (var t in trades)
        {
            text.AppendLine($"  Round {t.Round}: {Name(t.Offer.FromId)} traded with {Name(t.Offer.ToId)}"
                + (t.Counters > 0 ? $" after {t.Counters} counter-offer{(t.Counters == 1 ? "" : "s")}" : "")
                + $": {EventDescriber.DescribeTrade(t.Offer, Space, Card)}");
            text.AppendLine($"    Rent collected afterwards on what they got (whole color groups): {Name(t.Offer.FromId)} {Money(t.FromRent)}, {Name(t.Offer.ToId)} {Money(t.ToRent)}.");
        }
        return text.ToString();
    }
}
