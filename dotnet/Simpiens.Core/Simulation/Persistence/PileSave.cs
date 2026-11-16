using System.Runtime.InteropServices;
using MemoryPack;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Loot;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// The piles in a save (Epic 10 Part 3, K4.1), inside the world's block, written and read by <see cref="PileService"/>:
    /// the id sequence's counter, every pile in id order, and their stacks one after another (each pile's count in its record).
    /// </summary>
    [MemoryPackable]
    public sealed partial class PileSave
    {
        public long IdCounter { get; set; }
        public PileRecord[] Piles { get; set; }
        public ItemInstance[] Stacks { get; set; }
    }

    /// <summary>
    /// A pile as saved: who it is, where, when it fades, who may claim it until when, its gold and how many stacks, and whose
    /// body it is (Epic 10 Part 3, K5.4a; empty for a kill's drop). 72 bytes, no padding (56 until K5.4a, inside format 11).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PileRecord
    {
        public EntityId Id;
        public EntityId ClaimedFor;
        public EntityId BodyOf;
        public Vec2 Position;
        public uint FadesAt;
        public uint ClaimUntil;
        public int Gold;
        public int StackCount;

        public PileRecord(LootPile pile)
        {
            Id = pile.Id;
            ClaimedFor = pile.ClaimedFor;
            BodyOf = pile.BodyOf;
            Position = pile.Position;
            FadesAt = pile.FadesAt;
            ClaimUntil = pile.ClaimUntil;
            Gold = pile.Gold;
            StackCount = pile.Count;
        }
    }
}
