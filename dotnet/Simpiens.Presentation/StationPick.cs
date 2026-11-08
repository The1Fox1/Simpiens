namespace Simpiens.Presentation
{
    /// <summary>
    /// The ids a click picks a station by (Epic 10 Part 2, W1.6a). Stations aren't simulation entities (making them
    /// entities would shift every id after them and re-roll every seed), so the view gives each one an id of its own,
    /// from its index in the map's stations, in a pattern the simulation's seeded ids don't take.
    /// </summary>
    public static class StationPick
    {
        private const uint Tag0 = 0x53544154; // "STAT"
        private const uint Tag1 = 0x494F4E21; // "ION!"

        /// <summary>The pick id of the station at <paramref name="index"/>.</summary>
        public static EntityId Of(int index) => new EntityId(Tag0, Tag1, 0u, (uint)index + 1u);

        /// <summary>Which of the frame's stations <paramref name="id"/> picks; false for anything else.</summary>
        public static bool TryIndex(EntityId id, WorldViewFrame frame, out int index)
        {
            if (frame != null)
            {
                for (int i = 0; i < frame.StationCount; i++)
                {
                    if (Of(frame.Station(i).Index) != id) continue;
                    index = frame.Station(i).Index;
                    return true;
                }
            }
            index = -1;
            return false;
        }
    }
}
