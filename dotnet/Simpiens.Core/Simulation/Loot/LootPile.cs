using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Loot
{
    /// <summary>
    /// Items on the ground (Epic 10 Part 3, K4.1; A5: a kill's drop and a body are both piles), as plain data: where it lies,
    /// up to <see cref="PileService.Capacity"/> stacks and a purse, when it fades, and who may take it first until when (its
    /// victor). Main thread only: the pile service writes it; views read it through their frames.
    /// </summary>
    public sealed class LootPile
    {
        private readonly ItemInstance[] _stacks = new ItemInstance[PileService.Capacity];

        internal LootPile(EntityId id, Vec2 position, uint fadesAt, EntityId claimedFor, uint claimUntil)
        {
            Id = id;
            Position = position;
            FadesAt = fadesAt;
            ClaimedFor = claimedFor;
            ClaimUntil = claimUntil;
        }

        public EntityId Id { get; }
        public Vec2 Position { get; }

        /// <summary>The tick it is gone, taken by the floor.</summary>
        public uint FadesAt { get; internal set; }

        /// <summary>Who may take it before <see cref="ClaimUntil"/> (its victor); empty: anyone.</summary>
        public EntityId ClaimedFor { get; internal set; }
        public uint ClaimUntil { get; internal set; }

        /// <summary>The gold in it (a body's purse, K5); held in the ledger as gold on the ground.</summary>
        public int Gold { get; internal set; }

        /// <summary>How many stacks it holds.</summary>
        public int Count { get; private set; }

        public ItemInstance StackAt(int index) => _stacks[index];

        public bool IsEmpty => Count == 0 && Gold == 0;

        /// <summary>True when <paramref name="crawler"/> may take from it at <paramref name="tick"/>: anyone once its claim has run out.</summary>
        public bool MayTake(EntityId crawler, uint tick) => ClaimedFor == default || crawler == ClaimedFor || tick >= ClaimUntil;

        // Its entity in the world
        internal WorldEntity Entity { get; set; }

        internal bool TryAdd(in ItemInstance stack)
        {
            if (stack.IsEmpty || Count >= _stacks.Length) return false;
            _stacks[Count++] = stack;
            return true;
        }

        internal void SetAt(int index, in ItemInstance stack) => _stacks[index] = stack;

        // Drops the emptied stacks, keeping the rest in order
        internal void Compact()
        {
            int kept = 0;
            for (int i = 0; i < Count; i++)
            {
                if (_stacks[i].IsEmpty) continue;
                _stacks[kept++] = _stacks[i];
            }
            for (int i = kept; i < Count; i++) _stacks[i] = ItemInstance.Empty;
            Count = kept;
        }

        public override string ToString() => $"pile {Id} at {Position}: {Count} stacks, {Gold}g, fades t{FadesAt}";
    }
}
