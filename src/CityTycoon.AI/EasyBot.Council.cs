using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI;

/// <summary>
/// City Council in dollars (decision #159): what an ordinance is worth to a player over the rounds it lasts,
/// how much lobbying that is worth, and vote deals (paying another player to vote your way).
/// Every number comes from what everyone can see (who owns what, built how high), so a bot can
/// size up other players the same way it sizes up itself.
/// </summary>
public partial class EasyBot
{
    /// <summary>How the bot ranks ordinances and districts when it sets the agenda. The Easy bot uses the simple sign.</summary>
    protected virtual double OrdinanceScore(PlayerView view, string ordinanceId, string? districtId) =>
        Benefit(view, ordinanceId, districtId);

    /// <summary>Offers a vote deal, or not. The Easy bot never does.</summary>
    protected virtual GameAction OfferDeal(PlayerView view, PendingDecision decision) => new NoThanks();

    /// <summary>The Easy bot takes a deal that asks it to vote the way it would anyway (or doesn't care).</summary>
    protected virtual GameAction AnswerDeal(PlayerView view, PendingDecision decision)
    {
        VoteDeal deal = decision.Deal!;
        int benefit = Benefit(view, decision.OrdinanceId!, decision.DistrictId);
        bool agrees = benefit == 0 || (benefit > 0) == (deal.Choice == VoteChoice.Yes);
        return agrees ? new AcceptVoteDeal() : new DeclineVoteDeal();
    }

    /// <summary>A vote, held to the deal the bot took (if any): it may still lobby when the deal goes its way.</summary>
    private GameAction VoteWithDeal(PlayerView view, PendingDecision decision)
    {
        GameAction vote = Vote(view, decision);
        if (decision.Deal is not VoteDeal bound || vote is CastVote { Choice: var c } && c == bound.Choice)
            return vote;
        return new CastVote(bound.Choice);
    }

    // ----- Ordinances in dollars -----

    /// <summary>
    /// Roughly how many dollars better off this player ends up, compared with the average opponent,
    /// if the ordinance passes (negative: it hurts them). Rent changes count over the rounds the
    /// ordinance lasts; zoning changes over the next few rounds of building.
    /// </summary>
    protected double OrdinanceWorth(PlayerView view, int playerId, string ordinanceId, string? districtId)
    {
        OrdinanceDefinition def = content.Ordinance(ordinanceId);
        var active = view.Players.Where(p => !p.IsBankrupt).ToList();
        int n = Math.Max(2, active.Count);
        double spaces = view.Spaces.Count;
        double roundsLeft = Math.Max(1, RoundsLeft(view));
        double lasts = Math.Min(def.Duration ?? NearRounds, roundsLeft);

        // A rent change of delta on a property: the owner collects it from everyone, everyone else pays it now and then.
        double Relative(double delta, int? owner) =>
            owner == null ? 0 : owner == playerId ? delta * n / spaces : -delta * n / (spaces * (n - 1));

        IEnumerable<SpaceInfo> District() => view.Spaces.Where(s => s.DistrictId == districtId && s.Kind == SpaceKind.Property);

        switch (def.EffectType)
        {
            case "DistrictRent" when districtId != null:
                return District().Sum(s => Relative(EstimateRent(view, s.Id, 7) * def.Int("percent") / 100.0, view.Properties[s.Id].OwnerId)) * lasts;

            case "RailroadRent":
                return view.Properties.Values.Where(p => content.Property(p.Id).Type == PropertyType.Railroad)
                    .Sum(p => Relative(EstimateRent(view, p.Id, 7) * def.Int("percent") / 100.0, p.OwnerId)) * lasts;

            case "Rezone" when districtId != null && view.Zoning.TryGetValue(districtId, out ZoningLevel zoning):
            {
                int cap = content.Rules.MaxBuildingLevel(zoning);
                int direction = def.Int("direction");
                double building = Math.Min(roundsLeft, NearRounds) * 0.6;
                double worth = 0;
                foreach (SpaceInfo s in District())
                {
                    PropertyInfo p = view.Properties[s.Id];
                    PropertyDefinition prop = content.Property(s.Id);
                    if (p.OwnerId is not int owner || prop.Group == null || prop.Rents == null
                        || !content.GroupMembers(prop.Group).All(id => view.Properties[id].OwnerId == owner))
                    {
                        continue;
                    }
                    // Up: a group built to the limit can grow one more level. Down: a group not there yet loses its top level.
                    if (direction > 0 && p.Level >= cap && cap + 1 < prop.Rents.Count)
                        worth += Relative(prop.Rents[cap + 1] - prop.Rents[cap], owner) * building;
                    else if (direction < 0 && p.Level < cap && cap >= 1)
                        worth -= Relative(prop.Rents[cap] - prop.Rents[cap - 1], owner) * building;
                }
                return worth != 0 ? worth : Benefit(view, ordinanceId, districtId) * 20.0 * (playerId == view.ViewerId ? 1 : 0);
            }

            case "TaxPerLevel":
            {
                double Levels(int id) => view.Properties.Values.Where(p => p.OwnerId == id).Sum(p => p.Level);
                double others = active.Where(p => p.Id != playerId).Select(p => Levels(p.Id)).DefaultIfEmpty(0).Average();
                return -(Levels(playerId) - others) * def.Int("perLevel");
            }

            case "BuildingMoratorium":
            {
                double worth = 0;
                foreach (PropertyInfo p in view.Properties.Values)
                {
                    PropertyDefinition prop = content.Property(p.Id);
                    if (p.OwnerId is int owner && prop.Group != null && prop.Rents != null && p.Level + 1 < prop.Rents.Count
                        && content.GroupMembers(prop.Group).All(id => view.Properties[id].OwnerId == owner))
                    {
                        worth -= Relative(prop.Rents[p.Level + 1] - prop.Rents[p.Level], owner) * lasts * 0.5;
                    }
                }
                return worth;
            }

            // Everyone gets or pays the same: it hardly changes who is ahead.
            case "CollectFromBank":
                return 5;
            case "InfrastructureBond":
                return -5;

            default:
                return playerId == view.ViewerId ? Benefit(view, ordinanceId, districtId) * 30.0 : 0;
        }
    }
}
