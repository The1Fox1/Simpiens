using System;
using System.Runtime.InteropServices;

namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// A zero-allocation, 16-byte unmanaged stack struct representing a concrete item instance.
    /// Can be stored in fixed-size arrays without heap allocation.
    /// <para>
    /// Its first field is a masterpiece's mark (Epic 11, S8.1, D39): 0 on every ordinary piece, the id of its record
    /// (<see cref="Simpiens.Simulation.Workshops.MasterpieceService"/>) on a masterpiece. Until then the field was an item id
    /// stamped 1 everywhere, a quirk kept since Unity; the mark took its place in format 14.
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ItemInstance : IEquatable<ItemInstance>
    {
        public readonly ushort Masterpiece; // 2 bytes: a masterpiece's id, 0 for an ordinary piece
        public readonly ItemType Type;     // 1 byte
        public readonly byte Quality;      // 1 byte (0 = Standard, 1 = Fine, 2 = Masterwork)
        public readonly ushort Quantity;   // 2 bytes
        public readonly ushort BaseValue;  // 2 bytes
        public readonly ushort Durability; // 2 bytes
        private readonly ushort _pad0;     // 2 bytes
        private readonly uint _pad1;       // 4 bytes

        public static readonly ItemInstance Empty = default;

        public bool IsEmpty => Type == ItemType.None || Quantity == 0;

        /// <summary>Whether it is a masterpiece: a named piece with a record of its own (D38).</summary>
        public bool IsMasterpiece => Masterpiece != 0;

        public ItemInstance(ItemType type, ushort quantity, ushort baseValue, byte quality = 0, ushort durability = 100)
            : this(0, type, quantity, baseValue, quality, durability)
        {
        }

        private ItemInstance(ushort masterpiece, ItemType type, ushort quantity, ushort baseValue, byte quality, ushort durability)
        {
            Masterpiece = masterpiece;
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
            return new ItemInstance(Masterpiece, Type, newQty, BaseValue, Quality, Durability);
        }

        /// <summary>The same piece marked as the masterpiece <paramref name="id"/> (0: an ordinary piece again).</summary>
        public ItemInstance WithMasterpiece(ushort id)
        {
            return new ItemInstance(id, Type, Quantity, BaseValue, Quality, Durability);
        }

        public ItemInstance WithDurability(ushort durability)
        {
            return new ItemInstance(Masterpiece, Type, Quantity, BaseValue, Quality, durability);
        }

        public bool Equals(ItemInstance other)
        {
            return Masterpiece == other.Masterpiece &&
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
                int hash = (int)Masterpiece;
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
            return IsMasterpiece ? $"{Type} x{Quantity} (masterpiece {Masterpiece}, Q:{Quality}, Val:{BaseValue}g)" : $"{Type} x{Quantity} (Q:{Quality}, Val:{BaseValue}g)";
        }
    }
}
