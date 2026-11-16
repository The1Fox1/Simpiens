using System;
using System.Collections.Generic;
using Simpiens.Diagnostics;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Spatial;
using ZLogger;

namespace Simpiens.Simulation.Loot
{
    /// <summary>The world as the pile service sees it (the simulation): where a pile's entity enters and leaves, and where a fading pile's gold goes.</summary>
    public interface IPileWorld
    {
        void AddPile(WorldEntity entity);
        void RemovePile(EntityId id);

        /// <summary>A pile faded with gold in it (a body's purse, K5): the ledger's account for gold on the ground loses it.</summary>
        void GoldFaded(LootPile pile, int gold);

        /// <summary>A pile was dropped with gold in it (a body's purse, K5.4b): the ledger's account for gold on the ground holds it.</summary>
        void GoldDropped(LootPile pile, int gold);

        /// <summary>Gold was picked up from a pile into a purse (K5.4b): it leaves the ground for the purses.</summary>
        void GoldTaken(LootPile pile, int gold);
    }

    /// <summary>
    /// The items on the ground (Epic 10 Part 3, K4.1; A5): every pile in the world, in id order. A pile is dropped (a kill's
    /// loot, K4.2; a body, K5) where it lies, claimed for its victor for <see cref="ClaimHours"/>, and fades after
    /// <see cref="FadeHours"/> of the day. Ids come from a seeded sequence of their own (as creatures' do), so drops never
    /// shift who the next crawler or drifter is. Main thread only. Owned by the simulation (<see cref="SimulationManager.Piles"/>).
    /// </summary>
    public sealed class PileService
    {
        /// <summary>The stacks a pile holds: a body's pack and what it wore (K5).</summary>
        public const int Capacity = 16;

        /// <summary>A pile fades a game day after it is dropped (Q12).</summary>
        public const int FadeHours = 24;

        /// <summary>Its victor alone may take it for a game hour.</summary>
        public const int ClaimHours = 1;

        /// <summary>A pile's body on the ground, for picking and crowding.</summary>
        public const float Radius = 0.25f;

        private readonly List<LootPile> _piles = new List<LootPile>(8);
        private ulong _seed;
        private long _idCounter;

        public IReadOnlyList<LootPile> Piles => _piles;

        /// <summary>The gold every pile holds: what the ledger's account for gold on the ground should hold (K5.4b).</summary>
        public uint TotalGold
        {
            get
            {
                uint total = 0;
                for (int i = 0; i < _piles.Count; i++) total += (uint)_piles[i].Gold;
                return total;
            }
        }

        /// <summary>Units taken from piles, and pieces worn from them, since the session started (K4.3; diagnostics, not saved).</summary>
        public int UnitsTaken { get; private set; }
        public int PiecesWorn { get; private set; }

        /// <summary>Pile ids handed out since the session started: with the seed, all a save needs to continue the sequence.</summary>
        public long IdCounter => _idCounter;

        /// <summary>Starts a session on <paramref name="seed"/>: no piles.</summary>
        public void Begin(IPileWorld world, ulong seed)
        {
            for (int i = 0; i < _piles.Count; i++) world?.RemovePile(_piles[i].Id);
            _piles.Clear();
            _seed = seed;
            _idCounter = 0;
            UnitsTaken = 0;
            PiecesWorn = 0;
        }

        /// <summary>Forgets every pile (a respawn or a load tears the world down).</summary>
        public void Reset()
        {
            _piles.Clear();
            _idCounter = 0;
            UnitsTaken = 0;
            PiecesWorn = 0;
        }

        public bool TryGet(EntityId id, out LootPile pile)
        {
            int index = IndexOf(id);
            pile = index >= 0 ? _piles[index] : null;
            return index >= 0;
        }

        /// <summary>
        /// Drops a pile of <paramref name="items"/> (at most <see cref="Capacity"/> stacks; the rest are lost) and
        /// <paramref name="gold"/> at <paramref name="at"/>, claimed for <paramref name="claimedFor"/> (none: anyone) for
        /// <see cref="ClaimHours"/>; null when there is nothing to drop.
        /// </summary>
        public LootPile Drop(IPileWorld world, Vec2 at, ReadOnlySpan<ItemInstance> items, int gold, EntityId claimedFor, uint tick, EntityId bodyOf = default)
        {
            int stacks = 0;
            for (int i = 0; i < items.Length; i++) if (!items[i].IsEmpty) stacks++;
            if (stacks == 0 && gold <= 0 && bodyOf == default) return null; // a body lies even with nothing on it (K5.4a)
            var pile = new LootPile(NextId(), at, tick + DayClock.Hours(FadeHours), claimedFor, claimedFor != default ? tick + DayClock.Hours(ClaimHours) : 0u) { BodyOf = bodyOf };
            for (int i = 0; i < items.Length; i++) pile.TryAdd(items[i]);
            pile.Gold = gold > 0 ? gold : 0;
            Insert(world, pile);
            if (pile.Gold > 0) world?.GoldDropped(pile, pile.Gold);
            SimLog.Simulation.ZLogInformation($"A pile of {pile.Count} stacks and {pile.Gold}g lies at {at}");
            return pile;
        }

        /// <summary>
        /// <paramref name="taker"/> takes from <paramref name="pileId"/> what <see cref="LootRules"/> says it takes, if it may
        /// (its victor, or anyone past the claim): materials and consumables into <paramref name="inventory"/>'s pack as they
        /// fit, the purse, and each piece of equipment it would wear, worn at once, what it replaces left in the pile in its
        /// place. An emptied pile is gone. Returns the units taken (a piece worn counts one); the gold taken in
        /// <paramref name="gold"/>, the pieces worn in <paramref name="worn"/>.
        /// </summary>
        public int Take(IPileWorld world, EntityId pileId, Simpiens.Cognition.Inventory.AgentInventory inventory, EntityId taker, uint tick, out int gold, out int worn)
        {
            gold = 0;
            worn = 0;
            int index = IndexOf(pileId);
            if (index < 0 || inventory == null) return 0;
            var pile = _piles[index];
            if (!pile.MayTake(taker, tick)) return 0;
            int units = 0;
            int count = pile.Count; // pieces left in exchange are appended past it, and not looked at again
            for (int i = 0; i < count; i++)
            {
                var stack = pile.StackAt(i);
                if (stack.IsEmpty) continue;
                if (ItemCatalog.Get(stack.Type)?.Equipment != null)
                {
                    if (TryWear(pile, i, stack, inventory))
                    {
                        units++;
                        worn++;
                    }
                    continue;
                }
                inventory.TryAddItem(stack, out var remainder);
                units += stack.Quantity - remainder.Quantity;
                pile.SetAt(i, remainder);
            }
            pile.Compact();
            UnitsTaken += units;
            PiecesWorn += worn;
            if (pile.Gold > 0)
            {
                gold = pile.Gold;
                inventory.AddGold(gold);
                pile.Gold = 0;
                world?.GoldTaken(pile, gold);
            }
            if (pile.IsEmpty && !pile.IsBody) Remove(world, index); // a body stays until it fades, picked clean or not (K5.4a)
            return units;
        }

        // One piece of the stack at index, worn if the taker would wear it (LootRules); what its slot held goes into the
        // pile in its place (or after the rest of its stack, if there is room; else the swap is undone)
        private static bool TryWear(LootPile pile, int index, in ItemInstance stack, Simpiens.Cognition.Inventory.AgentInventory inventory)
        {
            var piece = stack.WithQuantity(1);
            if (!LootRules.WouldWear(inventory, piece, out var slot)) return false;
            var rest = stack.Quantity > 1 ? stack.WithQuantity((ushort)(stack.Quantity - 1)) : ItemInstance.Empty;
            var old = inventory.GetEquipped(slot);
            if (!rest.IsEmpty && !old.IsEmpty && pile.Count >= Capacity) return false; // nowhere to leave the old piece
            if (!old.IsEmpty) inventory.Unequip(slot);
            if (!inventory.TryEquip(piece, slot))
            {
                if (!old.IsEmpty) inventory.TryEquip(old, slot);
                return false;
            }
            if (rest.IsEmpty) pile.SetAt(index, old);
            else
            {
                pile.SetAt(index, rest);
                if (!old.IsEmpty) pile.TryAdd(old);
            }
            return true;
        }

        /// <summary>The piles' tick: a pile past its time fades, its gold with it (K5's account for gold on the ground). Allocates nothing.</summary>
        public void Tick(IPileWorld world, uint tick)
        {
            for (int i = _piles.Count - 1; i >= 0; i--)
            {
                var pile = _piles[i];
                if (tick < pile.FadesAt) continue;
                if (pile.Gold > 0) world?.GoldFaded(pile, pile.Gold);
                Remove(world, i);
            }
        }

        /// <summary>The piles for a save: the id counter, and each pile with its stacks, in id order. Main thread, between ticks.</summary>
        public Simpiens.Simulation.Persistence.PileSave Capture()
        {
            int stacks = 0;
            for (int i = 0; i < _piles.Count; i++) stacks += _piles[i].Count;
            var records = new Simpiens.Simulation.Persistence.PileRecord[_piles.Count];
            var items = new ItemInstance[stacks];
            int n = 0;
            for (int i = 0; i < _piles.Count; i++)
            {
                var pile = _piles[i];
                records[i] = new Simpiens.Simulation.Persistence.PileRecord(pile);
                for (int s = 0; s < pile.Count; s++) items[n++] = pile.StackAt(s);
            }
            return new Simpiens.Simulation.Persistence.PileSave { IdCounter = _idCounter, Piles = records, Stacks = items };
        }

        /// <summary>Why <paramref name="save"/>'s piles can't be loaded (player-facing text), or null: stacks past a pile's room or the save's, ids out of order.</summary>
        public static string Check(Simpiens.Simulation.Persistence.PileSave save)
        {
            if (save == null) return null;
            if (save.Piles == null || save.Stacks == null) return "its piles are incomplete";
            int stacks = 0;
            for (int i = 0; i < save.Piles.Length; i++)
            {
                if (save.Piles[i].StackCount > Capacity) return "a pile with more stacks than a pile holds";
                if (i > 0 && save.Piles[i - 1].Id.CompareTo(save.Piles[i].Id) >= 0) return "piles out of order";
                stacks += save.Piles[i].StackCount;
            }
            return stacks == save.Stacks.Length ? null : "its piles' stacks don't add up";
        }

        /// <summary>Puts a save's piles back (a load, after <see cref="Check"/>); a save without them starts with none.</summary>
        public void Restore(IPileWorld world, Simpiens.Simulation.Persistence.PileSave save, ulong seed)
        {
            Begin(world, seed);
            if (save == null) return;
            _idCounter = save.IdCounter;
            int n = 0;
            for (int i = 0; i < save.Piles.Length; i++)
            {
                var r = save.Piles[i];
                var pile = new LootPile(r.Id, r.Position, r.FadesAt, r.ClaimedFor, r.ClaimUntil) { Gold = r.Gold, BodyOf = r.BodyOf };
                for (int s = 0; s < r.StackCount; s++) pile.TryAdd(save.Stacks[n++]);
                Insert(world, pile);
            }
        }

        private void Insert(IPileWorld world, LootPile pile)
        {
            int at = ~IndexOf(pile.Id);
            _piles.Insert(at < 0 ? _piles.Count : at, pile);
            pile.Entity = new WorldEntity(pile.Id, EntityType.Pile, pile.Position, Radius, name: "Pile", pile: pile);
            world?.AddPile(pile.Entity);
        }

        private void Remove(IPileWorld world, int index)
        {
            var pile = _piles[index];
            _piles.RemoveAt(index);
            world?.RemovePile(pile.Id);
        }

        // The piles' own seeded sequence: the n-th id depends only on the seed and n
        private EntityId NextId()
        {
            while (true)
            {
                var rng = SimRandom.For(_seed, default, _idCounter++, SimRandomSalt.PileId);
                ulong hi = rng.NextULong();
                ulong lo = rng.NextULong();
                var id = new EntityId((uint)(hi >> 32), (uint)hi, (uint)(lo >> 32), (uint)lo);
                if (id != default && id != WorldRegistry.PlayerEntityId) return id;
            }
        }

        private int IndexOf(EntityId id)
        {
            int lo = 0, hi = _piles.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int c = _piles[mid].Id.CompareTo(id);
                if (c == 0) return mid;
                if (c < 0) lo = mid + 1;
                else hi = mid - 1;
            }
            return ~lo;
        }
    }
}
