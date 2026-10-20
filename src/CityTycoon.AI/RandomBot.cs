using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.Randomness;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI;

/// <summary>
/// The simplest possible bot: it picks a random legal choice. It exists to
/// test the engine (if random choices never crash it, sensible ones won't
/// either) and to drive the Sim.
///
/// It gets its own seeded random generator, separate from the game's, so a
/// game with the same seeds always plays out the same way.
/// </summary>
public sealed class RandomBot(ulong seed) : IPlayerController
{
    private readonly Pcg32 rng = new(seed, stream: 99);

    /// <summary>Cash the bot tries to keep in hand when bidding, so it can pay rent.</summary>
    public int SafetyReserve { get; init; } = 150;

    public Task<GameAction> DecideAsync(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions)
    {
        GameAction choice = decision.Kind == DecisionKind.SubmitBid
            ? ChooseBid(decision)
            : legalActions[rng.NextInt(legalActions.Count)];
        return Task.FromResult(choice);
    }

    private GameAction ChooseBid(PendingDecision decision)
    {
        int inc = decision.BidIncrement;
        int highest = Math.Min(decision.MaxBid - SafetyReserve, decision.MinBid * 3) / inc * inc;
        if (highest < decision.MinBid || rng.NextInt(100) < 35)
            return new PassBid();

        int steps = (highest - decision.MinBid) / inc;
        return new PlaceBid(decision.MinBid + rng.NextInt(steps + 1) * inc);
    }
}
