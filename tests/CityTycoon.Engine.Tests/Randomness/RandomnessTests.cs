using CityTycoon.Engine.Randomness;

namespace CityTycoon.Engine.Tests.Randomness;

public class RandomnessTests
{
    [Fact]
    public void Pcg32_MatchesTheOfficialReferenceNumbers()
    {
        // These are the first outputs of the official PCG32 demo for seed 42, stream 54.
        // If they ever change, saved games and replays would break.
        var rng = new Pcg32(42, 54);
        uint[] expected = [0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e];
        Assert.Equal(expected, expected.Select(_ => rng.NextUInt()).ToArray());
    }

    [Fact]
    public void Pcg32_SameSeed_GivesSameNumbers()
    {
        var a = new Pcg32(7);
        var b = new Pcg32(7);
        Assert.Equal(Enumerable.Range(0, 50).Select(_ => a.NextInt(100)), Enumerable.Range(0, 50).Select(_ => b.NextInt(100)));
    }

    [Fact]
    public void RngDice_AlwaysRoll1To6_AndEveryFaceComesUp()
    {
        var dice = new RngDiceSource(new Pcg32(3));
        var rolls = Enumerable.Range(0, 600).Select(_ => dice.RollDie()).ToList();
        Assert.All(rolls, r => Assert.InRange(r, 1, 6));
        Assert.Equal(6, rolls.Distinct().Count());
    }

    [Fact]
    public void ScriptedDice_RollsInTheOrderGiven_AndComplainsWhenEmpty()
    {
        var dice = new ScriptedDice(3, 4, 6);
        Assert.Equal(3, dice.RollDie());
        Assert.Equal(4, dice.RollDie());
        Assert.Equal(6, dice.RollDie());
        Assert.Throws<InvalidOperationException>(() => dice.RollDie());
    }
}
