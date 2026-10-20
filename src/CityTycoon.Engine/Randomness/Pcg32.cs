namespace CityTycoon.Engine.Randomness;

/// <summary>
/// A small, fast random number generator called PCG32 (see pcg-random.org).
///
/// Why not use C#'s built-in <see cref="System.Random"/>? Its exact results are
/// allowed to change between .NET versions. A game must replay identically
/// everywhere: the same seed and the same choices must always give the same
/// game (Section 20.5). Writing our own generator guarantees that, and its two
/// numbers of state can be saved with the game.
/// </summary>
public sealed class Pcg32
{
    private const ulong Multiplier = 6364136223846793005UL;

    /// <summary>The generator's current position. Saved with the game.</summary>
    public ulong State { get; set; }

    /// <summary>Which of the generator's many "streams" this is. Must be odd.</summary>
    public ulong Increment { get; set; } = 1;

    /// <summary>Used when loading a saved game.</summary>
    public Pcg32() { }

    public Pcg32(ulong seed, ulong stream = 54)
    {
        // This is the official PCG32 seeding recipe.
        State = 0;
        Increment = (stream << 1) | 1;
        NextUInt();
        State += seed;
        NextUInt();
    }

    /// <summary>A random whole number from 0 to 4,294,967,295.</summary>
    public uint NextUInt()
    {
        ulong old = State;
        // "unchecked" tells C# it's fine for this multiplication to wrap around.
        State = unchecked(old * Multiplier + Increment);
        uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        int rotate = (int)(old >> 59);
        return (xorShifted >> rotate) | (xorShifted << (-rotate & 31));
    }

    /// <summary>A random whole number from 0 up to (but not including) <paramref name="maxExclusive"/>.</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Must be at least 1.");

        // Simply using "NextUInt() % max" would make small numbers slightly more
        // likely. Throwing away the few values that cause that keeps it fair.
        uint bound = (uint)maxExclusive;
        uint threshold = (0u - bound) % bound;
        while (true)
        {
            uint r = NextUInt();
            if (r >= threshold)
                return (int)(r % bound);
        }
    }

    /// <summary>A random whole number from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
    public int NextInt(int min, int max) => min + NextInt(max - min + 1);

    /// <summary>A random fraction from 0 up to (but not including) 1.</summary>
    public double NextDouble() => NextUInt() / 4294967296.0;

    /// <summary>Shuffles a list in place (the Fisher-Yates shuffle: every order is equally likely).</summary>
    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = NextInt(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
