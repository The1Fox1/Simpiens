namespace Simpiens.Simulation.Life
{
    /// <summary>
    /// How wants are held (Epic LIFE-1 plan 2, W.2, D20): pure rules over a <see cref="CrawlerLife"/>, written on the main thread
    /// through the crawler's entries, read by cognition. A want takes a free slot (none if all three are held, or it is held
    /// already); it ends when it comes true or, lazily, when its time runs out.
    /// </summary>
    public static class WantRules
    {
        /// <summary>Whether <paramref name="life"/> holds <paramref name="want"/>, live at <paramref name="tick"/>.</summary>
        public static bool Holds(in CrawlerLife life, Want want, long tick)
        {
            for (int i = 0; i < CrawlerLife.WantCapacity; i++)
            {
                var slot = life.WantAt(i);
                if (slot.Want == (ushort)want && tick < slot.ExpiresAt) return true;
            }
            return false;
        }

        /// <summary>Takes a free slot for <paramref name="slot"/>; false when it is held already or every slot is taken.</summary>
        public static bool Add(ref CrawlerLife life, in WantSlot slot)
        {
            int free = -1;
            for (int i = 0; i < CrawlerLife.WantCapacity; i++)
            {
                var held = life.WantAt(i);
                if (held.Want == slot.Want && held.Want != 0) return false;
                if (held.Want == 0 && free < 0) free = i;
            }
            if (free < 0) return false;
            life.SetWant(free, slot);
            return true;
        }

        /// <summary>Clears every want run out at <paramref name="tick"/>, writing each into <paramref name="expired"/>; returns how many.</summary>
        public static int Expire(ref CrawlerLife life, long tick, System.Span<WantSlot> expired)
        {
            int n = 0;
            for (int i = 0; i < CrawlerLife.WantCapacity; i++)
            {
                var slot = life.WantAt(i);
                if (slot.Want == 0 || tick < slot.ExpiresAt) continue;
                if (n < expired.Length) expired[n++] = slot;
                life.SetWant(i, default);
            }
            return n;
        }

        /// <summary>
        /// A spell of <paramref name="activity"/> at venue <paramref name="venue"/> came true for the wants it answers: an
        /// activity's want for that activity (and venue, if it names one), a venue's for that venue. Each is cleared and written
        /// into <paramref name="fulfilled"/>; returns how many.
        /// </summary>
        public static int Fulfil(ref CrawlerLife life, Activity activity, int venue, long tick, System.Span<WantSlot> fulfilled)
        {
            int n = 0;
            for (int i = 0; i < CrawlerLife.WantCapacity; i++)
            {
                var slot = life.WantAt(i);
                var row = WantTable.Get((Want)slot.Want);
                if (row == null || tick >= slot.ExpiresAt) continue;
                bool answers = row.Target == WantTarget.Activity ? slot.Activity == (byte)activity && (slot.Venue == 0 || slot.Venue == venue + 1)
                    : row.Target == WantTarget.Venue && slot.Venue == venue + 1;
                if (!answers) continue;
                if (n < fulfilled.Length) fulfilled[n++] = slot;
                life.SetWant(i, default);
            }
            return n;
        }

        /// <summary>Meeting <paramref name="friend"/> came true for a want to see it; returns whether one was held.</summary>
        public static bool Saw(ref CrawlerLife life, EntityId friend, long tick)
        {
            for (int i = 0; i < CrawlerLife.WantCapacity; i++)
            {
                var slot = life.WantAt(i);
                if (slot.Want != (ushort)Want.SeeAFriend || slot.Friend != friend || tick >= slot.ExpiresAt) continue;
                life.SetWant(i, default);
                return true;
            }
            return false;
        }
    }
}
