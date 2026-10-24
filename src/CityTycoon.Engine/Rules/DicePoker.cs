using CityTycoon.Engine.Content;

namespace CityTycoon.Engine.Rules;

/// <summary>The hands in Dice Poker, weakest first, so a bigger number is a better hand.</summary>
public enum PokerHand { Nothing, Pair, TwoPair, ThreeOfAKind, Straight, FullHouse, FourOfAKind, FiveOfAKind }

/// <summary>
/// Dice Poker at the Casino (Section 15.3, decision #164): 5 dice make a hand, like a poker hand.
/// This class only scores and compares hands; the Casino and Showdown steps do the rolling.
/// </summary>
public static class DicePoker
{
    public const int DiceCount = 5;

    /// <summary>What hand these 5 dice make.</summary>
    public static PokerHand Score(IReadOnlyList<int> dice)
    {
        // How many of each face, biggest group first. For 4 4 4 2 2 this is [3, 2].
        var groups = dice.GroupBy(d => d).Select(g => g.Count()).OrderByDescending(n => n).ToList();
        if (groups[0] == 5)
            return PokerHand.FiveOfAKind;
        if (groups[0] == 4)
            return PokerHand.FourOfAKind;
        if (groups[0] == 3 && groups[1] == 2)
            return PokerHand.FullHouse;
        if (groups.Count == 5 && dice.Max() - dice.Min() == 4)
            return PokerHand.Straight;   // five different faces in a row: 1-5 or 2-6
        if (groups[0] == 3)
            return PokerHand.ThreeOfAKind;
        if (groups[0] == 2 && groups[1] == 2)
            return PokerHand.TwoPair;
        return groups[0] == 2 ? PokerHand.Pair : PokerHand.Nothing;
    }

    /// <summary>
    /// Which of two sets of dice is the better hand, for a Showdown: more than 0 means <paramref name="a"/> wins,
    /// less than 0 means <paramref name="b"/> wins, 0 is an exact tie.
    /// The hand counts first. Between the same kind of hand, the dice in the biggest group decide
    /// (three 5s beat three 2s), then the next group, then the single dice, highest first.
    /// </summary>
    public static int Compare(IReadOnlyList<int> a, IReadOnlyList<int> b)
    {
        int byHand = Score(a).CompareTo(Score(b));
        if (byHand != 0)
            return byHand;
        int[] faceA = TieOrder(a), faceB = TieOrder(b);
        for (int i = 0; i < faceA.Length && i < faceB.Length; i++)
            if (faceA[i] != faceB[i])
                return faceA[i].CompareTo(faceB[i]);
        return 0;
    }

    // Faces in the order they settle a tie: biggest group first, higher face first within the same size.
    private static int[] TieOrder(IReadOnlyList<int> dice) =>
        dice.GroupBy(d => d).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).Select(g => g.Key).ToArray();

    /// <summary>How much this hand pays back for every $100 riding on it (the stake included). 0 loses it all.</summary>
    public static int PayPercent(RulesConfig rules, PokerHand hand) => PayPercent(rules.CasinoPays, hand);

    public static int PayPercent(CasinoPayTable pays, PokerHand hand) => hand switch
    {
        PokerHand.FiveOfAKind => pays.FiveOfAKind,
        PokerHand.FourOfAKind => pays.FourOfAKind,
        PokerHand.FullHouse => pays.FullHouse,
        PokerHand.Straight => pays.Straight,
        PokerHand.ThreeOfAKind => pays.ThreeOfAKind,
        PokerHand.TwoPair => pays.TwoPair,
        PokerHand.Pair => pays.Pair,
        _ => 0,
    };

    /// <summary>The hand's name in a sentence: "a full house", "two pair".</summary>
    public static string Describe(PokerHand hand) => hand switch
    {
        PokerHand.FiveOfAKind => "five of a kind",
        PokerHand.FourOfAKind => "four of a kind",
        PokerHand.FullHouse => "a full house",
        PokerHand.Straight => "a straight",
        PokerHand.ThreeOfAKind => "three of a kind",
        PokerHand.TwoPair => "two pair",
        PokerHand.Pair => "a pair",
        _ => "nothing",
    };
}
