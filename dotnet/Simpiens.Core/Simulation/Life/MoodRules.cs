using System;
using Simpiens.Cognition.Contracts;

namespace Simpiens.Simulation.Life
{
    /// <summary>How a crawler's mood reads, from breaking to happy (Epic LIFE-1, L1.3, D8). Ordered, so a band can be compared.</summary>
    public enum MoodBand : byte
    {
        None = 0,
        Breaking = 1,
        Low = 2,
        Steady = 3,
        Good = 4,
        Happy = 5,
    }

    /// <summary>A need's or the body's term in mood (Epic LIFE-1, L1.3, D8): which state it reads.</summary>
    public enum MoodTerm : byte
    {
        None = 0,
        Hungry = 1,
        Starving = 2,
        Tired = 3,
        Exhausted = 4,
        Lonely = 5,
        Hurt = 6,
        Downed = 7,
        Comfortable = 8,
    }

    /// <summary>One need term's row: its words, its value to mood and the line it reads (a need's level, or health).</summary>
    public sealed class MoodTermDefinition
    {
        public MoodTermDefinition(MoodTerm id, string words, sbyte value, float line)
        {
            Id = id;
            Words = words;
            Value = value;
            Line = line;
        }

        public MoodTerm Id { get; }
        public string Words { get; }
        public sbyte Value { get; }

        /// <summary>The level it reads: hunger at least, energy or social at most, health under. Unused by downed and comfortable.</summary>
        public float Line { get; }
    }

    /// <summary>
    /// Mood (Epic LIFE-1, L1.3, D8; the epic's A5): a crawler's needs and body (the need terms, a table) plus its live
    /// thoughts, held within ±<see cref="Max"/>. A pure function of what the main thread and cognition both hold (needs,
    /// health, the life record) at a tick, so both read the same mood; derived, never saved. Its bands and terms are L1.3's,
    /// set from the measurement of moods on the gate seeds (docs/life-model.md).
    /// </summary>
    public static class MoodRules
    {
        public const int Max = 100;

        /// <summary>The bands' upper bounds: Breaking at most -35, Low at most -10, Steady under 10, Good under 30, Happy from 30.</summary>
        public const int BreakingAtMost = -35, LowAtMost = -10, GoodFrom = 10, HappyFrom = 30;

        private static readonly MoodTermDefinition[] Terms =
        {
            null,
            new MoodTermDefinition(MoodTerm.Hungry, "Hungry", -6, 60f),
            new MoodTermDefinition(MoodTerm.Starving, "Starving", -15, 80f),
            new MoodTermDefinition(MoodTerm.Tired, "Tired", -5, 30f),
            new MoodTermDefinition(MoodTerm.Exhausted, "Exhausted", -12, 15f),
            new MoodTermDefinition(MoodTerm.Lonely, "Lonely", -6, 25f),
            new MoodTermDefinition(MoodTerm.Hurt, "Hurt", -8, 50f),
            new MoodTermDefinition(MoodTerm.Downed, "Downed", -20, 0f),
            new MoodTermDefinition(MoodTerm.Comfortable, "Comfortable", 4, 0f),
        };

        /// <summary>The row of <paramref name="term"/>, or null.</summary>
        public static MoodTermDefinition Term(MoodTerm term) => (byte)term < Terms.Length ? Terms[(byte)term] : null;

        /// <summary>Every need term, in id order.</summary>
        public static ReadOnlySpan<MoodTermDefinition> AllTerms => Terms.AsSpan(1);

        /// <summary>
        /// The need terms that hold for these needs and this body, at most three (hunger's, energy's, and social's or the body's
        /// when both hold, the body's first), or comfortable when none does. Written into <paramref name="into"/>; returns how many.
        /// </summary>
        public static int TermsOf(in AgentNeeds needs, float health, bool downed, Span<MoodTerm> into)
        {
            int n = 0;
            if (needs.Hunger >= Terms[(int)MoodTerm.Starving].Line) Add(into, ref n, MoodTerm.Starving);
            else if (needs.Hunger >= Terms[(int)MoodTerm.Hungry].Line) Add(into, ref n, MoodTerm.Hungry);
            if (needs.Energy <= Terms[(int)MoodTerm.Exhausted].Line) Add(into, ref n, MoodTerm.Exhausted);
            else if (needs.Energy <= Terms[(int)MoodTerm.Tired].Line) Add(into, ref n, MoodTerm.Tired);
            if (downed) Add(into, ref n, MoodTerm.Downed);
            else if (health < Terms[(int)MoodTerm.Hurt].Line) Add(into, ref n, MoodTerm.Hurt);
            if (needs.Social <= Terms[(int)MoodTerm.Lonely].Line) Add(into, ref n, MoodTerm.Lonely);
            if (n == 0) Add(into, ref n, MoodTerm.Comfortable);
            return n;
        }

        private static void Add(Span<MoodTerm> into, ref int n, MoodTerm term)
        {
            if (n < into.Length) into[n++] = term;
        }

        /// <summary>The need terms' sum for these needs and this body.</summary>
        public static int NeedsOf(in AgentNeeds needs, float health, bool downed)
        {
            Span<MoodTerm> terms = stackalloc MoodTerm[4];
            int n = TermsOf(needs, health, downed, terms);
            int sum = 0;
            for (int i = 0; i < n; i++) sum += Terms[(int)terms[i]].Value;
            return sum;
        }

        /// <summary>The mood: the need terms plus the live thoughts at <paramref name="tick"/>, rounded and held within ±<see cref="Max"/>.</summary>
        public static int Of(in AgentNeeds needs, float health, bool downed, in CrawlerLife life, long tick)
        {
            float mood = NeedsOf(needs, health, downed) + ThoughtRules.Sum(life, tick);
            int rounded = (int)MathF.Round(mood, MidpointRounding.AwayFromZero);
            return rounded < -Max ? -Max : rounded > Max ? Max : rounded;
        }

        /// <summary>The mood of a context: what cognition reads (its needs, health and life record, a copy).</summary>
        public static int Of(in AgentContext context) => Of(context.Needs, context.Health, context.IsDowned, context.Life, context.CurrentTick);

        /// <summary>The band <paramref name="mood"/> reads.</summary>
        public static MoodBand BandOf(int mood) =>
            mood <= BreakingAtMost ? MoodBand.Breaking
            : mood <= LowAtMost ? MoodBand.Low
            : mood < GoodFrom ? MoodBand.Steady
            : mood < HappyFrom ? MoodBand.Good
            : MoodBand.Happy;
    }
}
