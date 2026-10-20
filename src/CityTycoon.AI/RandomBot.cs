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

    /// <summary>How often (out of 100) it does a "manage money" action instead of answering the question.</summary>
    public int ManagementChance { get; init; } = 20;

    public Task<GameAction> DecideAsync(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions)
    {
        if (decision.Kind == DecisionKind.SubmitBid)
            return Task.FromResult(ChooseBid(decision));

        // City Council: any vote, with a bit of lobbying so the Sim exercises the money side too.
        if (decision.Kind == DecisionKind.CouncilVote)
        {
            var choice = (VoteChoice)rng.NextInt(3);
            int lobby = choice == VoteChoice.Abstain ? 0 : rng.NextInt(Math.Max(1, decision.MaxLobby / 4 + 1));
            return Task.FromResult<GameAction>(new CastVote(choice, lobby, PlayLobbyist: choice != VoteChoice.Abstain && decision.HasLobbyist && rng.NextInt(2) == 0));
        }

        // Management actions (build, mortgage, loans...) can be taken again and
        // again, so a purely random bot could spend forever on them. Mostly it
        // answers the question itself instead.
        var main = legalActions.Where(a => a is not ManagementAction).ToList();
        var extra = legalActions.Where(a => a is ManagementAction).ToList();
        bool manage = main.Count == 0 || (extra.Count > 0 && rng.NextInt(100) < ManagementChance);
        var pool = manage ? extra : main;
        return Task.FromResult(pool[rng.NextInt(pool.Count)]);
    }

    private GameAction ChooseBid(PendingDecision decision)
    {
        int inc = decision.BidIncrement;
        int highest = Math.Min(decision.MaxBid - SafetyReserve, decision.MinBid * 3) / inc * inc;
        if (highest >= decision.MinBid && rng.NextInt(100) >= 35)
        {
            int steps = (highest - decision.MinBid) / inc;
            return new PlaceBid(decision.MinBid + rng.NextInt(steps + 1) * inc);
        }

        // Sometimes bid the minimum with a down payment, so the Sim exercises pending properties.
        if (decision.MaxDownPaymentBid >= decision.MinBid && rng.NextInt(100) < 25)
            return new PlaceBid(decision.MinBid, DownPayment: true);

        return new PassBid();
    }
}
