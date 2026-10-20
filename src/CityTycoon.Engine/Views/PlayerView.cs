using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.Modifiers;
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
    IReadOnlyDictionary<string, ZoningLevel> Zoning,
    int HotelPermitsLeft,
    int SkyscraperPermitsLeft,
    IReadOnlyList<StadiumBoost> StadiumBoosts,
    IReadOnlyList<PropertyBoost> PropertyBoosts,
    IReadOnlyList<string> MyHand,
    IReadOnlyList<string> MallShop,
    int OpportunityDeckCount,
    int OpportunityDiscardCount,
    int EventDeckCount,
    int EventDiscardCount,
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
            p.Id == state.ActivePlayer.Id, p.Loans, p.Momentum, p.IsInsured, p.IsRecovering,
            p.Hand.Count, p.RoadWorkCost > 0, p.ExtraBuildsThisTurn, p.BuildDiscountThisTurn)).ToList();

        // What this viewer knows: their own hand and any tiles they've peeked at. Nobody else's.
        PlayerState? me = state.Players.FirstOrDefault(p => p.Id == viewerId);
        var known = me?.KnownTiles ?? [];

        var spaces = state.Board.Select(s => new SpaceInfo(
            s.Index, s.Id, s.Name, s.Kind, s.DistrictId,
            // A hidden tile's type is secret, so it is left out unless revealed (or this viewer peeked at it).
            TileId: s.IsRevealed || known.Contains(s.Id) ? s.TileId : null,
            IsHiddenTile: s.Kind == SpaceKind.GovernmentTile && !s.IsRevealed)).ToList();

        var modifiers = RuleModifiers.Active(state, content);
        var properties = state.Properties.Values.ToDictionary(
            p => p.PropertyId,
            p => new PropertyInfo(p.PropertyId, p.OwnerId, p.Level, p.IsMortgaged, p.IsPending, p.PendingBalance,
                // Only revealed tiles change these, so they hold no secrets.
                RentPercent: Math.Clamp(RuleModifiers.RentPercents(state, content, p.PropertyId, modifiers).Sum(x => x.Percent), content.Rules.MinRentPercent, content.Rules.MaxRentPercent),
                NextLevelCost: BuildingRules.NextLevelCost(state, content, p.PropertyId, modifiers)));

        return new PlayerView(
            viewerId, state.Round, state.Settings.Mode, state.Settings.MaxRounds, state.ActivePlayer.Id,
            players, spaces, properties,
            new Dictionary<string, ZoningLevel>(state.Zoning), state.HotelPermits, state.SkyscraperPermits,
            state.StadiumBoosts.ToList(),
            state.PropertyBoosts.ToList(),
            MyHand: me?.Hand.ToList() ?? [],
            MallShop: state.MallShop.ToList(),
            OpportunityDeckCount: state.OpportunityDeck.Count,
            OpportunityDiscardCount: state.OpportunityDiscard.Count,
            EventDeckCount: state.EventDeck.Count,
            EventDiscardCount: state.EventDiscard.Count,
            MyDecision: state.Pending.FirstOrDefault(d => d.PlayerId == viewerId),
            WaitingOn: state.Pending.Select(d => d.PlayerId).Distinct().ToList(),
            // Events never contain secrets before they're revealed (sealed bids only appear in BidsRevealed,
            // and an Opportunity card only appears once it's played, sold or discarded).
            Log: state.Log.ToList(),
            Result: state.Result);
    }
}

public sealed record PlayerInfo(
    int Id, string Name, int Cash, int Position, bool InJail, int JailTurns, bool IsBankrupt, int NetWorth, bool IsActive, int Loans,
    int Momentum, bool IsInsured, bool IsRecovering,
    int HandCount, bool HasRoadWork, int ExtraBuildsThisTurn, int BuildDiscountThisTurn);

/// <param name="TileId">For a Government tile, which tile it is, or null while it's hidden from this viewer.</param>
public sealed record SpaceInfo(
    int Index, string Id, string Name, SpaceKind Kind, string? DistrictId, string? TileId, bool IsHiddenTile);

/// <param name="IsPending">Won with a down payment and not fully paid yet (Section 8.5).</param>
/// <param name="RentPercent">The total percentage rent change from revealed tiles (and later other modifiers).</param>
/// <param name="NextLevelCost">What the next building level costs its owner right now, with every discount.</param>
public sealed record PropertyInfo(string Id, int? OwnerId, int Level, bool IsMortgaged, bool IsPending, int PendingBalance,
    int RentPercent = 0, int NextLevelCost = 0);
