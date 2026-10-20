using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.Randomness;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI;

/// <summary>
/// The Easy bot (Section 23.2): mostly simple choices with basic sense. It
/// never bids more than its cash minus a safety reserve, and won't pay much
/// over a property's assessed value.
///
/// Each Easy bot gets a small random "personality" (how much it's willing to
/// overpay), so a table of Easy bots doesn't all play the same.
/// </summary>
public sealed class EasyBot : IPlayerController
{
    private readonly Pcg32 rng;
    private readonly GameContent content;

    /// <summary>Cash the bot keeps back so it can pay rent and taxes.</summary>
    public int SafetyReserve { get; }

    /// <summary>The most it will pay, as a share of assessed value (for example 1.1 = 110%).</summary>
    public double MaxPriceRatio { get; }

    public EasyBot(ulong seed, GameContent? content = null)
    {
        rng = new Pcg32(seed, stream: 101);
        this.content = content ?? GameContent.Default;
        SafetyReserve = rng.NextInt(100, 250);
        MaxPriceRatio = 0.9 + rng.NextDouble() * 0.4; // 90% to 130%
    }

    public Task<GameAction> DecideAsync(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions)
    {
        GameAction choice = decision.Kind switch
        {
            DecisionKind.SubmitBid => ChooseBid(view, decision),
            // Pay bail when comfortably rich; otherwise hope for doubles (it's free).
            DecisionKind.JailChoice => legalActions.Contains(new PayBail()) && view.Me.Cash > SafetyReserve * 3
                ? new PayBail()
                : new RollForDoubles(),
            _ => legalActions[0],
        };
        return Task.FromResult(choice);
    }

    private GameAction ChooseBid(PlayerView view, PendingDecision decision)
    {
        int inc = decision.BidIncrement;
        int assessed = content.Property(decision.PropertyId!).AssessedValue;
        int affordable = view.Me.Cash - SafetyReserve;
        int limit = Math.Min(affordable, (int)(assessed * MaxPriceRatio)) / inc * inc;
        if (limit < decision.MinBid)
            return new PassBid();

        // Bid somewhere between the minimum and its limit, leaning towards the limit.
        int steps = (limit - decision.MinBid) / inc;
        int chosen = Math.Max(rng.NextInt(steps + 1), rng.NextInt(steps + 1));
        return new PlaceBid(decision.MinBid + chosen * inc);
    }
}
