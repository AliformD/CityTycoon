using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

/// <summary>
/// Landed on someone else's property next to a revealed Transit Hub: pay the
/// rent, or pay the hop fee to the bank and move onto the hub instead
/// (Section 12.3). Hopping doesn't trigger the hub's own landing effect.
/// </summary>
public sealed class TransitHopStep : Step
{
    public int PlayerId { get; set; }
    public string PropertyId { get; set; } = "";
    public string HubSpaceId { get; set; } = "";
    public int Cost { get; set; }
    public int Rent { get; set; }
    public bool Answered { get; set; }

    public override StepResult Run(EngineContext ctx) => Answered
        ? StepResult.Done
        : StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.TransitHop,
            $"You owe ${Rent} rent for {ctx.Content.Property(PropertyId).Name}. Pay it, or pay ${Cost} to the bank and hop onto the Transit Hub instead?",
            PropertyId, AmountOwed: Rent));

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        [new NoThanks(), new HopToTransitHub()];

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        Answered = true;
        PlayerState player = ctx.Player(PlayerId);
        if (action is not HopToTransitHub)
        {
            ResolveSpaceStep.ChargeRent(ctx, PlayerId, PropertyId, Rent);
            return;
        }
        int from = player.Position;
        player.Cash -= Cost;
        player.Position = ctx.State.IndexOf(HubSpaceId);
        ctx.Emit(new TransitHopped(PlayerId, PropertyId, Cost));
        ctx.Emit(new PlayerMoved(PlayerId, from, player.Position));
    }
}

/// <summary>Landed on a revealed Transit Hub: pay the fare to ride to any railroad, or stay.</summary>
public sealed class TransitRideStep : Step
{
    public int PlayerId { get; set; }
    public int Cost { get; set; }
    public bool Answered { get; set; }

    public override StepResult Run(EngineContext ctx) => Answered
        ? StepResult.Done
        : StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.TransitRide,
            $"Welcome to the Transit Hub. Pay ${Cost} to ride to any railroad?"));

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        [new NoThanks(), .. ctx.State.Board
            .Where(s => s.Kind == SpaceKind.Property && ctx.Content.Property(s.Id).Type == PropertyType.Railroad)
            .Select(s => new RideToRailroad(s.Id))];

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        Answered = true;
        if (action is not RideToRailroad ride)
            return;
        PlayerState player = ctx.Player(PlayerId);
        player.Cash -= Cost;
        ctx.Emit(new TransitRidden(PlayerId, ride.PropertyId, Cost));
        // Ride forward round the board, collecting GO if you pass it, then
        // resolve the railroad normally: rent if owned, an auction if not.
        int spaces = (ctx.State.IndexOf(ride.PropertyId) - player.Position + ctx.State.Board.Count) % ctx.State.Board.Count;
        ctx.Push(new ResolveSpaceStep { PlayerId = PlayerId });
        Movement.MoveForward(ctx, player, spaces);
    }
}
