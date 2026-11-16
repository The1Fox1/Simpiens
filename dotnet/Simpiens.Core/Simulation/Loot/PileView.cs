using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Loot
{
    /// <summary>
    /// A pile as cognition sees it (Epic 10 Part 3, K4.3b): a copy in every snapshot, taken on the main thread as stalls and
    /// bounties are, so a crawler choosing what to pick up reads no live state. Where it lies, who may take it first until
    /// when, its purse, and which items it holds (a bit per item id), never how many: a crawler judges whether it would take
    /// anything from it (<see cref="LootRules"/>), and the take itself counts.
    /// </summary>
    public readonly struct PileView
    {
        public readonly EntityId Id;
        public readonly Vec2 Position;
        public readonly EntityId ClaimedFor;
        public readonly uint ClaimUntil;
        public readonly int Gold;
        private readonly ulong _items0, _items1, _items2, _items3;

        public PileView(LootPile pile)
        {
            Id = pile.Id;
            Position = pile.Position;
            ClaimedFor = pile.ClaimedFor;
            ClaimUntil = pile.ClaimUntil;
            Gold = pile.Gold;
            ulong a = 0, b = 0, c = 0, d = 0;
            for (int i = 0; i < pile.Count; i++)
            {
                var stack = pile.StackAt(i);
                if (stack.IsEmpty) continue;
                int id = (byte)stack.Type;
                ulong bit = 1UL << (id & 63);
                switch (id >> 6)
                {
                    case 0: a |= bit; break;
                    case 1: b |= bit; break;
                    case 2: c |= bit; break;
                    default: d |= bit; break;
                }
            }
            _items0 = a;
            _items1 = b;
            _items2 = c;
            _items3 = d;
        }

        /// <summary>True when it holds at least one stack of <paramref name="item"/>.</summary>
        public bool Holds(ItemType item)
        {
            int id = (byte)item;
            ulong bit = 1UL << (id & 63);
            ulong word = (id >> 6) switch { 0 => _items0, 1 => _items1, 2 => _items2, _ => _items3 };
            return item != ItemType.None && (word & bit) != 0;
        }

        /// <summary>True when it holds no stack at all (a purse alone).</summary>
        public bool HoldsNoItems => (_items0 | _items1 | _items2 | _items3) == 0;

        /// <summary>As <see cref="LootPile.MayTake"/>: its victor, or anyone once its claim has run out.</summary>
        public bool MayTake(EntityId crawler, uint tick) => ClaimedFor == default || crawler == ClaimedFor || tick >= ClaimUntil;
    }
}
