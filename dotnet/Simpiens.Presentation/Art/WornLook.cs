using System;
using System.Runtime.CompilerServices;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Presentation.Art
{
    /// <summary>A piece per slot id (<see cref="EquipmentSlots.Capacity"/>): the storage of a <see cref="WornLook"/>.</summary>
    [InlineArray(EquipmentSlots.Capacity)]
    public struct WornItems
    {
        private ItemType _item;
    }

    /// <summary>
    /// What a crawler shows worn (Epic 10 Part 3, K1.5): the piece in each slot whose art is worn somewhere on the body
    /// (<see cref="ItemArt"/>'s <see cref="Worn"/>), by slot id. Bodies, portraits and today's figures draw it up the
    /// slots' draw layers (<see cref="SlotDefinition.DrawLayer"/>): where two pieces show in one place, the higher layer's
    /// shows. Plain data, compared and hashed whole, so a body or a portrait is baked once per look.
    /// </summary>
    public readonly struct WornLook : IEquatable<WornLook>
    {
        private readonly WornItems _items;

        private WornLook(in WornItems items) => _items = items;

        /// <summary>What <paramref name="inventory"/>'s wearer shows: each slot's piece that is drawn worn; nothing for none.</summary>
        public static WornLook Of(IAgentInventory inventory)
        {
            var items = default(WornItems);
            if (inventory != null)
            {
                var slots = EquipmentSlots.All;
                for (int i = 0; i < slots.Count; i++)
                {
                    var piece = inventory.GetEquipped(slots[i].Id);
                    if (!piece.IsEmpty && ItemArt.WornOf(piece.Type) != Worn.None) items[(byte)slots[i].Id] = piece.Type;
                }
            }
            return new WornLook(items);
        }

        /// <summary>Wearing <paramref name="item"/> in <paramref name="slot"/> besides what this look shows (sheets, pins and tests); unchanged for a slot past the room.</summary>
        public WornLook And(EquipmentSlot slot, ItemType item)
        {
            if ((byte)slot >= EquipmentSlots.Capacity) return this;
            var items = _items;
            items[(byte)slot] = item;
            return new WornLook(items);
        }

        /// <summary>Wearing <paramref name="item"/> in the first slot that takes it, alone (sheets, pins and tests): nothing for an item that fits no slot.</summary>
        public static WornLook Wearing(ItemType item)
        {
            var equipment = item != ItemType.None ? ItemCatalog.Get(item)?.Equipment : null;
            return equipment != null && EquipmentSlots.TryFirstOf(equipment.Kind, out var slot) ? default(WornLook).And(slot, item) : default;
        }

        /// <summary>The piece shown in <paramref name="slot"/>; None for nothing.</summary>
        public ItemType In(EquipmentSlot slot) => (byte)slot < EquipmentSlots.Capacity ? _items[(byte)slot] : ItemType.None;

        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < EquipmentSlots.Capacity; i++)
                {
                    if (_items[i] != ItemType.None) return false;
                }
                return true;
            }
        }

        /// <summary>
        /// The piece that shows <paramref name="where"/> (on the head, over the face, on the torso): of the pieces worn so, the
        /// one in the highest draw layer; None for none.
        /// </summary>
        public ItemType On(Worn where)
        {
            var best = ItemType.None;
            int layer = -1;
            var slots = EquipmentSlots.All;
            for (int i = 0; i < slots.Count; i++)
            {
                var item = _items[(byte)slots[i].Id];
                if (item == ItemType.None || ItemArt.WornOf(item) != where || slots[i].DrawLayer <= layer) continue;
                best = item;
                layer = slots[i].DrawLayer;
            }
            return best;
        }

        /// <summary>The piece in the highest draw layer: what a crawler with no look, drawn as one figure a piece, shows. None for nothing.</summary>
        public ItemType Topmost
        {
            get
            {
                var best = ItemType.None;
                int layer = -1;
                var slots = EquipmentSlots.All;
                for (int i = 0; i < slots.Count; i++)
                {
                    var item = _items[(byte)slots[i].Id];
                    if (item == ItemType.None || slots[i].DrawLayer <= layer) continue;
                    best = item;
                    layer = slots[i].DrawLayer;
                }
                return best;
            }
        }

        public bool Equals(WornLook other)
        {
            for (int i = 0; i < EquipmentSlots.Capacity; i++)
            {
                if (_items[i] != other._items[i]) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is WornLook other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            for (int i = 0; i < EquipmentSlots.Capacity; i++) hash.Add((byte)_items[i]);
            return hash.ToHashCode();
        }

        public static bool operator ==(WornLook a, WornLook b) => a.Equals(b);
        public static bool operator !=(WornLook a, WornLook b) => !a.Equals(b);

        public override string ToString()
        {
            var text = new System.Text.StringBuilder();
            var slots = EquipmentSlots.All;
            for (int i = 0; i < slots.Count; i++)
            {
                var item = _items[(byte)slots[i].Id];
                if (item == ItemType.None) continue;
                text.Append(text.Length == 0 ? string.Empty : ", ").Append(slots[i].Name).Append(": ").Append(ItemCatalog.NameOf(item));
            }
            return text.Length == 0 ? "nothing" : text.ToString();
        }
    }
}
