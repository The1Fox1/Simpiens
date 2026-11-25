using System;

namespace Simpiens.Simulation.Life
{
    /// <summary>
    /// What a crawler means to do with its day (Epic LIFE-1 plan 3, R.7, D34): drawn once a routine day, saved in its life record
    /// (byte 276). A work day is 0, so a record written before plan 3 reads one. Never reused: the value is what the record holds.
    /// </summary>
    public enum DayIntention : byte
    {
        Work = 0,
        Rest = 1,
        Training = 2,
        Want = 3,
    }

    /// <summary>
    /// The day's intention (D34): its odds from weariness, a recent windfall, the purse against its target, passions and wants;
    /// the draw on its own salt (<see cref="SimRandomSalt.Intention"/>), once a routine day; and what a day off does to weariness
    /// (D33: the first halves it, a second in a row clears it). Pure; the main thread draws and records it.
    /// </summary>
    public static class IntentionRules
    {
        /// <summary>The odds of a rest day, a training day and a want's day, each 0 to 1, their sum at most 1; the rest is a work day.</summary>
        public readonly struct Odds
        {
            public readonly float Rest, Training, Want;

            public Odds(float rest, float training, float want)
            {
                Rest = rest;
                Training = training;
                Want = want;
            }
        }

        /// <summary>
        /// The odds for a crawler (D34): a rest day's base, plus weariness's share, plus a windfall within the last game day, plus
        /// a purse above its target; a training day's with a passion; a want's day's with a want held. Each within 0 and 1, and the
        /// three held to 1 together (rest first, then training, then the want).
        /// </summary>
        public static Odds For(in RhythmTuning tuning, float weariness, bool windfallYesterday, bool purseAboveTarget, bool hasPassion, bool holdsWant)
        {
            float rest = tuning.RestBase + tuning.RestByWeariness * Math.Clamp(weariness, 0f, CrawlerLife.MaxWeariness) / CrawlerLife.MaxWeariness
                + (windfallYesterday ? tuning.RestAfterWindfall : 0f) + (purseAboveTarget ? tuning.RestAbovePurse : 0f);
            rest = Math.Clamp(rest, 0f, 1f);
            float training = Math.Clamp(hasPassion ? tuning.TrainingWithPassion : 0f, 0f, 1f - rest);
            float want = Math.Clamp(holdsWant ? tuning.WantDayWithWant : 0f, 0f, 1f - rest - training);
            return new Odds(rest, training, want);
        }

        /// <summary>Whether a windfall at <paramref name="windfall"/> (0 for none) fell within the game day before <paramref name="tick"/>.</summary>
        public static bool WindfallWithinADay(uint windfall, uint tick) => windfall != 0 && tick >= windfall && tick - windfall <= DayClock.TicksPerDay;

        /// <summary>The intention drawn for <paramref name="crawler"/> on routine day <paramref name="day"/>, by <paramref name="odds"/>.</summary>
        public static DayIntention Draw(in Odds odds, ulong seed, EntityId crawler, long day)
        {
            var rng = SimRandom.For(seed, crawler, day, SimRandomSalt.Intention);
            float roll = rng.NextFloat();
            if (roll < odds.Rest) return DayIntention.Rest;
            if (roll < odds.Rest + odds.Training) return DayIntention.Training;
            if (roll < odds.Rest + odds.Training + odds.Want) return DayIntention.Want;
            return DayIntention.Work;
        }

        /// <summary>
        /// Records <paramref name="intention"/> for <paramref name="day"/> in <paramref name="life"/>, and a day off's effect on
        /// weariness (D33): a rest day halves it, and a rest day after a rest day clears it.
        /// </summary>
        public static void Begin(ref CrawlerLife life, DayIntention intention, long day)
        {
            if (intention == DayIntention.Rest)
            {
                bool secondInARow = life.IntentionFor(day - 1) == DayIntention.Rest;
                life.Weary(secondInARow ? -CrawlerLife.MaxWeariness : -life.Weariness / 2f);
            }
            life.SetIntention(intention, day);
        }
    }
}
