using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI;

/// <summary>
/// The Normal bot (Section 23.2). It plays like the Easy bot, but it uses simple estimates:
/// what a property is worth to it (a big bonus when it completes a color group), which building
/// pays back fastest, and how much an ordinance is worth in dollars, so it lobbies when a vote
/// clearly helps or hurts it.
///
/// Everything here only uses the bot's own <see cref="PlayerView"/>, so it can't see hidden tiles,
/// other players' cards or sealed bids (Section 23.1).
/// </summary>
public class NormalBot(ulong seed, GameContent? content = null, ulong stream = 102) : EasyBot(seed, content, stream)
{
    protected virtual double WorthScale => 1.0;

    /// <summary>Normal and Hard bots judge a color group in a trade by the rent it can earn (see EasyBot.Trades.cs).</summary>
    protected override bool JudgesGroupsCarefully => true;

    /// <summary>1 early in a Timed game, fading to 0.5 in the last few rounds, when a set no longer has time to pay off.</summary>
    protected static double TimeLeftFactor(PlayerView view) =>
        view.Mode == GameMode.Timed ? Math.Clamp((view.MaxRounds - view.Round) / 10.0, 0.5, 1.0) : 1.0;

    // ------------------------------------------------------------ Bidding

    protected override double PropertyWorth(PlayerView view, PendingDecision decision)
    {
        string id = decision.PropertyId!;
        PropertyDefinition def = content.Property(id);
        double worth = def.AssessedValue * MaxPriceRatio + SetBonus(view, id) * TimeLeftFactor(view);
        return WithBlocking(view, id, worth * WorthScale);
    }

    /// <summary>Extra dollars a property is worth because of the color group (or railroads and utilities) it belongs to.</summary>
    protected double SetBonus(PlayerView view, string id)
    {
        PropertyDefinition def = content.Property(id);
        if (def.Group == null)
        {
            // Railroads and utilities pay more for each one owned.
            int owned = CountOfType(view, view.ViewerId, def.Type);
            return def.AssessedValue * 0.2 * owned;
        }

        var others = content.GroupMembers(def.Group).Where(m => m != id).ToList();
        double groupValue = GroupValue(id);
        bool Mine(string m) => view.Properties[m].OwnerId == view.ViewerId && !view.Properties[m].IsPending;

        double bonus = 0;
        if (others.All(Mine))
            bonus += groupValue * 0.75;
        else if (others.Any(Mine) && others.All(m => Mine(m) || view.Properties[m].OwnerId == null))
            bonus += groupValue * 0.15 * others.Count(Mine);

        return bonus;
    }

    /// <summary>A sealed bid close to its limit, and all the way up to it when the set matters.</summary>
    protected override int SealedAmount(PlayerView view, PendingDecision decision, int limit, int inc)
    {
        bool matters = SetBonus(view, decision.PropertyId!) > 0 || RivalCompleting(view, decision.PropertyId!) != null;
        double share = matters ? 1.0 : 0.8 + rng.NextDouble() * 0.2;
        int amount = (int)(limit * share) / inc * inc;
        return Math.Clamp(amount, decision.MinBid, limit);
    }

    // ------------------------------------------------------------ Building

    /// <summary>Builds where each dollar buys the most extra rent.</summary>
    protected override Build? PickBuild(PlayerView view, IReadOnlyList<GameAction> legalActions, int spare)
    {
        double Payback(Build b)
        {
            PropertyDefinition def = content.Property(b.PropertyId);
            int level = view.Properties[b.PropertyId].Level;
            double cost = Math.Max(1, NextBuildCost(view, b.PropertyId));
            if (def.Rents == null || level + 1 >= def.Rents.Count)
                return 0;
            return (def.Rents[level + 1] - def.Rents[level]) / cost;
        }
        return legalActions.OfType<Build>()
            .Where(b => NextBuildCost(view, b.PropertyId) <= spare)
            .OrderByDescending(Payback)
            .FirstOrDefault();
    }

    // ------------------------------------------------------------ City Council

    /// <summary>
    /// Votes for what helps it, and lobbies in proportion to how many dollars the ordinance is worth to it,
    /// paying only from cash it can spare.
    /// </summary>
    protected override GameAction Vote(PlayerView view, PendingDecision decision)
    {
        int benefit = Benefit(view, decision.OrdinanceId!, decision.DistrictId);
        if (benefit == 0)
            return new CastVote(VoteChoice.Abstain);
        double impact = Impact(view, decision.OrdinanceId!, decision.DistrictId);
        int spare = Math.Max(0, view.Me.Cash - Reserve(view) * LobbyReserveFactor);
        int lobby = (int)Math.Min(decision.MaxLobby, Math.Min(spare * LobbyShare, impact * 0.4));
        lobby = Math.Max(0, lobby) / 10 * 10;
        bool useLobbyist = decision.HasLobbyist && impact >= 100;
        return new CastVote(benefit > 0 ? VoteChoice.Yes : VoteChoice.No, lobby, useLobbyist);
    }

    /// <summary>How much of its spare cash it is willing to spend lobbying on one vote.</summary>
    protected virtual double LobbyShare => 0.2;
    protected virtual int LobbyReserveFactor => 2;

    /// <summary>A rough dollar size of how much an ordinance matters to this bot.</summary>
    protected double Impact(PlayerView view, string ordinanceId, string? districtId)
    {
        OrdinanceDefinition def = content.Ordinance(ordinanceId);
        if (def.EffectType == "DistrictRent" && districtId != null)
        {
            int others = Math.Max(1, view.Players.Count(p => !p.IsBankrupt) - 1);
            double mine = 0, theirs = 0;
            foreach (SpaceInfo space in view.Spaces.Where(s => s.DistrictId == districtId && s.Kind == SpaceKind.Property))
            {
                double rent = EstimateRent(view, space.Id, 7);
                if (view.Properties[space.Id].OwnerId == view.ViewerId) mine += rent;
                else theirs += rent;
            }
            double swing = Math.Abs(def.Int("percent")) / 100.0 * Math.Abs(mine - theirs / others) * 3;
            return Math.Max(60, swing);
        }
        return 80;
    }
}
