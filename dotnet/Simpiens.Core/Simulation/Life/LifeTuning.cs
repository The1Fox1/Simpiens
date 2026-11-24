using System.Runtime.CompilerServices;

namespace Simpiens.Simulation.Life
{
    /// <summary>A bias for each of 16 motive ids: a motive's session weight is 1 plus its bias, so the zeroed default weighs every motive 1.</summary>
    [InlineArray(LifeTuning.MotiveCapacity)]
    public struct MotiveBiases
    {
        private float _first;
    }

    /// <summary>
    /// The life core's numbers for a session (Epic LIFE-1, the epic's A11), inside <see cref="Economy.EconomyTuning"/> as the
    /// day's pace is. The motive model reads them (<c>Cognition.Planning.MotiveModel</c>). The zeroed default changes nothing:
    /// every weight 1, no commitment, no fallback, so a session that sets none plays as before the life core.
    /// </summary>
    public readonly struct LifeTuning
    {
        /// <summary>Motive ids below this have a weight (the motive enum is a byte; ids are never reused).</summary>
        public const int MotiveCapacity = 16;

        private readonly MotiveBiases _motiveBiases;

        /// <summary>
        /// What the goal whose plan just ended, still valid and unsatisfied, gains at the next choice (L0.7, D5): 0 adds
        /// nothing, so a choice stays as without it.
        /// </summary>
        public readonly float Commitment;

        /// <summary>How many goals after the best are planned when the best can't be (L0.6, D4); 0 falls to Tier 3 at once.</summary>
        public readonly byte FallbackDepth;

        public LifeTuning(float commitment, byte fallbackDepth)
        {
            _motiveBiases = default;
            Commitment = commitment;
            FallbackDepth = fallbackDepth;
        }

        private LifeTuning(in LifeTuning from, in MotiveBiases biases)
        {
            _motiveBiases = biases;
            Commitment = from.Commitment;
            FallbackDepth = from.FallbackDepth;
        }

        /// <summary>The life core's numbers before any milestone sets one: every weight 1, no commitment, no fallback.</summary>
        public static LifeTuning Default => default;

        /// <summary>The session's weight on every goal of <paramref name="motiveId"/>: 1 unless set (a tuning dial, and the tests' lever).</summary>
        public float MotiveWeight(int motiveId) => motiveId >= 0 && motiveId < MotiveCapacity ? 1f + _motiveBiases[motiveId] : 1f;

        /// <summary>A copy that weighs every goal of <paramref name="motiveId"/> by <paramref name="weight"/>.</summary>
        public LifeTuning WithMotiveWeight(int motiveId, float weight)
        {
            if (motiveId < 0 || motiveId >= MotiveCapacity) return this;
            var biases = _motiveBiases;
            biases[motiveId] = weight - 1f;
            return new LifeTuning(this, biases);
        }

        public LifeTuning WithCommitment(float commitment) => new LifeTuning(new LifeTuning(commitment, FallbackDepth), _motiveBiases);

        public LifeTuning WithFallbackDepth(byte depth) => new LifeTuning(new LifeTuning(Commitment, depth), _motiveBiases);
    }
}
