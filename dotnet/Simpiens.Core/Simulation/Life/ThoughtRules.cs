namespace Simpiens.Simulation.Life
{
    /// <summary>
    /// How thoughts are held (Epic LIFE-1, L1.2, D7): pure rules over a <see cref="CrawlerLife"/>, so the main thread writes
    /// them through one entry (<c>AgentState.Think</c>) and cognition reads the same record.
    /// <list type="bullet">
    /// <item><b>Restacking:</b> the same thought naming the same crawler adds a stack up to its limit and starts its duration
    /// again.</item>
    /// <item><b>A full record</b> gives the new thought the slot worth least: its value's size times the game time it has left.</item>
    /// <item><b>Expiry</b> is lazy: a thought past its duration is cleared whenever the record is written, and is never counted
    /// once past it (<see cref="IsLive"/>), so nothing sweeps every tick.</item>
    /// <item><b>Subjects:</b> a thought names a crawler by one of the record's four subject slots; a slot frees when no live
    /// thought names it, and with all four taken the subject named by the least weight is let go with its thoughts.</item>
    /// </list>
    /// </summary>
    public static class ThoughtRules
    {
        /// <summary>The tick <paramref name="slot"/> ends at: its start plus its row's duration; 0 for an empty slot or no row.</summary>
        public static long EndsAt(in ThoughtSlot slot)
        {
            var row = ThoughtTable.Get((Thought)slot.Thought);
            return row == null ? 0 : slot.BegunAt + (long)row.Hours * DayClock.TicksPerHour;
        }

        /// <summary>Whether <paramref name="slot"/> holds a thought still live at <paramref name="tick"/>.</summary>
        public static bool IsLive(in ThoughtSlot slot, long tick) => slot.Thought != 0 && slot.Stacks > 0 && tick < EndsAt(slot);

        /// <summary>What <paramref name="slot"/> is worth to mood at <paramref name="tick"/>: its stacks' value while live, else 0.</summary>
        public static float Worth(in ThoughtSlot slot, long tick)
        {
            if (!IsLive(slot, tick)) return 0f;
            return ThoughtTable.Get((Thought)slot.Thought).ValueAt(slot.Stacks);
        }

        /// <summary>The sum of every live thought's value at <paramref name="tick"/>.</summary>
        public static float Sum(in CrawlerLife life, long tick)
        {
            float sum = 0f;
            for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++) sum += Worth(life.ThoughtAt(i), tick);
            return sum;
        }

        /// <summary>Whether <paramref name="life"/> holds <paramref name="thought"/> live at <paramref name="tick"/> (naming anyone).</summary>
        public static bool Holds(in CrawlerLife life, Thought thought, long tick)
        {
            for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++)
            {
                var slot = life.ThoughtAt(i);
                if (slot.Thought == (ushort)thought && IsLive(slot, tick)) return true;
            }
            return false;
        }

        /// <summary>Clears every thought past its duration at <paramref name="tick"/>, then frees every subject no live thought names.</summary>
        public static void Expire(ref CrawlerLife life, long tick)
        {
            for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++)
            {
                var slot = life.ThoughtAt(i);
                if (slot.Thought != 0 && !IsLive(slot, tick)) life.SetThought(i, default);
            }
            for (int s = 1; s <= CrawlerLife.SubjectCapacity; s++)
            {
                if (life.SubjectAt(s) == default) continue;
                bool named = false;
                for (int i = 0; i < CrawlerLife.ThoughtCapacity && !named; i++) named = life.ThoughtAt(i).Subject == s;
                if (!named) life.SetSubject(s, default);
            }
        }

        /// <summary>
        /// <paramref name="thought"/>, naming <paramref name="subject"/> if its row names a crawler, thought at
        /// <paramref name="tick"/>. Nothing for no thought, a thought with no row, or a subject missing where the row names one.
        /// Returns whether it was held.
        /// </summary>
        public static bool Think(ref CrawlerLife life, Thought thought, EntityId subject, uint tick)
        {
            var row = ThoughtTable.Get(thought);
            if (row == null) return false;
            if (row.NamesSubject && subject == default) return false;
            Expire(ref life, tick);

            byte subjectSlot = 0;
            if (row.NamesSubject)
            {
                subjectSlot = FindSubject(life, subject);
                if (subjectSlot == 0) subjectSlot = TakeSubject(ref life, subject, tick);
            }

            // The same thought naming the same crawler: a stack more, its time started again
            for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++)
            {
                var slot = life.ThoughtAt(i);
                if (slot.Thought != (ushort)thought || slot.Subject != subjectSlot) continue;
                slot.Stacks = (byte)(slot.Stacks < row.StackLimit ? slot.Stacks + 1 : row.StackLimit);
                slot.BegunAt = tick;
                life.SetThought(i, slot);
                return true;
            }

            // A free slot, or the one worth least
            int place = -1;
            float least = float.MaxValue;
            for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++)
            {
                var slot = life.ThoughtAt(i);
                if (slot.Thought == 0) { place = i; break; }
                float worth = System.Math.Abs(Worth(slot, tick)) * (EndsAt(slot) - tick);
                if (worth < least) { least = worth; place = i; }
            }
            life.SetThought(place, new ThoughtSlot { Thought = (ushort)thought, Stacks = 1, Subject = subjectSlot, BegunAt = tick });
            Expire(ref life, tick); // frees a subject the replaced thought was the last to name
            return true;
        }

        private static byte FindSubject(in CrawlerLife life, EntityId subject)
        {
            for (int s = 1; s <= CrawlerLife.SubjectCapacity; s++) if (life.SubjectAt(s) == subject) return (byte)s;
            return 0;
        }

        /// <summary>A subject slot for <paramref name="subject"/>: a free one, or the one whose live thoughts weigh least, let go with them.</summary>
        private static byte TakeSubject(ref CrawlerLife life, EntityId subject, long tick)
        {
            int take = 0;
            float least = float.MaxValue;
            for (int s = 1; s <= CrawlerLife.SubjectCapacity; s++)
            {
                if (life.SubjectAt(s) == default) { take = s; break; }
                float weight = 0f;
                for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++)
                {
                    var slot = life.ThoughtAt(i);
                    if (slot.Subject == s) weight += System.Math.Abs(Worth(slot, tick));
                }
                if (weight < least) { least = weight; take = s; }
            }
            for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++) if (life.ThoughtAt(i).Subject == take) life.SetThought(i, default);
            life.SetSubject(take, subject);
            return (byte)take;
        }
    }
}
