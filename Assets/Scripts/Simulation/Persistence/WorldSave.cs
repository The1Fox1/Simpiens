using System.Runtime.InteropServices;
using MemoryPack;
using Simpiens.Simulation.Economy;
using UnityEngine;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// The world's changing state in a save: resource nodes and their regrowth, the fog, and per-session tallies.
    /// The map itself comes from the session definition, and a load refuses a save made on another map.
    /// </summary>
    [MemoryPackable]
    public sealed partial class WorldSave
    {
        /// <summary>By entity id. A map node missing here was used up for good and stays gone.</summary>
        public ResourceRecord[] Resources { get; set; }

        public SupplySave Supply { get; set; }

        /// <summary>Whether each sector id (0-255) is charted.</summary>
        public bool[] ExploredSectors { get; set; }

        /// <summary>Completed bunk sleeps per crawler, by crawler id.</summary>
        public IdCountRecord[] BunkSleeps { get; set; }

        /// <summary>Units harvested this session, by item type.</summary>
        public ItemCountRecord[] Harvested { get; set; }

        /// <summary>The session's keyed entities (map nodes and POIs, the stall), in ordinal key order.</summary>
        public string[] EntityKeys { get; set; }
        public GUID[] EntityIds { get; set; }
    }

    /// <summary>The supply service in a save: its regrowth queue, in its own order, and its counters.</summary>
    [MemoryPackable]
    public sealed partial class SupplySave
    {
        public DormantRecord[] Dormant { get; set; }
        public SupplyCounters Counters { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ResourceRecord
    {
        public GUID EntityId;
        public int RemainingYield;
        public int MaxYield;
        public uint RegrowTicks;
        public uint DepletedAtTick;
        public ItemType YieldItem;
        public bool IsDormant;
        private ushort _pad;

        public ResourceRecord(ResourceData data)
        {
            EntityId = data.EntityId;
            RemainingYield = data.RemainingYield;
            MaxYield = data.MaxYield;
            RegrowTicks = data.RegrowTicks;
            DepletedAtTick = data.DepletedAtTick;
            YieldItem = data.YieldItem;
            IsDormant = data.IsDormant;
            _pad = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DormantRecord
    {
        public GUID ResourceId;
        public uint ReadyTick;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SupplyCounters
    {
        public int Regrowths;
        public int Restocks;
        public int SalvageSaleVisits;
        public int UpkeepPaid;
        public int UpkeepShort;
        public int PartialRestocks;
        public int SkippedRestocks;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IdCountRecord
    {
        public GUID Id;
        public int Count;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ItemCountRecord
    {
        public int Count;
        public ItemType Item;
        private byte _pad0;
        private ushort _pad1;

        public ItemCountRecord(ItemType item, int count)
        {
            Count = count;
            Item = item;
            _pad0 = 0;
            _pad1 = 0;
        }
    }
}
