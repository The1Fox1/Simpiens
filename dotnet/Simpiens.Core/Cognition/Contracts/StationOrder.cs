namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// A crawler's part in its station's work (Epic 10 Part 2, W1.3, D13), copied from the workshops every tick for
    /// cognition, as its expedition order is: on shift at a built station with an order, and not on a break. An artisan off
    /// shift carries only its shifts (Epic LIFE-1 plan 3, R.1: its routine's working block is its shifts); default for a
    /// crawler who works no station.
    /// </summary>
    public readonly struct StationOrder
    {
        /// <summary>The station: its index among the map's stations.</summary>
        public readonly byte StationIndex;
        public readonly Vec2 Position;

        /// <summary>Within this of the station the artisan works it.</summary>
        public readonly float Radius;

        public readonly bool OnShift;

        /// <summary>The blocks of the day the artisan works (a <c>ShiftBlocks</c> mask), on shift or off; 0 for a crawler who works no station.</summary>
        public readonly byte Shifts;

        public StationOrder(byte stationIndex, Vec2 position, float radius, byte shifts = 0)
        {
            StationIndex = stationIndex;
            Position = position;
            Radius = radius;
            OnShift = true;
            Shifts = shifts;
        }

        private StationOrder(byte shifts)
        {
            StationIndex = 0;
            Position = default;
            Radius = 0f;
            OnShift = false;
            Shifts = shifts;
        }

        /// <summary>An artisan off shift: nothing to work now, only the hours it keeps.</summary>
        public static StationOrder OffShift(byte shifts) => new StationOrder(shifts);

        public bool IsActive => OnShift;
    }
}
