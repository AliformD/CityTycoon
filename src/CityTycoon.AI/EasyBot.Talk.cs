using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI;

/// <summary>How a bot feels about another player right now (decision #160).</summary>
public enum Mood { Friendly, Neutral, Wary, Hostile }

/// <summary>
/// Table talk and temperament (decision #160). Every bot watches the trades other players make, sizes them up
/// from what everyone can see (face value, and whether a trade finishes a color group), and speaks up when one
/// looks lopsided: a warning before it is answered, a scolding or a threat after. Each bot also keeps a temperament
/// toward every other player, worked out from the game log: trades with it warm it up; rent paid, cards played
/// against it, rivals' monopolies handed over in lopsided trades and rude remarks cool it down. A bot asks a
/// bigger margin in trades with players it dislikes and a smaller one with friends.
/// The words never use the bot's random numbers, so talking doesn't change its other choices.
/// </summary>
public partial class EasyBot : ITableTalker
{
    // ----- Temperament -----

    private int moodScanned;
    private Dictionary<int, double> moodScores = [];

    /// <summary>
    /// How the bot feels about a player: the sum of everything they did to it (and to the table), from the log.
    /// It reads only the events added since it last looked, so it stays quick in long games.
    /// </summary>
    public double Temperament(PlayerView view, int playerId)
    {
        if (view.Log.Count < moodScanned)
        {
            moodScores = [];
            moodScanned = 0;
        }
        for (; moodScanned < view.Log.Count; moodScanned++)
            ScoreEvent(view, view.Log[moodScanned]);
        return moodScores.GetValueOrDefault(playerId);
    }

    public Mood MoodToward(PlayerView view, int playerId) => Temperament(view, playerId) switch
    {
        >= FriendlyAt => Mood.Friendly,
        <= HostileAt => Mood.Hostile,
        <= WaryAt => Mood.Wary,
        _ => Mood.Neutral,
    };

    private const double FriendlyAt = 2, WaryAt = -2, HostileAt = -5;

    private void ScoreEvent(PlayerView view, GameEvent e)
    {
        int me = view.ViewerId;
        // An Aggressive bot holds a grudge longer; a Reserved one lets more go.
        double grudge = Personality switch
        {
            BotPersonality.Aggressive => 1.3,
            BotPersonality.Reserved => 0.8,
            _ => 1.0,
        };
        void Add(int id, double amount)
        {
            if (id != me)
                moodScores[id] = moodScores.GetValueOrDefault(id) + (amount < 0 ? amount * grudge : amount);
        }

        switch (e)
        {
            case TradeCompleted t when t.Offer.FromId == me || t.Offer.ToId == me:
                Add(t.Offer.FromId == me ? t.Offer.ToId : t.Offer.FromId, 2);
                break;
            case TradeCompleted t:
                // A trade between two others that hands one of them a color group cheaply hurts everyone else.
                if (Judge(view, t.Offer) is { Loser: int loser, Winner: int winner, SetName: not null })
                {
                    Add(loser, -1.5);
                    Add(winner, -1);
                }
                break;
            case TradeDeclined d when d.Offer.FromId == me && d.Reason == "declined":
                Add(d.Offer.ToId, -0.5);
                break;
            case RentPaid r when r.FromId == me:
                Add(r.ToId, -r.Amount / 500.0);
                break;
            case CardPlayed c when c.TargetPlayerId == me:
                Add(c.PlayerId, -1.5);
                break;
            case ShowdownCalled d when d.OpponentId == me:
                Add(d.ChallengerId, -0.5);
                break;
            case ShowdownWon d when d.LoserId == me:
                Add(d.WinnerId, -1);
                break;
            case VoteDealMade v when v.Deal.FromId == me || v.Deal.ToId == me:
                Add(v.Deal.FromId == me ? v.Deal.ToId : v.Deal.FromId, 1);
                break;
            case TableTalk talk when talk.AboutId == me:
                Add(talk.SpeakerId, talk.Tone == TalkTone.Friendly ? 0.5 : talk.Tone == TalkTone.Warning ? 0 : -1);
                break;
        }
    }

    /// <summary>How much the bot's trade margin grows (or shrinks) with how it feels about the other player.</summary>
    private double MoodMargin(PlayerView view, int otherId) => MoodToward(view, otherId) switch
    {
        Mood.Hostile => 2.0,
        Mood.Wary => 1.3,
        Mood.Friendly => 0.8,
        _ => 1.0,
    };

    // ----- Sizing up other players' trades -----

    /// <summary>
    /// An onlooker's view of a trade: who comes out behind, and the color group it hands over, if any.
    /// Uses only face values and group ownership, which everyone can see (no one's private sums).
    /// </summary>
    private sealed record Verdict(int? Loser, int? Winner, string? SetName, double Gap);

    private Verdict Judge(PlayerView view, TradeOffer offer)
    {
        double PropertyFace(IEnumerable<string> ids) => ids.Sum(id => content.Property(id).AssessedValue);
        // What the proposer gets minus what it gives, at face value.
        double fromGets = offer.GetCash + PropertyFace(offer.GetProperties) + offer.GetCards.Count * 40;
        double fromGives = offer.GiveCash + PropertyFace(offer.GiveProperties) + offer.GiveCards.Count * 40;
        double total = fromGets + fromGives;

        string? fromSet = CompletedGroup(view, offer.FromId, offer.GetProperties, offer.GiveProperties);
        string? toSet = CompletedGroup(view, offer.ToId, offer.GiveProperties, offer.GetProperties);
        // A finished group is worth a lot more than its face value: count it as worth its face value again.
        double SetBonus(string? group) => group == null ? 0 : content.GroupMembers(group).Sum(id => content.Property(id).AssessedValue);
        double fromNet = fromGets - fromGives + SetBonus(fromSet) - SetBonus(toSet);

        if (Math.Abs(fromNet) < Math.Max(LopsidedFloor, total * LopsidedShare))
            return new Verdict(null, null, null, Math.Abs(fromNet));
        bool fromWins = fromNet > 0;
        string? set = fromWins ? fromSet : toSet;
        return new Verdict(fromWins ? offer.ToId : offer.FromId, fromWins ? offer.FromId : offer.ToId,
            set == null ? null : content.Groups[set].Name, Math.Abs(fromNet));
    }

    private const double LopsidedFloor = 80, LopsidedShare = 0.35;

    /// <summary>The color group this player would own whole after getting <paramref name="gets"/> and giving <paramref name="gives"/>, if any.</summary>
    private string? CompletedGroup(PlayerView view, int playerId, IReadOnlyList<string> gets, IReadOnlyList<string> gives)
    {
        foreach (string id in gets)
        {
            if (content.Property(id).Group is not string group)
                continue;
            bool whole = content.GroupMembers(group).All(m =>
                gets.Contains(m) || (view.Properties[m].OwnerId == playerId && !gives.Contains(m)));
            if (whole)
                return group;
        }
        return null;
    }

    // ----- Talking -----

    /// <summary>
    /// What the bot says about what just happened. One remark per event at most, and only one bot speaks up
    /// about any one trade (the first to notice), so the table doesn't pile on.
    /// </summary>
    public IReadOnlyList<Remark> Talk(IReadOnlyList<GameEvent> newEvents, int fromLogIndex, Func<PlayerView> makeView)
    {
        if (!newEvents.Any(e => e is TradeProposed or TradeCompleted || e is RentPaid { Amount: >= BigRent }))
            return [];
        return Talk(makeView(), fromLogIndex);
    }

    /// <summary>What the bot says about the log from <paramref name="fromLogIndex"/> on.</summary>
    public IReadOnlyList<Remark> Talk(PlayerView view, int fromLogIndex)
    {
        // Size up a finished trade right away, while the board still looks the way the trade left it.
        Temperament(view, view.ViewerId);
        var remarks = new List<Remark>();
        int me = view.ViewerId;
        for (int i = Math.Max(0, fromLogIndex); i < view.Log.Count && remarks.Count < 2; i++)
        {
            Remark? remark = view.Log[i] switch
            {
                TradeProposed p when MySayAbout(view, p.Offer, i) => OnProposed(view, p.Offer, i),
                TradeCompleted t when MySayAbout(view, t.Offer, i) => OnCompleted(view, t.Offer, i),
                TradeCompleted t => OnMyTrade(view, t.Offer, i),
                RentPaid r when r.ToId == me && r.Amount >= BigRent && MoodToward(view, r.FromId) == Mood.Hostile =>
                    new Remark(Pick(i, $"Thanks for the donation, {Name(view, r.FromId)}.", $"Pay up, {Name(view, r.FromId)}. Every dollar."), r.FromId, TalkTone.Sassy),
                RentPaid r when r.FromId == me && r.Amount >= BigRent && MoodToward(view, r.ToId) == Mood.Hostile =>
                    new Remark(Pick(i, $"Enjoy it while it lasts, {Name(view, r.ToId)}.", $"I'll remember that one, {Name(view, r.ToId)}."), r.ToId, TalkTone.Threat),
                _ => null,
            };
            if (remark != null && !SaidThisTurn(view, remark.AboutId))
                remarks.Add(remark);
        }
        return remarks;
    }

    private const int BigRent = 300;

    private static string Name(PlayerView view, int id) => view.Player(id).Name;

    /// <summary>True when the bot is watching a trade between two other players.</summary>
    private static bool Onlooker(PlayerView view, TradeOffer offer) => offer.FromId != view.ViewerId && offer.ToId != view.ViewerId;

    /// <summary>
    /// True when it is this bot's turn to speak up about a trade between two others. The onlookers take turns
    /// (by the event's place in the log), so different voices comment; a bot that dislikes the winner may
    /// always speak. Nobody does if someone already has.
    /// </summary>
    private bool MySayAbout(PlayerView view, TradeOffer offer, int eventIndex)
    {
        if (!Onlooker(view, offer) || AlreadyDiscussed(view, eventIndex))
            return false;
        var onlookers = view.Players.Where(p => !p.IsBankrupt && p.Id != offer.FromId && p.Id != offer.ToId).Select(p => p.Id).ToList();
        if (onlookers[eventIndex % onlookers.Count] == view.ViewerId)
            return true;
        return Judge(view, offer) is { Winner: int winner, SetName: not null } && MoodToward(view, winner) is Mood.Hostile or Mood.Wary;
    }

    /// <summary>True if the bot already said something about this player this turn: once is enough.</summary>
    private static bool SaidThisTurn(PlayerView view, int? aboutId)
    {
        for (int i = view.Log.Count - 1; i >= 0 && view.Log[i] is not TurnStarted; i--)
            if (view.Log[i] is TableTalk t && t.SpeakerId == view.ViewerId && t.AboutId == aboutId)
                return true;
        return false;
    }

    /// <summary>True if someone has already spoken up since this event, so the bot keeps quiet.</summary>
    private static bool AlreadyDiscussed(PlayerView view, int eventIndex)
    {
        for (int i = eventIndex + 1; i < view.Log.Count; i++)
            if (view.Log[i] is TableTalk)
                return true;
        return false;
    }

    /// <summary>A line chosen from the event's place in the log and the speaker, not from the bot's random numbers.</summary>
    private string Pick(int logIndex, params string[] lines) => lines[(int)((uint)(logIndex * 31 + ViewerSalt) % (uint)lines.Length)];

    private int ViewerSalt => (int)(Personality) * 7 + 3;

    /// <summary>Before the answer: warn the player about to take a bad deal, or tease the one offering a gift.</summary>
    private Remark? OnProposed(PlayerView view, TradeOffer offer, int i)
    {
        if (Judge(view, offer) is not { Loser: int loser, Winner: int winner } verdict)
            return null;
        string l = Name(view, loser), w = Name(view, winner);
        if (loser == offer.ToId)
        {
            return verdict.SetName != null
                ? new Remark(Pick(i, $"Don't take that, {l}. It hands {w} the {verdict.SetName} set.", $"{l}, read that twice. That's the {verdict.SetName} group for {w}.",
                    $"Hold on, {l}. {w} gets the whole {verdict.SetName} set out of that, and we all pay for it.", $"{l}, you're not seriously thinking about giving {w} the {verdict.SetName} group?"), loser, TalkTone.Warning)
                : new Remark(Pick(i, $"{l}, that's a lowball. Ask for more.", $"Careful, {l}. {w} is getting the better end of that.", $"{l}, I'd want a lot more than that."), loser, TalkTone.Warning);
        }
        // The proposer is giving it away.
        return new Remark(Pick(i, $"Feeling generous, {l}?", $"{l}, are you sure you meant to offer that?", $"Wow, {l}. Can I get a deal like that too?"), loser, TalkTone.Sassy);
    }

    /// <summary>After a lopsided trade between two others: scold the one who lost, and threaten the winner if the bot dislikes them.</summary>
    private Remark? OnCompleted(PlayerView view, TradeOffer offer, int i)
    {
        if (Judge(view, offer) is not { Loser: int loser, Winner: int winner } verdict)
            return null;
        string l = Name(view, loser), w = Name(view, winner);
        Mood towardWinner = MoodToward(view, winner);
        if (verdict.SetName != null && towardWinner is Mood.Hostile or Mood.Wary)
            return new Remark(Pick(i, $"Enjoy the {verdict.SetName} set while you can, {w}. I'm coming for it.", $"Fine, {w}. Just don't expect any favors from me.", $"Noted, {w}. I won't forget who built that monopoly."), winner, TalkTone.Threat);
        if (verdict.SetName != null)
            return new Remark(Pick(i, $"Thanks a lot, {l}. You just handed {w} the {verdict.SetName} group.", $"{l}, you know we all pay for that now, right?", $"Great move, {l}. Now {w} owns the {verdict.SetName} set and the rest of us pay."), loser, TalkTone.Scold);
        return new Remark(Pick(i, $"{w} robbed you, {l}.", $"Oof. {l}, that was a steal for {w}.", $"{l}, I hope you got a receipt, because {w} just took you to the cleaners."), loser, TalkTone.Sassy);
    }

    /// <summary>After the bot's own trade: a kind word to a friend.</summary>
    private Remark? OnMyTrade(PlayerView view, TradeOffer offer, int i)
    {
        int other = offer.FromId == view.ViewerId ? offer.ToId : offer.FromId;
        if (offer.FromId != view.ViewerId && offer.ToId != view.ViewerId || MoodToward(view, other) != Mood.Friendly)
            return null;
        return new Remark(Pick(i, $"Pleasure doing business, {Name(view, other)}.", $"Good deal, {Name(view, other)}. Let's do it again."), other, TalkTone.Friendly);
    }
}
