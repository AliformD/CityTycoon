using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;

namespace CityTycoon.Engine.Tests.Helpers;

/// <summary>Short helpers for City Council tests.</summary>
public static class CouncilHelpers
{
    /// <summary>Makes every district Residential, so a test doesn't depend on which one the seed made Commercial.</summary>
    public static void AllResidential(this TestGame game)
    {
        foreach (string district in game.State.Zoning.Keys.ToList())
            game.State.Zoning[district] = Engine.Content.ZoningLevel.Residential;
    }

    /// <summary>
    /// Calls an Emergency session (player 0 draws the Emergency Session card) with these two ordinances on top of the deck.
    /// The second one is a harmless filler so the setter really has a choice.
    /// </summary>
    public static void Convene(this TestGame game, string ordinanceId, string? other = null)
    {
        other ??= ordinanceId == "stimulus-check" ? "public-works" : "stimulus-check";
        game.State.OrdinanceDeck.InsertRange(0, [ordinanceId, other]);
        game.DrawEvent(0, "emergency-session");
    }

    /// <summary>Convenes, then player 0 picks the ordinance (and district) so the vote is open.</summary>
    public static void ConveneAndPropose(this TestGame game, string ordinanceId, string? district = null, string? other = null)
    {
        game.Convene(ordinanceId, other);
        game.Do(0, new ChooseOrdinance(ordinanceId));
        if (district != null)
            game.Do(0, new ChooseDistrict(district));
    }

    public static ActionResult Vote(this TestGame game, int playerId, VoteChoice choice, int lobby = 0, bool lobbyist = false) =>
        game.Do(playerId, new CastVote(choice, lobby, lobbyist));

    /// <summary>Everybody who still has to vote says Yes (or No) with no lobbying, and the roll is scripted to pass (or fail).</summary>
    public static void EveryoneVotes(this TestGame game, bool pass)
    {
        game.Dice.AddCouncilRoll(pass ? 0 : int.MaxValue);
        foreach (var d in game.Engine.GetPendingDecisions().Where(d => d.Kind == DecisionKind.CouncilVote).ToList())
            game.Vote(d.PlayerId, pass ? VoteChoice.Yes : VoteChoice.No);
    }

    /// <summary>Convene, propose, and pass the ordinance with every player voting Yes.</summary>
    public static void PassOrdinance(this TestGame game, string ordinanceId, string? district = null)
    {
        game.ConveneAndPropose(ordinanceId, district);
        game.EveryoneVotes(pass: true);
    }

    public static CouncilResolved LastResolved(this TestGame game) =>
        game.State.Log.OfType<CouncilResolved>().Last();
}
