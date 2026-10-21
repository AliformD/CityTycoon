namespace CityTycoon.AI.Tests;

/// <summary>Secret bot personalities (decision #144).</summary>
public class PersonalityTests
{
    private static List<BotPersonality> Dealt(int count) =>
        Enumerable.Range(1, count).Select(s => new EasyBot((ulong)s).Personality).ToList();

    [Fact]
    public void EachBotDrawsItsOwn_SoATableCanRepeatOne()
    {
        var dealt = Dealt(200);
        // All three show up, each roughly a third of the time.
        foreach (BotPersonality p in Enum.GetValues<BotPersonality>())
            Assert.InRange(dealt.Count(d => d == p), 40, 100);
        // Some four-bot tables get two (or more) of the same, and some get none of one kind.
        var tables = dealt.Chunk(4).ToList();
        Assert.Contains(tables, t => t.Distinct().Count() < 3);
    }

    [Fact]
    public void TheSameSeed_GivesTheSamePersonality()
    {
        Assert.Equal(new EasyBot(7).Personality, new EasyBot(7).Personality);
    }

    [Fact]
    public void AggressiveBots_PayMore_AndKeepLessCash_ThanReservedOnes()
    {
        var bots = Enumerable.Range(1, 200).Select(s => new EasyBot((ulong)s)).ToList();
        var aggressive = bots.Where(b => b.Personality == BotPersonality.Aggressive).ToList();
        var reserved = bots.Where(b => b.Personality == BotPersonality.Reserved).ToList();
        Assert.True(aggressive.Average(b => b.MaxPriceRatio) > reserved.Average(b => b.MaxPriceRatio));
        Assert.True(aggressive.Average(b => b.SafetyReserve) < reserved.Average(b => b.SafetyReserve));
    }
}
