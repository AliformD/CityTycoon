using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.Views;

namespace CityTycoon.Web.Services;

/// <summary>Small drawing helpers shared by several components.</summary>
public static class UiHelpers
{
    /// <summary>One color per seat. Tokens also show the player's initial, so color isn't the only clue.</summary>
    private static readonly string[] PlayerColors = ["#d62839", "#1d6fb8", "#2a9d47", "#e07a10", "#7b3fbf", "#5c6670"];

    public static string PlayerColor(int playerId) => PlayerColors[playerId % PlayerColors.Length];

    public static string Initial(string name) => string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();

    /// <summary>
    /// Where a board index sits on the 13 x 13 grid (Section 4.4). Returns
    /// 1-based CSS grid column and row. GO is bottom-right; play goes clockwise.
    /// </summary>
    public static (int Column, int Row) GridCell(int index)
    {
        (int x, int y) = index switch
        {
            0 => (12, 12),
            < 12 => (12 - index, 12),
            12 => (0, 12),
            < 24 => (0, 24 - index),
            24 => (0, 0),
            < 36 => (index - 24, 0),
            36 => (12, 0),
            _ => (12, index - 36),
        };
        return (x + 1, y + 1);
    }

    /// <summary>Which side of the board a space is on, so its color band can face the middle.</summary>
    public static string Side(int index) => index switch
    {
        > 0 and < 12 => "bottom",
        > 12 and < 24 => "left",
        > 24 and < 36 => "top",
        > 36 => "right",
        _ => "corner",
    };

    public static string Money(int amount) => amount < 0 ? $"-${-amount:N0}" : $"${amount:N0}";

    public static GroupDefinition? GroupOf(GameContent content, string spaceId) =>
        content.Properties.TryGetValue(spaceId, out var p) && p.Group != null ? content.Groups[p.Group] : null;

    /// <summary>The name to show on a space. Hidden Government tiles stay a mystery.</summary>
    public static string DisplayName(SpaceInfo space, GameContent content) => space.Kind switch
    {
        SpaceKind.GovernmentTile when space.IsHiddenTile => "Gov. tile",
        SpaceKind.GovernmentTile => content.GovTile(space.TileId!).Name,
        _ => space.Name,
    };

    public static string KindIcon(SpaceInfo space) => space.Kind switch
    {
        SpaceKind.Go => "➜",
        SpaceKind.Jail => "▦",
        SpaceKind.CityPark => "♣",
        SpaceKind.GoToJail => "⚑",
        SpaceKind.Event => "!",
        SpaceKind.Opportunity => "★",
        SpaceKind.Tax => "$",
        SpaceKind.Landmark => "◆",
        SpaceKind.GovernmentTile => "?",
        _ => "",
    };

    /// <summary>A button label for any action, with the money it costs or brings in.</summary>
    public static string ActionLabel(GameAction action, PlayerView view, GameContent content)
    {
        string Name(string id) => content.Property(id).Name;
        int Level(string id) => view.Properties[id].Level;
        RulesConfig rules = content.Rules;
        return action switch
        {
            RollDice => "🎲 Roll dice",
            EndTurn => "End turn",
            PayBail => $"Pay {Money(rules.JailBail)} bail",
            RollForDoubles => "Try for doubles",
            Build b => $"Build {EventDescriber.LevelName(Level(b.PropertyId) + 1)} on {Name(b.PropertyId)} ({Money(BuildCost(b.PropertyId, view, content))})",
            SellBuilding s => $"Sell a building on {Name(s.PropertyId)} (+{Money(BuildingRules.SellValue(content, content.Property(s.PropertyId), Level(s.PropertyId)))})",
            Mortgage m => $"Mortgage {Name(m.PropertyId)} (+{Money(NetWorth.MortgageValue(content, content.Property(m.PropertyId)))})",
            Unmortgage u => $"Unmortgage {Name(u.PropertyId)} ({Money(MoneyRules.UnmortgageCost(content, content.Property(u.PropertyId)))})",
            TakeLoan => $"Take a loan (+{Money(rules.LoanAmount)}, repay {Money(rules.LoanRepay)})",
            RepayLoan => $"Repay a loan ({Money(rules.LoanRepay)})",
            ForfeitProperty => "Give the property up",
            AcceptTrade => "Accept the trade",
            DeclineTrade => "Decline",
            PrecisionRoll => $"🎯 Precision Roll ({rules.PrecisionRollCost} tokens: roll {rules.PrecisionRollDice} dice, keep 2)",
            Nudge { Spaces: 0 } => "Don't Nudge",
            Nudge n => $"Nudge {(n.Spaces > 0 ? "+" : "−")}{Math.Abs(n.Spaces)} ({rules.NudgeCost} token)",
            NoThanks => "No thanks",
            BuyInsurance => $"Buy an Insured marker ({Money(rules.InsuredMarkerCost)})",
            AcceptDuel => "Accept the duel",
            DeclineDuel => "No thanks",
            PickStadiumGroup g => content.Groups[g.GroupId].Name,
            PlayCard play => $"Play {content.Card(play.CardId).Name}{PlayTarget(play, view, content)}",
            DiscardCard d => $"Discard {content.Card(d.CardId).Name}",
            TakeMomentum => $"Take {rules.CityParkMomentum} Momentum token",
            DrawOpportunity => "Draw an Opportunity card",
            BuyCard buy when buy.Slot < view.MallShop.Count => $"Buy {content.Card(view.MallShop[buy.Slot]).Name} ({Money(rules.MallPrices[buy.Slot])})",
            SellCard sell => $"Sell {content.Card(sell.CardId).Name} (+{Money(rules.MallSellPrice)})",
            ChooseProperty c => Name(c.PropertyId),
            ClearRoadWork => "Spend Momentum to clear the Road Work (roll both dice)",
            ArrangeCards => "Put them back in this order",
            _ => action.GetType().Name,
        };
    }

    /// <summary>What the next building level costs this player, including a Construction Permit discount.</summary>
    public static int BuildCost(string propertyId, PlayerView view, GameContent content) =>
        BuildingRules.ApplyDiscount(content.Rules,
            BuildingRules.LevelCost(content, content.Property(propertyId), view.Properties[propertyId].Level + 1),
            view.Me.BuildDiscountThisTurn);

    /// <summary>The " on Bo" or " (+1 space)" part of a card's button: what the play is aimed at.</summary>
    public static string PlayTarget(PlayCard play, PlayerView view, GameContent content)
    {
        var effect = content.Card(play.CardId).EffectType;
        var parts = new List<string>();
        if (play.TargetPlayerId is int target && effect != "Detour")
            parts.Add(view.Player(target).Name);
        if (play.TargetSpaceId is string spaceId)
            parts.Add(content.Properties.ContainsKey(spaceId) ? content.Property(spaceId).Name : view.Spaces.First(s => s.Id == spaceId).Name);
        string on = parts.Count > 0 ? " on " + string.Join(", ", parts) : "";
        string number = effect switch
        {
            "Detour" => $" ({(play.Number > 0 ? "+" : "−")}{Math.Abs(play.Number)} space)",
            "Shortcut" => $": move {play.Number} space{(play.Number == 1 ? "" : "s")}",
            "DiceDuel" => $" for {Money(play.Number)}",
            _ => play.Number != 0 ? $" ({play.Number})" : "",
        };
        return on + number;
    }

    /// <summary>The board space a player would land on after moving this many spaces.</summary>
    public static SpaceInfo LandingSpace(PlayerView view, int playerId, int spaces) =>
        view.Spaces[(view.Player(playerId).Position + spaces) % view.Spaces.Count];

    /// <summary>A short marker for a building level on the board: 1-4 houses, H for hotel, S for skyscraper.</summary>
    public static string LevelMarker(int level) => level switch
    {
        0 => "",
        <= 4 => $"⌂{level}",
        5 => "H",
        _ => "S",
    };

    public static string ZoningName(ZoningLevel level) => level switch
    {
        ZoningLevel.HighRise => "High-rise",
        _ => level.ToString(),
    };
}
