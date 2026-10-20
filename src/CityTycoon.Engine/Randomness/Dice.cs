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
}

/// <summary>Real dice: rolls come from the game's seeded random generator.</summary>
public sealed class RngDiceSource(Pcg32 rng) : IDiceSource
{
    public int RollDie() => rng.NextInt(1, 6);
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

    public int RollDie() => rolls.Count > 0
        ? rolls.Dequeue()
        : throw new InvalidOperationException("The test's ScriptedDice ran out of rolls. Add more with Add().");
}
