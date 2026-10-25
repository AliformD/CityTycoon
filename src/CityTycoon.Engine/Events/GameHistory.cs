using CityTycoon.Engine.Content;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.Views;

namespace CityTycoon.Engine.Events;

/// <summary>What a money line in the ledger was for, so the screen can group or filter them.</summary>
public enum MoneyKind
{
    Start, Salary, Rent, Property, Building, Mortgage, Loan, Tax, Card, Trade,
    Perk, Council, Casino, Jail, Hospital, Travel, Mall, Bankruptcy,
}

/// <summary>
/// One line of a player's money ledger. <paramref name="Amount"/> is positive when money came in
/// and negative when it went out. <paramref name="Round"/> and <paramref name="TurnPlayerId"/> say
/// whose turn it was. Round 0 is the start of the game, and a null turn after round 0 means the
/// end of that round (when the City Council meets).
/// </summary>
public sealed record LedgerEntry(int Round, int? TurnPlayerId, int PlayerId, int Amount, MoneyKind Kind, string Text, int? OtherPlayerId = null);

/// <summary>What a line in a space's history is about.</summary>
public enum HistoryKind { Bought, Traded, Inherited, Lost, Built, Sold, Burned, Mortgaged, Unmortgaged, PaidOff, Revealed, Other }

/// <summary>One thing that happened to a space, like being bought, built on or traded.</summary>
public sealed record HistoryEntry(int Round, int? TurnPlayerId, HistoryKind Kind, string Text, int? PlayerId = null, int? Amount = null);

/// <summary>How the current owner got a property: from whom, for how much, and when.</summary>
/// <param name="Price">What they paid. Null when no single price fits, like a property swapped in a trade.</param>
public sealed record Acquisition(int OwnerId, int Round, HistoryKind How, int? Price, string Text);

/// <summary>Everything the history says about one space.</summary>
public sealed record SpaceHistory(IReadOnlyList<HistoryEntry> Entries, Acquisition? Acquired, int TimesLandedOn, int RentCollected, int TimesRentPaid, int FeesCollected);

/// <summary>
/// Works out the money ledger and the history of each space from the game log.
///
/// Nothing new is stored in the game state: the log already holds every event, it is saved
/// with the game, and it is public (secret things never go in it). So older saves get a full
/// history too, and a player's view is all this needs.
/// </summary>
public static class GameHistory
{
    /// <summary>
    /// Every money movement, in the order it happened, for every player (or just one).
    /// The first line for each player is their starting cash, so adding up a player's
    /// lines gives their cash right now.
    /// </summary>
    public static IReadOnlyList<LedgerEntry> Ledger(PlayerView view, GameContent content, int? playerId = null)
    {
        string Name(int id) => view.Player(id).Name;
        string Space(string id) => content.Properties.TryGetValue(id, out var p) ? p.Name : view.Spaces.FirstOrDefault(s => s.Id == id)?.Name ?? id;
        string Card(string id) => content.Cards.TryGetValue(id, out var c) ? c.Name : id;
        string Ordinance(string id) => content.Ordinance(id).Name;
        string Tile(string id) => content.GovTile(id).Name;

        var lines = new List<LedgerEntry>();
        int round = 0;
        int? turn = null;
        void Add(int who, int amount, MoneyKind kind, string text, int? other = null)
        {
            if (amount != 0 && (playerId == null || playerId == who))
                lines.Add(new LedgerEntry(round, turn, who, amount, kind, text, other));
        }
        // A payment from one player to another is two lines: out for one, in for the other.
        void Move(int fromId, int? toId, int amount, MoneyKind kind, string outText, string inText)
        {
            Add(fromId, -amount, kind, outText, toId);
            if (toId is int to)
                Add(to, amount, kind, inText, fromId);
        }
        string To(int? id) => id is int x ? Name(x) : "the bank";

        foreach (GameEvent e in view.Log)
        {
            switch (e)
            {
                case GameStarted g:
                    foreach (int id in g.TurnOrder)
                        Add(id, content.Rules.StartingCash, MoneyKind.Start, "Starting cash");
                    break;
                case TurnStarted t:
                    round = t.Round;
                    turn = t.PlayerId;
                    break;
                case RoundEnded:
                    // What happens between rounds (the City Council) is nobody's turn.
                    turn = null;
                    break;
                case PassedGo g:
                    Add(g.PlayerId, g.Salary, MoneyKind.Salary, "GO salary");
                    break;
                case CatchUpPaid c:
                    Add(c.PlayerId, c.Amount, MoneyKind.Salary, "Catch-up bonus at GO (lowest net worth)");
                    break;
                case AuctionWon w:
                    if (w.DownPayment)
                        Add(w.PlayerId, -w.PaidNow, MoneyKind.Property, $"Deposit on {Space(w.PropertyId)} (won for ${w.Price}, the rest due at GO)");
                    else
                        Move(w.PlayerId, w.PaidToId, w.PaidNow, MoneyKind.Property,
                            w.ListedPrice ? $"Bought {Space(w.PropertyId)} at its listed price"
                            : $"Won {Space(w.PropertyId)} at auction" + (w.PaidToId is int owner ? $", paid to {Name(owner)}" : ""),
                            $"Sold {Space(w.PropertyId)} at a forced auction to {Name(w.PlayerId)}");
                    break;
                case DownPaymentPaid d:
                    Add(d.PlayerId, -d.Amount, MoneyKind.Property, $"Paid off the balance on {Space(d.PropertyId)}");
                    break;
                case RentPaid r:
                    Move(r.FromId, r.ToId, r.Amount, MoneyKind.Rent, $"Rent for {Space(r.PropertyId)} to {Name(r.ToId)}", $"Rent for {Space(r.PropertyId)} from {Name(r.FromId)}");
                    break;
                case TaxPaid t:
                    Add(t.PlayerId, -t.Amount, MoneyKind.Tax, Space(t.SpaceId));
                    break;
                case BailPaid b:
                    Add(b.PlayerId, -b.Amount, MoneyKind.Jail, "Bail to leave Jail");
                    break;
                case BuildingBuilt b:
                    Add(b.PlayerId, -b.Cost, MoneyKind.Building, $"Built on {Space(b.PropertyId)} (now {EventDescriber.LevelName(b.NewLevel)})");
                    break;
                case BuildingSold b:
                    Add(b.PlayerId, b.Refund, MoneyKind.Building, $"Sold a building on {Space(b.PropertyId)} (now {EventDescriber.LevelName(b.NewLevel)})");
                    break;
                case PropertyMortgaged m:
                    Add(m.PlayerId, m.Amount, MoneyKind.Mortgage, $"Mortgaged {Space(m.PropertyId)}");
                    break;
                case PropertyUnmortgaged u:
                    Add(u.PlayerId, -u.Cost, MoneyKind.Mortgage, $"Unmortgaged {Space(u.PropertyId)}");
                    break;
                case LoanTaken l:
                    Add(l.PlayerId, l.Amount, MoneyKind.Loan, "Took a loan from the bank");
                    break;
                case LoanRepaid l:
                    Add(l.PlayerId, -l.Amount, MoneyKind.Loan, "Paid back a loan");
                    break;
                case TradeCompleted t:
                {
                    var o = t.Offer;
                    string give = EventDescriber.DescribeTrade(o, Space, Card);
                    string get = EventDescriber.DescribeTrade(o.Reversed(), Space, Card);
                    int net = o.GetCash - o.GiveCash;
                    Add(o.FromId, net, MoneyKind.Trade, $"Trade with {Name(o.ToId)}: {give}", o.ToId);
                    Add(o.ToId, -net, MoneyKind.Trade, $"Trade with {Name(o.FromId)}: {get}", o.FromId);
                    // Saves from before the fees were split only know the total, so it is put on the player who offered.
                    bool split = t.FromFees + t.ToFees == t.MortgageFees;
                    Add(o.FromId, -(split ? t.FromFees : t.MortgageFees), MoneyKind.Trade, "Mortgage fee for a property from the trade");
                    Add(o.ToId, -(split ? t.ToFees : 0), MoneyKind.Trade, "Mortgage fee for a property from the trade");
                    break;
                }
                case InsuranceBought i:
                    Move(i.PlayerId, i.ToId, i.Cost, MoneyKind.Hospital, "Bought an Insured marker" + (i.ToId is int seller ? $" from {Name(seller)}" : ""), $"Insured marker sold to {Name(i.PlayerId)}");
                    break;
                case HospitalBillPaid h:
                    Move(h.PlayerId, h.ToId, h.Amount, MoneyKind.Hospital, "Hospital bill" + (h.ToId is int medic ? $" to {Name(medic)}" : ""), $"Hospital bill from {Name(h.PlayerId)}");
                    break;
                case CasinoCoinFlip f:
                    Add(f.PlayerId, f.Net, MoneyKind.Casino, $"Casino coin flip: {(f.Net > 0 ? "won" : "lost")}");
                    break;
                case CasinoHighLow c:
                    Add(c.PlayerId, c.Net, MoneyKind.Casino, $"Casino bet on {(c.CalledHigh ? "High" : "Low")}: rolled {c.Total}, {(c.Net > 0 ? "won" : "lost")}");
                    break;
                case DuelWon d:
                    Move(d.LoserId, d.WinnerId, d.Stake, MoneyKind.Casino, $"Lost a Dice Duel to {Name(d.WinnerId)}", $"Won a Dice Duel against {Name(d.LoserId)}");
                    break;
                case CasinoHandPaid p:
                    Add(p.PlayerId, p.Net, MoneyKind.Casino, $"Casino Dice Poker: {DicePoker.Describe(p.Hand)}");
                    break;
                case ShowdownFolded f:
                    Move(f.OpponentId, f.ChallengerId, f.Amount, MoneyKind.Casino, $"Folded in a Showdown with {Name(f.ChallengerId)}", $"{Name(f.OpponentId)} folded in a Showdown");
                    break;
                case ShowdownWon w:
                    Move(w.LoserId, w.WinnerId, w.Stake, MoneyKind.Casino, $"Lost a Showdown to {Name(w.WinnerId)}", $"Won a Showdown against {Name(w.LoserId)}");
                    break;
                case MallCardBought b:
                    Add(b.PlayerId, -b.Price, MoneyKind.Mall, $"Bought {Card(b.CardId)} at the Mall");
                    break;
                case MallCardSold s:
                    Add(s.PlayerId, s.Price, MoneyKind.Mall, $"Sold {Card(s.CardId)} to the Mall");
                    break;
                case CashFromBank c:
                    Add(c.PlayerId, c.Amount, MoneyKind.Card, Card(c.CardId));
                    break;
                case CashToBank c:
                    Add(c.PlayerId, -c.Amount, MoneyKind.Card, Card(c.CardId));
                    break;
                case CardPaymentMade p:
                    Move(p.FromId, p.ToId, p.Amount, MoneyKind.Card, $"{Card(p.CardId)}: paid {To(p.ToId)}", $"{Card(p.CardId)}: paid by {Name(p.FromId)}");
                    break;
                case LotteryWon l:
                    foreach (int id in l.Entrants ?? [])
                        Add(id, -l.Stake, MoneyKind.Card, "Lottery ticket");
                    Add(l.WinnerId, l.Pot, MoneyKind.Card, "Won the Lottery pot");
                    break;
                case TileFeePaid f:
                    Move(f.FromId, f.ToId, f.Amount, MoneyKind.Tax, $"{Tile(f.TileId)} fee" + (f.ToId is int payee ? $" to {Name(payee)}" : ""), $"{Tile(f.TileId)} fee from {Name(f.FromId)}");
                    break;
                case TransitHopped h:
                    Add(h.PlayerId, -h.Cost, MoneyKind.Travel, $"Hopped onto the Transit Hub instead of paying rent for {Space(h.PropertyId)}");
                    break;
                case TransitRidden r:
                    Add(r.PlayerId, -r.Cost, MoneyKind.Travel, $"Transit Hub fare to {Space(r.RailroadId)}");
                    break;
                case ExpressLineUsed x:
                    Add(x.PlayerId, -x.Cost, MoneyKind.Travel, $"Express Line to {Space(x.RailroadId)}");
                    break;
                case PerkIncome p:
                    Add(p.PlayerId, p.Amount, MoneyKind.Perk, p.Perk);
                    break;
                case PerkPaid p:
                    Move(p.FromId, p.ToId, p.Amount, MoneyKind.Perk, $"{p.Perk} to {Name(p.ToId)}", $"{p.Perk} from {Name(p.FromId)}");
                    break;
                case CouncilResolved r:
                    foreach (var vote in r.Votes)
                        Add(vote.PlayerId, -vote.Lobby, MoneyKind.Council, $"Lobbied {vote.Choice.ToString().ToLowerInvariant()} on {Ordinance(r.OrdinanceId)}");
                    break;
                case VoteDealMade v:
                    Move(v.Deal.FromId, v.Deal.ToId, v.Deal.Amount, MoneyKind.Council,
                        $"Paid {Name(v.Deal.ToId)} to vote {v.Deal.Choice.ToString().ToLowerInvariant()}", $"Paid by {Name(v.Deal.FromId)} to vote {v.Deal.Choice.ToString().ToLowerInvariant()}");
                    break;
                case CouncilPayout c:
                    Add(c.PlayerId, c.Amount, MoneyKind.Council, Ordinance(c.OrdinanceId));
                    break;
                case CouncilPaymentMade c:
                    Add(c.PlayerId, -c.Amount, MoneyKind.Council, Ordinance(c.OrdinanceId));
                    break;
                case PlayerBankrupt b:
                    Add(b.PlayerId, b.BuildingsSoldFor, MoneyKind.Bankruptcy, "Bankrupt: buildings sold to the bank");
                    Add(b.PlayerId, -b.LoansPaidBack, MoneyKind.Bankruptcy, "Bankrupt: loans paid back to the bank");
                    Move(b.PlayerId, b.CreditorId, b.CashHandedOver, MoneyKind.Bankruptcy,
                        $"Bankrupt: last cash went to {To(b.CreditorId)}", $"{Name(b.PlayerId)} went bankrupt and handed over their cash");
                    break;
            }
        }
        return lines;
    }

    /// <summary>
    /// The history of one space: for a property, every purchase, trade, building change and
    /// mortgage; for any space, how often it was landed on and what was paid there.
    /// </summary>
    public static SpaceHistory ForSpace(PlayerView view, GameContent content, string spaceId)
    {
        string Name(int id) => view.Player(id).Name;
        string Space(string id) => content.Properties.TryGetValue(id, out var p) ? p.Name : view.Spaces.FirstOrDefault(s => s.Id == id)?.Name ?? id;
        string? tileId = view.Spaces.FirstOrDefault(s => s.Id == spaceId)?.TileId;

        var entries = new List<HistoryEntry>();
        Acquisition? acquired = null;
        int round = 0, landed = 0, rent = 0, rentTimes = 0, fees = 0;
        int? turn = null;
        void Add(HistoryKind kind, string text, int? who = null, int? amount = null) =>
            entries.Add(new HistoryEntry(round, turn, kind, text, who, amount));
        void Gained(int owner, HistoryKind how, int? price, string text)
        {
            Add(how, text, owner, price);
            acquired = new Acquisition(owner, round, how, price, text);
        }

        foreach (GameEvent e in view.Log)
        {
            switch (e)
            {
                case TurnStarted t:
                    round = t.Round;
                    turn = t.PlayerId;
                    break;
                case RoundEnded:
                    // What happens between rounds (the City Council) is nobody's turn.
                    turn = null;
                    break;
                case LandedOn l when l.SpaceId == spaceId:
                    landed++;
                    break;
                case AuctionWon w when w.PropertyId == spaceId:
                    Gained(w.PlayerId, HistoryKind.Bought, w.Price,
                        w.ListedPrice ? $"{Name(w.PlayerId)} bought it for its listed price of ${w.Price} (first lap)."
                        : w.DownPayment ? $"{Name(w.PlayerId)} won it at auction for ${w.Price} with a down payment (${w.PaidNow} now)."
                        : w.PaidToId is int seller ? $"{Name(w.PlayerId)} won it from {Name(seller)} at a forced auction for ${w.Price}."
                        : $"{Name(w.PlayerId)} won it at auction for ${w.Price}.");
                    break;
                case OwnerKeptProperty k when k.PropertyId == spaceId:
                    Add(HistoryKind.Other, $"A forced auction got no bids, so {Name(k.OwnerId)} kept it.", k.OwnerId);
                    break;
                case HostileTakeoverUsed h when h.PropertyId == spaceId:
                    Add(HistoryKind.Other, $"{Name(h.PlayerId)} used Hostile Takeover to force an auction on it.", h.PlayerId);
                    break;
                case DownPaymentPaid d when d.PropertyId == spaceId:
                    Add(HistoryKind.PaidOff, $"{Name(d.PlayerId)} paid off the ${d.Amount} balance.", d.PlayerId, d.Amount);
                    break;
                case PropertyForfeited f when f.PropertyId == spaceId:
                    Add(HistoryKind.Lost, $"{Name(f.PlayerId)} couldn't pay the balance, so it went back to the bank.", f.PlayerId);
                    acquired = null;
                    break;
                case TradeCompleted t when t.Offer.GiveProperties.Contains(spaceId) || t.Offer.GetProperties.Contains(spaceId):
                {
                    var o = t.Offer;
                    bool fromGave = o.GiveProperties.Contains(spaceId);
                    int giver = fromGave ? o.FromId : o.ToId, taker = fromGave ? o.ToId : o.FromId;
                    // What the new owner handed over for everything they got (cash and other properties).
                    int takerPaid = fromGave ? o.GetCash : o.GiveCash;
                    var takerGave = fromGave ? o.GetProperties : o.GiveProperties;
                    var parts = new List<string>();
                    if (takerPaid > 0) parts.Add($"${takerPaid}");
                    parts.AddRange(takerGave.Select(Space));
                    int others = (fromGave ? o.GiveProperties : o.GetProperties).Count - 1;
                    string text = $"{Name(giver)} traded it to {Name(taker)}"
                        + (parts.Count > 0 ? $" for {string.Join(" + ", parts)}" : " for nothing back")
                        + (others > 0 ? $" (along with {others} other propert{(others == 1 ? "y" : "ies")})." : ".");
                    // A price only makes sense when this one property was swapped for cash alone.
                    Gained(taker, HistoryKind.Traded, others == 0 && takerGave.Count == 0 ? takerPaid : null, text);
                    break;
                }
                case BuildingBuilt b when b.PropertyId == spaceId:
                    Add(HistoryKind.Built, $"{Name(b.PlayerId)} built: now {EventDescriber.LevelName(b.NewLevel)} (${b.Cost}).", b.PlayerId, b.Cost);
                    break;
                case BuildingSold b when b.PropertyId == spaceId:
                    Add(HistoryKind.Sold, $"{Name(b.PlayerId)} sold a building: now {EventDescriber.LevelName(b.NewLevel)} (got ${b.Refund}).", b.PlayerId, b.Refund);
                    break;
                case BuildingBurned b when b.PropertyId == spaceId:
                    Add(HistoryKind.Burned, $"Fire! It lost a level: now {EventDescriber.LevelName(b.NewLevel)}.", b.PlayerId);
                    break;
                case PropertyMortgaged m when m.PropertyId == spaceId:
                    Add(HistoryKind.Mortgaged, $"{Name(m.PlayerId)} mortgaged it for ${m.Amount}.", m.PlayerId, m.Amount);
                    break;
                case PropertyUnmortgaged u when u.PropertyId == spaceId:
                    Add(HistoryKind.Unmortgaged, $"{Name(u.PlayerId)} paid ${u.Cost} to unmortgage it.", u.PlayerId, u.Cost);
                    break;
                case PlayerBankrupt b when b.CreditorId is int creditor && b.PropertiesHandedOver.Contains(spaceId):
                    Gained(creditor, HistoryKind.Inherited, null, $"{Name(b.PlayerId)} went bankrupt and it passed to {Name(creditor)}.");
                    break;
                case PlayerBankrupt b when acquired?.OwnerId == b.PlayerId && content.Properties.ContainsKey(spaceId):
                    Add(HistoryKind.Lost, $"{Name(b.PlayerId)} went bankrupt and it went back to the bank.", b.PlayerId);
                    acquired = null;
                    break;
                case RentPaid r when r.PropertyId == spaceId:
                    rent += r.Amount;
                    rentTimes++;
                    break;
                case RentBoostStarted b when b.PropertyId == spaceId:
                    Add(HistoryKind.Other, $"Grand Opening: rent ×{b.Multiplier} until {Name(b.PlayerId)}'s next turn.", b.PlayerId);
                    break;
                case TaxPaid t when t.SpaceId == spaceId:
                    fees += t.Amount;
                    break;
                case TileRevealed t when t.SpaceId == spaceId:
                    Add(HistoryKind.Revealed, $"{Name(t.PlayerId)} landed here and revealed the {content.GovTile(t.TileId).Name}.", t.PlayerId);
                    break;
                case TileRevealedByCouncil t when t.SpaceId == spaceId:
                    Add(HistoryKind.Revealed, $"Public Works revealed the {content.GovTile(t.TileId).Name}.");
                    break;
                case TileFeePaid f when tileId != null && f.TileId == tileId:
                    fees += f.Amount;
                    break;
            }
        }
        return new SpaceHistory(entries, acquired, landed, rent, rentTimes, fees);
    }
}
