namespace Simpiens.Simulation.Items
{
    /// <summary>The bounds a stat is held within once every worn piece has had its say (Epic 10 Part 3, K1.3): a row of <see cref="StatRules"/>.</summary>
    public readonly struct StatRule
    {
        public StatRule(float min, float max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>The least it can be, however much worn pieces take off (a pickaxe can't make mining stop).</summary>
        public float Min { get; }

        /// <summary>The most it can be, however many pieces add to it (no protection stops all of a hazard).</summary>
        public float Max { get; }

        /// <summary><paramref name="value"/> held within the bounds.</summary>
        public float Bound(float value) => value < Min ? Min : value > Max ? Max : value;

        public override string ToString() => $"[{Min:0.##}, {Max:0.##}]";
    }

    /// <summary>
    /// How each stat stacks over every worn piece, whichever slots they are in (Epic 10 Part 3, K1.3): its base, plus every
    /// piece's Add modifiers, times every piece's Multiply modifiers (each at its piece's grade, in the slot table's id
    /// order, so the sum never varies), then held within the stat's bounds here. <see cref="CrawlerStats.Get"/> applies it.
    /// <para>
    /// A hard hat and hazard gear worn together add their resistances, held at <see cref="MaxResistance"/>; two rings of
    /// attack would add theirs. A new stat is a <see cref="StatKind"/> value and, if it needs bounds, a row below; a stat
    /// with no row is unbounded.
    /// </para>
    /// </summary>
    public static class StatRules
    {
        /// <summary>The most of a hazard's harm worn protection can stop, of each resistance and of them together (D47).</summary>
        public const float MaxResistance = 0.9f;

        private static readonly StatRule Unbounded = new StatRule(float.MinValue, float.MaxValue);

        private static readonly StatRule[] ByStat = Build();

        private static StatRule[] Build()
        {
            var rules = new StatRule[256];
            for (int i = 0; i < rules.Length; i++) rules[i] = Unbounded;
            // Protection: from none to the most any protection stops
            rules[(byte)StatKind.HazardResistance] = new StatRule(0f, MaxResistance);
            rules[(byte)StatKind.AirResistance] = new StatRule(0f, MaxResistance);
            rules[(byte)StatKind.RockResistance] = new StatRule(0f, MaxResistance);
            // Work: never stopped altogether (W1.4's floor on mining)
            rules[(byte)StatKind.HarvestSpeed] = new StatRule(0.1f, float.MaxValue);
            rules[(byte)StatKind.MoveSpeed] = new StatRule(0.1f, float.MaxValue);
            // Combat (K0.3): no negative blow, guard, reach or range; a swing takes at least a tick
            rules[(byte)StatKind.Attack] = new StatRule(0f, float.MaxValue);
            rules[(byte)StatKind.Defense] = new StatRule(0f, float.MaxValue);
            rules[(byte)StatKind.Reach] = new StatRule(0f, float.MaxValue);
            rules[(byte)StatKind.AttackInterval] = new StatRule(0.1f, float.MaxValue);
            rules[(byte)StatKind.Range] = new StatRule(0f, float.MaxValue);
            return rules;
        }

        /// <summary>The stat's bounds; unbounded for a stat with no row.</summary>
        public static StatRule Of(StatKind stat) => ByStat[(byte)stat];
    }
}
