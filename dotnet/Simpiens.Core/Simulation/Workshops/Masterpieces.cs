using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Workshops
{
    /// <summary>Where a masterpiece's record says it is (Epic 11, S8.1, D40): out in the world (found by a search), on the stall's shelf, or held at the station that made it while the stall has no room.</summary>
    public enum MasterpiecePlace : byte
    {
        World = 0,
        Shelf = 1,
        Held = 2
    }

    /// <summary>The form of a masterpiece's name (D42): "the {adjective} {noun}", or a dead friend's name and a noun.</summary>
    public enum MasterpieceForm : byte
    {
        Plain = 0,
        Grief = 1
    }

    /// <summary>A masterpiece's owners after its first, the newest last (D40): the last three.</summary>
    [InlineArray(MasterpieceRecord.RecentOwnerSlots)]
    public struct MasterpieceOwners
    {
        private EntityId _owner;
    }

    /// <summary>
    /// A masterpiece's record (Epic 11, S8.1, D40), its provenance: what it is, its name's parts and whom it is named for,
    /// its maker and where and when it was made, its kills and the last named creature slain with it, and its owners (the
    /// first and the last three). Unmanaged, saved raw in the workshops' block (format 14), its size pinned: an
    /// <see cref="EntityId"/> sits on a 4-byte boundary. A piece carries its record's id as its mark
    /// (<see cref="ItemInstance.Masterpiece"/>).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MasterpieceRecord
    {
        public const int RecentOwnerSlots = 3;

        public ushort Id;
        public ItemType Item;
        public StationKind Station;
        public byte StationIndex;
        public MasterpiecePlace Place;
        public MasterpieceForm Form;
        public byte Adjective;
        public byte Noun;
        public byte OwnerCount;
        public ushort Kills;
        public uint MadeAt;
        public ushort NamedSlain;
        public byte SlainRank;
        private byte _pad0;
        public EntityId Maker;
        public EntityId NamedFor;
        public EntityId SlainVictim;
        public EntityId FirstOwner;
        public MasterpieceOwners Owners;

        /// <summary>Whom it was last handed to: its newest owner, or nobody yet.</summary>
        public readonly EntityId LastOwner
        {
            get
            {
                if (OwnerCount == 0) return default;
                if (OwnerCount == 1) return FirstOwner;
                int recent = Math.Min(OwnerCount - 1, RecentOwnerSlots);
                return Owners[recent - 1];
            }
        }

        /// <summary>The <paramref name="index"/>-th of its recent owners after the first, oldest first (as many as <see cref="RecentCount"/>).</summary>
        public readonly EntityId RecentAt(int index) => Owners[index];

        /// <summary>How many of its owners after the first are kept: up to three.</summary>
        public readonly int RecentCount => OwnerCount <= 1 ? 0 : Math.Min(OwnerCount - 1, RecentOwnerSlots);

        /// <summary>
        /// A new owner (D43): the first is kept for good, the later ones as the last three; the count saturates at 255. The
        /// same owner again (bought back from the shelf it handed it to) is not a new one.
        /// </summary>
        public void AddOwner(EntityId owner)
        {
            if (owner == default || owner == LastOwner) return;
            if (OwnerCount == 0) FirstOwner = owner;
            else
            {
                int recent = Math.Min(OwnerCount - 1, RecentOwnerSlots);
                if (recent == RecentOwnerSlots)
                {
                    for (int i = 1; i < RecentOwnerSlots; i++) Owners[i - 1] = Owners[i];
                    Owners[RecentOwnerSlots - 1] = owner;
                }
                else Owners[recent] = owner;
            }
            if (OwnerCount < byte.MaxValue) OwnerCount++;
        }
    }

    /// <summary>
    /// The outpost's masterpieces (Epic 11, S8.1, D40): one record per masterpiece made, by id (1 onward, in the order
    /// made; never reused), owned by the workshops and saved in their block. Main thread only; views read it through the
    /// dashboard's context. Where a piece is now is found, not tracked (D40): its record says only whether it is on the
    /// shelf or held at its station; out in the world, a search of the crawlers and the piles finds it, or it is lost.
    /// </summary>
    public sealed class MasterpieceService
    {
        private readonly List<MasterpieceRecord> _records = new List<MasterpieceRecord>(8);

        /// <summary>How many masterpieces have been made.</summary>
        public int Count => _records.Count;

        /// <summary>The record at <paramref name="index"/> (0 onward), a copy: masterpiece <c>index + 1</c>.</summary>
        public MasterpieceRecord this[int index] => _records[index];

        /// <summary>The record of masterpiece <paramref name="id"/>; false for 0 or an id never made.</summary>
        public bool TryGet(ushort id, out MasterpieceRecord record)
        {
            if (id == 0 || id > _records.Count)
            {
                record = default;
                return false;
            }
            record = _records[id - 1];
            return true;
        }

        /// <summary>The next masterpiece's id, or 0 when the ids are spent (65,535 made).</summary>
        public ushort NextId => _records.Count < ushort.MaxValue ? (ushort)(_records.Count + 1) : (ushort)0;

        /// <summary>A new masterpiece's record, given the next id; that id, or 0 when the ids are spent. Main thread only.</summary>
        public ushort Add(MasterpieceRecord record)
        {
            ushort id = NextId;
            if (id == 0) return 0;
            record.Id = id;
            _records.Add(record);
            return id;
        }

        /// <summary>A masterpiece's record changed (its owner, its place, its kills). Main thread only.</summary>
        public bool Set(in MasterpieceRecord record)
        {
            if (record.Id == 0 || record.Id > _records.Count) return false;
            _records[record.Id - 1] = record;
            return true;
        }

        // --- Through hands (Epic 11, S8.3, D43) ---

        /// <summary>The masterpiece of <paramref name="item"/> on the stall's shelf that a sale or a gift hands out first (the oldest), or 0.</summary>
        public ushort OnShelf(ItemType item)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].Place == MasterpiecePlace.Shelf && _records[i].Item == item) return _records[i].Id;
            }
            return 0;
        }

        /// <summary>
        /// The piece a sale or a gift hands out (D43): when the shelf's best is Masterwork and a masterpiece of the item waits
        /// there, that masterpiece, at its grade and marked, and its id in <paramref name="masterpiece"/>; otherwise an
        /// ordinary piece at <paramref name="grade"/>. Nothing changes until <see cref="HandOut"/>.
        /// </summary>
        public ItemInstance PieceFor(ItemType item, byte grade, ushort price, out ushort masterpiece)
        {
            masterpiece = grade == Grades.Masterwork ? OnShelf(item) : (ushort)0;
            if (masterpiece != 0) grade = Grades.Masterpiece;
            var equipment = ItemCatalog.Get(item)?.Equipment;
            var piece = new ItemInstance(item, 1, price, quality: grade, durability: equipment != null ? equipment.DurabilityAt(grade) : (ushort)100);
            return masterpiece != 0 ? piece.WithMasterpiece(masterpiece) : piece;
        }

        /// <summary>A masterpiece left the shelf for <paramref name="owner"/> (a sale or a gift): out in the world, its new owner recorded.</summary>
        public void HandOut(ushort id, EntityId owner)
        {
            if (!TryGet(id, out var record)) return;
            record.Place = MasterpiecePlace.World;
            record.AddOwner(owner);
            _records[id - 1] = record;
        }

        /// <summary>A masterpiece taken up from the ground by <paramref name="owner"/> (D43): its new owner.</summary>
        public void TakenUp(ushort id, EntityId owner)
        {
            if (!TryGet(id, out var record)) return;
            record.Place = MasterpiecePlace.World;
            record.AddOwner(owner);
            _records[id - 1] = record;
        }

        /// <summary>
        /// A worn piece handed back at the stall (a swap, a trade-in): a masterpiece goes back onto the shelf whole, among its
        /// good's Masterwork pieces, never onto the worn shelf, where a repair would make it Standard (D43); any other piece by
        /// <see cref="Crafting.HandBack"/>.
        /// </summary>
        public TradeStallData HandBack(in TradeStallData stall, in ItemInstance piece)
        {
            if (!piece.IsMasterpiece || !TryGet(piece.Masterpiece, out var record) || !stall.Trades(piece.Type) || !Crafting.HasRoom(stall, piece.Type, 1))
                return Crafting.HandBack(stall, piece);
            record.Place = MasterpiecePlace.Shelf;
            _records[record.Id - 1] = record;
            return stall.WithStocked(piece.Type, 1, Grades.Masterwork);
        }

        /// <summary>
        /// A creature slain by a crawler wearing masterpieces (D44): each counts the kill, and a named creature by whom it was
        /// named for and its rank.
        /// </summary>
        public void CountKill(Simpiens.Cognition.Inventory.IAgentInventory inventory, bool named, EntityId victim, byte rank)
        {
            if (inventory == null || _records.Count == 0) return;
            var slots = EquipmentSlots.All;
            for (int i = 0; i < slots.Count; i++)
            {
                var piece = inventory.GetEquipped(slots[i].Id);
                if (!piece.IsMasterpiece || !TryGet(piece.Masterpiece, out var record)) continue;
                if (record.Kills < ushort.MaxValue) record.Kills++;
                if (named)
                {
                    if (record.NamedSlain < ushort.MaxValue) record.NamedSlain++;
                    record.SlainVictim = victim;
                    record.SlainRank = rank;
                }
                _records[record.Id - 1] = record;
            }
        }

        /// <summary>The masterpieces held at station <paramref name="stationIndex"/> are on the shelf now: the stall had room for its make.</summary>
        public void Shelve(int stationIndex)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                var record = _records[i];
                if (record.Place != MasterpiecePlace.Held || record.StationIndex != stationIndex) continue;
                record.Place = MasterpiecePlace.Shelf;
                _records[i] = record;
            }
        }

        public void Reset() => _records.Clear();

        /// <summary>The records for a save, by id.</summary>
        public MasterpieceRecord[] Capture() => _records.ToArray();

        /// <summary>A save's records put back (a save without them: none). The ids were checked on load (<see cref="Check"/>).</summary>
        public void Restore(MasterpieceRecord[] records)
        {
            _records.Clear();
            if (records != null) _records.AddRange(records);
        }

        /// <summary>Why a save's records can't be loaded (ids not 1 onward in order, an item that isn't worn), or null.</summary>
        public static string Check(MasterpieceRecord[] records)
        {
            if (records == null) return null;
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].Id != i + 1) return $"masterpiece record {i} has id {records[i].Id}, not {i + 1}";
                if (ItemCatalog.Get(records[i].Item)?.Equipment == null) return $"masterpiece {records[i].Id} is of {records[i].Item}, which isn't worn";
            }
            return null;
        }
    }
}
