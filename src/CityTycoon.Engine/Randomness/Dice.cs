namespace CityTycoon.Engine.Randomness;

/// <summary>
/// Anything that can roll one six-sided die.
///
/// The engine asks this interface for dice instead of making random numbers
/// itself. In a real game it's backed by the seeded generator. In a test it's
/// <see cref="ScriptedDice"/>, so the test decides exactly what gets rolled.
/// Swapping one for the other without changing the engine is called
/// "dependency injection".
/// </summary>
public interface IDiceSource
{
    int RollDie();

    /// <summary>
    /// A whole number from 0 up to (but not including) <paramref name="max"/>. The City Council uses it
    /// as its "roll" (Section 11.3): with 500 Yes and 300 No influence, 0-499 passes and 500-799 fails.
    /// </summary>
    int RollBelow(int max);
}

/// <summary>Real dice: rolls come from the game's seeded random generator.</summary>
public sealed class RngDiceSource(Pcg32 rng) : IDiceSource
{
    public int RollDie() => rng.NextInt(1, 6);

    public int RollBelow(int max) => rng.NextInt(0, max - 1);
}

/// <summary>
/// Pre-planned dice for tests: <c>new ScriptedDice(3, 4, 6, 6)</c> means the
/// next rolls are 3+4, then 6+6.
/// </summary>
public sealed class ScriptedDice : IDiceSource
{
    private readonly Queue<int> rolls = new();

    public ScriptedDice(params int[] rolls) => Add(rolls);

    /// <summary>Adds more rolls to the end of the script.</summary>
    public void Add(params int[] values)
    {
        foreach (int value in values)
        {
            if (value is < 1 or > 6)
                throw new ArgumentOutOfRangeException(nameof(values), $"A die can't roll {value}.");
            rolls.Enqueue(value);
        }
    }

    public int Remaining => rolls.Count;

    // City Council rolls are scripted separately so they don't disturb the dice. With none scripted,
    // a plain seeded generator answers (the same every time, so tests stay repeatable).
    private readonly Queue<int> councilRolls = new();
    private readonly Pcg32 fallback = new(7);

    /// <summary>Scripts the next City Council roll(s): 0 always passes (if Yes has any influence), a huge number always fails.</summary>
    public void AddCouncilRoll(params int[] values)
    {
        foreach (int value in values)
            councilRolls.Enqueue(value);
    }

    public int RollBelow(int max) => councilRolls.Count > 0 ? Math.Min(councilRolls.Dequeue(), max - 1) : fallback.NextInt(0, max - 1);

    public int RollDie() => rolls.Count > 0
        ? rolls.Dequeue()
        : throw new InvalidOperationException("The test's ScriptedDice ran out of rolls. Add more with Add().");
}
