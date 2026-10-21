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

    /// <summary>Ordinances in force right now, with the rounds each has left (null means it never ends).</summary>
    public IReadOnlyList<ActiveOrdinance> ActiveOrdinances { get; init; } = [];

    /// <summary>The round at the end of which the next Regular City Council session happens.</summary>
    public int NextCouncilRound { get; init; }

    public int OrdinanceDeckCount { get; init; }

    /// <summary>Each player's net worth split into cash, property, buildings, cards and loans (public, like net worth itself).</summary>
    public IReadOnlyDictionary<int, NetWorth.Breakdown> NetWorthBreakdowns { get; init; } = new Dictionary<int, NetWorth.Breakdown>();

    public PlayerInfo Me => Players.First(p => p.Id == ViewerId);

    public PlayerInfo Player(int id) => Players.First(p => p.Id == id);

    public static PlayerView Create(GameState state, GameContent content, int viewerId)
    {
        var modifiers = RuleModifiers.Active(state, content);
        var players = state.Players.Select(p => new PlayerInfo(
            p.Id, p.Name, p.Cash, p.Position, p.InJail, p.JailTurns, p.IsBankrupt,
            NetWorth.Calculate(state, content, p.Id, modifiers),
            p.Id == state.ActivePlayer.Id, p.Loans, p.Momentum, p.IsInsured, p.IsRecovering,
            p.Hand.Count, p.RoadWorkCost > 0, p.ExtraBuildsThisTurn, p.BuildDiscountThisTurn)
        {
            // Characters and perks are public, and so are the limits they change.
            CharacterId = p.CharacterId,
            Perks = modifiers.Where(m => m.OwnerId == p.Id).Select(m => m.Source).Distinct()
                .Where(name => p.CharacterId == null || content.Character(p.CharacterId).Name != name).ToList(),
            HandLimit = RuleModifiers.HandLimit(state, content, p.Id, modifiers),
            MomentumCap = RuleModifiers.MomentumCap(state, content, p.Id, modifiers),
            LoanRepayCost = RuleModifiers.LoanRepayCost(state, content, p.Id, modifiers),
            MaxLoans = RuleModifiers.MaxLoans(state, content, p.Id, modifiers),
            Bail = RuleModifiers.Bail(state, content, p.Id),
            TakeoverReady = !p.TakeoverUsed && modifiers.Any(m => m.CanHostileTakeover(state, p.Id)),
        }).ToList();

        // What this viewer knows: their own hand and any tiles they've peeked at. Nobody else's.
        PlayerState? me = state.Players.FirstOrDefault(p => p.Id == viewerId);
        var known = me?.KnownTiles ?? [];

        var spaces = state.Board.Select(s => new SpaceInfo(
            s.Index, s.Id, s.Name, s.Kind, s.DistrictId,
            // A hidden tile's type is secret, so it is left out unless revealed (or this viewer peeked at it).
            TileId: s.IsRevealed || known.Contains(s.Id) ? s.TileId : null,
            IsHiddenTile: s.Kind == SpaceKind.GovernmentTile && !s.IsRevealed)).ToList();

        var properties = state.Properties.Values.ToDictionary(
            p => p.PropertyId,
            p => new PropertyInfo(p.PropertyId, p.OwnerId, p.Level, p.IsMortgaged, p.IsPending, p.PendingBalance,
                // Only revealed tiles change these, so they hold no secrets.
                RentPercent: RuleModifiers.NetRentPercent(state, content, p.PropertyId, modifiers),
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
            Result: state.Result)
        {
            ActiveOrdinances = state.ActiveOrdinances.ToList(),
            NextCouncilRound = (state.Round + content.Rules.CouncilEveryNRounds - 1) / content.Rules.CouncilEveryNRounds * content.Rules.CouncilEveryNRounds,
            OrdinanceDeckCount = state.OrdinanceDeck.Count,
            NetWorthBreakdowns = state.Players.ToDictionary(p => p.Id, p => NetWorth.Explain(state, content, p.Id, modifiers)),
        };
    }
}

public sealed record PlayerInfo(
    int Id, string Name, int Cash, int Position, bool InJail, int JailTurns, bool IsBankrupt, int NetWorth, bool IsActive, int Loans,
    int Momentum, bool IsInsured, bool IsRecovering,
    int HandCount, bool HasRoadWork, int ExtraBuildsThisTurn, int BuildDiscountThisTurn)
{
    /// <summary>The character this player picked, or null in a game without characters.</summary>
    public string? CharacterId { get; init; }

    /// <summary>The color perks and railroad/utility bonuses this player has switched on, by name.</summary>
    public IReadOnlyList<string> Perks { get; init; } = [];

    /// <summary>How many Opportunity cards they may keep at the end of a turn (3, or more with the Pink perk).</summary>
    public int HandLimit { get; init; } = 3;

    /// <summary>How many Momentum tokens they can hold.</summary>
    public int MomentumCap { get; init; } = 2;

    /// <summary>What paying back one loan costs them, and how many loans they may hold.</summary>
    public int LoanRepayCost { get; init; } = 250;
    public int MaxLoans { get; init; } = 2;

    /// <summary>What it costs them to leave Jail early ($0 for the Lawyer).</summary>
    public int Bail { get; init; } = 50;

    /// <summary>True if they hold the Red perk and haven't used this lap's Hostile Takeover yet.</summary>
    public bool TakeoverReady { get; init; }
}

/// <param name="TileId">For a Government tile, which tile it is, or null while it's hidden from this viewer.</param>
public sealed record SpaceInfo(
    int Index, string Id, string Name, SpaceKind Kind, string? DistrictId, string? TileId, bool IsHiddenTile);

/// <param name="IsPending">Won with a down payment and not fully paid yet (Section 8.5).</param>
/// <param name="RentPercent">The total percentage rent change from revealed tiles (and later other modifiers).</param>
/// <param name="NextLevelCost">What the next building level costs its owner right now, with every discount.</param>
public sealed record PropertyInfo(string Id, int? OwnerId, int Level, bool IsMortgaged, bool IsPending, int PendingBalance,
    int RentPercent = 0, int NextLevelCost = 0);
