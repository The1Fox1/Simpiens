using System.Runtime.InteropServices;
using MemoryPack;
using Simpiens.Narrative;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// The chronicle in a save, written and read by <see cref="NarrativeChronicleService"/>. A sector keeps its own short
    /// history, which can reach further back than the facility's, so each is saved as it is.
    /// </summary>
    [MemoryPackable]
    public sealed partial class ChronicleSave
    {
        /// <summary>The last event id handed out.</summary>
        public ulong Sequence { get; set; }

        /// <summary>The facility's history, oldest first.</summary>
        public ChronicleEvent[] Events { get; set; }

        /// <summary>Non-empty sector histories by sector id; each is SectorEvents[Start .. Start + Count), oldest first.</summary>
        public ChronicleSectorRecord[] Sectors { get; set; }

        public ChronicleEvent[] SectorEvents { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ChronicleSectorRecord
    {
        public int SectorId;
        public int Start;
        public int Count;
    }
}
