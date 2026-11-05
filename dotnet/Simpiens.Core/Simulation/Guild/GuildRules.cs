using Simpiens.Cognition;
using Simpiens.Cognition.Planning;
using Simpiens.Intervention;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

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
            /// <summary>No party is out to recall.</summary>
            NoExpedition = 13,
            /// <summary>A party is already out: one at a time (D7).</summary>
            ExpeditionOut = 14,
            /// <summary>Not one to four distinct members of the guild, none downed.</summary>
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
            WorksAStation = 21
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
        /// Whether a party may be sent (Epic 10 G5, D7): none out already; one to four distinct members, none downed; a
        /// charted hazard sector whose nodes yield <paramref name="sectorYield"/>; a goal within the most an order asks; a
        /// stall to muster at and bring the haul to. Checked in that order.
        /// </summary>
        public static Check CheckDispatch(GuildService guild, IGuildHost host, in DispatchExpeditionCommand order, ItemType sectorYield, in GuildTuning tuning)
        {
            if (guild.HasExpedition) return Check.ExpeditionOut;
            if (order.Count < 1 || order.Count > DispatchExpeditionCommand.MaxMembers) return Check.BadParty;
            for (int i = 0; i < order.Count; i++)
            {
                var id = order[i];
                if (!guild.IsMember(id) || !host.TryGetCrawler(id, out var crawler) || crawler.IsDowned) return Check.BadParty;
                for (int j = 0; j < i; j++)
                {
                    if (order[j] == id) return Check.BadParty;
                }
            }
            if (!host.TryGetSector(order.TargetSectorId, out var sector) || !sector.Hazard.IsHazardous || !host.IsSectorCharted(order.TargetSectorId)
                || sectorYield == ItemType.None)
            {
                return Check.BadTarget;
            }
            if (order.Goal == 0 || order.Goal > tuning.Expedition.MaxGoal) return Check.BadGoal;
            if (host.ActiveStalls.Count == 0) return Check.NoStall;
            return Check.Ok;
        }

        public static Check CheckRecall(GuildService guild) => guild.HasExpedition ? Check.Ok : Check.NoExpedition;

        /// <summary>An empty slot, or a piece worn to half its life or less: it gets fresh gear (D12).</summary>
        public static bool WornOut(in ItemInstance worn)
        {
            if (worn.IsEmpty) return true;
            var equipment = ItemCatalog.Get(worn.Type)?.Equipment;
            return equipment == null || equipment.Durability == 0 || worn.Durability * 2 <= equipment.Durability;
        }

        /// <summary>
        /// Whether a member may be given <paramref name="item"/> from the stall's stock (D12): a member, something worn, in
        /// stock, and its slot free or the piece in it worn to half its life or less (gear wears out with each trip).
        /// </summary>
        public static Check CheckGiveGear(GuildService guild, IGuildHost host, EntityId crawlerId, ItemType item)
        {
            if (!guild.IsMember(crawlerId)) return Check.NotMember;
            var equipment = ItemCatalog.Get(item)?.Equipment;
            if (equipment == null) return Check.NotGear;
            if (host.ActiveStalls.Count == 0) return Check.NoStall;
            if (host.ActiveStalls[0].SellStockOf(item) < 1) return Check.OutOfStock;
            if (!host.TryGetCrawler(crawlerId, out var crawler) || crawler.Inventory == null) return Check.NoCrawler;
            if (!WornOut(crawler.Inventory.GetEquipped(equipment.Slot))) return Check.AlreadyWearing;
            return Check.Ok;
        }

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
                case Check.NoExpedition: return "No party is out.";
                case Check.ExpeditionOut: return "A party is already out: one at a time.";
                case Check.BadParty: return $"Send one to {DispatchExpeditionCommand.MaxMembers} members of the guild, none of them downed.";
                case Check.BadTarget: return "Send a party to a charted hazard sector with something to bring back.";
                case Check.BadGoal: return $"Ask for 1 to {tuning.Expedition.MaxGoal}.";
                case Check.NotGear: return "That isn't gear.";
                case Check.OutOfStock: return "The stall has none in stock.";
                case Check.AlreadyWearing: return $"{name} already wears gear.";
                default: return string.Empty;
            }
        }
    }
}
