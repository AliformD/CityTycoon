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

    /// <summary>Normal and Hard bots are less cautious than Easy about handing over a color group in a trade.</summary>
    protected override double SetCaution => 1.0;

    /// <summary>1 early in a Timed game, fading to 0.5 in the last few rounds, when a set no longer has time to pay off.</summary>
    protected static double TimeLeftFactor(PlayerView view) =>
        view.Mode == GameMode.Timed ? Math.Clamp((view.MaxRounds - view.Round) / 10.0, 0.5, 1.0) : 1.0;

    // ------------------------------------------------------------ Bidding

    protected override double PropertyWorth(PlayerView view, PendingDecision decision)
    {
        string id = decision.PropertyId!;
        PropertyDefinition def = content.Property(id);
        double worth = def.AssessedValue * MaxPriceRatio + (SetBonus(view, id) + CharacterFit(view, id)) * TimeLeftFactor(view)
            + PerkBonus(view, id);
        return WithBlocking(view, id, worth * WorthScale);
    }

    /// <summary>
    /// The share of a color group's perk (what trades count, see <c>PerkWorth</c>) a bot adds to its bid for the
    /// last piece of the group; a smaller share for each piece it already has when the group is still open (decision #156).
    /// </summary>
    protected virtual double BidPerkShare => 0.5;

    /// <summary>How much of <see cref="CharacterFit"/> the bot adds to a bid (decision #156).</summary>
    protected virtual double CharacterFitShare => 0.3;

    /// <summary>Part of the group's perk, for winning the last piece, or for a piece of a group it could still finish.</summary>
    protected double PerkBonus(PlayerView view, string id)
    {
        string? group = content.Property(id).Group;
        if (group == null || BidPerkShare <= 0)
            return 0;
        var others = content.GroupMembers(group).Where(m => m != id).ToList();
        bool Mine(string m) => view.Properties[m].OwnerId == view.ViewerId && !view.Properties[m].IsPending;
        double perk = PerkWorthFor(view, view.ViewerId, group);
        if (others.All(Mine))
            return perk * BidPerkShare;
        if (others.Any(Mine) && others.All(m => Mine(m) || view.Properties[m].OwnerId == null))
            return perk * BidPerkShare * 0.2 * others.Count(Mine);
        return 0;
    }

    /// <summary>
    /// Extra a property is worth because it suits the bot's character, while its group can still be finished:
    /// the Contractor saves on every house, so groups with dear houses suit it; the Commuter
    /// (longer Nudges) likes the extra token room Light Blue's Commuter Belt gives.
    /// </summary>
    protected double CharacterFit(PlayerView view, string id)
    {
        PropertyDefinition def = content.Property(id);
        if (def.Group == null || CharacterFitShare <= 0
            || content.GroupMembers(def.Group).Any(m => view.Properties[m].OwnerId is int o && o != view.ViewerId))
        {
            return 0;
        }
        double fit = 0;
        // About 3 levels on this property, at the discount, and half of that because the set isn't sure yet.
        fit += CharacterModifier(view, view.ViewerId, "BuildDiscount") / 100.0 * def.HouseCost * 3 * 0.5;
        if (CharacterModifier(view, view.ViewerId, "NudgeRange") > 0 && PerkModifier(def.Group, "MomentumCap") > 0)
            fit += def.AssessedValue * 0.25;
        return fit * CharacterFitShare;
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

    /// <summary>
    /// Builds where each dollar buys the most extra rent. With Green's Dividends, a new hotel also
    /// pays its dividend every turn, counted here as the same money in rent.
    /// </summary>
    protected override Build? PickBuild(PlayerView view, IReadOnlyList<GameAction> legalActions, int spare)
    {
        int dividend = GroupPerkModifier(view, view.ViewerId, "DividendPerTower");
        int opponents = Math.Max(1, view.Players.Count(p => !p.IsBankrupt && p.Id != view.ViewerId));
        double Payback(Build b)
        {
            PropertyDefinition def = content.Property(b.PropertyId);
            int level = view.Properties[b.PropertyId].Level;
            double cost = Math.Max(1, NextBuildCost(view, b.PropertyId));
            if (def.Rents == null || level + 1 >= def.Rents.Count)
                return 0;
            double gain = def.Rents[level + 1] - def.Rents[level];
            // Rent is paid about (opponents / spaces) times a round; the dividend once a round.
            if (level + 1 == content.Rules.HotelLevel)
                gain += dividend * (double)view.Spaces.Count / opponents;
            return gain / cost;
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

    /// <summary>
    /// What one more building level on each of its groups in this district would earn before the game ends
    /// (plus Green's dividend for each new hotel), for a group already built to the zoning limit.
    /// </summary>
    private double RezoneWorth(PlayerView view, string districtId)
    {
        int opponents = Math.Max(1, view.Players.Count(p => !p.IsBankrupt && p.Id != view.ViewerId));
        int dividend = GroupPerkModifier(view, view.ViewerId, "DividendPerTower");
        double rounds = Math.Min(RoundsLeft(view), NearRounds);
        double worth = 0;
        foreach (SpaceInfo space in view.Spaces.Where(s => s.DistrictId == districtId && s.Kind == SpaceKind.Property))
        {
            PropertyInfo property = view.Properties[space.Id];
            PropertyDefinition def = content.Property(space.Id);
            if (property.OwnerId != view.ViewerId || def.Rents == null || property.Level + 1 >= def.Rents.Count)
                continue;
            double gain = (def.Rents[property.Level + 1] - def.Rents[property.Level]) * opponents / (double)view.Spaces.Count;
            if (property.Level + 1 == content.Rules.HotelLevel)
                gain += dividend;
            worth += gain * rounds - property.NextLevelCost * 0.5;
        }
        return Math.Max(80, worth);
    }

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
        if (def.EffectType == "Rezone" && districtId != null && Benefit(view, ordinanceId, districtId) == 2)
            return RezoneWorth(view, districtId);
        return 80;
    }
}
