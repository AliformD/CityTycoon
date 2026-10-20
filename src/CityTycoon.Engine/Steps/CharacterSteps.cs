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

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState player = ctx.Player(PlayerId);
        if (Answered || player.IsBankrupt || TileRules.HiddenTilesFor(ctx.State, player).Count == 0)
            return StepResult.Done;
        return StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.PeekTile,
            "Choose a hidden Government tile to peek at. Only you will see what it is."));
    }

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        TileRules.HiddenTilesFor(ctx.State, ctx.Player(PlayerId)).Select(s => (GameAction)new PeekAtTile(s.Id)).ToList();

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        Answered = true;
        TileRules.Peek(ctx, ctx.Player(PlayerId), ((PeekAtTile)action).SpaceId);
    }
}
