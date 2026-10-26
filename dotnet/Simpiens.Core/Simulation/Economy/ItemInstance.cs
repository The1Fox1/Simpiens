using System;
using System.Runtime.InteropServices;

namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// A zero-allocation, 16-byte unmanaged stack struct representing a concrete item instance.
    /// Can be stored in fixed-size arrays without heap allocation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ItemInstance : IEquatable<ItemInstance>
    {
        public readonly ushort ItemId;     // 2 bytes
        public readonly ItemType Type;     // 1 byte
        public readonly byte Quality;      // 1 byte (0 = Standard, 1 = Fine, 2 = Masterwork)
        public readonly ushort Quantity;   // 2 bytes
        public readonly ushort BaseValue;  // 2 bytes
        public readonly ushort Durability; // 2 bytes
        private readonly ushort _pad0;     // 2 bytes
        private readonly uint _pad1;       // 4 bytes

        public static readonly ItemInstance Empty = default;

        public bool IsEmpty => Type == ItemType.None || Quantity == 0;

        public ItemInstance(ushort itemId, ItemType type, ushort quantity, ushort baseValue, byte quality = 0, ushort durability = 100)
        {
            ItemId = itemId;
            Type = type;
            Quality = quality;
            Quantity = quantity;
            BaseValue = baseValue;
            Durability = durability;
            _pad0 = 0;
            _pad1 = 0;
        }

        public ItemInstance WithQuantity(ushort newQty)
        {
            return new ItemInstance(ItemId, Type, newQty, BaseValue, Quality, Durability);
        }

        public ItemInstance WithDurability(ushort durability)
        {
            return new ItemInstance(ItemId, Type, Quantity, BaseValue, Quality, durability);
        }

        public bool Equals(ItemInstance other)
        {
            return ItemId == other.ItemId &&
                   Type == other.Type &&
                   Quality == other.Quality &&
                   Quantity == other.Quantity &&
                   BaseValue == other.BaseValue &&
                   Durability == other.Durability;
        }

        public override bool Equals(object obj) => obj is ItemInstance other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)ItemId;
                hash = (hash * 397) ^ (int)Type;
                hash = (hash * 397) ^ (int)Quality;
                hash = (hash * 397) ^ (int)Quantity;
                return hash;
            }
        }

        public static bool operator ==(ItemInstance left, ItemInstance right) => left.Equals(right);
        public static bool operator !=(ItemInstance left, ItemInstance right) => !left.Equals(right);

        public override string ToString()
        {
            if (IsEmpty) return "[Empty]";
            return $"{Type} x{Quantity} (Q:{Quality}, Val:{BaseValue}g)";
        }
    }
}
