using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Cards;

/// <summary>Section 15.4: every Opportunity card.</summary>
public class OpportunityCardTests
{
    [Fact]
    public void Audit_TargetPaysTenPercentOfTheirCash()
    {
        var game = new TestGame();
        game.GiveCard(0, "audit");
        game.Do(0, new PlayCard("audit", TargetPlayerId: 1));
        Assert.Equal(1350, game.Player(1).Cash);
        Assert.Empty(game.Player(0).Hand);
        Assert.Equal("audit", game.State.OpportunityDeck[^1]);
        // The same question comes back: you can still roll.
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void Insurance_CancelsACardPlayedAgainstYou()
    {
        var game = new TestGame();
        game.GiveCard(0, "audit");
        game.GiveCard(1, "insurance");
        game.Do(0, new PlayCard("audit", TargetPlayerId: 1));

        PendingDecision ask = game.DecisionFor(1)!;
        Assert.Equal(DecisionKind.PlayReaction, ask.Kind);
        Assert.Equal(["audit"], ask.Cards);
        game.Do(1, new PlayCard("insurance"));

        Assert.Equal(1500, game.Player(1).Cash);
        Assert.Contains(game.State.Log, e => e is CardCancelled { CardId: "audit", CancelledById: 1 });
        Assert.Empty(game.Player(0).Hand); // the Audit is used up anyway
    }

    [Fact]
    public void Insurance_YouCanChooseNotToUseIt()
    {
        var game = new TestGame();
        game.GiveCard(0, "audit");
        game.GiveCard(1, "insurance");
        game.Do(0, new PlayCard("audit", TargetPlayerId: 1));
        game.Do(1, new NoThanks());
        Assert.Equal(1350, game.Player(1).Cash);
        Assert.Equal(["insurance"], game.Player(1).Hand);
    }

    [Fact]
    public void RentStrike_PaysHalfTheRent()
    {
        var game = new TestGame();
        game.Give(1, "market-street"); // rent $14
        game.GiveCard(0, "rent-strike");
        game.RollOnto(0, "market-street");

        Assert.Equal(DecisionKind.PlayReaction, game.DecisionFor(0)!.Kind);
        game.Do(0, new PlayCard("rent-strike"));
        Assert.Equal(1493, game.Player(0).Cash);
        Assert.Equal(1507, game.Player(1).Cash);
    }

    [Fact]
    public void RentStrike_TheOwnerCanCancelItWithInsurance()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        game.GiveCard(0, "rent-strike");
        game.GiveCard(1, "insurance");
        game.RollOnto(0, "market-street");
        game.Do(0, new PlayCard("rent-strike"));
        game.Do(1, new PlayCard("insurance"));
        Assert.Equal(1486, game.Player(0).Cash);
    }

    [Fact]
    public void NobodyWithAReactionCard_IsNeverAsked()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        game.GiveCard(0, "fast-track"); // not a reaction card
        game.RollOnto(0, "market-street");
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);
        Assert.Equal(1486, game.Player(0).Cash);
    }

    [Fact]
    public void Detour_MovesAnotherPlayersLandingSpot()
    {
        var game = new TestGame();
        game.GiveCard(1, "detour");
        game.PlaceBefore(0, "jail", 5);
        game.Roll(0, 1, 4);

        PendingDecision ask = game.DecisionFor(1)!;
        Assert.Equal(DecisionKind.PlayReaction, ask.Kind);
        Assert.Equal(0, ask.OtherPlayerId);
        Assert.Equal(5, ask.Spaces);
        game.Do(1, new PlayCard("detour", TargetPlayerId: 0, Number: -1));
        Assert.Equal(game.State.IndexOf("jail") - 1, game.Player(0).Position);
    }

    [Fact]
    public void Detour_TheMoverCanCancelItWithInsurance()
    {
        var game = new TestGame();
        game.GiveCard(1, "detour");
        game.GiveCard(0, "insurance");
        game.PlaceBefore(0, "jail", 5);
        game.Roll(0, 1, 4);
        game.Do(1, new PlayCard("detour", TargetPlayerId: 0, Number: 1));
        game.Do(0, new PlayCard("insurance"));
        Assert.Equal(game.State.IndexOf("jail"), game.Player(0).Position);
    }

    [Fact]
    public void Detour_CancelsTheNudge_AndMovesFromTheRoll()
    {
        var game = new TestGame();
        game.GiveCard(1, "detour");
        game.PlaceBefore(0, "jail", 7);
        game.Dice.Add(3, 4);
        game.Do(0, new RollDice());
        game.Do(0, new Nudge(2)); // aiming for 9 spaces

        // Rolled 7, so a Detour lands on 6 or 8: 3 or 1 spaces back from where the Nudge aimed.
        Assert.Equal([new NoThanks(), new PlayCard("detour", 0, null, -3), new PlayCard("detour", 0, null, -1)], game.Engine.GetLegalActions(1));
        game.Do(1, new PlayCard("detour", TargetPlayerId: 0, Number: -3));
        Assert.Equal(game.State.IndexOf("jail") - 1, game.Player(0).Position);
        Assert.Equal(0, game.Player(0).Momentum); // both tokens stay spent
        Assert.Contains(game.State.Log, e => e is Detoured { Spaces: -3, CancelledNudge: true });
    }

    [Fact]
    public void Detour_CantStopAMoveDead()
    {
        var game = new TestGame();
        game.GiveCard(1, "detour");
        game.Player(0).Momentum = 2;
        game.PlaceBefore(0, "jail", 2);
        game.Dice.Add(1, 2);
        game.Do(0, new RollDice());
        game.Do(0, new Nudge(-2)); // moving just 1 space
        // Rolled 3: a Detour lands on 2 or 4, never 0.
        Assert.Equal([new NoThanks(), new PlayCard("detour", 0, null, 1), new PlayCard("detour", 0, null, 3)], game.Engine.GetLegalActions(1));
    }

    [Fact]
    public void Shortcut_MovesInsteadOfRolling_AndIsNeverDoubles()
    {
        var game = new TestGame();
        game.GiveCard(0, "shortcut");
        game.PlaceBefore(0, "jail", 3);
        Assert.DoesNotContain(new PlayCard("shortcut", Number: 7), game.Engine.GetLegalActions(0));
        game.Do(0, new PlayCard("shortcut", Number: 3));
        Assert.Equal(game.State.IndexOf("jail"), game.Player(0).Position);
        game.EndTurn(0);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(1)!.Kind);
    }

    [Fact]
    public void Shortcut_CollectsGoIfItPassesIt()
    {
        var game = new TestGame();
        game.GiveCard(0, "shortcut");
        game.PlaceBefore(0, "go", 1);
        game.Do(0, new PlayCard("shortcut", Number: 1));
        Assert.Equal(1700, game.Player(0).Cash);
    }

    [Fact]
    public void Shortcut_CantBePlayedAfterMoving()
    {
        var game = new TestGame();
        game.GiveCard(0, "shortcut");
        game.SkipTurnWithoutEnding(0);
        Assert.DoesNotContain(game.Engine.GetLegalActions(0), a => a is PlayCard);
    }

    [Fact]
    public void HostileBid_ForcesAnAuction_AndTheWinnerPaysTheOwner()
    {
        var game = new TestGame(players: 3);
        game.Give(1, "market-street"); // assessed $180
        game.GiveCard(0, "hostile-bid");
        game.Do(0, new PlayCard("hostile-bid", TargetSpaceId: "market-street"));

        Assert.Equal(180, game.DecisionFor(0)!.MinBid); // 100% of assessed value
        Assert.Equal(0, game.DecisionFor(0)!.MaxDownPaymentBid);
        game.Do(0, new PlaceBid(200));
        game.Do(1, new PassBid());
        game.Do(2, new PassBid());

        Assert.Equal(0, game.Property("market-street").OwnerId);
        Assert.Equal(1300, game.Player(0).Cash);
        Assert.Equal(1700, game.Player(1).Cash);
    }

    [Fact]
    public void HostileBid_IfTheOwnerWins_TheyPayTheBank()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        game.GiveCard(0, "hostile-bid");
        game.Do(0, new PlayCard("hostile-bid", TargetSpaceId: "market-street"));
        game.Do(0, new PlaceBid(180));
        game.Do(1, new PlaceBid(190));
        Assert.Equal(1, game.Property("market-street").OwnerId);
        Assert.Equal(1310, game.Player(1).Cash);
        Assert.Equal(1500, game.Player(0).Cash);
    }

    [Fact]
    public void HostileBid_WithNoBids_TheOwnerKeepsIt()
    {
        var game = new TestGame();
        game.Give(1, "market-street");
        game.GiveCard(0, "hostile-bid");
        game.Do(0, new PlayCard("hostile-bid", TargetSpaceId: "market-street"));
        game.EveryonePasses();
        Assert.Equal(1, game.Property("market-street").OwnerId);
        Assert.Contains(game.State.Log, e => e is OwnerKeptProperty { OwnerId: 1 });
    }

    [Fact]
    public void HostileBid_CantTargetFullGroupsOrBuildings()
    {
        var game = new TestGame();
        game.Give(1, "mill-street", "cobble-lane"); // all of Brown
        game.Give(1, "elm-avenue");
        game.Property("elm-avenue").Level = 1;
        game.Give(1, "pier-road");
        game.GiveCard(0, "hostile-bid");
        var targets = game.Engine.GetLegalActions(0).OfType<PlayCard>().Select(p => p.TargetSpaceId).ToList();
        Assert.Equal(["pier-road"], targets);
    }

    [Fact]
    public void ConstructionPermit_TwoMoreLevels_AtAQuarterOff()
    {
        var game = new TestGame();
        game.Give(0, "mill-street", "cobble-lane");
        game.GiveCard(0, "construction-permit");
        game.Do(0, new PlayCard("construction-permit"));
        foreach (string id in new[] { "mill-street", "cobble-lane", "mill-street", "cobble-lane", "mill-street" })
            game.Do(0, new Build(id));
        Assert.Equal(5, game.Player(0).BuildsThisTurn);
        Assert.Equal(1500 - 5 * 35, game.Player(0).Cash); // $50 less 25% is $37.50, rounded down to $35
        Assert.NotNull(game.Engine.WhyIllegal(0, new Build("cobble-lane")));
    }

    [Fact]
    public void GetOutOfJailFree_LeavesJail()
    {
        var game = new TestGame();
        game.GiveCard(1, "get-out-of-jail-free", "audit");
        game.JailThenStartTheirTurn(1);
        // In Jail, only Get Out of Jail Free can be played.
        Assert.Equal([new PayBail(), new RollForDoubles(), new PlayCard("get-out-of-jail-free")], game.MainActions(1));
        game.Do(1, new PlayCard("get-out-of-jail-free"));
        Assert.False(game.Player(1).InJail);
        Assert.Equal(1500, game.Player(1).Cash);
        Assert.Equal(DecisionKind.ChooseMoveMethod, game.DecisionFor(1)!.Kind);
    }

    [Fact]
    public void GetOutOfJailFree_SkipsTheHospitalRecoveryTurn()
    {
        var game = new TestGame();
        game.GiveCard(1, "get-out-of-jail-free");
        game.Player(1).IsRecovering = true;
        game.SkipTurn(0);
        game.Do(1, new PlayCard("get-out-of-jail-free"));
        game.PlaceBefore(1, "jail", 5);
        game.Roll(1, 1, 4); // 2 dice again
        Assert.Equal(game.State.IndexOf("jail"), game.Player(1).Position);
    }

    [Fact]
    public void GetOutOfJailFree_CantBePlayedOnANormalTurn()
    {
        var game = new TestGame();
        game.GiveCard(0, "get-out-of-jail-free");
        Assert.DoesNotContain(game.Engine.GetLegalActions(0), a => a is PlayCard);
    }

    [Fact]
    public void DiceDuelCard_ChallengesFromAnywhere()
    {
        var game = new TestGame();
        game.GiveCard(0, "dice-duel");
        game.Dice.Add(6, 6, 1, 1);
        game.Do(0, new PlayCard("dice-duel", TargetPlayerId: 1, Number: 300));
        Assert.Equal(DecisionKind.RespondToDuel, game.DecisionFor(1)!.Kind);
        game.Do(1, new AcceptDuel());
        Assert.Equal(1800, game.Player(0).Cash);
        Assert.Equal(1200, game.Player(1).Cash);
    }

    [Fact]
    public void DiceDuelCard_StakeCantBeMoreThanTheSmallerCashPile()
    {
        var game = new TestGame();
        game.GiveCard(0, "dice-duel");
        game.Player(1).Cash = 100;
        Assert.False(game.Engine.Submit(0, new PlayCard("dice-duel", TargetPlayerId: 1, Number: 101)).Accepted);
    }

    [Fact]
    public void TipOff_RearrangesTheTopEventCards_Secretly()
    {
        var game = new TestGame();
        game.GiveCard(0, "tip-off");
        game.StackEvents("tax-refund", "fire", "lottery");
        game.Do(0, new PlayCard("tip-off"));

        PendingDecision ask = game.DecisionFor(0)!;
        Assert.Equal(DecisionKind.ArrangeEvents, ask.Kind);
        Assert.Equal(["tax-refund", "fire", "lottery"], ask.Cards);
        Assert.Null(game.Engine.GetView(1).MyDecision);

        game.Do(0, new ArrangeCards([2, 0, 1]));
        Assert.Equal(["lottery", "tax-refund", "fire"], game.State.EventDeck.Take(3));
    }

    [Fact]
    public void TipOff_RejectsAnOrderThatIsntAShuffleOfTheCards()
    {
        var game = new TestGame();
        game.GiveCard(0, "tip-off");
        game.Do(0, new PlayCard("tip-off"));
        Assert.False(game.Engine.Submit(0, new ArrangeCards([0, 0, 1])).Accepted);
    }

    [Fact]
    public void FastTrack_GainsTwoTokens()
    {
        var game = new TestGame();
        game.Player(0).Momentum = 0;
        game.GiveCard(0, "fast-track");
        game.Do(0, new PlayCard("fast-track"));
        Assert.Equal(2, game.Player(0).Momentum);
    }

    [Fact]
    public void GrandOpening_DoublesOnePropertysRent_UntilYourNextTurn()
    {
        var game = new TestGame();
        game.Give(0, "market-street");
        game.GiveCard(0, "grand-opening");
        game.Do(0, new PlayCard("grand-opening", TargetSpaceId: "market-street"));
        game.SkipTurnFromHere(0);

        game.RollOnto(1, "market-street");
        Assert.Equal(1472, game.Player(1).Cash); // $14 × 2
        game.EndTurn(1);
        Assert.Empty(game.State.PropertyBoosts);
    }

    [Fact]
    public void InsideSource_OnlyThePeekerSeesTheTile()
    {
        var game = new TestGame();
        game.GiveCard(0, "inside-source");
        BoardSpace tile = game.State.Board.First(s => s.Kind == CityTycoon.Engine.Content.SpaceKind.GovernmentTile);
        game.Do(0, new PlayCard("inside-source", TargetSpaceId: tile.Id));

        Assert.Equal(tile.TileId, game.Engine.GetView(0).Spaces[tile.Index].TileId);
        Assert.Null(game.Engine.GetView(1).Spaces[tile.Index].TileId);
        Assert.Contains(game.State.Log, e => e is TilePeeked { PlayerId: 0 });
    }

    [Fact]
    public void Lobbyist_CantBePlayedUntilTheCouncilArrives()
    {
        var game = new TestGame();
        game.GiveCard(0, "lobbyist");
        Assert.DoesNotContain(game.Engine.GetLegalActions(0), a => a is PlayCard);
    }

    [Fact]
    public void YouCantPlayCardsYouDontHold()
    {
        var game = new TestGame();
        Assert.False(game.Engine.Submit(0, new PlayCard("fast-track")).Accepted);
    }
}
