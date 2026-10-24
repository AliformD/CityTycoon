using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.Tests.Helpers;

namespace CityTycoon.Engine.Tests.Landmarks;

/// <summary>Section 15.3 and decision #164: Dice Poker at the Casino, and the Showdown.</summary>
public class CasinoTests
{
    private static TestGame AtTheCasino(int players = 2, string[]? characters = null)
    {
        var game = new TestGame(players, characters: characters);
        game.Player(0).Momentum = 0; // no Nudge question
        game.PlaceBefore(0, "casino", 5);
        game.Roll(0, 1, 4);
        Assert.Equal(DecisionKind.CasinoBet, game.DecisionFor(0)!.Kind);
        return game;
    }

    /// <summary>Puts <paramref name="stake"/> on the hand, rolls these 5 dice, and keeps them all.</summary>
    private static void PlayHand(TestGame game, int stake, params int[] dice)
    {
        game.Dice.Add(dice);
        game.Do(0, new PlayDicePoker(stake));
        Assert.Equal(DecisionKind.KeepDice, game.DecisionFor(0)!.Kind);
        game.Do(0, new KeepDice([0, 1, 2, 3, 4]));
    }

    // ----- Dice Poker -----

    [Theory]
    [InlineData(400, 6, 6, 6, 6, 6)]   // five of a kind: 10 times the $40 chip
    [InlineData(120, 4, 4, 1, 4, 4)]   // four of a kind: 3 times
    [InlineData(120, 3, 1, 5, 2, 4)]   // straight 1-5: 3 times
    [InlineData(120, 2, 3, 4, 5, 6)]   // straight 2-6
    [InlineData(80, 3, 2, 3, 2, 3)]    // full house: 2 times
    [InlineData(20, 5, 5, 1, 5, 2)]    // three of a kind: half back
    [InlineData(0, 5, 5, 2, 2, 1)]     // two pair pays nothing
    [InlineData(0, 1, 2, 3, 4, 6)]     // nothing
    public void TheFreeChip_IsPaidByThePayTable(int win, params int[] dice)
    {
        var game = AtTheCasino();
        PlayHand(game, 0, dice);

        Assert.Equal(1500 + win, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is CasinoHandPaid { Chip: 40, Stake: 0 } p && p.Net == win);
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);   // nobody is richer: no Showdown question
    }

    [Theory]
    [InlineData(180, 3, 3, 3, 6, 6)]   // full house: ($40 + $100) x 2 = $280 back, $180 more than the stake
    [InlineData(-30, 3, 3, 3, 6, 1)]   // three of a kind: half of $140 back is $70, $30 less than the stake
    [InlineData(-100, 3, 3, 6, 6, 1)]  // two pair: the stake is lost (the chip was free)
    public void YourOwnMoney_RidesOnTheSameHand(int net, params int[] dice)
    {
        var game = AtTheCasino();
        PlayHand(game, 100, dice);

        Assert.Equal(1500 + net, game.Player(0).Cash);
        Assert.Contains(game.State.Log, e => e is CasinoHandPaid { Stake: 100 } p && p.Net == net);
    }

    [Fact]
    public void KeptDiceStay_AndTheOthersAreRolledOnceMore()
    {
        var game = AtTheCasino();
        game.Dice.Add(1, 5, 1, 6, 2);
        game.Do(0, new PlayDicePoker(0));
        Assert.Equal([1, 5, 1, 6, 2], game.DecisionFor(0)!.Dice);

        game.Dice.Add(1, 1, 4);   // the second, fourth and fifth dice, in that order
        game.Do(0, new KeepDice([2, 0]));

        Assert.Contains(game.State.Log, e => e is PokerDiceRolled { Kept: not null } r && r.Dice.SequenceEqual([1, 1, 1, 1, 4]) && r.Kept.SequenceEqual([0, 2]));
        Assert.Contains(game.State.Log, e => e is CasinoHandPaid { Hand: PokerHand.FourOfAKind, Net: 120 });
    }

    [Fact]
    public void KeepingNothing_RollsAllFiveAgain()
    {
        var game = AtTheCasino();
        game.Dice.Add(1, 2, 3, 4, 6);
        game.Do(0, new PlayDicePoker(0));
        game.Dice.Add(5, 5, 5, 5, 5);
        game.Do(0, new KeepDice([]));
        Assert.Equal(1900, game.Player(0).Cash);
    }

    [Fact]
    public void AKeep_MustNameRealDice_OnceEach()
    {
        var game = AtTheCasino();
        game.Dice.Add(1, 2, 3, 4, 6);
        game.Do(0, new PlayDicePoker(0));
        Assert.False(game.Engine.Submit(0, new KeepDice([5])).Accepted);
        Assert.False(game.Engine.Submit(0, new KeepDice([0, 0])).Accepted);
        Assert.False(game.Engine.Submit(0, new KeepDice([-1])).Accepted);
    }

    [Fact]
    public void ThereIsNoMaximum_ButYouCantStakeMoreThanYouHave()
    {
        var game = AtTheCasino();
        Assert.Equal(1500, game.DecisionFor(0)!.MaxBet);
        Assert.Equal(40, game.DecisionFor(0)!.FreeChip);
        Assert.False(game.Engine.Submit(0, new PlayDicePoker(1501)).Accepted);
        Assert.False(game.Engine.Submit(0, new PlayDicePoker(-1)).Accepted);
        PlayHand(game, 1500, 6, 6, 6, 6, 6);
        Assert.Equal(1500 - 1500 + 15400, game.Player(0).Cash);   // ($40 + $1,500) x 10
    }

    [Fact]
    public void WalkingPast_ChangesNothing()
    {
        var game = AtTheCasino();
        game.Do(0, new NoThanks());
        Assert.Equal(1500, game.Player(0).Cash);
        Assert.DoesNotContain(game.State.Log, e => e is PokerDiceRolled);
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void APlayerWithNoCash_StillPlaysTheFreeChip()
    {
        var game = new TestGame(2);
        game.Player(0).Momentum = 0;
        game.Player(0).Cash = 0;
        game.PlaceBefore(0, "casino", 5);
        game.Dice.Add(1, 4, 2, 2, 2, 3, 3);   // the move, then the hand
        game.Do(0, new RollDice());
        Assert.Equal(DecisionKind.KeepDice, game.DecisionFor(0)!.Kind);
        game.Do(0, new KeepDice([0, 1, 2, 3, 4]));
        Assert.Equal(80, game.Player(0).Cash);
    }

    [Fact]
    public void TheHustler_GetsABiggerFreeChip()
    {
        var game = AtTheCasino(characters: ["hustler"]);
        Assert.Equal(60, game.DecisionFor(0)!.FreeChip);
        PlayHand(game, 0, 3, 3, 3, 2, 2);
        Assert.Equal(1620, game.Player(0).Cash);
    }

    // ----- The Showdown -----

    /// <summary>At the Casino with a full house, and player 1 worth more.</summary>
    private static TestGame ReadyForAShowdown(string? opponentCharacter = null)
    {
        var game = AtTheCasino();
        if (opponentCharacter != null)
            game.SetCharacter(1, opponentCharacter);
        game.Player(1).Cash = 2000;
        PlayHand(game, 0, 4, 4, 4, 2, 2);   // full house, +$80
        Assert.Equal(DecisionKind.CallShowdown, game.DecisionFor(0)!.Kind);
        return game;
    }

    [Fact]
    public void AShowdown_CanOnlyBeCalledOnSomeoneWorthMore()
    {
        var game = new TestGame(3);
        game.Player(0).Momentum = 0;
        game.Player(1).Cash = 2000;
        game.Player(2).Cash = 900;
        game.PlaceBefore(0, "casino", 5);
        game.Roll(0, 1, 4);
        PlayHand(game, 0, 4, 4, 4, 2, 2);

        Assert.Contains(new CallShowdown(1), game.Engine.GetLegalActions(0));
        Assert.False(game.Engine.Submit(0, new CallShowdown(2)).Accepted);
        game.Do(0, new NoThanks());
        Assert.Equal(DecisionKind.PostMoveActions, game.DecisionFor(0)!.Kind);
    }

    [Fact]
    public void TheChallengedPlayer_SeesTheHand_AndMayFold()
    {
        var game = ReadyForAShowdown();
        game.Do(0, new CallShowdown(1));

        var ask = game.DecisionFor(1)!;
        Assert.Equal(DecisionKind.RespondToShowdown, ask.Kind);
        Assert.Equal(100, ask.Showdown!.Stake);
        Assert.Equal(25, ask.Showdown.FoldPayment);
        Assert.Equal([4, 4, 4, 2, 2], ask.Showdown.ChallengerDice);
        Assert.DoesNotContain(new NoThanks(), game.Engine.GetLegalActions(1));   // they can't just say no

        game.Do(1, new FoldShowdown());
        Assert.Equal(1580 + 25, game.Player(0).Cash);
        Assert.Equal(2000 - 25, game.Player(1).Cash);
        Assert.Contains(game.State.Log, e => e is ShowdownFolded { ChallengerId: 0, OpponentId: 1, Amount: 25 });
    }

    [Fact]
    public void PlayingIt_TheBetterHandTakesTheStake()
    {
        var game = ReadyForAShowdown();
        game.Do(0, new CallShowdown(1));
        game.Dice.Add(5, 5, 1, 2, 3);
        game.Do(1, new PlayShowdown());
        Assert.Equal(DecisionKind.KeepDice, game.DecisionFor(1)!.Kind);
        Assert.NotNull(game.DecisionFor(1)!.Showdown);   // the hand to beat is shown while choosing

        game.Dice.Add(5, 5, 1);   // keeps the 5s and rolls three: 5 5 5 5 1
        game.Do(1, new KeepDice([0, 1]));

        Assert.Equal(1580 - 100, game.Player(0).Cash);
        Assert.Equal(2100, game.Player(1).Cash);
        Assert.Contains(game.State.Log, e => e is ShowdownWon { WinnerId: 1, LoserId: 0, Stake: 100, WinnerHand: PokerHand.FourOfAKind, LoserHand: PokerHand.FullHouse });
    }

    [Fact]
    public void PlayingIt_AndLosing_PaysTheChallenger()
    {
        var game = ReadyForAShowdown();
        game.Do(0, new CallShowdown(1));
        game.Dice.Add(1, 2, 3, 4, 6);
        game.Do(1, new PlayShowdown());
        game.Dice.Add(6);
        game.Do(1, new KeepDice([0, 1, 2, 3]));   // 1 2 3 4 6: still nothing

        Assert.Equal(1680, game.Player(0).Cash);
        Assert.Equal(1900, game.Player(1).Cash);
    }

    [Fact]
    public void AnExactTie_PaysNobody()
    {
        var game = ReadyForAShowdown();
        game.Do(0, new CallShowdown(1));
        game.Dice.Add(2, 2, 4, 4, 4);
        game.Do(1, new PlayShowdown());
        game.Do(1, new KeepDice([0, 1, 2, 3, 4]));

        Assert.Equal(1580, game.Player(0).Cash);
        Assert.Equal(2000, game.Player(1).Cash);
        Assert.Contains(game.State.Log, e => e is ShowdownTied);
    }

    [Fact]
    public void TheMogul_WinsATiedShowdown()
    {
        var game = ReadyForAShowdown(opponentCharacter: "mogul");
        game.Do(0, new CallShowdown(1));
        game.Dice.Add(2, 2, 4, 4, 4);
        game.Do(1, new PlayShowdown());
        game.Do(1, new KeepDice([0, 1, 2, 3, 4]));

        Assert.Equal(2100, game.Player(1).Cash);
        Assert.Contains(game.State.Log, e => e is DiceTieWon { PlayerId: 1 });
    }

    [Fact]
    public void TheStake_IsNoMoreThanEitherPlayersCash()
    {
        var game = AtTheCasino();
        game.Give(1, "market-street", "fountain-square", "baker-street");
        game.Player(1).Cash = 60;
        game.Player(0).Cash = 100;
        PlayHand(game, 0, 4, 4, 4, 2, 2);
        game.Do(0, new CallShowdown(1));
        Assert.Equal(60, game.DecisionFor(1)!.Showdown!.Stake);
    }

    // ----- The Showdown card -----

    [Fact]
    public void TheShowdownCard_RollsTheChallengersHandFirst_AndCanTargetAnyone()
    {
        var game = new TestGame(2);
        game.Player(1).Cash = 500;   // poorer: the card doesn't mind
        game.GiveCard(0, "dice-duel");
        game.Dice.Add(6, 6, 1, 2, 3);
        game.Do(0, new PlayCard("dice-duel", TargetPlayerId: 1));
        Assert.Equal(DecisionKind.KeepDice, game.DecisionFor(0)!.Kind);

        game.Dice.Add(6, 6, 6);
        game.Do(0, new KeepDice([0, 1]));
        var ask = game.DecisionFor(1)!;
        Assert.Equal(DecisionKind.RespondToShowdown, ask.Kind);
        Assert.Equal([6, 6, 6, 6, 6], ask.Showdown!.ChallengerDice);

        game.Do(1, new FoldShowdown());
        Assert.Equal(1525, game.Player(0).Cash);
    }

    // ----- Scoring hands -----

    [Theory]
    [InlineData(PokerHand.FiveOfAKind, 2, 2, 2, 2, 2)]
    [InlineData(PokerHand.FourOfAKind, 2, 6, 2, 2, 2)]
    [InlineData(PokerHand.FullHouse, 6, 1, 6, 1, 6)]
    [InlineData(PokerHand.Straight, 5, 4, 3, 2, 1)]
    [InlineData(PokerHand.ThreeOfAKind, 3, 3, 3, 1, 6)]
    [InlineData(PokerHand.TwoPair, 3, 3, 1, 1, 6)]
    [InlineData(PokerHand.Pair, 3, 3, 1, 2, 6)]
    [InlineData(PokerHand.Nothing, 1, 2, 3, 4, 6)]
    [InlineData(PokerHand.Nothing, 1, 3, 4, 5, 6)]
    public void Hands_AreScored(PokerHand hand, params int[] dice) => Assert.Equal(hand, DicePoker.Score(dice));

    [Theory]
    [InlineData(new[] { 3, 3, 3, 5, 5 }, new[] { 2, 2, 2, 6, 6 })]   // the three decide a full house
    [InlineData(new[] { 2, 3, 4, 5, 6 }, new[] { 1, 2, 3, 4, 5 })]   // the high straight wins
    [InlineData(new[] { 4, 4, 6, 2, 1 }, new[] { 4, 4, 5, 3, 2 })]   // same pair: the best single die wins
    [InlineData(new[] { 1, 1, 1, 2, 2 }, new[] { 6, 6, 6, 5, 4 })]   // a full house beats three of a kind
    public void TheBetterHandWins(int[] better, int[] worse)
    {
        Assert.True(DicePoker.Compare(better, worse) > 0);
        Assert.True(DicePoker.Compare(worse, better) < 0);
    }

    [Fact]
    public void TheSameHandInAnyOrder_IsATie() => Assert.Equal(0, DicePoker.Compare([4, 4, 1, 2, 3], [3, 1, 4, 2, 4]));
}
