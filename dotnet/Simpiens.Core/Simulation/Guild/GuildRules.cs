using Simpiens.Cognition;
using Simpiens.Cognition.Planning;
using Simpiens.Simulation.Economy;

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
            WageOutOfRange = 12
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

        /// <summary>A player-facing reason for a refusal (allocates; call only on the refusal path).</summary>
        public static string Describe(Check check, string name, AgentState crawler, GuildService guild, uint tick, in GuildTuning tuning)
        {
            switch (check)
            {
                case Check.NoCrawler: return "There is no such crawler here.";
                case Check.Drifter: return $"{name} is passing through: drifters don't sign on.";
                case Check.Downed: return $"{name} is downed; ask again once it's back on its feet.";
                case Check.AlreadyMember: return $"{name} is already in the guild.";
                case Check.NotYetTrusted:
                    var (trust, warmth) = Standing(crawler, tick);
                    return $"Not yet: trust {trust} of {tuning.RecruitTrust}, warmth {warmth} of {tuning.RecruitWarmth}.";
                case Check.GuildFull: return $"The guild is full ({guild.Members.Count} of {tuning.MaxMembers}).";
                case Check.CoolingDown:
                    uint left = guild.CooldownUntil(crawler.AgentId) - tick;
                    return $"{name} left the guild recently; it can sign again in {left / 600}:{left / 10 % 60:00}.";
                case Check.WageTooLow: return $"{name} asks at least {ReservationWage(crawler, tuning)}g a visit.";
                case Check.WageOutOfRange: return $"A wage is 1 to {tuning.MaxWage}g a visit.";
                case Check.NoStall: return "There is no stall to pay the signing fee from.";
                case Check.CannotPayFee: return $"The stall can't pay the {tuning.SigningFee}g signing fee.";
                case Check.NotMember: return $"{name} isn't in the guild.";
                default: return string.Empty;
            }
        }
    }
}
