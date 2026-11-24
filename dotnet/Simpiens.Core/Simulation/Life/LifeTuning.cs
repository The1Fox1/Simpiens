using System.Runtime.CompilerServices;

namespace Simpiens.Simulation.Life
{
    /// <summary>A bias for each of 16 motive ids: a motive's session weight is 1 plus its bias, so the zeroed default weighs every motive 1.</summary>
    [InlineArray(LifeTuning.MotiveCapacity)]
    public struct MotiveBiases
    {
        private float _first;
    }

    /// <summary>A bias for each mood band and motive id (6 x 16): a goal's mood weight is 1 plus its band's bias for its motive.</summary>
    [InlineArray(LifeTuning.BandCapacity * LifeTuning.MotiveCapacity)]
    public struct MoodBiases
    {
        private float _first;
    }

    /// <summary>What each mood band adds to frustration a game hour (a value for each of 6 bands).</summary>
    [InlineArray(LifeTuning.BandCapacity)]
    public struct BandValues
    {
        private float _first;
    }

    /// <summary>
    /// The life core's numbers for a session (Epic LIFE-1, the epic's A11), inside <see cref="Economy.EconomyTuning"/> as the
    /// day's pace is. The motive model reads them (<c>Cognition.Planning.MotiveModel</c>), and the drives read mood's
    /// frustration. A zeroed tuning (<c>default</c>) weighs every motive 1 in every mood, adds no frustration, and has no
    /// commitment or fallback.
    /// </summary>
    public readonly struct LifeTuning
    {
        /// <summary>Motive ids below this have a weight (the motive enum is a byte; ids are never reused).</summary>
        public const int MotiveCapacity = 16;

        /// <summary>Mood bands below this have weights (<see cref="MoodBand"/>, with None at 0).</summary>
        public const int BandCapacity = 6;

        private readonly MotiveBiases _motiveBiases;
        private readonly MoodBiases _moodBiases;
        private readonly BandValues _moodFrustration;

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
            _moodBiases = default;
            _moodFrustration = default;
            Commitment = commitment;
            FallbackDepth = fallbackDepth;
        }

        private LifeTuning(float commitment, byte fallbackDepth, in MotiveBiases motives, in MoodBiases moods, in BandValues frustration)
        {
            _motiveBiases = motives;
            _moodBiases = moods;
            _moodFrustration = frustration;
            Commitment = commitment;
            FallbackDepth = fallbackDepth;
        }

        /// <summary>
        /// The life core's numbers for the live game:
        /// <list type="bullet">
        /// <item>every session weight 1, no commitment, and the fallback to the next two goals (L0.6, D4);</item>
        /// <item>mood's weights (L1.4, D9): low, income pulls 0.9; breaking, 0.75; steady and above, 1. Company's pull waits for plan
        /// 2's fun: weighed here, a hurt crawler's low mood sent it to the hearth instead of its bunk, where it healed slower and
        /// stayed low (the epic's F12). Duty (plan 2's O.1, D25): low 0.85, breaking 0.6, under orders as anywhere, since orders
        /// are a motive a crawler may set aside. Fun (L2.8, D21): low 1.25, breaking 1.5, safe since Recreate is never chosen hungry,
        /// tired or hurt (F12);</item>
        /// <item>mood's frustration a game hour (L1.4, D9): breaking +15, low +5, good and happy -5 (eased faster).</item>
        /// </list>
        /// </summary>
        public static LifeTuning Default => new LifeTuning(commitment: 0f, fallbackDepth: 2)
            .WithMoodWeight(MoodBand.Low, Income, 0.9f)
            .WithMoodWeight(MoodBand.Breaking, Income, 0.75f)
            .WithMoodWeight(MoodBand.Low, Duty, 0.85f)
            .WithMoodWeight(MoodBand.Breaking, Duty, 0.6f)
            .WithMoodWeight(MoodBand.Low, Fun, 1.25f)
            .WithMoodWeight(MoodBand.Breaking, Fun, 1.5f)
            .WithMoodFrustration(MoodBand.Breaking, 15f).WithMoodFrustration(MoodBand.Low, 5f)
            .WithMoodFrustration(MoodBand.Good, -5f).WithMoodFrustration(MoodBand.Happy, -5f);

        private const int Income = (int)Simpiens.Cognition.Planning.Motive.Income;
        private const int Duty = (int)Simpiens.Cognition.Planning.Motive.Duty;
        private const int Fun = (int)Simpiens.Cognition.Planning.Motive.Fun;

        /// <summary>The session's weight on every goal of <paramref name="motiveId"/>: 1 unless set (a tuning dial, and the tests' lever).</summary>
        public float MotiveWeight(int motiveId) => motiveId >= 0 && motiveId < MotiveCapacity ? 1f + _motiveBiases[motiveId] : 1f;

        /// <summary>The weight a mood of <paramref name="band"/> puts on every goal of <paramref name="motiveId"/> (L1.4, D9): 1 unless set.</summary>
        public float MoodWeight(MoodBand band, int motiveId) =>
            (int)band < BandCapacity && motiveId >= 0 && motiveId < MotiveCapacity ? 1f + _moodBiases[(int)band * MotiveCapacity + motiveId] : 1f;

        /// <summary>What a mood of <paramref name="band"/> adds to frustration a game hour (L1.4, D9): 0 unless set; negative eases it.</summary>
        public float MoodFrustrationPerHour(MoodBand band) => (int)band < BandCapacity ? _moodFrustration[(int)band] : 0f;

        /// <summary>A copy that weighs every goal of <paramref name="motiveId"/> by <paramref name="weight"/>.</summary>
        public LifeTuning WithMotiveWeight(int motiveId, float weight)
        {
            if (motiveId < 0 || motiveId >= MotiveCapacity) return this;
            var biases = _motiveBiases;
            biases[motiveId] = weight - 1f;
            return new LifeTuning(Commitment, FallbackDepth, biases, _moodBiases, _moodFrustration);
        }

        /// <summary>A copy in which a mood of <paramref name="band"/> weighs every goal of <paramref name="motiveId"/> by <paramref name="weight"/>.</summary>
        public LifeTuning WithMoodWeight(MoodBand band, int motiveId, float weight)
        {
            if ((int)band >= BandCapacity || motiveId < 0 || motiveId >= MotiveCapacity) return this;
            var biases = _moodBiases;
            biases[(int)band * MotiveCapacity + motiveId] = weight - 1f;
            return new LifeTuning(Commitment, FallbackDepth, _motiveBiases, biases, _moodFrustration);
        }

        /// <summary>A copy in which a mood of <paramref name="band"/> adds <paramref name="perHour"/> frustration a game hour.</summary>
        public LifeTuning WithMoodFrustration(MoodBand band, float perHour)
        {
            if ((int)band >= BandCapacity) return this;
            var values = _moodFrustration;
            values[(int)band] = perHour;
            return new LifeTuning(Commitment, FallbackDepth, _motiveBiases, _moodBiases, values);
        }

        /// <summary>A copy with neither mood's weights nor its frustration: mood read, and acting on nothing.</summary>
        public LifeTuning WithoutMood() => new LifeTuning(Commitment, FallbackDepth, _motiveBiases, default, default);

        public LifeTuning WithCommitment(float commitment) => new LifeTuning(commitment, FallbackDepth, _motiveBiases, _moodBiases, _moodFrustration);

        public LifeTuning WithFallbackDepth(byte depth) => new LifeTuning(Commitment, depth, _motiveBiases, _moodBiases, _moodFrustration);
    }
}
