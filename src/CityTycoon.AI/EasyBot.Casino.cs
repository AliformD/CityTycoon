using System.Collections.Concurrent;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.Views;

namespace CityTycoon.AI;

/// <summary>How well a bot plays Dice Poker at the Casino (decision #164).</summary>
public enum PokerSkill
{
    /// <summary>Keeps its biggest group of matching dice, like most people's first try. Gets back about 84 cents per dollar.</summary>
    KeepBiggestGroup,
    /// <summary>Also keeps two pair, and goes for a straight when it has 4 in a row. About 95 cents.</summary>
    ChaseStraights,
    /// <summary>Works out the best keep every time. About 98 cents.</summary>
    Perfect,
}

// The Casino: Dice Poker and the Showdown (Section 15.3, decision #164).
public partial class EasyBot
{
    protected virtual PokerSkill CasinoSkill => PokerSkill.KeepBiggestGroup;

    /// <summary>
    /// Own money on a Casino hand. Even perfect play loses about 2 cents per dollar, so a bot only adds money when it is
    /// desperate (far behind near the end, when a gamble is its best hope). Otherwise it plays the free chip alone.
    /// </summary>
    private GameAction CasinoStake(PlayerView view, PendingDecision decision)
    {
        int bet = IsDesperate(view) ? Math.Min(decision.MaxBet, view.Me.Cash / 3) / 10 * 10 : 0;
        return new PlayDicePoker(bet);
    }

    /// <summary>Which dice to keep. Against a Showdown hand, the Perfect bot plays to beat it; otherwise for the pay table.</summary>
    private GameAction KeepDiceChoice(PlayerView view, PendingDecision decision)
    {
        int[] dice = [.. decision.Dice!];
        if (decision.Showdown is { } showdown && showdown.OpponentId == view.ViewerId && CasinoSkill == PokerSkill.Perfect)
            return new KeepDice(BestKeepToBeat(dice, [.. showdown.ChallengerDice]));
        return new KeepDice(Keep(dice, CasinoSkill));
    }

    /// <summary>
    /// After a Casino hand (or with the Showdown card): call the Showdown when it is worth it.
    /// The bot works out the other player's best answer: they fold when their chance to beat the hand is too low.
    /// </summary>
    private GameAction ShowdownCall(PlayerView view, PendingDecision decision, IReadOnlyList<GameAction> legalActions)
    {
        var calls = legalActions.OfType<CallShowdown>().ToList();
        if (calls.Count == 0)
            return new NoThanks();
        // The richest player has the most to lose, and the stake is the same for anyone with enough cash.
        CallShowdown call = calls.MaxBy(c => view.Player(c.OpponentId).NetWorth)!;
        int[] mine = [.. decision.Dice!];
        if (CasinoSkill == PokerSkill.KeepBiggestGroup)
            return DicePoker.Score(mine) >= PokerHand.FullHouse ? call : new NoThanks();

        int stake = Math.Min(content.Rules.ShowdownStake, Math.Min(view.Me.Cash, view.Player(call.OpponentId).Cash));
        double beat = ChanceToBeat(mine);
        return ShowdownValue(beat, stake, Math.Min(content.Rules.ShowdownFoldPayment, stake)) > 0 ? call : new NoThanks();
    }

    /// <summary>What a Showdown is worth to the challenger, if the other player answers well.</summary>
    private static double ShowdownValue(double theyBeatIt, int stake, int fold) =>
        PlaysOn(theyBeatIt, stake, fold) ? stake * (1 - 2 * theyBeatIt) : fold;

    /// <summary>Playing on is right when its average result, stake x (2 x chance - 1), beats the fold payment's loss.</summary>
    private static bool PlaysOn(double chanceToBeat, int stake, int fold) => stake * (2 * chanceToBeat - 1) > -fold;

    /// <summary>Fold or play when called to a Showdown.</summary>
    private GameAction ShowdownAnswer(PlayerView view, PendingDecision decision)
    {
        ShowdownChallenge showdown = decision.Showdown!;
        if (CasinoSkill == PokerSkill.KeepBiggestGroup)
            return DicePoker.Score(showdown.ChallengerDice) <= PokerHand.TwoPair ? new PlayShowdown() : new FoldShowdown();
        return PlaysOn(ChanceToBeat([.. showdown.ChallengerDice]), showdown.Stake, showdown.FoldPayment) ? new PlayShowdown() : new FoldShowdown();
    }

    /// <summary>The Casino's worth when planning a move, in dollars: the free chip times what this bot's play gets back.</summary>
    protected double CasinoLandingValue(PlayerView view) =>
        content.Rules.CasinoFreeChip * (100 + CharacterModifier(view, view.ViewerId, "CasinoChipBonus")) / 100.0
            * Returns.GetOrAdd((content.Rules.CasinoPays, CasinoSkill), key => ReturnPerDollar(key.Item1, key.Item2));

    // ----- Working out the odds -----
    // Rolling n dice is listed as each possible set of faces (sorted, ignoring order) with its chance,
    // so 5 dice is 252 sets instead of 7,776 rolls. Everything is cached, since the numbers never change.

    private static readonly ConcurrentDictionary<int, (int[] Faces, double Chance)[]> Rolls = new();
    private static readonly ConcurrentDictionary<(CasinoPayTable, string), double> KeepValues = new();
    private static readonly ConcurrentDictionary<(CasinoPayTable, PokerSkill), double> Returns = new();
    private static readonly ConcurrentDictionary<string, double> BeatChances = new();

    private static (int[] Faces, double Chance)[] RollsOf(int n) => Rolls.GetOrAdd(n, count =>
    {
        var result = new List<(int[], double)>();
        void Build(List<int> faces, int from)
        {
            if (faces.Count == count)
            {
                // The chance of these faces in any order: count! / (each face's repeats)! / 6^count.
                double ways = Factorial(count);
                foreach (var g in faces.GroupBy(f => f))
                    ways /= Factorial(g.Count());
                result.Add((faces.ToArray(), ways / Math.Pow(6, count)));
                return;
            }
            for (int f = from; f <= 6; f++)
            {
                faces.Add(f);
                Build(faces, f);
                faces.RemoveAt(faces.Count - 1);
            }
        }
        Build([], 1);
        return [.. result];
    });

    private static double Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);

    /// <summary>Each different set of positions to keep (keeping one 4 or the other is the same thing, so only one is listed).</summary>
    private static IEnumerable<int[]> Keeps(int[] dice)
    {
        var seen = new HashSet<string>();
        for (int mask = 0; mask < 1 << dice.Length; mask++)
        {
            int[] keep = [.. Enumerable.Range(0, dice.Length).Where(i => (mask >> i & 1) == 1)];
            if (seen.Add(Key(keep.Select(i => dice[i]))))
                yield return keep;
        }
    }

    private static string Key(IEnumerable<int> faces) => string.Concat(faces.Order());

    /// <summary>The pay table's average return per dollar after keeping these faces and rolling the rest once.</summary>
    private static double KeepValue(CasinoPayTable pays, int[] kept) => KeepValues.GetOrAdd((pays, Key(kept)), _ =>
        RollsOf(DicePoker.DiceCount - kept.Length).Sum(r => r.Chance * PayPerDollar(pays, [.. kept, .. r.Faces])));

    private static double PayPerDollar(CasinoPayTable pays, int[] dice) =>
        DicePoker.PayPercent(pays, DicePoker.Score(dice)) / 100.0;

    /// <summary>Which positions to keep, for the pay table, at this skill.</summary>
    private int[] Keep(int[] dice, PokerSkill skill)
    {
        CasinoPayTable pays = content.Rules.CasinoPays;
        if (skill == PokerSkill.Perfect)
            return Keeps(dice).MaxBy(k => KeepValue(pays, [.. k.Select(i => dice[i])]))!;

        PokerHand hand = DicePoker.Score(dice);
        if (hand is PokerHand.FiveOfAKind or PokerHand.FullHouse or PokerHand.Straight)
            return [0, 1, 2, 3, 4];
        if (skill == PokerSkill.ChaseStraights)
        {
            if (hand == PokerHand.TwoPair)
                return [.. Positions(dice, f => dice.Count(d => d == f) == 2)];
            // Four in a row (1-2-3-4, 2-3-4-5 or 3-4-5-6) with no three of a kind: keep one of each and go for the straight.
            for (int low = 1; low <= 3; low++)
            {
                int[] run = [low, low + 1, low + 2, low + 3];
                if (hand < PokerHand.ThreeOfAKind && run.All(dice.Contains))
                    return [.. run.Select(f => Array.IndexOf(dice, f))];
            }
        }
        // The biggest group of matching dice, the higher face if two are the same size.
        var biggest = dice.GroupBy(d => d).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First();
        return biggest.Count() >= 2 ? [.. Positions(dice, f => f == biggest.Key)] : [];
    }

    private static IEnumerable<int> Positions(int[] dice, Func<int, bool> face) =>
        Enumerable.Range(0, dice.Length).Where(i => face(dice[i]));

    /// <summary>What a whole hand from the free chip gets back per dollar at this skill: the first roll, then its keep.</summary>
    private double ReturnPerDollar(CasinoPayTable pays, PokerSkill skill) =>
        RollsOf(DicePoker.DiceCount).Sum(r => r.Chance * KeepValue(pays, [.. Keep(r.Faces, skill).Select(i => r.Faces[i])]));

    // ----- Beating a Showdown hand -----

    /// <summary>The keep that gives the best chance of a strictly better hand than <paramref name="target"/>.</summary>
    private static int[] BestKeepToBeat(int[] dice, int[] target) =>
        Keeps(dice).MaxBy(k => ChanceToBeatKeeping([.. k.Select(i => dice[i])], target))!;

    private static double ChanceToBeatKeeping(int[] kept, int[] target)
    {
        int toBeat = Strength(target);
        return RollsOf(DicePoker.DiceCount - kept.Length).Sum(r => Strength([.. kept, .. r.Faces]) > toBeat ? r.Chance : 0);
    }

    /// <summary>The chance that a player who plays well beats this hand: a fresh roll of 5, then the best keep and reroll.</summary>
    protected static double ChanceToBeat(int[] target) => BeatChances.GetOrAdd(Key(target), _ =>
        RollsOf(DicePoker.DiceCount).Sum(r => r.Chance * Keeps(r.Faces).Max(k => ChanceToBeatKeeping([.. k.Select(i => r.Faces[i])], target))));

    /// <summary>
    /// Every hand of 5 dice, numbered weakest to strongest (equal hands get the same number), so comparing two hands
    /// is comparing two numbers. A hand is looked up by its faces in order, as one number: 1 1 2 3 4 is 11234.
    /// </summary>
    private static readonly Lazy<Dictionary<int, int>> Strengths = new(() =>
    {
        var hands = RollsOf(DicePoker.DiceCount).Select(r => r.Faces).ToList();
        hands.Sort((a, b) => DicePoker.Compare(a, b));
        var strengths = new Dictionary<int, int>();
        int rank = 0;
        for (int i = 0; i < hands.Count; i++)
        {
            if (i > 0 && DicePoker.Compare(hands[i - 1], hands[i]) != 0)
                rank++;
            strengths[HandNumber(hands[i])] = rank;
        }
        return strengths;
    });

    private static int HandNumber(int[] dice)
    {
        int[] sorted = [.. dice];
        Array.Sort(sorted);
        return sorted.Aggregate(0, (n, d) => n * 10 + d);
    }

    private static int Strength(int[] dice) => Strengths.Value[HandNumber(dice)];
}
