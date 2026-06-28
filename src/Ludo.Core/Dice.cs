using System;

namespace Ludo.Core
{
    /// <summary>
    /// Dice source. In production the SERVER owns this (CSPRNG, audited) — clients never roll.
    /// SeededDiceRoller gives deterministic, reproducible games for tests and simulation.
    /// </summary>
    public interface IDiceRoller
    {
        int Roll(); // 1..6
    }

    public sealed class SeededDiceRoller : IDiceRoller
    {
        private readonly Random _rng;
        public SeededDiceRoller(int seed) => _rng = new Random(seed);
        public int Roll() => _rng.Next(1, 7);
    }

    /// <summary>Lets tests force exact dice sequences.</summary>
    public sealed class ScriptedDiceRoller : IDiceRoller
    {
        private readonly int[] _values;
        private int _i;
        private readonly int _fallback;
        public ScriptedDiceRoller(int fallback, params int[] values) { _values = values; _fallback = fallback; }
        public int Roll() => _i < _values.Length ? _values[_i++] : _fallback;
    }
}
