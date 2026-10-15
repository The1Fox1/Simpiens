using System;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;

namespace Simpiens.Cognition.Inventory
{
    /// <summary>
    /// Fixed-capacity, zero-allocation inventory storage for autonomous agents.
    /// Manages unmanaged ItemInstance slots and gold currency.
    /// </summary>
    public class AgentInventory : IAgentInventory
    {
        public const int DefaultCapacity = 4;
        public const ushort MaxStackSize = 99;

        private readonly ItemInstance[] _slots;
        private readonly int _capacity;
        private readonly AgentInventorySnapshot[] _snapshots;
        private int _snapshotIndex;

        public int Capacity => _capacity;
        public int Gold { get; set; }

        public AgentInventory(int capacity = DefaultCapacity, int startingGold = Simpiens.Simulation.Economy.EconomyTuning.InventoryDefaultGold)
        {
            _capacity = capacity > 0 ? capacity : DefaultCapacity;
            _slots = new ItemInstance[_capacity];
            Gold = Math.Max(0, startingGold);

            _snapshots = new AgentInventorySnapshot[2]
            {
                new AgentInventorySnapshot(_capacity),
                new AgentInventorySnapshot(_capacity)
            };
            _snapshotIndex = 0;
        }

        public IAgentInventory GetSnapshot()
        {
            _snapshotIndex = 1 - _snapshotIndex;
            var snap = _snapshots[_snapshotIndex];
            snap.PopulateFrom(this);
            return snap;
        }

        public ItemInstance GetSlot(int index)
        {
            if (index < 0 || index >= _capacity) return ItemInstance.Empty;
            return _slots[index];
        }

        public int OccupiedSlots
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _capacity; i++)
                {
                    if (!_slots[i].IsEmpty) count++;
                }
                return count;
            }
        }

        public bool TryAddItem(in ItemInstance item, out ItemInstance remainder)
        {
            if (item.IsEmpty)
            {
                remainder = ItemInstance.Empty;
                return true;
            }

            ushort remainingQty = item.Quantity;

            // 1. Stack into existing slots of the same Type and Quality
            for (int i = 0; i < _capacity; i++)
            {
                if (!_slots[i].IsEmpty && _slots[i].Type == item.Type && _slots[i].Quality == item.Quality)
                {
                    ushort currentQty = _slots[i].Quantity;
                    ushort spaceInSlot = (ushort)Math.Max(0, MaxStackSize - currentQty);

                    if (spaceInSlot > 0)
                    {
                        ushort qtyToAdd = Math.Min(spaceInSlot, remainingQty);
                        _slots[i] = _slots[i].WithQuantity((ushort)(currentQty + qtyToAdd));
                        remainingQty -= qtyToAdd;

                        if (remainingQty == 0)
                        {
                            remainder = ItemInstance.Empty;
                            return true;
                        }
                    }
                }
            }

            // 2. Occupy the first available empty slot
            for (int i = 0; i < _capacity; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    ushort qtyToAdd = Math.Min(MaxStackSize, remainingQty);
                    _slots[i] = item.WithQuantity(qtyToAdd);
                    remainingQty -= qtyToAdd;

                    if (remainingQty == 0)
                    {
                        remainder = ItemInstance.Empty;
                        return true;
                    }
                }
            }

            // Return whatever could not fit
            remainder = item.WithQuantity(remainingQty);
            return false;
        }

        public bool HasItem(ItemType type, int count = 1)
        {
            if (type == ItemType.None || count <= 0) return false;

            int total = 0;
            for (int i = 0; i < _capacity; i++)
            {
                if (_slots[i].Type == type)
                {
                    total += _slots[i].Quantity;
                    if (total >= count) return true;
                }
            }
            return false;
        }

        public int GetItemCount(ItemType type)
        {
            if (type == ItemType.None) return 0;

            int total = 0;
            for (int i = 0; i < _capacity; i++)
            {
                if (_slots[i].Type == type)
                {
                    total += _slots[i].Quantity;
                }
            }
            return total;
        }

        public bool TryRemoveItem(ItemType type, int count)
        {
            if (type == ItemType.None || count <= 0) return false;
            if (!HasItem(type, count)) return false;

            int remainingToRemove = count;
            for (int i = 0; i < _capacity; i++)
            {
                if (_slots[i].Type == type)
                {
                    if (_slots[i].Quantity <= remainingToRemove)
                    {
                        remainingToRemove -= _slots[i].Quantity;
                        _slots[i] = ItemInstance.Empty;
                    }
                    else
                    {
                        _slots[i] = _slots[i].WithQuantity((ushort)(_slots[i].Quantity - remainingToRemove));
                        remainingToRemove = 0;
                    }

                    if (remainingToRemove <= 0) break;
                }
            }
            return true;
        }

        public void AddGold(int amount)
        {
            if (amount > 0)
            {
                Gold += amount;
            }
        }

        public bool TryDeductGold(int amount)
        {
            if (amount < 0) return false;
            if (Gold < amount) return false;

            Gold -= amount;
            return true;
        }

        public void Clear()
        {
            Array.Clear(_slots, 0, _capacity);
        }

        /// <summary>Writes the purse and every slot into a save, reusing its slot array when the capacity matches.</summary>
        public void CaptureTo(InventorySave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.Slots == null || save.Slots.Length != _capacity) save.Slots = new ItemInstance[_capacity];
            Array.Copy(_slots, save.Slots, _capacity);
            save.Gold = Gold;
        }

        /// <summary>Replaces the purse and the slots with a save's. The save must have this inventory's capacity.</summary>
        public void RestoreFrom(InventorySave save)
        {
            if (save?.Slots == null) throw new ArgumentNullException(nameof(save));
            if (save.Slots.Length != _capacity)
            {
                throw new ArgumentException($"The saved pack has {save.Slots.Length} slots; this inventory has {_capacity}.", nameof(save));
            }
            Array.Copy(save.Slots, _slots, _capacity);
            Gold = save.Gold;
        }
    }
}
