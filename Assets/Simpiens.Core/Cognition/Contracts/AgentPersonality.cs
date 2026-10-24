using Simpiens.Simulation;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Five stable traits that make agents respond differently to the same situation (review §3.2).
    /// Each trait is stored as a signed deviation in [-100, 100] and read as [-1, 1]; 0 is neutral.
    /// default(AgentPersonality) is fully neutral, and a neutral personality reproduces pre-personality behaviour
    /// exactly (every trait enters scoring as a factor that equals 1 at 0). Blittable, 5 bytes.
    /// </summary>
    public readonly struct AgentPersonality
    {
        private readonly sbyte _greed;
        private readonly sbyte _riskTolerance;
        private readonly sbyte _sociability;
        private readonly sbyte _loyalty;
        private readonly sbyte _diligence;

        /// <summary>Values money: pays less, asks more, weighs rewards higher.</summary>
        public float Greed => _greed / 100f;

        /// <summary>Tolerates danger: who goes into a hazard sector (Epic 10 G2, Vitality.WouldVenture).</summary>
        public float RiskTolerance => _riskTolerance / 100f;

        /// <summary>The same in whole percent, for exact comparisons: a float threshold on a value divided by 100 can round
        /// differently in Unity's Mono and in CoreCLR (Epic CORE F1).</summary>
        public int RiskTolerancePercent => _riskTolerance;

        /// <summary>Seeks company: socializing and hearth gathering weigh more.</summary>
        public float Sociability => _sociability / 100f;

        /// <summary>Follows group directives more readily.</summary>
        public float Loyalty => _loyalty / 100f;

        /// <summary>Works for income and minds distance less.</summary>
        public float Diligence => _diligence / 100f;

        public static AgentPersonality Neutral => default;

        public AgentPersonality(float greed, float riskTolerance, float sociability, float loyalty, float diligence)
        {
            _greed = ToDeviation(greed);
            _riskTolerance = ToDeviation(riskTolerance);
            _sociability = ToDeviation(sociability);
            _loyalty = ToDeviation(loyalty);
            _diligence = ToDeviation(diligence);
        }

        /// <summary>
        /// Deterministic personality for an entity under a session seed. Each trait is the mean of two uniform draws
        /// (triangular on [-1, 1]), so moderate personalities are common and extremes are rare.
        /// </summary>
        public static AgentPersonality Generate(ulong seed, EntityId entityId)
        {
            var rng = SimRandom.For(seed, entityId, 0, SimRandomSalt.Personality);
            return new AgentPersonality(Draw(ref rng), Draw(ref rng), Draw(ref rng), Draw(ref rng), Draw(ref rng));
        }

        private static float Draw(ref SimRandom rng)
        {
            return (rng.Range(-1f, 1f) + rng.Range(-1f, 1f)) * 0.5f;
        }

        private static sbyte ToDeviation(float value)
        {
            return (sbyte)SimMath.RoundToInt(SimMath.Clamp(value, -1f, 1f) * 100f);
        }

        public override string ToString()
        {
            return $"greed {Greed:+0.00;-0.00}, risk {RiskTolerance:+0.00;-0.00}, social {Sociability:+0.00;-0.00}, loyalty {Loyalty:+0.00;-0.00}, diligence {Diligence:+0.00;-0.00}";
        }
    }
}
