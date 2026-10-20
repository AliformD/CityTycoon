using CityTycoon.Engine.Content;
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
}
