using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Views;

namespace CityTycoon.Engine.Hosting;

/// <summary>
/// Whoever controls a seat at the table (Section 22): a person clicking in the
/// browser, a bot, or (later) a person on another device. The engine never
/// knows which kind it is. That's the point of an interface: lots of
/// different classes can promise to do the same job in their own way.
/// </summary>
public interface IPlayerController
{
    /// <summary>Given only what this player may see, choose an action.</summary>
    Task<GameAction> DecideAsync(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions);
}

/// <summary>
/// Runs a game: asks the engine what it's waiting for, routes each question
/// to the right controller, and hands the answers back (Section 22).
/// </summary>
public sealed class GameHost(GameEngine engine, IReadOnlyDictionary<int, IPlayerController> controllers)
{
    public GameEngine Engine { get; } = engine;

    /// <summary>Raised after each accepted action, with the events it caused (for logs and animations).</summary>
    public event Action<IReadOnlyList<GameEvent>>? EventsHappened;

    /// <summary>Plays until the game ends.</summary>
    public async Task RunAsync(int maxDecisions = 1_000_000, CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < maxDecisions && !Engine.State.IsOver; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await StepAsync();
        }
        if (!Engine.State.IsOver)
            throw new InvalidOperationException($"The game didn't finish within {maxDecisions} decisions.");
    }

    /// <summary>Answers everything the engine is waiting for right now.</summary>
    public async Task StepAsync()
    {
        var decisions = Engine.GetPendingDecisions().ToList();
        if (decisions.Count == 0)
            throw new InvalidOperationException("The engine isn't waiting for anyone, but the game isn't over.");

        // Collect every answer first, then submit them. With sealed bids nobody
        // can see anyone else's choice before making their own.
        var answers = new List<(int PlayerId, GameAction Action)>();
        foreach (PendingDecision decision in decisions)
        {
            IPlayerController controller = controllers[decision.PlayerId];
            GameAction action = await controller.DecideAsync(
                Engine.GetView(decision.PlayerId), decision, Engine.GetLegalActions(decision.PlayerId));
            answers.Add((decision.PlayerId, action));
        }

        foreach (var (playerId, action) in answers)
        {
            ActionResult result = Engine.Submit(playerId, action);
            if (!result.Accepted)
                throw new InvalidOperationException($"Player {playerId} chose an illegal action: {result.Error}");
            EventsHappened?.Invoke(result.Events);
        }
    }
}
