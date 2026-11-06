using Simpiens.Cognition;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Planning;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;

namespace Simpiens.Simulation.Workshops
{
    /// <summary>
    /// What the player may do with the stations, and why not (Epic 10 Part 2, W1). The simulation enforces it when a
    /// command is applied; the station window uses the same rule to explain a refusal before the player sends one, as
    /// the guild's rules do. A future multiplayer server refuses the same commands.
    /// </summary>
    public static class WorkshopRules
    {
        public enum Check : byte
        {
            Ok = 0,
            /// <summary>No station with that index on the map.</summary>
            NoStation = 1,
            AlreadyBuilt = 2,
            /// <summary>No stall to pay from.</summary>
            NoStall = 3,
            /// <summary>The stall's treasury can't pay the construction.</summary>
            CannotPay = 4,

            // Hiring (W1.2, Q3, D9)
            /// <summary>The station isn't built yet.</summary>
            NotBuilt = 5,
            /// <summary>The station has its artisan: one per station (Q3).</summary>
            Staffed = 6,
            /// <summary>No crawler with that id in the session.</summary>
            NoCrawler = 7,
            /// <summary>Drifters pass through; they don't take work.</summary>
            Drifter = 8,
            Downed = 9,
            /// <summary>It is in the guild: one role at a time (Q4, D6).</summary>
            GuildMember = 10,
            /// <summary>It already works a station.</summary>
            AlreadyAnArtisan = 11,
            /// <summary>Trust or warmth toward the player below the threshold (Q3).</summary>
            NotYetTrusted = 12,
            /// <summary>Its risk tolerance is at the threshold or above: it would rather go into danger (Q3).</summary>
            TooBold = 13,
            /// <summary>It quit or was let go too recently.</summary>
            CoolingDown = 14,
            /// <summary>A wage of 0, or above the most a wage may be.</summary>
            WageOutOfRange = 15,
            /// <summary>The wage is below the crawler's reservation wage.</summary>
            WageTooLow = 16,
            /// <summary>It works no station.</summary>
            NotAnArtisan = 17,

            // Orders and shifts (W1.3, D12, D32)
            /// <summary>The item isn't made at this station: no recipe, or another station's.</summary>
            NotThisStationsRecipe = 18,
            /// <summary>A finished make waits for room in the stall's stock; the order can't change until it is stocked.</summary>
            HoldingOutput = 19,
            /// <summary>A mask with bits past the day's six blocks.</summary>
            NoSuchBlock = 20,

            // Repair (W4, D59)
            /// <summary>Only what is worn wears, and only what a station makes is mended there.</summary>
            NotRepairable = 21
        }

        /// <summary>Whether the station at <paramref name="index"/> can be built now (D10): it exists, stands unbuilt, and the stall can pay. Checked in that order.</summary>
        public static Check CheckBuild(WorkshopService workshops, int index, bool hasStall, uint treasury, in WorkshopTuning tuning)
        {
            if ((uint)index >= (uint)workshops.Count) return Check.NoStation;
            if (workshops[index].Built) return Check.AlreadyBuilt;
            if (!hasStall) return Check.NoStall;
            if (treasury < tuning.ConstructionCost) return Check.CannotPay;
            return Check.Ok;
        }

        /// <summary>The least wage per caravan visit <paramref name="crawler"/> works a station for: the reservation wage times its greed factor, as a member's.</summary>
        public static ushort ReservationWage(AgentState crawler, in WorkshopTuning tuning)
        {
            float wage = tuning.ReservationWage * Considerations.TraitFactor(crawler.Personality.Greed, Considerations.GreedOnPrice);
            return (ushort)SimMath.Clamp(SimMath.RoundToInt(wage), 1, ushort.MaxValue);
        }

        /// <summary>
        /// Whether <paramref name="crawler"/> takes work at the station at <paramref name="index"/> for
        /// <paramref name="wage"/> now (Q3, D9). Checked in a fixed order, so a refusal names the first reason: the
        /// station, who it is, its roles, its trust, its boldness, its cooldown, the wage.
        /// </summary>
        public static Check CheckHire(WorkshopService workshops, GuildService guild, AgentState crawler, int index, ushort wage, uint tick, in WorkshopTuning tuning)
        {
            if ((uint)index >= (uint)workshops.Count) return Check.NoStation;
            if (!workshops[index].Built) return Check.NotBuilt;
            if (workshops.ArtisanAt(index) != default) return Check.Staffed;
            if (crawler == null || crawler.HasLeft) return Check.NoCrawler;
            if (crawler.IsDrifter) return Check.Drifter;
            if (crawler.IsDowned) return Check.Downed;
            if ((guild != null && guild.IsMember(crawler.AgentId)) || crawler.Memberships.Has(GroupType.FactionGuild)) return Check.GuildMember;
            if (workshops.IsArtisan(crawler.AgentId)) return Check.AlreadyAnArtisan;
            var (trust, warmth) = GuildRules.Standing(crawler, tick);
            if (trust < tuning.HireTrust || warmth < tuning.HireWarmth) return Check.NotYetTrusted;
            if (crawler.Personality.RiskTolerancePercent >= tuning.MaxRiskTolerancePercent) return Check.TooBold;
            if (workshops.CooldownUntil(crawler.AgentId) > tick) return Check.CoolingDown;
            if (wage == 0 || wage > tuning.MaxWage) return Check.WageOutOfRange;
            if (wage < ReservationWage(crawler, tuning)) return Check.WageTooLow;
            return Check.Ok;
        }

        public static Check CheckSetWage(WorkshopService workshops, EntityId crawlerId, ushort wage, in WorkshopTuning tuning)
        {
            if (!workshops.IsArtisan(crawlerId)) return Check.NotAnArtisan;
            if (wage == 0 || wage > tuning.MaxWage) return Check.WageOutOfRange;
            return Check.Ok;
        }

        public static Check CheckDismiss(WorkshopService workshops, EntityId crawlerId) => workshops.IsArtisan(crawlerId) ? Check.Ok : Check.NotAnArtisan;

        /// <summary>
        /// Whether the station at <paramref name="index"/> takes an order for <paramref name="count"/> of
        /// <paramref name="item"/> (D32): it exists and stands, the item is its recipe (none, or a count of 0, cancels), and
        /// no finished make waits for room.
        /// </summary>
        public static Check CheckOrder(WorkshopService workshops, int index, ItemType item, ushort count, bool repair = false)
        {
            if ((uint)index >= (uint)workshops.Count) return Check.NoStation;
            var station = workshops[index];
            if (!station.Built) return Check.NotBuilt;
            if (station.HeldOutput > 0) return Check.HoldingOutput;
            if (item == ItemType.None || count == 0) return Check.Ok;
            var recipe = Simpiens.Simulation.Items.RecipeBook.For(item);
            if (recipe == null || recipe.Station != station.Kind) return Check.NotThisStationsRecipe;
            if (repair && !Crafting.Repairable(item)) return Check.NotRepairable; // only what is worn wears (W4, D59)
            return Check.Ok;
        }

        /// <summary>Whether an artisan's shifts can be <paramref name="shifts"/> (D12): it works a station, and the mask names only the day's blocks.</summary>
        public static Check CheckSetShifts(WorkshopService workshops, EntityId crawlerId, byte shifts)
        {
            if (!workshops.IsArtisan(crawlerId)) return Check.NotAnArtisan;
            if ((shifts & ~ShiftBlocks.All) != 0) return Check.NoSuchBlock;
            return Check.Ok;
        }

        /// <summary>
        /// A refusal in the player's words (allocates; call only on the refusal path and in the window).
        /// <paramref name="name"/> and <paramref name="crawler"/> are the crawler's, when the check concerns one.
        /// </summary>
        public static string Describe(Check check, WorkshopService workshops, int index, in WorkshopTuning tuning, string name = null, AgentState crawler = null, uint tick = 0)
        {
            string station = (uint)index < (uint)workshops.Count ? StationName(workshops[index].Kind) : "station";
            name ??= "That crawler";
            switch (check)
            {
                case Check.NoStation: return "There is no such station here.";
                case Check.AlreadyBuilt: return $"The {station} already stands.";
                case Check.NoStall: return "There is no stall to pay for it.";
                case Check.CannotPay: return $"The stall can't pay the {tuning.ConstructionCost}g to build the {station}.";
                case Check.NotBuilt: return $"Build the {station} first.";
                case Check.Staffed: return $"The {station} has its artisan: one to a station.";
                case Check.NoCrawler: return "There is no such crawler here.";
                case Check.Drifter: return $"{name} is passing through: drifters don't take work.";
                case Check.Downed: return $"{name} is downed; ask again once it's back on its feet.";
                case Check.GuildMember: return $"{name} is in the guild: one role at a time.";
                case Check.AlreadyAnArtisan: return $"{name} already works a station.";
                case Check.NotYetTrusted:
                    var (trust, warmth) = GuildRules.Standing(crawler, tick);
                    return $"Not yet: trust {trust} of {tuning.HireTrust}, warmth {warmth} of {tuning.HireWarmth}.";
                case Check.TooBold: return $"{name} would rather go out into danger than work the {station}.";
                case Check.CoolingDown:
                    uint until = crawler != null ? workshops.CooldownUntil(crawler.AgentId) : 0;
                    return $"{name} left a station recently; ask again in {DayClock.SpanText(until - tick)}.";
                case Check.WageOutOfRange: return $"A wage is 1 to {tuning.MaxWage}g a visit.";
                case Check.WageTooLow: return $"{name} asks at least {(crawler != null ? ReservationWage(crawler, tuning) : tuning.ReservationWage)}g a visit.";
                case Check.NotAnArtisan: return $"{name} works no station.";
                case Check.NotThisStationsRecipe: return $"That isn't made at the {station}.";
                case Check.HoldingOutput: return $"The {station} holds a finished make until the stall has room for it.";
                case Check.NoSuchBlock: return "A day has six blocks of four hours, from 06:00.";
                case Check.NotRepairable: return $"Nothing of that wears out to mend at the {station}.";
                default: return string.Empty;
            }
        }

        /// <summary>A station's name in the player's words: "forge", "still".</summary>
        public static string StationName(Simpiens.Simulation.Items.StationKind kind) => kind switch
        {
            Simpiens.Simulation.Items.StationKind.Forge => "forge",
            Simpiens.Simulation.Items.StationKind.Still => "still",
            _ => "station"
        };
    }
}
