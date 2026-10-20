using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;

namespace CityTycoon.Engine.Rules;

/// <summary>
/// The "in between" actions: build, sell, mortgage, loans and trades. Several
/// decisions allow some of them (see <see cref="ManagementOptions"/>), so the
/// engine handles them in one place instead of every step repeating the code.
/// </summary>
public static class ManagementRules
{
    /// <summary>Every management action this player could take right now under these options.</summary>
    public static List<GameAction> LegalActions(EngineContext ctx, int playerId, ManagementOptions options)
    {
        var actions = new List<GameAction>();
        var mine = ctx.State.Board
            .Where(s => ctx.State.Properties.TryGetValue(s.Id, out var p) && p.OwnerId == playerId)
            .Select(s => s.Id)
            .ToList();

        if (options.HasFlag(ManagementOptions.Build))
            actions.AddRange(mine.Where(id => BuildingRules.WhyCantBuild(ctx, playerId, id) == null).Select(id => new Build(id)));
        if (options.HasFlag(ManagementOptions.Sell))
            actions.AddRange(mine.Where(id => BuildingRules.WhyCantSell(ctx, playerId, id) == null).Select(id => new SellBuilding(id)));
        if (options.HasFlag(ManagementOptions.Mortgage))
            actions.AddRange(mine.Where(id => MoneyRules.WhyCantMortgage(ctx, playerId, id) == null).Select(id => new Mortgage(id)));
        if (options.HasFlag(ManagementOptions.Unmortgage))
            actions.AddRange(mine.Where(id => MoneyRules.WhyCantUnmortgage(ctx, playerId, id) == null).Select(id => new Unmortgage(id)));
        if (options.HasFlag(ManagementOptions.TakeLoan) && MoneyRules.CanTakeLoan(ctx, playerId))
            actions.Add(new TakeLoan());
        if (options.HasFlag(ManagementOptions.RepayLoan) && MoneyRules.CanRepayLoan(ctx, playerId))
            actions.Add(new RepayLoan());
        // Trades can't be listed (there are endless combinations), so they're checked in IsLegal.
        return actions;
    }

    public static bool CanProposeTrades(EngineContext ctx, int playerId, ManagementOptions options) =>
        options.HasFlag(ManagementOptions.Trade)
        && ctx.Player(playerId).TradesProposedThisTurn < ctx.Rules.MaxTradeProposalsPerTurn;

    public static string? WhyIllegal(EngineContext ctx, int playerId, ManagementOptions options, ManagementAction action) => action switch
    {
        Build b => !options.HasFlag(ManagementOptions.Build) ? "You can't build right now." : BuildingRules.WhyCantBuild(ctx, playerId, b.PropertyId),
        SellBuilding s => !options.HasFlag(ManagementOptions.Sell) ? "You can't sell right now." : BuildingRules.WhyCantSell(ctx, playerId, s.PropertyId),
        Mortgage m => !options.HasFlag(ManagementOptions.Mortgage) ? "You can't mortgage right now." : MoneyRules.WhyCantMortgage(ctx, playerId, m.PropertyId),
        Unmortgage u => !options.HasFlag(ManagementOptions.Unmortgage) ? "You can't unmortgage right now." : MoneyRules.WhyCantUnmortgage(ctx, playerId, u.PropertyId),
        TakeLoan => options.HasFlag(ManagementOptions.TakeLoan) && MoneyRules.CanTakeLoan(ctx, playerId) ? null : "You can't take a loan right now.",
        RepayLoan => options.HasFlag(ManagementOptions.RepayLoan) && MoneyRules.CanRepayLoan(ctx, playerId) ? null : "You can't repay a loan right now.",
        ProposeTrade t => !CanProposeTrades(ctx, playerId, options) ? "You can't offer a trade right now."
            : t.Offer.FromId != playerId ? "You can only offer your own trades."
            : TradeRules.WhyInvalid(ctx, t.Offer),
        _ => "Unknown action.",
    };

    public static void Apply(EngineContext ctx, int playerId, ManagementAction action)
    {
        switch (action)
        {
            case Build b: BuildingRules.Build(ctx, playerId, b.PropertyId); break;
            case SellBuilding s: BuildingRules.Sell(ctx, playerId, s.PropertyId); break;
            case Mortgage m: MoneyRules.MortgageProperty(ctx, playerId, m.PropertyId); break;
            case Unmortgage u: MoneyRules.UnmortgageProperty(ctx, playerId, u.PropertyId); break;
            case TakeLoan: MoneyRules.TakeLoan(ctx, playerId); break;
            case RepayLoan: MoneyRules.RepayLoan(ctx, playerId); break;
            case ProposeTrade t:
                ctx.Player(playerId).TradesProposedThisTurn++;
                ctx.Emit(new TradeProposed(t.Offer));
                // The other player answers next. The proposer's own decision comes back afterwards.
                ctx.Push(new TradeResponseStep { Offer = t.Offer });
                break;
        }
    }
}
