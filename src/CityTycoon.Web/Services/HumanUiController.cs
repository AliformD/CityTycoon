using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.Views;

namespace CityTycoon.Web.Services;

/// <summary>A question waiting for a person to click an answer.</summary>
public sealed record HumanRequest(int PlayerId, PlayerView View, PendingDecision Decision, IReadOnlyList<GameAction> LegalActions);

/// <summary>
/// The controller for a person playing in the browser (Section 22).
///
/// When the game host asks this person a question, <see cref="DecideAsync"/>
/// can't answer straight away: it has to wait for a click. It does that with a
/// <see cref="TaskCompletionSource{T}"/>, which is a "promise" of an answer
/// that the UI fulfils later by calling <see cref="Answer"/>.
/// </summary>
public sealed class HumanUiController(GameSession session) : IPlayerController
{
    private TaskCompletionSource<GameAction>? waiting;

    public Task<GameAction> DecideAsync(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions)
    {
        waiting = new TaskCompletionSource<GameAction>();
        session.ShowRequest(new HumanRequest(decision.PlayerId, view, decision, legalActions));
        return waiting.Task;
    }

    /// <summary>Called by the UI when the person clicks a button.</summary>
    public void Answer(GameAction action)
    {
        var current = waiting;
        waiting = null;
        current?.TrySetResult(action);
    }
}
