using System;
using Simpiens.Simulation;
using Simpiens.Simulation.Life;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The life core's words (Epic LIFE-1, L1.5, D10): a thought as the player reads it, a mood's band and terms, a time left
    /// and a time of day. Latin-1 only, as every word the client draws. Decides nothing.
    /// </summary>
    public static class LifeTexts
    {
        /// <summary>The word for someone a thought names when its subject has no name to give.</summary>
        public const string Someone = "Someone";

        /// <summary>A thought's words, its subject named ("Rescued by Bram"), with its stacks past one ("A good talk ×3").</summary>
        public static string Thought(in ThoughtSlot slot, in CrawlerLife life, Func<EntityId, string> nameOf)
        {
            var row = ThoughtTable.Get((Simpiens.Simulation.Life.Thought)slot.Thought);
            if (row == null) return string.Empty;
            string words = row.Words;
            if (row.NamesSubject)
            {
                var who = life.SubjectAt(slot.Subject);
                string name = who != default && nameOf != null ? nameOf(who) : null;
                if (string.IsNullOrEmpty(name)) name = words.StartsWith("{0}", StringComparison.Ordinal) ? Someone : "someone";
                words = words.Replace("{0}", name);
            }
            return slot.Stacks > 1 ? words + " ×" + slot.Stacks : words;
        }

        /// <summary>A mood's band in a word: "Breaking", "Low", "Steady", "Good", "Happy".</summary>
        public static string BandWord(MoodBand band) => band switch
        {
            MoodBand.Breaking => "Breaking",
            MoodBand.Low => "Low",
            MoodBand.Steady => "Steady",
            MoodBand.Good => "Good",
            MoodBand.Happy => "Happy",
            _ => string.Empty,
        };

        /// <summary>A need term's words: "Hungry", "Comfortable".</summary>
        public static string Term(MoodTerm term) => MoodRules.Term(term)?.Words ?? string.Empty;

        /// <summary>A value to mood, signed: "+3", "-25", "+4.5".</summary>
        public static string Value(float value) => Invariant($"{value:+0.#;-0.#;0}");

        /// <summary>How long a thought has left, in words: "2 days left", "5 h left", "40 min left".</summary>
        public static string Left(long ticks)
        {
            if (ticks <= 0) return "ending";
            long hours = ticks / DayClock.TicksPerHour;
            if (hours >= 48) return Invariant($"{hours / 24} days left");
            if (hours >= 1) return Invariant($"{hours} h left");
            return Invariant($"{Math.Max(1, ticks / DayClock.TicksPerMinute)} min left");
        }

        /// <summary>The time of day: "07:40".</summary>
        public static string Clock(long tick)
        {
            int minute = DayClock.MinuteOfDay(tick);
            return Invariant($"{minute / 60:D2}:{minute % 60:D2}");
        }

        /// <summary>The tick the calendar day of <paramref name="tick"/> began at (midnight; 0 on day 1, which starts at 06:00).</summary>
        public static long DayStart(long tick)
        {
            long start = (DayClock.Day(tick) - 1) * DayClock.TicksPerDay - (long)DayClock.StartMinute * DayClock.TicksPerMinute;
            return start < 0 ? 0 : start;
        }
    }
}
