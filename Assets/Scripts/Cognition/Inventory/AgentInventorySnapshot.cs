using System;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Inventory
{
    /// <summary>
    /// A preallocated, zero-allocation read-only snapshot of an agent's inventory.
    /// Handed to background cognitive evaluators to prevent struct tearing and data races.
    /// </summary>
    public class AgentInventorySnapshot : IAgentInventory
    {
        private readonly ItemInstance[] _slots;
        private readonly int _capacity;

        public int Capacity => _capacity;
        public int Gold { get; private set; }

        public AgentInventorySnapshot(int capacity = AgentInventory.DefaultCapacity)
        {
            _capacity = capacity > 0 ? capacity : AgentInventory.DefaultCapacity;
            _slots = new ItemInstance[_capacity];
            Gold = 0;
        }

        /// <summary>
        /// Populates this snapshot buffer from live AgentInventory with zero heap allocations.
        /// Must be called on the main simulation thread.
        /// </summary>
        public void PopulateFrom(AgentInventory source)
        {
            if (source == null)
            {
                Array.Clear(_slots, 0, _capacity);
                Gold = 0;
                return;
            }

            Gold = source.Gold;
            int copyCount = Math.Min(_capacity, source.Capacity);
            for (int i = 0; i < copyCount; i++)
            {
                _slots[i] = source.GetSlot(i);
            }

            for (int i = copyCount; i < _capacity; i++)
            {
                _slots[i] = ItemInstance.Empty;
            }
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
    }
}
