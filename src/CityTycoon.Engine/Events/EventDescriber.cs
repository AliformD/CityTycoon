using CityTycoon.Engine.Content;
using CityTycoon.Engine.Rules;
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
        string GroupName(string id) => content.Groups[id].Name;
        static string Tokens(int n) => n == 1 ? "1 Momentum token" : $"{n} Momentum tokens";
        static string Faces(IReadOnlyList<int> dice) => string.Join(" ", dice);
        string Card(string id) => content.Cards.TryGetValue(id, out var c) ? c.Name : id;
        string Tile(string id) => content.GovTile(id).Name;
        string District(string id) => content.Board.Districts.First(d => d.Id == id).Name;
        static string Spaces(int n) => n == 1 ? "1 space" : $"{n} spaces";

        return e switch
        {
            GameStarted g => $"New game (seed {g.Seed}). Turn order: {string.Join(", ", g.TurnOrder.Select(Name))}.",
            TurnStarted t => $"Round {t.Round}: {Name(t.PlayerId)}'s turn.",
            DiceRolled { IsOneDie: true } d => $"{Name(d.PlayerId)} rolled 1 die: {d.Die1}.",
            DiceRolled d => $"{Name(d.PlayerId)} rolled {d.Die1} + {d.Die2} = {d.Total}{(d.IsDoubles ? " (doubles!)" : "")}.",
            PlayerMoved m => $"{Name(m.PlayerId)} moved to {SpaceAt(m.To)}.",
            PassedGo g => $"{Name(g.PlayerId)} passed GO and collected ${g.Salary}.",
            LandedOn _ => "",
            AuctionStarted { FixedPrice: true } a => $"{Space(a.PropertyId)} is offered at its listed price of ${a.MinBid} (first lap).",
            AuctionStarted { Open: true } a => $"Open auction for {Space(a.PropertyId)} (opening bid ${a.MinBid}). Players take turns to raise or drop out.",
            AuctionStarted a => $"Silent auction for {Space(a.PropertyId)} (minimum bid ${a.MinBid}). Everyone bids in secret at the same time.",
            BidRaised r => $"{Name(r.PlayerId)} bid ${r.Amount}{(r.DownPayment ? " with a down payment" : "")}.",
            AuctionPassed p => $"{Name(p.PlayerId)} dropped out of the auction for {Space(p.PropertyId)}.",
            DiceTieWon t => $"The dice tied, and {Name(t.PlayerId)} wins the tie ({t.Source}).",
            BidsRevealed b => "Bids: " + string.Join(", ", b.Bids.Select(x => $"{Name(x.PlayerId)} {(x.Amount is int amount ? "$" + amount : "passed")}")) + ".",
            AuctionWon w => w.DownPayment
                ? $"{Name(w.PlayerId)} won {Space(w.PropertyId)} for ${w.Price} with a down payment: ${w.PaidNow} now, ${w.Price - w.PaidNow} at their next GO."
                : w.PaidToId is int owner ? $"{Name(w.PlayerId)} won {Space(w.PropertyId)} for ${w.Price}, paid to {Name(owner)}."
                : $"{Name(w.PlayerId)} won {Space(w.PropertyId)} for ${w.Price}.",
            OwnerKeptProperty k => $"Nobody bid, so {Name(k.OwnerId)} keeps {Space(k.PropertyId)}.",
            AuctionNoBids n => $"Nobody bid on {Space(n.PropertyId)}. It stays with the bank.",
            RentPaid r => $"{Name(r.FromId)} paid ${r.Amount} rent to {Name(r.ToId)} for {Space(r.PropertyId)}.",
            TaxPaid t => $"{Name(t.PlayerId)} paid ${t.Amount} {Space(t.SpaceId)}.",
            BailPaid { Amount: 0 } b => $"{Name(b.PlayerId)} didn't have to pay any bail.",
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
            TradeProposed t => $"{Name(t.Offer.FromId)} offered {Name(t.Offer.ToId)} a trade: {DescribeTrade(t.Offer, Space, Card)}.",
            TradeCompleted t => $"{Name(t.Offer.ToId)} accepted the trade from {Name(t.Offer.FromId)}."
                + (t.MortgageFees > 0 ? $" Mortgage fees paid: ${t.MortgageFees}." : ""),
            TradeDeclined { Why: string why } t => $"{Name(t.Offer.ToId)} declined {Name(t.Offer.FromId)}'s trade: \"{why}\"",
            TradeDeclined t => $"The trade between {Name(t.Offer.FromId)} and {Name(t.Offer.ToId)} didn't happen ({t.Reason}).",
            TradeCountered t => $"{Name(t.Counter.FromId)} turned down {Name(t.Counter.ToId)}'s offer and sent a counter-offer: {DescribeTrade(t.Counter, Space, Card)}."
                + (t.Why != null ? $" \"{t.Why}\"" : ""),
            MomentumGained m => $"{Name(m.PlayerId)} gained {Tokens(m.Amount)} ({m.Source}).",
            PrecisionRolled p => $"{Name(p.PlayerId)} spent {Tokens(p.TokensSpent)} on a Precision Roll: {string.Join(", ", p.Dice)}.",
            Nudged n => $"{Name(n.PlayerId)} spent {Tokens(n.TokensSpent)} to move {Math.Abs(n.Spaces)} space{(Math.Abs(n.Spaces) == 1 ? "" : "s")} {(n.Spaces > 0 ? "further" : "less")}.",
            InsuranceBought i => $"{Name(i.PlayerId)} bought an Insured marker for ${i.Cost}" + (i.ToId is int seller ? $", paid to {Name(seller)}." : "."),
            InsuranceUsed i => $"{Name(i.PlayerId)}'s Insured marker was used up.",
            SentToHospital h => $"{Name(h.PlayerId)} went to the Hospital ({h.Reason}).",
            HospitalBillPaid h => $"{Name(h.PlayerId)} paid a ${h.Amount} Hospital bill" + (h.ToId is int medic ? $" to {Name(medic)}" : "") + " and rolls 1 die next turn.",
            CasinoCoinFlip f => f.Net > 0 ? $"{Name(f.PlayerId)} won ${f.Net} on the Casino coin flip." : $"{Name(f.PlayerId)} lost ${-f.Net} on the Casino coin flip.",
            CasinoHighLow c => $"{Name(c.PlayerId)} bet ${c.Bet} on {(c.CalledHigh ? "High" : "Low")} and rolled {c.Die1} + {c.Die2} = {c.Total}: "
                + (c.Net > 0 ? $"won ${c.Net}!" : $"lost ${-c.Net}."),
            DuelChallenged d => $"{Name(d.ChallengerId)} challenged {Name(d.OpponentId)} to a ${d.Stake} Dice Duel.",
            DuelDeclined d => $"{Name(d.OpponentId)} said no to the Dice Duel.",
            DuelRolled d => $"Duel: {Name(d.ChallengerId)} rolled {d.ChallengerTotal}, {Name(d.OpponentId)} rolled {d.OpponentTotal}"
                + (d.ChallengerTotal == d.OpponentTotal ? " (a tie, roll again)." : "."),
            DuelWon d => $"{Name(d.WinnerId)} won the duel and takes ${d.Stake} from {Name(d.LoserId)}.",
            PokerDiceRolled r => r.Kept == null
                ? $"{Name(r.PlayerId)} rolled {Faces(r.Dice)}: {DicePoker.Describe(DicePoker.Score(r.Dice))}."
                : $"{Name(r.PlayerId)} kept {(r.Kept.Count == 0 ? "nothing" : Faces(r.Kept.Select(i => r.Dice[i]).ToList()))} and rolled again: {Faces(r.Dice)}, {DicePoker.Describe(DicePoker.Score(r.Dice))}.",
            CasinoHandPaid p => $"{Name(p.PlayerId)} made {DicePoker.Describe(p.Hand)} at the Casino"
                + (p.Net > 0 ? $" and won ${p.Net}!" : p.Net < 0 ? $" and lost the ${-p.Net} they put on it." : p.Stake > 0 ? " and got their money back." : ", which pays nothing."),
            ShowdownCalled s => $"{Name(s.ChallengerId)} calls {Name(s.OpponentId)} to a ${s.Stake} Showdown with {DicePoker.Describe(s.Hand)}.",
            ShowdownFolded f => $"{Name(f.OpponentId)} folded and paid {Name(f.ChallengerId)} ${f.Amount}.",
            ShowdownWon w => $"{Name(w.WinnerId)}'s {DicePoker.Describe(w.WinnerHand)} beats {DicePoker.Describe(w.LoserHand)}: {Name(w.WinnerId)} takes ${w.Stake} from {Name(w.LoserId)}.",
            ShowdownCalledOff o => $"{Name(o.ChallengerId)} decided not to call {Name(o.OpponentId)} to a Showdown after all.",
            ShowdownTied t => $"The Showdown is an exact tie ({DicePoker.Describe(t.Hand)}), so nobody pays.",
            StadiumChosen s => $"{Name(s.PlayerId)} filled the Stadium: {GroupName(s.GroupId)} rent is doubled until their next turn.",
            StadiumEnded s => $"The Stadium boost on {GroupName(s.GroupId)} is over.",
            OpportunityDrawn o => $"{Name(o.PlayerId)} drew an Opportunity card ({o.Source}).",
            EventDrawn d => $"{Name(d.PlayerId)} drew the Event card \"{Card(d.CardId)}\": {content.Card(d.CardId).Text}",
            NoCardToDraw n => $"There are no {(n.Deck == CardDeck.Opportunity ? "Opportunity" : "Event")} cards left to draw.",
            DeckReshuffled r => $"The {(r.Deck == CardDeck.Opportunity ? "Opportunity" : "Event")} discard pile was shuffled into a new deck ({r.Cards} cards).",
            CardPlayed c => DescribePlay(c, view, content),
            CardCancelled c => $"{Name(c.CancelledById)} cancelled {Name(c.PlayerId)}'s {Card(c.CardId)} with Insurance.",
            CardDiscarded d => $"{Name(d.PlayerId)} discarded {Card(d.CardId)} (hand limit).",
            MallCardBought b => $"{Name(b.PlayerId)} bought {Card(b.CardId)} at the Mall for ${b.Price}.",
            MallCardSold s => $"{Name(s.PlayerId)} sold {Card(s.CardId)} to the Mall for ${s.Price}.",
            CashFromBank c => $"{Name(c.PlayerId)} collected ${c.Amount} ({Card(c.CardId)}).",
            CashToBank c => $"{Name(c.PlayerId)} paid ${c.Amount} to the bank ({Card(c.CardId)}).",
            CardPaymentMade p => $"{Name(p.FromId)} paid ${p.Amount} to {(p.ToId is int to ? Name(to) : "the bank")} ({Card(p.CardId)}).",
            RentReduced r => $"Rent Strike! {Name(r.PlayerId)} pays ${r.To} instead of ${r.From} for {Space(r.PropertyId)}.",
            Detoured d => $"{Name(d.ByPlayerId)}'s Detour {(d.CancelledNudge ? "cancels the Nudge and " : "")}moves {Name(d.MoverId)} {Spaces(Math.Abs(d.Spaces))} {(d.Spaces > 0 ? "further" : "back")}.",
            BuildingBurned b => $"Fire! {Space(b.PropertyId)} lost a level (now {LevelName(b.NewLevel)}).",
            FireAvoided f => $"Fire! But {Name(f.PlayerId)} is safe ({f.Reason}).",
            LotteryRolled l => "Lottery rolls: " + string.Join(", ", l.Rolls.Select(r => $"{Name(r.PlayerId)} {r.Die}")) + ".",
            LotteryWon l => $"{Name(l.WinnerId)} won the ${l.Pot} Lottery pot!",
            RoadWorkStarted r => $"{Name(r.PlayerId)} rolls only 1 die next turn because of Road Work.",
            RoadWorkCleared r => $"{Name(r.PlayerId)} spent {Tokens(r.TokensSpent)} to get past the Road Work.",
            RentBoostStarted b => $"{Space(b.PropertyId)}'s rent is ×{b.Multiplier} until {Name(b.PlayerId)}'s next turn.",
            RentBoostEnded b => $"The Grand Opening on {Space(b.PropertyId)} is over.",
            TilePeeked t => $"{Name(t.PlayerId)} secretly peeked at a hidden Government tile.",
            TileSwapped t => $"{Name(t.PlayerId)} swapped a hidden Government tile for one that wasn't in play. Nobody else knows what either one is.",
            TileFeeWaived t => $"{Name(t.PlayerId)} paid no {content.GovTile(t.TileId).Name} fee (saved ${t.Amount}).",
            EventsArranged a => $"{Name(a.PlayerId)} looked at the top {a.Count} Event cards and put them back.",
            RecoverySkipped r => $"{Name(r.PlayerId)} skips the Hospital recovery and rolls 2 dice.",
            TileRevealed t => $"{Name(t.PlayerId)} revealed the Government tile: it's the {Tile(t.TileId)}!",
            TileFeePaid f => $"{Name(f.FromId)} paid ${f.Amount} to {(f.ToId is int to ? Name(to) : "the bank")} ({Tile(f.TileId)}).",
            CatchUpPaid c => $"{Name(c.PlayerId)} has the lowest net worth, so they collected a ${c.Amount} catch-up bonus.",
            CouncilStarted { Emergency: true } c => $"Emergency City Council session! {Name(c.SetterId)} sets the agenda.",
            CouncilStarted c => $"City Council session. {Name(c.SetterId)} has the lowest net worth, so they set the agenda.",
            CouncilCancelled c => $"The City Council session was called off: {c.Reason}",
            OrdinanceProposed o => $"{Name(o.SetterId)} put {content.Ordinance(o.OrdinanceId).Name}{(o.DistrictId is string d ? " for " + District(d) : "")} to a vote.",
            CouncilResolved r => DescribeCouncil(r, Name, District),
            OrdinanceEnded o => $"{content.Ordinance(o.OrdinanceId).Name}{(o.DistrictId is string d ? " (" + District(d) + ")" : "")} has ended.",
            ZoningChanged z => $"{District(z.DistrictId)} is now zoned {ZoneName(z.To)} (was {ZoneName(z.From)}).",
            PermitsAdded p => $"The city added {(p.Hotels > 0 ? p.Hotels + " hotel permits" : "")}{(p.Hotels > 0 && p.Skyscrapers > 0 ? " and " : "")}{(p.Skyscrapers > 0 ? p.Skyscrapers + " skyscraper permits" : "")}.",
            CouncilPayout c => $"{Name(c.PlayerId)} collected ${c.Amount} ({content.Ordinance(c.OrdinanceId).Name}).",
            CouncilPaymentMade c => $"{Name(c.PlayerId)} paid ${c.Amount} ({content.Ordinance(c.OrdinanceId).Name}).",
            TileRevealedByCouncil t => $"Public Works revealed the {Tile(t.TileId)}.",
            TransitHopped h => $"{Name(h.PlayerId)} paid ${h.Cost} and hopped onto the Transit Hub instead of paying rent for {Space(h.PropertyId)}.",
            CharacterChosen c => $"{Name(c.PlayerId)} is {content.Character(c.CharacterId).Name}: {content.Character(c.CharacterId).Text}",
            ExpressLineUsed x => $"{Name(x.PlayerId)} paid ${x.Cost} for the Express Line and jumped to {Space(x.RailroadId)}.",
            HostileTakeoverUsed h => $"{Name(h.PlayerId)} used Hostile Takeover and forced an auction on {Space(h.PropertyId)}.",
            PerkIncome p => $"{Name(p.PlayerId)} collected ${p.Amount} ({p.Perk}).",
            PerkPaid p => $"{Name(p.FromId)} paid ${p.Amount} to {Name(p.ToId)} ({p.Perk}).",
            CasinoRerolled r => $"{Name(r.PlayerId)} used their Hustler reroll on the {r.What}.",
            MoveRerolled r => r.Die is int die
                ? $"{Name(r.PlayerId)} didn't like {(r.Die2 == 0 ? $"a {r.Die1}" : $"{r.Die1} + {r.Die2}")} and used their Hustler reroll on the {(die == 0 ? r.Die1 : r.Die2)} (once per lap)."
                : $"{Name(r.PlayerId)} didn't like {(r.Die2 == 0 ? $"a {r.Die1}" : $"{r.Die1} + {r.Die2}")} and used their Hustler reroll (once per lap).",
            TableTalk t => $"{Name(t.SpeakerId)}: \u201C{t.Text}\u201D",
            VoteDealMade v => $"{Name(v.Deal.ToId)} took ${v.Deal.Amount} from {Name(v.Deal.FromId)} to vote {v.Deal.Choice.ToString().ToUpperInvariant()}.",
            VoteDealDeclined v => $"{Name(v.Deal.ToId)} turned down ${v.Deal.Amount} from {Name(v.Deal.FromId)} to vote {v.Deal.Choice.ToString().ToUpperInvariant()}.",
            VoteContractBid v => $"{Name(v.Deal.FromId)} offers {Name(v.Deal.ToId)} ${v.Deal.Amount} to vote {v.Deal.Choice.ToString().ToUpperInvariant()}.",
            CouncilReversed r => $"{Name(r.PlayerId)} reversed the City Council result as the City Planner: {content.Ordinance(r.OrdinanceId).Name} now {(r.NowPassed ? "passes" : "fails")}.",
            CardKept k => $"{Name(k.PlayerId)} drew two Opportunity cards and kept one.",
            HospitalShrugged h => $"{Name(h.PlayerId)} shrugged off the Hospital: no bill, and they roll normally next turn.",
            BidEdgeWon b => $"{Name(b.PlayerId)}'s edge won {Space(b.PropertyId)} with a bid of ${b.Bid}, below the top bid of ${b.TopBid}.",
            TransitRidden r => $"{Name(r.PlayerId)} paid ${r.Cost} to ride the Transit Hub to {Space(r.RailroadId)}.",
            RoundEnded r => $"Round {r.Round} is over.",
            GameEnded g => $"Game over: {g.Reason} Winner: {string.Join(" and ", g.WinnerIds.Select(Name))}.",
            _ => e.ToString(),
        };
    }

    /// <summary>"Ann played Audit against Bo." with the details that matter for each card.</summary>
    private static string DescribePlay(CardPlayed c, PlayerView view, GameContent content)
    {
        CardDefinition card = content.Card(c.CardId);
        string text = $"{view.Player(c.PlayerId).Name} played {card.Name}";
        if (card.EffectType == "Shortcut")
            return text + $" to move {c.Number} space{(c.Number == 1 ? "" : "s")}.";
        if (c.TargetPlayerId is int target)
            text += $" against {view.Player(target).Name}";
        if (c.TargetSpaceId is string space && card.Target != CardTarget.HiddenTile)
            text += $" on {(content.Properties.TryGetValue(space, out var p) ? p.Name : space)}";
        return text + ".";
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

    /// <summary>"$100 + Mill Street + Audit card for Platinum Row", read from the proposer's side.</summary>
    public static string DescribeTrade(Actions.TradeOffer offer, Func<string, string> spaceName, Func<string, string>? cardName = null)
    {
        string Side(int cash, IReadOnlyList<string> props, IReadOnlyList<string> cards)
        {
            var parts = new List<string>();
            if (cash > 0) parts.Add($"${cash}");
            parts.AddRange(props.Select(spaceName));
            parts.AddRange(cards.Select(id => $"{cardName?.Invoke(id) ?? id} card"));
            return parts.Count == 0 ? "nothing" : string.Join(" + ", parts);
        }
        return $"{Side(offer.GiveCash, offer.GiveProperties, offer.GiveCards)} for {Side(offer.GetCash, offer.GetProperties, offer.GetCards)}";
    }

    public static string ZoneName(ZoningLevel level) => level == ZoningLevel.HighRise ? "High-Rise" : level.ToString();

    private static string DescribeCouncil(CouncilResolved r, Func<int, string> name, Func<string, string> district)
    {
        string votes = string.Join(", ", r.Votes.Select(v => v.Choice == Actions.VoteChoice.Abstain
            ? $"{name(v.PlayerId)} abstained"
            : $"{name(v.PlayerId)} {v.Choice.ToString().ToLowerInvariant()}{(v.Contract > 0 ? $" (contract ${v.Contract})" : "")}{(v.Lobby > 0 ? $" (lobbied ${v.Lobby})" : "")}{(v.UsedLobbyist ? " + Lobbyist" : "")}"));
        int total = r.YesInfluence + r.NoInfluence;
        double chance = total == 0 ? 0 : 100.0 * r.YesInfluence / total;
        return $"Votes: {votes}. Yes {r.YesInfluence} vs No {r.NoInfluence}: {chance:0.#}% chance. The roll was {r.Roll + 1} of {total}: the ordinance {(r.Passed ? "PASSED" : "FAILED")}.";
    }
}
