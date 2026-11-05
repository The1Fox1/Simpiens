using System.Runtime.InteropServices;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Workshops
{
    /// <summary>
    /// One station's state (Epic 10 Part 2, W0.5, D5): whether it is built, what it is ordered to make and how far the
    /// current make has come. Plain data, saved as raw memory (format 10), one per station of the map in its order.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct StationState
    {
        public StationKind Kind;
        public bool Built;

        /// <summary>What it is ordered to make; None when it has no order.</summary>
        public ItemType Order;

        /// <summary>Finished items waiting for room in the stall's stock (D3).</summary>
        public byte HeldOutput;

        /// <summary>Makes left in the order, the current one included.</summary>
        public ushort Remaining;

        /// <summary>Ticks of work on the current make.</summary>
        public ushort Progress;

        /// <summary>The current make's inputs are taken from the stall's stock: they are spent from here on.</summary>
        public bool InputsTaken;

        /// <summary>The station's crew, a Workshop group (Q4); 0 while nobody is hired.</summary>
        public byte CrewGroupId;

        private ushort _pad;

        /// <summary>Makes finished here, all told.</summary>
        public uint Made;

        public static StationState Unbuilt(StationKind kind) => new StationState { Kind = kind };

        public readonly bool HasOrder => Order != ItemType.None && (Remaining > 0 || HeldOutput > 0);
    }

    /// <summary>What one tick of an artisan's work at a station came to.</summary>
    public enum CraftStep : byte
    {
        /// <summary>Not built, or nothing ordered.</summary>
        Idle = 0,
        /// <summary>The stall's stock lacks the next make's inputs.</summary>
        WaitingForInputs = 1,
        Working = 2,
        /// <summary>A make went into the stall's stock.</summary>
        Made = 3,
        /// <summary>A make is finished, and the stall has no room for it yet.</summary>
        Holding = 4
    }
}
