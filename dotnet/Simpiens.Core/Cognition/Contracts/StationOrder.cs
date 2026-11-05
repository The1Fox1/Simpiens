namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// A crawler's part in its station's work (Epic 10 Part 2, W1.3, D13), copied from the workshops every tick for
    /// cognition, as its expedition order is: on shift at a built station with an order, and not on a break. Default
    /// otherwise.
    /// </summary>
    public readonly struct StationOrder
    {
        /// <summary>The station: its index among the map's stations.</summary>
        public readonly byte StationIndex;
        public readonly Vec2 Position;

        /// <summary>Within this of the station the artisan works it.</summary>
        public readonly float Radius;

        public readonly bool OnShift;

        public StationOrder(byte stationIndex, Vec2 position, float radius)
        {
            StationIndex = stationIndex;
            Position = position;
            Radius = radius;
            OnShift = true;
        }

        public bool IsActive => OnShift;
    }
}
