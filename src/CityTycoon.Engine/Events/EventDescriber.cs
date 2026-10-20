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
            AuctionWon w => w.DownPayment
                ? $"{Name(w.PlayerId)} won {Space(w.PropertyId)} for ${w.Price} with a down payment: ${w.PaidNow} now, ${w.Price - w.PaidNow} at their next GO."
                : $"{Name(w.PlayerId)} won {Space(w.PropertyId)} for ${w.Price}.",
            AuctionNoBids n => $"Nobody bid on {Space(n.PropertyId)}. It stays with the bank.",
            RentPaid r => $"{Name(r.FromId)} paid ${r.Amount} rent to {Name(r.ToId)} for {Space(r.PropertyId)}.",
            TaxPaid t => $"{Name(t.PlayerId)} paid ${t.Amount} {Space(t.SpaceId)}.",
            BailPaid b => $"{Name(b.PlayerId)} paid ${b.Amount} bail.",
            SentToJail j => $"{Name(j.PlayerId)} went to Jail ({j.Reason}).",
            LeftJail l => $"{Name(l.PlayerId)} left Jail ({l.How}).",
            StayedInJail s => $"{Name(s.PlayerId)} stays in Jail.",
            PlayerBankrupt b => $"{Name(b.PlayerId)} went bankrupt" + (b.CreditorId is int c ? $" and handed everything to {Name(c)}." : " to the bank."),
            BuildingBuilt b => $"{Name(b.PlayerId)} built on {Space(b.PropertyId)} (now {LevelName(b.NewLevel)}) for ${b.Cost}.",
            BuildingSold b => $"{Name(b.PlayerId)} sold a building on {Space(b.PropertyId)} (now {LevelName(b.NewLevel)}) for ${b.Refund}.",
            PropertyMortgaged m => $"{Name(m.PlayerId)} mortgaged {Space(m.PropertyId)} for ${m.Amount}.",
            PropertyUnmortgaged u => $"{Name(u.PlayerId)} paid ${u.Cost} to unmortgage {Space(u.PropertyId)}.",
            LoanTaken l => $"{Name(l.PlayerId)} took a ${l.Amount} loan from the bank.",
            LoanRepaid l => $"{Name(l.PlayerId)} paid back a loan (${l.Amount}).",
            DownPaymentPaid d => $"{Name(d.PlayerId)} paid the ${d.Amount} balance on {Space(d.PropertyId)}. It's fully theirs now.",
            PropertyForfeited f => $"{Name(f.PlayerId)} couldn't pay for {Space(f.PropertyId)}, so it goes back to the bank.",
            RaisingMoney r => $"{Name(r.PlayerId)} owes ${r.AmountOwed} and must raise money.",
            TradeProposed t => $"{Name(t.Offer.FromId)} offered {Name(t.Offer.ToId)} a trade: {DescribeTrade(t.Offer, Space)}.",
            TradeCompleted t => $"{Name(t.Offer.ToId)} accepted the trade from {Name(t.Offer.FromId)}."
                + (t.MortgageFees > 0 ? $" Mortgage fees paid: ${t.MortgageFees}." : ""),
            TradeDeclined t => $"The trade between {Name(t.Offer.FromId)} and {Name(t.Offer.ToId)} didn't happen ({t.Reason}).",
            RoundEnded r => $"Round {r.Round} is over.",
            GameEnded g => $"Game over: {g.Reason} Winner: {string.Join(" and ", g.WinnerIds.Select(Name))}.",
            _ => e.ToString(),
        };
    }

    /// <summary>"2 houses", "a hotel", and so on (Section 9.1: levels 1-4 are houses, 5 a hotel, 6 a skyscraper).</summary>
    public static string LevelName(int level) => level switch
    {
        0 => "no buildings",
        1 => "1 house",
        <= 4 => $"{level} houses",
        5 => "a hotel",
        _ => "a skyscraper",
    };

    /// <summary>"$100 + Mill Street for Platinum Row", read from the proposer's side.</summary>
    public static string DescribeTrade(Actions.TradeOffer offer, Func<string, string> spaceName)
    {
        static string Side(int cash, IReadOnlyList<string> props, Func<string, string> spaceName)
        {
            var parts = new List<string>();
            if (cash > 0) parts.Add($"${cash}");
            parts.AddRange(props.Select(spaceName));
            return parts.Count == 0 ? "nothing" : string.Join(" + ", parts);
        }
        return $"{Side(offer.GiveCash, offer.GiveProperties, spaceName)} for {Side(offer.GetCash, offer.GetProperties, spaceName)}";
    }
}
