using CityTycoon.Engine.Content;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI;

/// <summary>
/// The Hard bot (Section 23.2): the Normal bot, plus:
/// it keeps a cash reserve sized to the biggest rent or tax in the next 12 spaces, it bids a little more
/// for properties (including when it blocks an opponent's color group, which every bot does a little), it only lobbies on votes
/// that matter, and it spends a Nudge token more readily to dodge a big rent or land somewhere good.
///
/// The numbers below came from Sim runs of Hard against Normal (see docs/BALANCE.md).
/// </summary>
public sealed class HardBot(ulong seed, GameContent? content = null) : NormalBot(seed, content, 103)
{
    /// <summary>Hard bots value property higher than Normal bots do, so they win more auctions. Tested: 1.2 beat 1.0, 1.4 did worse.</summary>
    protected override double WorthScale => 1.2;

    /// <summary>Lobbying lost money in the Sim, so Hard bots only spend a little of what they can spare.</summary>
    protected override double LobbyShare => 0.1;
    protected override int LobbyReserveFactor => 1;
    protected override double NudgeThreshold => TokenValue * 0.6;

    /// <summary>Half of what the next 12 spaces could cost (rent or tax), plus a small cushion. Spare cash goes into buildings.</summary>
    protected override int Reserve(PlayerView view)
    {
        double danger = 0;
        for (int step = 1; step <= 12; step++)
        {
            SpaceInfo space = view.Spaces[(view.Me.Position + step) % view.Spaces.Count];
            double cost = space.Kind switch
            {
                SpaceKind.Property when view.Properties[space.Id].OwnerId is int owner && owner != view.ViewerId => EstimateRent(view, space.Id, 7),
                SpaceKind.Tax => space.Id == "income-tax" ? content.Rules.IncomeTax : content.Rules.LuxuryTax,
                _ => 0,
            };
            danger = Math.Max(danger, cost);
        }
        return (int)(Math.Clamp(danger + 40, 80, 700) * 0.5);
    }
}
