using Simpiens.Cognition;
using Simpiens.Cognition.Planning;
using Simpiens.Intervention;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.World;

namespace Simpiens.Simulation.Guild
{
    /// <summary>
    /// Who may sign on with the guild, and on what terms (Epic 10 G4, D2). The simulation enforces it when a command is
    /// applied; the dossier uses the same rule to explain a refusal before the player sends one, as the bounty board uses
    /// <see cref="BountyRules"/>. A future multiplayer server refuses the same commands.
    /// </summary>
    public static class GuildRules
    {
        public enum Check : byte
        {
            Ok = 0,
            /// <summary>No crawler with that id in the session.</summary>
            NoCrawler = 1,
            /// <summary>Drifters pass through; they don't sign on (Q7).</summary>
            Drifter = 2,
            Downed = 3,
            AlreadyMember = 4,
            /// <summary>Trust or warmth toward the player below the threshold (Q5).</summary>
            NotYetTrusted = 5,
            GuildFull = 6,
            /// <summary>It quit or was dismissed too recently.</summary>
            CoolingDown = 7,
            /// <summary>The wage is below the crawler's reservation wage.</summary>
            WageTooLow = 8,
            /// <summary>No stall to pay the fee from.</summary>
            NoStall = 9,
            /// <summary>The stall's treasury can't pay the signing fee.</summary>
            CannotPayFee = 10,
            NotMember = 11,
            /// <summary>A wage of 0, or above the most a wage may be.</summary>
            WageOutOfRange = 12,

            // Expeditions (Epic 10 G5)
            /// <summary>That party isn't out to recall.</summary>
            NoExpedition = 13,
            /// <summary>That party is already out: a party has one expedition at a time (D7; per party since Epic 10 Part 2, D66).</summary>
            ExpeditionOut = 14,
            /// <summary>Not one to four distinct members of the guild (to form a party), or a party none of them downed (to send it).</summary>
            BadParty = 15,
            /// <summary>Not a charted hazard sector with nodes to work.</summary>
            BadTarget = 16,
            /// <summary>A goal of 0, or above the most an order may ask.</summary>
            BadGoal = 17,
            /// <summary>The item isn't something worn.</summary>
            NotGear = 18,
            /// <summary>The stall has none of it to give.</summary>
            OutOfStock = 19,
            /// <summary>The member already wears something in that slot, with more than half its life left.</summary>
            AlreadyWearing = 20,

            // Workshops (Epic 10 Part 2)
            /// <summary>It works a station: one role at a time (Q4).</summary>
            WorksAStation = 21,

            // The guildhall's parties (Epic 10 Part 2, W5, D65)
            /// <summary>No party with that slot.</summary>
            BadSlot = 22,
            /// <summary>The party, or a member's party, is out: its members change once it is home.</summary>
            PartyOut = 23,
            /// <summary>Another party is out to that sector: one party to a sector (D66).</summary>
            SectorTaken = 24
        }

        /// <summary>The least wage per caravan visit a crawler of <paramref name="crawler"/>'s greed signs for: the reservation wage times its greed factor.</summary>
        public static ushort ReservationWage(AgentState crawler, in GuildTuning tuning)
        {
            float wage = tuning.ReservationWage * Considerations.TraitFactor(crawler.Personality.Greed, Considerations.GreedOnPrice);
            return (ushort)SimMath.Clamp(SimMath.RoundToInt(wage), 1, ushort.MaxValue);
        }

        /// <summary>The crawler's trust and warmth toward the player now.</summary>
        public static (int Trust, int Warmth) Standing(AgentState crawler, uint tick)
        {
            if (crawler?.Memory == null) return (0, 0);
            var affinity = crawler.Memory.GetAffinity(WorldRegistry.PlayerEntityId, tick);
            return (affinity.Trust, affinity.Warmth);
        }

        /// <summary>
        /// Whether <paramref name="crawler"/> signs on at <paramref name="wage"/> now. Checked in a fixed order, so a
        /// refusal names the first reason: who it is, its trust, the guild's room, its cooldown, the wage, the fee.
        /// </summary>
        public static Check CheckRecruit(GuildService guild, AgentState crawler, ushort wage, uint tick, in GuildTuning tuning, bool hasStall, uint treasury)
        {
            if (crawler == null || crawler.HasLeft) return Check.NoCrawler;
            if (crawler.IsDrifter) return Check.Drifter;
            if (crawler.IsDowned) return Check.Downed;
            if (guild.IsMember(crawler.AgentId)) return Check.AlreadyMember;
            if (crawler.Memberships.Has(Simpiens.Cognition.Groups.GroupType.Workshop)) return Check.WorksAStation;
            var (trust, warmth) = Standing(crawler, tick);
            if (trust < tuning.RecruitTrust || warmth < tuning.RecruitWarmth) return Check.NotYetTrusted;
            if (guild.Members.Count >= tuning.MaxMembers) return Check.GuildFull;
            if (guild.CooldownUntil(crawler.AgentId) > tick) return Check.CoolingDown;
            if (wage == 0 || wage > tuning.MaxWage) return Check.WageOutOfRange;
            if (wage < ReservationWage(crawler, tuning)) return Check.WageTooLow;
            if (!hasStall) return Check.NoStall;
            if (treasury < tuning.SigningFee) return Check.CannotPayFee;
            return Check.Ok;
        }

        public static Check CheckSetWage(GuildService guild, EntityId crawlerId, ushort wage, in GuildTuning tuning)
        {
            if (!guild.IsMember(crawlerId)) return Check.NotMember;
            if (wage == 0 || wage > tuning.MaxWage) return Check.WageOutOfRange;
            return Check.Ok;
        }

        public static Check CheckDismiss(GuildService guild, EntityId crawlerId) => guild.IsMember(crawlerId) ? Check.Ok : Check.NotMember;

        /// <summary>
        /// Whether a party may be formed as <paramref name="order"/> says (Epic 10 Part 2, W5, D65): one of the guild's slots;
        /// no more than four distinct members of the guild (none empties it); the party home, and each member's own party
        /// home. Checked in that order.
        /// </summary>
        public static Check CheckFormParty(GuildService guild, in FormPartyCommand order)
        {
            if (order.PartySlot < 1 || order.PartySlot > guild.Parties.Count) return Check.BadSlot;
            if (order.Count < 0 || order.Count > DispatchExpeditionCommand.MaxMembers) return Check.BadParty;
            for (int i = 0; i < order.Count; i++)
            {
                if (!guild.IsMember(order[i])) return Check.BadParty;
                for (int j = 0; j < i; j++)
                {
                    if (order[j] == order[i]) return Check.BadParty;
                }
            }
            var party = guild.Parties[order.PartySlot - 1];
            if (party.IsOut) return Check.PartyOut;
            for (int i = 0; i < order.Count; i++)
            {
                var from = guild.PartyOf(order[i]);
                if (from != null && from.IsOut) return Check.PartyOut;
            }
            return Check.Ok;
        }

        /// <summary>
        /// Whether a party may be sent (Epic 10 G5, D7): one of the guild's slots; the party not out already (two parties may
        /// be, Epic 10 Part 2, D66); formed, its members all in the guild and none downed; a charted hazard sector whose
        /// nodes yield <paramref name="sectorYield"/>, with no other party out there; a goal within the most an order asks; a
        /// stall to muster at and bring the haul to. Checked in that order.
        /// </summary>
        public static Check CheckDispatch(GuildService guild, IGuildHost host, in DispatchExpeditionCommand order, ItemType sectorYield, in GuildTuning tuning)
        {
            if (order.PartySlot < 1 || order.PartySlot > guild.Parties.Count) return Check.BadSlot;
            var party = guild.Parties[order.PartySlot - 1];
            if (party.IsOut) return Check.ExpeditionOut;
            var members = party.Members;
            if (members.Count < 1) return Check.BadParty;
            for (int i = 0; i < members.Count; i++)
            {
                var id = members[i].CrawlerId;
                if (!guild.IsMember(id) || !host.TryGetCrawler(id, out var crawler) || crawler.IsDowned) return Check.BadParty;
            }
            byte targetSector = order.TargetSectorId;
            if (order.Kind == ExpeditionKind.Clear)
            {
                // A clear (Epic 10 Part 3, K7.1, Q8): a charted lair with a creature in it; its sector is the lair's
                var lairs = host.Lairs;
                if (lairs == null || order.LairIndex >= lairs.Count || !host.IsLairCharted(order.LairIndex) || lairs[order.LairIndex].Alive == 0) return Check.BadTarget;
                targetSector = (byte)host.GetSectorForPosition(lairs[order.LairIndex].Definition.Position);
            }
            else if (!host.TryGetSector(order.TargetSectorId, out var sector) || !sector.Hazard.IsHazardous || !host.IsSectorCharted(order.TargetSectorId)
                || sectorYield == ItemType.None)
            {
                return Check.BadTarget;
            }
            for (int i = 0; i < guild.Parties.Count; i++)
            {
                var other = guild.Parties[i];
                if (other != party && other.IsOut && other.Expedition.TargetSectorId == targetSector) return Check.SectorTaken;
            }
            if (order.Goal == 0 || order.Goal > tuning.Expedition.MaxGoal) return Check.BadGoal;
            if (host.ActiveStalls.Count == 0) return Check.NoStall;
            return Check.Ok;
        }

        public static Check CheckRecall(GuildService guild, byte partySlot) =>
            partySlot >= 1 && partySlot <= guild.Parties.Count && guild.Parties[partySlot - 1].IsOut ? Check.Ok : Check.NoExpedition;

        /// <summary>
        /// True when the crawler wears, in any slot, a piece that cuts some hazard's harm and isn't worn to half its life (D12;
        /// every slot since Epic 10 Part 3, K1.3; by its stats, K1.4).
        /// </summary>
        public static bool WearsSoundProtection(Simpiens.Cognition.Inventory.IAgentInventory inventory)
        {
            if (inventory == null) return false;
            var slots = EquipmentSlots.All;
            for (int i = 0; i < slots.Count; i++)
            {
                var worn = inventory.GetEquipped(slots[i].Id);
                if (worn.IsEmpty || WornOut(worn)) continue;
                if (Outfitting.Score(null, OutfitNeed.EveryHazard, new WornSwap(slots[i].Id, worn)) > 0f) return true;
            }
            return false;
        }

        /// <summary>An empty slot, or a piece worn to half its life or less: it gets fresh gear (D12).</summary>
        public static bool WornOut(in ItemInstance worn)
        {
            if (worn.IsEmpty) return true;
            var equipment = ItemCatalog.Get(worn.Type)?.Equipment;
            return equipment == null || equipment.Durability == 0 || worn.Durability * 2 <= equipment.Durability;
        }

        /// <summary>
        /// Whether <paramref name="item"/> from the stall's stock may go to member <paramref name="crawlerId"/> (D12): a member,
        /// gear, in stock; and the member wears none in the slot it goes in, or the piece there is worn to half its life, or
        /// (given for a hazard, Epic 10 Part 2, D50) wearing it in place of that piece would outfit the member better against
        /// <paramref name="against"/>, by the stats of all it wears (<see cref="Outfitting"/>, Epic 10 Part 3, K1.4).
        /// </summary>
        public static Check CheckGiveGear(GuildService guild, IGuildHost host, EntityId crawlerId, ItemType item, HazardKind against = HazardKind.None, bool forFighting = false)
        {
            if (!guild.IsMember(crawlerId)) return Check.NotMember;
            var equipment = ItemCatalog.Get(item)?.Equipment;
            if (equipment == null) return Check.NotGear;
            if (host.ActiveStalls.Count == 0) return Check.NoStall;
            if (host.ActiveStalls[0].SellStockOf(item) < 1) return Check.OutOfStock;
            if (!host.TryGetCrawler(crawlerId, out var crawler) || crawler.Inventory == null) return Check.NoCrawler;
            // The piece it would take the place of, by the rule the stall's sales share (Epic 10 Part 3, K1.1)
            if (!WornGear.TryReplacing(crawler.Inventory, item, out var replacing)) return Check.NotGear;
            var worn = crawler.Inventory.GetEquipped(replacing);
            var need = forFighting ? OutfitNeed.Against(against).AndFighting() : OutfitNeed.Against(against); // a fight's gift (Epic 10 Part 3, K7.4)
            if (!WornOut(worn) && Outfitting.Gain(crawler.Inventory, need, replacing, Outfitting.NewPiece(item, host.ActiveStalls[0].BestGradeOf(item))) <= 0f) return Check.AlreadyWearing;
            return Check.Ok;
        }

        /// <summary>
        /// The piece from <paramref name="stock"/> (the stall, less what is promised) to give a member wearing
        /// <paramref name="worn"/> for <paramref name="slot"/> on a trip against <paramref name="hazard"/> (D50; slot by slot,
        /// Epic 10 Part 3, K1.4): the piece that most improves its outfit against the hazard over what the slot holds, or over
        /// the slot bare when what it holds is worn to half its life (D12). None when no piece gains, or for no hazard.
        /// </summary>
        public static ItemType GiftFor(in TradeStallData stock, Simpiens.Cognition.Inventory.IAgentInventory worn, EquipmentSlot slot, HazardKind hazard)
        {
            if (worn == null) return ItemType.None;
            var piece = worn.GetEquipped(slot);
            bool refresh = !piece.IsEmpty && WornOut(piece);
            return Outfitting.BestInStock(stock, worn, OutfitNeed.Against(hazard), slot, refresh, out _);
        }

        /// <summary>The piece in the stall's stock that best outfits a member bare in <paramref name="slot"/> against <paramref name="hazard"/>: what outfitting gives there (the party builder's words). None for none.</summary>
        public static ItemType BestForBare(in TradeStallData stock, EquipmentSlot slot, HazardKind hazard)
            => Outfitting.BestInStock(stock, null, OutfitNeed.Against(hazard), slot, overBare: true, out _);

        /// <summary>
        /// The gifts that outfit <paramref name="members"/> for a trip against <paramref name="hazard"/> from the stall's stock
        /// (D12, D50; slot by slot since Epic 10 Part 3, K1.4), the party builder's and the scripted player's alike, in two
        /// rounds so scarce stock goes to the unprotected first:
        /// <list type="bullet">
        /// <item><see cref="OutfitRound.Unprotected"/>: each member with no sound protection, in the order given, gets the one
        /// piece that outfits it best, whichever slot;</item>
        /// <item><see cref="OutfitRound.EverySlot"/>: then each member, every slot in the table's order not already given a
        /// piece, gets the piece <see cref="GiftFor"/> picks.</item>
        /// </list>
        /// A gift is planned only if the guild would give it now. Each is taken off <paramref name="stock"/> (the caller's copy
        /// of the stall), so the next sees what is left, and appended to <paramref name="gifts"/> at <paramref name="count"/>
        /// (as many as fit); a later round skips what an earlier one planned. <see cref="OutfitRound.Both"/> runs the two in turn.
        /// </summary>
        public static void PlanOutfit(GuildService guild, IGuildHost host, System.Collections.Generic.IReadOnlyList<EntityId> members, HazardKind hazard,
            OutfitRound round, ref TradeStallData stock, System.Span<GearGift> gifts, ref int count)
        {
            if (guild == null || host == null || members == null) return;
            var need = OutfitNeed.Against(hazard);
            var slots = EquipmentSlots.All;
            if (round == OutfitRound.Unprotected || round == OutfitRound.Both)
            {
                for (int i = 0; i < members.Count && count < gifts.Length; i++)
                {
                    if (!host.TryGetCrawler(members[i], out var crawler) || crawler.Inventory == null || WearsSoundProtection(crawler.Inventory)) continue;
                    var best = ItemType.None;
                    var bestSlot = default(EquipmentSlot);
                    float bestGain = 0f;
                    for (int s = 0; s < slots.Count; s++)
                    {
                        var piece = crawler.Inventory.GetEquipped(slots[s].Id);
                        var item = Outfitting.BestInStock(stock, crawler.Inventory, need, slots[s].Id, overBare: !piece.IsEmpty && WornOut(piece), out float gain);
                        if (item == ItemType.None || gain <= bestGain || CheckGiveGear(guild, host, members[i], item, hazard) != Check.Ok) continue;
                        best = item;
                        bestSlot = slots[s].Id;
                        bestGain = gain;
                    }
                    if (best == ItemType.None) continue;
                    gifts[count++] = new GearGift(members[i], best, bestSlot);
                    stock = stock.TakeBest(best, out _);
                }
            }
            if (round == OutfitRound.EverySlot || round == OutfitRound.Both)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    if (!host.TryGetCrawler(members[i], out var crawler) || crawler.Inventory == null) continue;
                    for (int s = 0; s < slots.Count && count < gifts.Length; s++)
                    {
                        if (Planned(gifts, count, members[i], slots[s].Id)) continue;
                        var item = GiftFor(stock, crawler.Inventory, slots[s].Id, hazard);
                        if (item == ItemType.None || CheckGiveGear(guild, host, members[i], item, hazard) != Check.Ok) continue;
                        gifts[count++] = new GearGift(members[i], item, slots[s].Id);
                        stock = stock.TakeBest(item, out _);
                    }
                }
            }
        }

        private static bool Planned(System.Span<GearGift> gifts, int count, EntityId crawler, EquipmentSlot slot)
        {
            for (int i = 0; i < count; i++)
            {
                if (gifts[i].Crawler == crawler && gifts[i].Slot == slot) return true;
            }
            return false;
        }

        /// <summary>
        /// How much of a hazard of <paramref name="kind"/> a piece of protection stops, from its definition (D47): its
        /// resistance to any hazard plus its kind's; 0 for no item, or one that isn't protection.
        /// </summary>
        public static float ProtectionAgainst(ItemType item, HazardKind kind)
        {
            var equipment = item != ItemType.None ? ItemCatalog.Get(item)?.Equipment : null;
            if (equipment == null) return 0f;
            var against = CrawlerStats.StatAgainst(kind);
            float stopped = 0f;
            for (int i = 0; i < equipment.ModifierCount; i++)
            {
                var modifier = equipment.GetModifier(i);
                if (modifier.Op == ModifierOp.Add && (modifier.Stat == StatKind.HazardResistance || (against != StatKind.None && modifier.Stat == against))) stopped += modifier.Value;
            }
            return stopped < CrawlerStats.MaxHazardResistance ? stopped : CrawlerStats.MaxHazardResistance;
        }

        /// <summary>The hazard of the sector <paramref name="sectorId"/>, from the loaded map; None for a safe sector or none found.</summary>
        public static HazardKind HazardOf(IGuildHost host, byte sectorId)
            => host != null && host.TryGetSector(sectorId, out var sector) ? sector.Hazard.Kind : HazardKind.None;

        /// <summary>A player-facing reason for a refusal (allocates; call only on the refusal path).</summary>
        public static string Describe(Check check, string name, AgentState crawler, GuildService guild, uint tick, in GuildTuning tuning)
        {
            switch (check)
            {
                case Check.NoCrawler: return "There is no such crawler here.";
                case Check.Drifter: return $"{name} is passing through: drifters don't sign on.";
                case Check.Downed: return $"{name} is downed; ask again once it's back on its feet.";
                case Check.AlreadyMember: return $"{name} is already in the guild.";
                case Check.WorksAStation: return $"{name} works a station: one role at a time.";
                case Check.NotYetTrusted:
                    var (trust, warmth) = Standing(crawler, tick);
                    return $"Not yet: trust {trust} of {tuning.RecruitTrust}, warmth {warmth} of {tuning.RecruitWarmth}.";
                case Check.GuildFull: return $"The guild is full ({guild.Members.Count} of {tuning.MaxMembers}).";
                case Check.CoolingDown:
                    uint left = guild.CooldownUntil(crawler.AgentId) - tick;
                    return $"{name} left the guild recently; it can sign again in {DayClock.SpanText(left)}.";
                case Check.WageTooLow: return $"{name} asks at least {ReservationWage(crawler, tuning)}g a visit.";
                case Check.WageOutOfRange: return $"A wage is 1 to {tuning.MaxWage}g a visit.";
                case Check.NoStall: return "There is no stall to pay the signing fee from.";
                case Check.CannotPayFee: return $"The stall can't pay the {tuning.SigningFee}g signing fee.";
                case Check.NotMember: return $"{name} isn't in the guild.";
                case Check.NoExpedition: return "That party isn't out.";
                case Check.ExpeditionOut: return "That party is already out.";
                case Check.SectorTaken: return "Another party is out there: one party to a sector.";
                case Check.BadParty: return $"A party is one to {DispatchExpeditionCommand.MaxMembers} members of the guild; it goes with none of them downed.";
                case Check.BadSlot: return $"The guild has parties 1 to {guild.Parties.Count}.";
                case Check.PartyOut: return "That party is out: change it when it is home.";
                case Check.BadTarget: return "Send a party to a charted hazard sector with something to bring back.";
                case Check.BadGoal: return $"Ask for 1 to {tuning.Expedition.MaxGoal}.";
                case Check.NotGear: return "That isn't gear.";
                case Check.OutOfStock: return "The stall has none in stock.";
                case Check.AlreadyWearing: return $"{name} already wears gear.";
                default: return string.Empty;
            }
        }
    }

    /// <summary>A piece from the stall's stock for a member's slot (<see cref="GuildRules.PlanOutfit"/>): sent as a GiveGear command.</summary>
    public readonly struct GearGift
    {
        public GearGift(EntityId crawler, ItemType item, EquipmentSlot slot)
        {
            Crawler = crawler;
            Item = item;
            Slot = slot;
        }

        public EntityId Crawler { get; }
        public ItemType Item { get; }

        /// <summary>The slot it was planned for (the command wears it by the shared rule, <see cref="WornGear"/>).</summary>
        public EquipmentSlot Slot { get; }

        public override string ToString() => $"{Item} for {Crawler}'s {EquipmentSlots.NameOf(Slot)}";
    }

    /// <summary>The rounds of <see cref="GuildRules.PlanOutfit"/>: the unprotected's one piece each, then a piece for every slot, or both in turn.</summary>
    public enum OutfitRound : byte
    {
        Both = 0,
        Unprotected = 1,
        EverySlot = 2
    }
}
