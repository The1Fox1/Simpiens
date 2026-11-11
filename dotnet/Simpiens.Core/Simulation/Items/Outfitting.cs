using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// A piece worn in a slot in place of what the slot holds, for asking what a crawler's stats would be (Epic 10 Part 3,
    /// K1.4): nothing is moved. An empty piece is the slot bare. The default is no swap.
    /// </summary>
    public readonly struct WornSwap
    {
        public WornSwap(EquipmentSlot slot, in ItemInstance piece)
        {
            Slot = slot;
            Piece = piece;
            IsActive = true;
        }

        public EquipmentSlot Slot { get; }
        public ItemInstance Piece { get; }
        public bool IsActive { get; }
    }

    /// <summary>
    /// What a wearer is outfitted for (K1.4): the hazards it faces and whether it mines. A piece is scored by what it does
    /// to the stats that matter for these, never by its name or group. K3.3 adds fighting creatures (attack and defense).
    /// </summary>
    public readonly struct OutfitNeed
    {
        private readonly uint _hazards;

        private OutfitNeed(uint hazards, bool mining, bool fighting = false)
        {
            _hazards = hazards;
            Mining = mining;
            Fighting = fighting;
        }

        /// <summary>Fights creatures (Epic 10 Part 3, K3.3): attack over a bare hand's, and defense, count.</summary>
        public static OutfitNeed ForFighting => new OutfitNeed(0u, false, true);

        /// <summary>Fights creatures as well as what this need covers.</summary>
        public bool Fighting { get; }

        /// <summary>Faces a hazard of <paramref name="kind"/>; nothing for none.</summary>
        public static OutfitNeed Against(HazardKind kind) => default(OutfitNeed).And(kind);

        /// <summary>Mines: harvest speed counts.</summary>
        public static OutfitNeed ForMining => new OutfitNeed(0u, true);

        /// <summary>Faces every kind of hazard the game knows: what any protection is scored against ("does it protect at all").</summary>
        public static OutfitNeed EveryHazard { get; } = BuildEveryHazard();

        private static OutfitNeed BuildEveryHazard()
        {
            var need = default(OutfitNeed);
            foreach (var kind in System.Enum.GetValues<HazardKind>()) need = need.And(kind);
            return need;
        }

        /// <summary>This need, facing a hazard of <paramref name="kind"/> too.</summary>
        public OutfitNeed And(HazardKind kind) => kind == HazardKind.None || (byte)kind >= 32 ? this : new OutfitNeed(_hazards | (1u << (byte)kind), Mining, Fighting);

        public bool Faces(HazardKind kind) => kind != HazardKind.None && (byte)kind < 32 && (_hazards & (1u << (byte)kind)) != 0;

        public bool Mining { get; }

        /// <summary>Nothing to outfit for: every piece scores 0.</summary>
        public bool IsEmpty => _hazards == 0 && !Mining && !Fighting;

        public override string ToString() => IsEmpty ? "nothing" : $"hazards 0x{_hazards:x}{(Mining ? ", mining" : string.Empty)}{(Fighting ? ", fighting" : string.Empty)}";
    }

    /// <summary>
    /// How well a piece outfits a wearer, by stats (Epic 10 Part 3, K1.4): the one score residents' purchases, the guild's
    /// gifts, the scripted players and the party builder choose by, slot by slot. A piece is worth what it changes in the
    /// wearer's stats, stacked and bounded (<see cref="StatRules"/>), with it worn in its slot: hazard gear over a hard hat
    /// against falling rock is worth 0.15, not its 0.5, since the two together stop at most 0.9. W3's outfitting by hazard
    /// kind is the case of a need with one hazard.
    /// </summary>
    public static class Outfitting
    {
        /// <summary>
        /// How well <paramref name="inventory"/>'s wearer is outfitted for <paramref name="need"/>: for each hazard it faces,
        /// the share of the harm its gear stops (at most 0.9 each), plus how much faster than bare hands it mines.
        /// </summary>
        public static float Score(IAgentInventory inventory, in OutfitNeed need) => Score(inventory, need, default);

        /// <summary>The same, with <paramref name="swap"/>'s piece worn in its slot in place of what is there.</summary>
        public static float Score(IAgentInventory inventory, in OutfitNeed need, in WornSwap swap)
        {
            float score = 0f;
            for (byte kind = 1; kind < 32; kind++)
            {
                if (need.Faces((HazardKind)kind)) score += CrawlerStats.HazardResistance(inventory, (HazardKind)kind, swap);
            }
            if (need.Mining) score += CrawlerStats.Get(inventory, StatKind.HarvestSpeed, 1f, swap) - 1f;
            // Fighting (Epic 10 Part 3, K3.3): attack in bare hands' worth over one, and defense as the share of a blow it stops
            if (need.Fighting)
            {
                float unarmed = Simpiens.Simulation.Combat.CombatRules.UnarmedAttack;
                score += (CrawlerStats.Get(inventory, StatKind.Attack, unarmed, swap) - unarmed) / unarmed;
                float defense = CrawlerStats.Get(inventory, StatKind.Defense, 0f, swap);
                score += defense / (defense + Simpiens.Simulation.Combat.CombatRules.MitigationScale);
            }
            return score;
        }

        /// <summary>How much better wearing <paramref name="piece"/> in <paramref name="slot"/>, in place of what is there, outfits the wearer for <paramref name="need"/>.</summary>
        public static float Gain(IAgentInventory inventory, in OutfitNeed need, EquipmentSlot slot, in ItemInstance piece)
            => Score(inventory, need, new WornSwap(slot, piece)) - Score(inventory, need);

        /// <summary>
        /// The same, against the slot bare (what <paramref name="slot"/> holds taken off): what a fresh piece is worth in place
        /// of one worn down, which is given for its wear alone (D12).
        /// </summary>
        public static float GainOverBare(IAgentInventory inventory, in OutfitNeed need, EquipmentSlot slot, in ItemInstance piece)
            => Score(inventory, need, new WornSwap(slot, piece)) - Score(inventory, need, new WornSwap(slot, ItemInstance.Empty));

        /// <summary>A new piece of <paramref name="item"/> at <paramref name="grade"/>, as a stall hands it over: its full life at the grade (D57).</summary>
        public static ItemInstance NewPiece(ItemType item, byte grade, ushort price = 0)
        {
            var equipment = item != ItemType.None ? ItemCatalog.Get(item)?.Equipment : null;
            return equipment == null ? ItemInstance.Empty : new ItemInstance(1, item, 1, price, quality: grade, durability: equipment.DurabilityAt(grade));
        }

        /// <summary>
        /// The piece the stall sells and has in stock that best outfits a wearer for <paramref name="need"/> in
        /// <paramref name="slot"/> (at the best grade on the shelf, D57), and how much it gains over what the slot holds; over
        /// the slot bare when <paramref name="overBare"/>. The first in id order on a tie (as W3's D50). None, with a gain of
        /// 0, when no piece fits the slot or none gains.
        /// </summary>
        public static ItemType BestInStock(in TradeStallData stall, IAgentInventory inventory, in OutfitNeed need, EquipmentSlot slot, bool overBare, out float gain)
        {
            gain = 0f;
            var best = ItemType.None;
            var definition = EquipmentSlots.Get(slot);
            if (definition == null || need.IsEmpty) return best;
            var items = ItemCatalog.All;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i].Id;
                if (!definition.Fits(item) || stall.SellStockOf(item) < 1) continue;
                var piece = NewPiece(item, stall.BestGradeOf(item));
                float g = overBare ? GainOverBare(inventory, need, slot, piece) : Gain(inventory, need, slot, piece);
                if (g <= gain) continue;
                gain = g;
                best = item;
            }
            return best;
        }
    }
}
