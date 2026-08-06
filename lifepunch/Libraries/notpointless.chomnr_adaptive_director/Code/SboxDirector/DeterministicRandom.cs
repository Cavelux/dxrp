namespace SboxDirector;

public readonly record struct RandomState(ulong S0, ulong S1);

public sealed class DeterministicRandom
{
    ulong _s0, _s1;
    public DeterministicRandom(int seed)
    {
        var x = unchecked((ulong)(uint)seed) + 0x9E3779B97F4A7C15UL;
        _s0 = SplitMix(ref x); _s1 = SplitMix(ref x);
        if ((_s0 | _s1) == 0) _s1 = 1;
    }
    public DeterministicRandom(RandomState state) { _s0 = state.S0; _s1 = state.S1; if ((_s0 | _s1) == 0) throw new ArgumentException("RNG state cannot be all zero."); }
    public RandomState State => new(_s0, _s1);
    public ulong NextUInt64() { var x = _s0; var y = _s1; _s0 = y; x ^= x << 23; _s1 = x ^ y ^ (x >> 17) ^ (y >> 26); return _s1 + y; }
    public float NextFloat() => (NextUInt64() >> 40) * (1f / 16777216f);
    public int NextInt(int exclusiveMaximum) { if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum)); return (int)(NextUInt64() % (uint)exclusiveMaximum); }
    static ulong SplitMix(ref ulong x) { x += 0x9E3779B97F4A7C15UL; var z = x; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); }
}
