using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Workshops
{
    /// <summary>
    /// What the player may do with the stations, and why not (Epic 10 Part 2, W1). The simulation enforces it when a
    /// command is applied; the station window uses the same rule to explain a refusal before the player sends one, as
    /// the guild's rules do. A future multiplayer server refuses the same commands.
    /// </summary>
    public static class WorkshopRules
    {
        public enum Check : byte
        {
            Ok = 0,
            /// <summary>No station with that index on the map.</summary>
            NoStation = 1,
            AlreadyBuilt = 2,
            /// <summary>No stall to pay from.</summary>
            NoStall = 3,
            /// <summary>The stall's treasury can't pay the construction.</summary>
            CannotPay = 4
        }

        /// <summary>Whether the station at <paramref name="index"/> can be built now (D10): it exists, stands unbuilt, and the stall can pay. Checked in that order.</summary>
        public static Check CheckBuild(WorkshopService workshops, int index, bool hasStall, uint treasury, in WorkshopTuning tuning)
        {
            if ((uint)index >= (uint)workshops.Count) return Check.NoStation;
            if (workshops[index].Built) return Check.AlreadyBuilt;
            if (!hasStall) return Check.NoStall;
            if (treasury < tuning.ConstructionCost) return Check.CannotPay;
            return Check.Ok;
        }

        /// <summary>A refusal in the player's words (allocates; call only on the refusal path and in the window).</summary>
        public static string Describe(Check check, WorkshopService workshops, int index, in WorkshopTuning tuning)
        {
            string station = (uint)index < (uint)workshops.Count ? StationName(workshops[index].Kind) : "station";
            switch (check)
            {
                case Check.NoStation: return "There is no such station here.";
                case Check.AlreadyBuilt: return $"The {station} already stands.";
                case Check.NoStall: return "There is no stall to pay for it.";
                case Check.CannotPay: return $"The stall can't pay the {tuning.ConstructionCost}g to build the {station}.";
                default: return string.Empty;
            }
        }

        /// <summary>A station's name in the player's words: "forge", "still".</summary>
        public static string StationName(Simpiens.Simulation.Items.StationKind kind) => kind switch
        {
            Simpiens.Simulation.Items.StationKind.Forge => "forge",
            Simpiens.Simulation.Items.StationKind.Still => "still",
            _ => "station"
        };
    }
}
