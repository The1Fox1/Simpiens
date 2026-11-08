using System.Collections.Generic;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// A place a crawler wears or wields a piece, by id (Epic 10 Part 3, K1.1, seam A3). The id is the slot's index in a
    /// crawler's worn equipment and in its save: never reuse one, and keep it under <see cref="EquipmentSlots.Capacity"/>.
    /// What a slot is (its kind, region, layer and place on the paper doll) is its row in <see cref="EquipmentSlots"/>;
    /// code asks the table, not the id.
    /// </summary>
    public enum EquipmentSlot : byte
    {
        // 0: protection, the one protective piece (Epic 10 G3; the item system's I4). Retired in K1.1: head and body took it
        Weapon = 1,
        Tool = 2,
        Head = 3,
        Body = 4
    }

    /// <summary>
    /// What fits a slot (K1.1): an item's equipment names a kind, never a slot, so a kind may have several slots (two
    /// rings, two hands) and the piece goes in the first that is free. A new kind is a value here (never reused) and the
    /// slots that take it.
    /// </summary>
    public enum SlotKind : byte
    {
        None = 0,
        Weapon = 1,
        Tool = 2,
        Head = 3,
        Body = 4
    }

    /// <summary>The part of the body a slot's piece is worn on (K1.1): what a portrait shows (the head, the shoulders) and where a worn sprite hangs.</summary>
    public enum BodyRegion : byte
    {
        None = 0,
        Head = 1,
        Torso = 2,
        Hands = 3,
        Waist = 4,
        Legs = 5,
        Feet = 6
    }

    /// <summary>One equipment slot: a row of <see cref="EquipmentSlots"/>.</summary>
    public sealed class SlotDefinition
    {
        public SlotDefinition(EquipmentSlot id, string name, string displayName, SlotKind kind, BodyRegion region)
        {
            Id = id;
            Name = name;
            DisplayName = displayName;
            Kind = kind;
            Region = region;
        }

        public EquipmentSlot Id { get; }

        /// <summary>A code name, for logs and saves' refusals.</summary>
        public string Name { get; }

        /// <summary>The player's word for it ("Head").</summary>
        public string DisplayName { get; }

        /// <summary>What fits it: an item whose equipment is of this kind.</summary>
        public SlotKind Kind { get; }

        public BodyRegion Region { get; }

        /// <summary>Worn sprites are drawn up the layers, lowest first: the body's coat under the head's helm under the weapon in hand.</summary>
        public byte DrawLayer { get; init; }

        /// <summary>The slot's cell on the paper doll (the dossier's and the party builder's equipment panels): its column.</summary>
        public byte DollColumn { get; init; }

        /// <summary>The slot's cell on the paper doll: its row, from the top.</summary>
        public byte DollRow { get; init; }

        /// <summary>Where it comes in a list of slots (a party row's gear, a dossier's lines): lowest first, then by id.</summary>
        public byte Order { get; init; }

        /// <summary>True when an item of <paramref name="item"/> may be worn here: it is equipment, of this slot's kind.</summary>
        public bool Fits(ItemType item) => item != ItemType.None && ItemCatalog.Get(item)?.Equipment?.Kind == Kind;

        public override string ToString() => $"{Name} (slot {(byte)Id}, {Kind})";
    }

    /// <summary>
    /// Every equipment slot the game knows, by id (Epic 10 Part 3, K1.1, seam A3; the producer's Q11: "the foundation of
    /// a more complex appendable equipment system"). Static and immutable, as the item catalogue is: any code reads it,
    /// on any thread. Stats, wear, outfitting, the dossier and worn sprites read the slots from here, so a new slot is a row.
    /// <para>
    /// To add a slot: an <see cref="EquipmentSlot"/> value under <see cref="Capacity"/> (never reuse one), a row below, and
    /// the items that fit its kind; a new kind is a <see cref="SlotKind"/> value. A crawler's storage has room for every
    /// id, so a new slot loads an older save with nothing in it.
    /// </para>
    /// </summary>
    public static class EquipmentSlots
    {
        /// <summary>The most slots there can be: the room a crawler's worn equipment and its save keep (K1.2). A slot id at or above it changes the save format.</summary>
        public const int Capacity = 16;

        /// <summary>The table, built once and swapped whole (only by a test's <see cref="ExtendForTest"/>), so readers never see a half-built one.</summary>
        private sealed class Tables
        {
            public readonly SlotDefinition[] ById = new SlotDefinition[Capacity];
            public readonly SlotDefinition[] All;
            public readonly SlotDefinition[] InDisplayOrder;

            public Tables(IEnumerable<SlotDefinition> definitions)
            {
                var all = new List<SlotDefinition>(Capacity);
                foreach (var definition in definitions)
                {
                    int id = (byte)definition.Id;
                    if (id >= Capacity) throw new System.ArgumentException($"Slot {definition.Name} has id {id}; slots have ids under {Capacity}.");
                    if (definition.Kind == SlotKind.None) throw new System.ArgumentException($"Slot {definition.Name} takes no kind of item.");
                    if (ById[id] != null) throw new System.ArgumentException($"Two slots have id {id}.");
                    ById[id] = definition;
                }
                for (int id = 0; id < Capacity; id++)
                {
                    if (ById[id] != null) all.Add(ById[id]);
                }
                All = all.ToArray();
                var shown = new List<SlotDefinition>(all);
                shown.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : ((byte)a.Id).CompareTo((byte)b.Id));
                InDisplayOrder = shown.ToArray();
            }
        }

        private static volatile Tables _tables = new Tables(Definitions());

        /// <summary>The game's slots (Q11's reading: head and body take protection's place; the weapon and the tool stay).</summary>
        private static SlotDefinition[] Definitions() => new[]
        {
            // The hard hat, the respirator worn as a mask; an iron helm (K3)
            new SlotDefinition(EquipmentSlot.Head, "Head", "Head", SlotKind.Head, BodyRegion.Head)
                { DrawLayer = 30, DollColumn = 1, DollRow = 0, Order = 0 },
            // Hazard gear; armour (K3, K4)
            new SlotDefinition(EquipmentSlot.Body, "Body", "Body", SlotKind.Body, BodyRegion.Torso)
                { DrawLayer = 20, DollColumn = 1, DollRow = 1, Order = 1 },
            // Melee arms (K3), ranged ones (K8)
            new SlotDefinition(EquipmentSlot.Weapon, "Weapon", "Weapon", SlotKind.Weapon, BodyRegion.Hands)
                { DrawLayer = 40, DollColumn = 0, DollRow = 1, Order = 2 },
            // The pickaxe
            new SlotDefinition(EquipmentSlot.Tool, "Tool", "Tool", SlotKind.Tool, BodyRegion.Waist)
                { DrawLayer = 10, DollColumn = 2, DollRow = 1, Order = 3 }
        };

        /// <summary>
        /// Tests only: the table with <paramref name="extra"/> added, until the returned scope is disposed (K1.6, the proof
        /// that a slot needs no code beyond its row). The table is swapped whole, between ticks; tests that use it run serially.
        /// </summary>
        internal static System.IDisposable ExtendForTest(params SlotDefinition[] extra)
        {
            var previous = _tables;
            var definitions = new List<SlotDefinition>(previous.All);
            definitions.AddRange(extra);
            _tables = new Tables(definitions);
            return new Restore(previous);
        }

        private sealed class Restore : System.IDisposable
        {
            private readonly Tables _previous;
            public Restore(Tables previous) => _previous = previous;
            public void Dispose() => _tables = _previous;
        }

        /// <summary>Every slot, in id order: the order stats are summed and pieces worn down in, so it never varies.</summary>
        public static IReadOnlyList<SlotDefinition> All => _tables.All;

        /// <summary>Every slot in the order lists show them (<see cref="SlotDefinition.Order"/>).</summary>
        public static IReadOnlyList<SlotDefinition> InDisplayOrder => _tables.InDisplayOrder;

        public static bool IsDefined(EquipmentSlot id) => (byte)id < Capacity && _tables.ById[(byte)id] != null;

        /// <summary>The slot's row, or null for a retired or undefined id.</summary>
        public static SlotDefinition Get(EquipmentSlot id) => (byte)id < Capacity ? _tables.ById[(byte)id] : null;

        /// <summary>True when <paramref name="item"/> may be worn in slot <paramref name="id"/>.</summary>
        public static bool Fits(EquipmentSlot id, ItemType item) => Get(id)?.Fits(item) == true;

        /// <summary>The first slot (id order) that takes <paramref name="kind"/>; false when none does.</summary>
        public static bool TryFirstOf(SlotKind kind, out EquipmentSlot slot)
        {
            var all = _tables.All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Kind != kind) continue;
                slot = all[i].Id;
                return true;
            }
            slot = default;
            return false;
        }

        /// <summary>A code name for logs: the row's, or the raw id for a retired or undefined one.</summary>
        public static string NameOf(EquipmentSlot id) => Get(id)?.Name ?? $"Slot{(byte)id}";
    }
}
