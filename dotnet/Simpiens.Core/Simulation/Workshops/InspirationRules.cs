using Simpiens.Cognition;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;

namespace Simpiens.Simulation.Workshops
{
    /// <summary>
    /// Inspiration (Epic 11, S8.2, D41), engine-free: whether a finished make of a worn piece at Masterwork grade comes out a
    /// masterpiece, and its name's words. The chance is <see cref="Chance"/> a make, raised by the maker's passion for the
    /// work (by its practice multiplier: keen ×1.5, burning ×2) and by grief (×2: a friend's death in its memory within two
    /// game days, whose name the piece then carries). A maker is inspired at most once a game day. The roll is on its own
    /// salt (<see cref="SimRandomSalt.Inspiration"/>), keyed by the maker and the tick, so it is the same on any thread and in
    /// any order. Allocates nothing.
    /// </summary>
    public static class InspirationRules
    {
        /// <summary>The chance a Masterwork make is a masterpiece (S8.0, `docs/skills-model.md` §5.14): about one a smith in two weeks.</summary>
        public const float Chance = 0.08f;

        /// <summary>How much grief raises it.</summary>
        public const float GriefFactor = 2f;

        /// <summary>How long a friend's death is grief (two game days).</summary>
        public static uint GriefTicks => (uint)(2 * DayClock.TicksPerDay);

        /// <summary>How long a maker waits for its next inspiration (a game day).</summary>
        public static uint LimitTicks => (uint)DayClock.TicksPerDay;

        /// <summary>The chance a make at <paramref name="grade"/> is a masterpiece: none below Masterwork.</summary>
        public static float ChanceFor(byte grade, Passion passion, bool grieving)
        {
            if (grade < Grades.Masterwork) return 0f;
            float chance = Chance * PassionTable.Get(passion).PracticePercent / 100f;
            if (grieving) chance *= GriefFactor;
            return chance < 1f ? chance : 1f;
        }

        /// <summary>The friend whose death <paramref name="maker"/> grieves (the newest within <see cref="GriefTicks"/> in its memory), if any.</summary>
        public static bool TryGrief(AgentState maker, uint tick, out EntityId fallen)
        {
            fallen = default;
            var ledger = maker?.Memory?.EventLedger;
            if (ledger == null) return false;
            for (int i = ledger.Count - 1; i >= 0; i--)
            {
                var memory = ledger.GetEvent(i);
                if (memory.EventType != Simpiens.Cognition.Memory.MemoryEventType.Grieved) continue;
                if (memory.SimulationTick > tick || tick - memory.SimulationTick > GriefTicks) continue;
                fallen = memory.SubjectId;
                return true;
            }
            return false;
        }

        /// <summary>Whether <paramref name="maker"/> made a masterpiece within the last game day (<see cref="LimitTicks"/>).</summary>
        public static bool InspiredLately(MasterpieceService masterpieces, EntityId maker, uint tick)
        {
            if (masterpieces == null) return false;
            for (int i = masterpieces.Count - 1; i >= 0; i--)
            {
                var record = masterpieces[i];
                if (record.Maker != maker) continue;
                return record.MadeAt <= tick && tick - record.MadeAt < LimitTicks;
            }
            return false;
        }

        /// <summary>
        /// The roll: true when the make is inspired at <paramref name="chance"/>, with its name's words for
        /// <paramref name="item"/> (an adjective and a noun of its kind), drawn on the same stream.
        /// </summary>
        public static bool Roll(ulong seed, EntityId maker, uint tick, float chance, ItemType item, out byte adjective, out byte noun)
        {
            adjective = 0;
            noun = 0;
            if (chance <= 0f) return false;
            var random = SimRandom.For(seed, maker, tick, SimRandomSalt.Inspiration);
            if (random.NextFloat() >= chance) return false;
            adjective = (byte)(random.NextULong() % (ulong)MasterpieceNames.Adjectives.Count);
            noun = (byte)(random.NextULong() % (ulong)MasterpieceNames.NounsFor(MasterpieceNames.KindOf(item)).Count);
            return true;
        }
    }
}
