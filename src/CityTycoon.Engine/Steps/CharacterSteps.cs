using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Tiles;

namespace CityTycoon.Engine.Steps;

/// <summary>
/// Character selection (Section 5). Turn order has been decided already. Players pick in
/// reverse turn order, so the last player picks first and the first player picks last.
/// Everyone sees each pick as it happens.
/// </summary>
public sealed class CharacterPickStep : Step
{
    /// <summary>The players still to pick, in the order they pick (last in turn order first).</summary>
    public List<int> Order { get; set; } = [];

    /// <summary>How many have picked so far.</summary>
    public int Picked { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        if (Picked >= Order.Count)
        {
            CharacterRules.FinishSetup(ctx);
            return StepResult.Done;
        }
        return StepResult.WaitFor(new PendingDecision(Order[Picked], DecisionKind.ChooseCharacter,
            $"Pick your character (pick {Picked + 1} of {Order.Count}). The last player in turn order picks first."));
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        CharacterRules.Available(ctx.State, ctx.Content).Select(id => (GameAction)new PickCharacter(id)).ToList();

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        CharacterRules.Assign(ctx, ctx.Player(Order[Picked]), ((PickCharacter)action).CharacterId);
        Picked++;
    }
}

/// <summary>
/// The City Planner's secret peek (Section 5): look at one hidden Government tile of your
/// choice. Only that player learns what it is. If every hidden tile is already known to
/// them, there is nothing to peek at and the step ends at once.
/// </summary>
public sealed class PeekTileStep : Step
{
    public int PlayerId { get; set; }
    public bool Answered { get; set; }

    /// <summary>The tile just peeked at, while the City Planner decides whether to swap it (decision #158).</summary>
    public string? PeekedSpaceId { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState player = ctx.Player(PlayerId);
        if (Answered || player.IsBankrupt)
            return StepResult.Done;
        if (PeekedSpaceId != null)
        {
            var tile = ctx.Content.GovTile(ctx.State.Space(PeekedSpaceId).TileId!);
            return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.SwapTile,
                $"It's the {tile.Name}: {tile.NeighborText} Swap it for a random tile that isn't in play? Nobody else will know what either one is.",
                PropertyId: PeekedSpaceId));
        }
        if (TileRules.HiddenTilesFor(ctx.State, player).Count == 0)
            return StepResult.Done;
        return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.PeekTile,
            "Choose a hidden Government tile to peek at. Only you will see what it is."));
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        PeekedSpaceId != null
            ? [new SwapTile(PeekedSpaceId), new NoThanks()]
            : TileRules.HiddenTilesFor(ctx.State, ctx.Player(PlayerId)).Select(s => (GameAction)new PeekAtTile(s.Id)).ToList();

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        PlayerState player = ctx.Player(PlayerId);
        switch (action)
        {
            case PeekAtTile peek:
                TileRules.Peek(ctx, player, peek.SpaceId);
                // The City Planner may then swap it, while it's still hidden and there's a tile to swap in.
                if (RuleModifiers.SwapsPeekedTiles(ctx.State, ctx.Content, PlayerId) && TileRules.UnusedTiles(ctx.State, ctx.Content).Count > 0)
                    PeekedSpaceId = peek.SpaceId;
                else
                    Answered = true;
                break;
            case SwapTile swap:
                TileRules.Swap(ctx, player, swap.SpaceId);
                Answered = true;
                break;
            default:
                Answered = true;
                break;
        }
    }
}
