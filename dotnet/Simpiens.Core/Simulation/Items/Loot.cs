using System;
using System.Collections.Generic;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Items
{
    public enum LootEntryKind : byte
    {
        /// <summary>An item, in a quantity range.</summary>
        Item = 0,

        /// <summary>Another table, rolled in its place (nested at most <see cref="LootRoller.MaxDepth"/> deep).</summary>
        Table = 1,

        /// <summary>Nothing: a chance of no drop.</summary>
        Nothing = 2
    }

    /// <summary>One entry of a loot table: what it gives, and its weight among the table's entries.</summary>
    public readonly struct LootEntry
    {
        public readonly LootEntryKind Kind;
        public readonly ItemType Item;
        public readonly ushort TableId;
        public readonly ushort MinQuantity;
        public readonly ushort MaxQuantity;

        /// <summary>The entry's weight; 0 on an item entry means the item's rarity score (ItemRarity.Score).</summary>
        public readonly uint Weight;

        private LootEntry(LootEntryKind kind, ItemType item, ushort tableId, ushort minQuantity, ushort maxQuantity, uint weight)
        {
            Kind = kind;
            Item = item;
            TableId = tableId;
            MinQuantity = minQuantity;
            MaxQuantity = maxQuantity;
            Weight = weight;
        }

        /// <summary>An item, <paramref name="min"/> to <paramref name="max"/> units; weight 0 takes its rarity score.</summary>
        public static LootEntry ItemDrop(ItemType item, ushort min = 1, ushort max = 1, uint weight = 0) => new LootEntry(LootEntryKind.Item, item, 0, min, max, weight);

        public static LootEntry NestedTable(ushort tableId, uint weight) => new LootEntry(LootEntryKind.Table, ItemType.None, tableId, 0, 0, weight);

        public static LootEntry Nothing(uint weight) => new LootEntry(LootEntryKind.Nothing, ItemType.None, 0, 0, 0, weight);

        /// <summary>The weight the roll uses: an item entry's own, or its item's rarity score.</summary>
        public uint EffectiveWeight => Kind == LootEntryKind.Item && Weight == 0 ? (ItemCatalog.Get(Item)?.Rarity.Score() ?? 0) : Weight;
    }

    /// <summary>
    /// A loot table (item system I7, a foundation not yet wired into play): a number of rolls, each picking one entry by
    /// weight. Immutable; validated by <see cref="LootTables"/>.
    /// </summary>
    public sealed class LootTable
    {
        private readonly LootEntry[] _entries;
        private readonly uint[] _cumulative;

        public LootTable(ushort id, string name, byte minRolls, byte maxRolls, params LootEntry[] entries)
        {
            if (id == 0) throw new ArgumentException("Table id 0 means none.", nameof(id));
            if (maxRolls < minRolls) throw new ArgumentException($"Table '{name}': {minRolls} to {maxRolls} rolls.", nameof(maxRolls));
            if (entries == null || entries.Length == 0) throw new ArgumentException($"Table '{name}' has no entries.", nameof(entries));
            Id = id;
            Name = name;
            MinRolls = minRolls;
            MaxRolls = maxRolls;
            _entries = (LootEntry[])entries.Clone();
            _cumulative = new uint[_entries.Length];
            ulong total = 0;
            for (int i = 0; i < _entries.Length; i++)
            {
                var entry = _entries[i];
                if (entry.Kind == LootEntryKind.Item)
                {
                    if (!ItemCatalog.IsDefined(entry.Item)) throw new ArgumentException($"Table '{name}': item {(byte)entry.Item} isn't in the catalog.");
                    if (entry.MinQuantity < 1 || entry.MaxQuantity < entry.MinQuantity) throw new ArgumentException($"Table '{name}': {entry.Item} drops {entry.MinQuantity} to {entry.MaxQuantity}.");
                }
                uint weight = entry.EffectiveWeight;
                if (weight == 0) throw new ArgumentException($"Table '{name}': entry {i} weighs nothing.");
                total += weight;
                if (total > uint.MaxValue) throw new ArgumentException($"Table '{name}': the weights add up past {uint.MaxValue}.");
                _cumulative[i] = (uint)total;
            }
            TotalWeight = (uint)total;
        }

        public ushort Id { get; }
        public string Name { get; }
        public byte MinRolls { get; }
        public byte MaxRolls { get; }
        public uint TotalWeight { get; }
        public int EntryCount => _entries.Length;
        public LootEntry GetEntry(int index) => _entries[index];

        /// <summary>The entry a draw in [0, TotalWeight) lands on.</summary>
        internal int EntryAt(uint draw)
        {
            for (int i = 0; i < _cumulative.Length; i++)
            {
                if (draw < _cumulative[i]) return i;
            }
            return _cumulative.Length - 1;
        }
    }

    /// <summary>
    /// A set of loot tables by id, checked as a whole: every nested table exists, nothing nests in a cycle, and no chain
    /// is deeper than <see cref="LootRoller.MaxDepth"/>.
    /// </summary>
    public sealed class LootTables
    {
        private readonly Dictionary<ushort, LootTable> _byId = new Dictionary<ushort, LootTable>();

        public LootTables(params LootTable[] tables)
        {
            foreach (var table in tables ?? Array.Empty<LootTable>())
            {
                if (_byId.ContainsKey(table.Id)) throw new ArgumentException($"Two loot tables have id {table.Id}.");
                _byId[table.Id] = table;
            }
            foreach (var table in _byId.Values) CheckNesting(table, 1, new HashSet<ushort>());
        }

        public bool TryGet(ushort id, out LootTable table) => _byId.TryGetValue(id, out table);

        private void CheckNesting(LootTable table, int depth, HashSet<ushort> path)
        {
            if (!path.Add(table.Id)) throw new ArgumentException($"Loot table '{table.Name}' nests in itself.");
            if (depth > LootRoller.MaxDepth) throw new ArgumentException($"Loot table '{table.Name}' nests deeper than {LootRoller.MaxDepth}.");
            for (int i = 0; i < table.EntryCount; i++)
            {
                var entry = table.GetEntry(i);
                if (entry.Kind != LootEntryKind.Table) continue;
                if (!_byId.TryGetValue(entry.TableId, out var nested)) throw new ArgumentException($"Loot table '{table.Name}' nests table {entry.TableId}, which doesn't exist.");
                CheckNesting(nested, depth + 1, path);
            }
            path.Remove(table.Id);
        }
    }

    /// <summary>
    /// Rolls loot tables deterministically (item system I7): the stream is SimRandom's for (seed, entity, tick,
    /// <see cref="SimRandomSalt.Loot"/>), so the same inputs drop the same items on any thread and in any order, and the
    /// weighted pick is integer arithmetic, the same on every machine. Drops go into the caller's buffer: no allocation.
    /// </summary>
    public static class LootRoller
    {
        public const int MaxDepth = 4;

        /// <summary>Rolls table <paramref name="tableId"/> into <paramref name="drops"/>; returns how many drops were written (it stops when the buffer is full).</summary>
        public static int Roll(LootTables tables, ushort tableId, ulong seed, EntityId entity, long tick, Span<ItemInstance> drops)
        {
            if (tables == null || !tables.TryGet(tableId, out var table)) return 0;
            var random = SimRandom.For(seed, entity, tick, SimRandomSalt.Loot);
            int count = 0;
            RollTable(tables, table, ref random, drops, ref count, 1);
            return count;
        }

        private static void RollTable(LootTables tables, LootTable table, ref SimRandom random, Span<ItemInstance> drops, ref int count, int depth)
        {
            int rolls = table.MinRolls + (int)Below(ref random, (uint)(table.MaxRolls - table.MinRolls + 1));
            for (int r = 0; r < rolls; r++)
            {
                var entry = table.GetEntry(table.EntryAt(Below(ref random, table.TotalWeight)));
                switch (entry.Kind)
                {
                    case LootEntryKind.Item:
                        ushort quantity = (ushort)(entry.MinQuantity + Below(ref random, (uint)(entry.MaxQuantity - entry.MinQuantity + 1)));
                        if (count < drops.Length) drops[count++] = new ItemInstance(1, entry.Item, quantity, 0);
                        break;
                    case LootEntryKind.Table:
                        if (depth < MaxDepth && tables.TryGet(entry.TableId, out var nested)) RollTable(tables, nested, ref random, drops, ref count, depth + 1);
                        break;
                }
            }
        }

        /// <summary>A uniform draw in [0, bound), by integer arithmetic (the modulo's bias is under 2^-32 for any bound).</summary>
        private static uint Below(ref SimRandom random, uint bound) => bound <= 1 ? 0u : (uint)(random.NextULong() % bound);
    }
}
