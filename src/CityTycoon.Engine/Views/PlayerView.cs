using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Views;

/// <summary>
/// Everything one player is allowed to see (Section 20.1). Bots and screens
/// only ever get a view, never the full <see cref="GameState"/>, so they can't
/// peek at hidden Government tiles or other players' sealed bids.
/// Later, the online server will send each player only their own view.
/// </summary>
public sealed record PlayerView(
    int ViewerId,
    int Round,
    GameMode Mode,
    int MaxRounds,
    int ActivePlayerId,
    IReadOnlyList<PlayerInfo> Players,
    IReadOnlyList<SpaceInfo> Spaces,
    IReadOnlyDictionary<string, PropertyInfo> Properties,
    PendingDecision? MyDecision,
    IReadOnlyList<int> WaitingOn,
    IReadOnlyList<GameEvent> Log,
    GameResult? Result)
{
    public bool IsOver => Result != null;

    public PlayerInfo Me => Players.First(p => p.Id == ViewerId);

    public PlayerInfo Player(int id) => Players.First(p => p.Id == id);

    public static PlayerView Create(GameState state, GameContent content, int viewerId)
    {
        var players = state.Players.Select(p => new PlayerInfo(
            p.Id, p.Name, p.Cash, p.Position, p.InJail, p.JailTurns, p.IsBankrupt,
            NetWorth.Calculate(state, content, p.Id),
            p.Id == state.ActivePlayer.Id)).ToList();

        var spaces = state.Board.Select(s => new SpaceInfo(
            s.Index, s.Id, s.Name, s.Kind, s.DistrictId,
            // A hidden tile's type is secret, so it is left out unless revealed.
            TileId: s.IsRevealed ? s.TileId : null,
            IsHiddenTile: s.Kind == SpaceKind.GovernmentTile && !s.IsRevealed)).ToList();

        var properties = state.Properties.Values.ToDictionary(
            p => p.PropertyId,
            p => new PropertyInfo(p.PropertyId, p.OwnerId, p.Level, p.IsMortgaged));

        return new PlayerView(
            viewerId, state.Round, state.Settings.Mode, state.Settings.MaxRounds, state.ActivePlayer.Id,
            players, spaces, properties,
            MyDecision: state.Pending.FirstOrDefault(d => d.PlayerId == viewerId),
            WaitingOn: state.Pending.Select(d => d.PlayerId).Distinct().ToList(),
            // Events never contain secrets before they're revealed (sealed bids only appear in BidsRevealed).
            Log: state.Log.ToList(),
            Result: state.Result);
    }
}

public sealed record PlayerInfo(
    int Id, string Name, int Cash, int Position, bool InJail, int JailTurns, bool IsBankrupt, int NetWorth, bool IsActive);

/// <param name="TileId">For a Government tile, which tile it is, or null while it's hidden from this viewer.</param>
public sealed record SpaceInfo(
    int Index, string Id, string Name, SpaceKind Kind, string? DistrictId, string? TileId, bool IsHiddenTile);

public sealed record PropertyInfo(string Id, int? OwnerId, int Level, bool IsMortgaged);
