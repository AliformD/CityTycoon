using CityTycoon.Engine.Content;
using CityTycoon.Engine.Views;

namespace CityTycoon.Engine.Events;

/// <summary>Turns events into short sentences for the game log.</summary>
public static class EventDescriber
{
    /// <summary>
    /// Describes an event using only what a player can see (their view), so the
    /// log can never leak anything secret. Returns "" for events not worth a line.
    /// </summary>
    public static string Describe(GameEvent e, PlayerView view, GameContent content)
    {
        string Name(int id) => view.Player(id).Name;
        string Space(string id) => content.Properties.TryGetValue(id, out var p) ? p.Name : view.Spaces.First(s => s.Id == id).Name;
        string SpaceAt(int index) => view.Spaces[index].Name;

        return e switch
        {
            GameStarted g => $"New game (seed {g.Seed}). Turn order: {string.Join(", ", g.TurnOrder.Select(Name))}.",
            TurnStarted t => $"Round {t.Round}: {Name(t.PlayerId)}'s turn.",
            DiceRolled d => $"{Name(d.PlayerId)} rolled {d.Die1} + {d.Die2} = {d.Total}{(d.IsDoubles ? " (doubles!)" : "")}.",
            PlayerMoved m => $"{Name(m.PlayerId)} moved to {SpaceAt(m.To)}.",
            PassedGo g => $"{Name(g.PlayerId)} passed GO and collected ${g.Salary}.",
            LandedOn _ => "",
            AuctionStarted a => $"Auction for {Space(a.PropertyId)} (minimum bid ${a.MinBid}).",
            BidsRevealed b => "Bids: " + string.Join(", ", b.Bids.Select(x => $"{Name(x.PlayerId)} {(x.Amount is int amount ? "$" + amount : "passed")}")) + ".",
            AuctionWon w => $"{Name(w.PlayerId)} won {Space(w.PropertyId)} for ${w.Price}.",
            AuctionNoBids n => $"Nobody bid on {Space(n.PropertyId)}. It stays with the bank.",
            RentPaid r => $"{Name(r.FromId)} paid ${r.Amount} rent to {Name(r.ToId)} for {Space(r.PropertyId)}.",
            TaxPaid t => $"{Name(t.PlayerId)} paid ${t.Amount} {Space(t.SpaceId)}.",
            BailPaid b => $"{Name(b.PlayerId)} paid ${b.Amount} bail.",
            SentToJail j => $"{Name(j.PlayerId)} went to Jail ({j.Reason}).",
            LeftJail l => $"{Name(l.PlayerId)} left Jail ({l.How}).",
            StayedInJail s => $"{Name(s.PlayerId)} stays in Jail.",
            PlayerBankrupt b => $"{Name(b.PlayerId)} went bankrupt" + (b.CreditorId is int c ? $" and handed everything to {Name(c)}." : " to the bank."),
            RoundEnded r => $"Round {r.Round} is over.",
            GameEnded g => $"Game over: {g.Reason} Winner: {string.Join(" and ", g.WinnerIds.Select(Name))}.",
            _ => e.ToString(),
        };
    }
}
