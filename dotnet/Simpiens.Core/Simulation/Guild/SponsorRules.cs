using Simpiens.Cognition;
using Simpiens.Diagnostics;
using Simpiens.Narrative;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Skills;
using Simpiens.Simulation.Workshops;
using ZLogger;
using static System.FormattableString;

namespace Simpiens.Simulation.Guild
{
    /// <summary>
    /// Sponsorship (Epic 11, S4.6, A6, Q3, D16): the patron proposes, the crawler disposes. The player offers gold for one of
    /// its people (a guild member or an artisan) to take a band's other technique in place of the one it picked; trust and
    /// nature decide. A rule with a reason for each refusal, as <see cref="GuildRules"/> decides a recruit, and a transfer
    /// from the stall's treasury into the crawler's purse, so the ledger balances with no new sink. Main thread.
    /// </summary>
    public static class SponsorRules
    {
        public enum Check : byte
        {
            Ok = 0,
            /// <summary>No crawler by that id is here.</summary>
            NoSuchCrawler = 1,
            /// <summary>Neither a guild member nor an artisan: not the patron's to sponsor.</summary>
            NotOurs = 2,
            /// <summary>No such technique.</summary>
            NoSuchTechnique = 3,
            /// <summary>The crawler hasn't reached the band that offers it.</summary>
            BandNotReached = 4,
            /// <summary>It holds that technique already.</summary>
            AlreadyHeld = 5,
            /// <summary>There is no stall to pay from.</summary>
            NoStall = 6,
            /// <summary>The stall can't pay the price.</summary>
            CannotPay = 7,
            /// <summary>Its trust or warmth toward the player is below the line.</summary>
            NotYetTrusted = 8,
            /// <summary>The technique leans strongly against its personality.</summary>
            AgainstItsNature = 9
        }

        /// <summary>A crawler refuses a technique whose lean in its personality is this or less: "too cautious for Reckless Fury".</summary>
        public const float AgainstNatureAt = -0.5f;

        /// <summary>
        /// The rule's verdict on <paramref name="crawlerId"/> taking up <paramref name="technique"/> for the patron's gold, without
        /// acting, and the price it would cost (by the technique's band, <see cref="GuildTuning.SponsorPrice"/>). The first reason
        /// in the order of <see cref="Check"/>.
        /// </summary>
        public static Check CheckSponsor(GuildService guild, WorkshopService workshops, IGuildHost host, EntityId crawlerId, Technique technique, uint tick, out ushort price)
        {
            var tuning = (host?.Economy ?? EconomyTuning.Default).Guild;
            var row = TechniqueTable.Get(technique);
            price = row == null ? (ushort)0 : tuning.SponsorPrice(row.Band);
            if (host == null || !host.TryGetCrawler(crawlerId, out var crawler) || crawler.HasLeft) return Check.NoSuchCrawler;
            bool ours = (guild != null && guild.TryGetContract(crawlerId, out _)) || (workshops != null && workshops.IsArtisan(crawlerId));
            if (!ours) return Check.NotOurs;
            if (row == null) return Check.NoSuchTechnique;
            if (crawler.Skills.Band(row.Skill) < row.Band) return Check.BandNotReached;
            if (crawler.Traits.TechniqueIn(row.Skill, row.Band) == technique) return Check.AlreadyHeld;
            if (host.ActiveStalls == null || host.ActiveStalls.Count == 0) return Check.NoStall;
            if (host.ActiveStalls[0].TreasuryGold < price) return Check.CannotPay;
            var (trust, warmth) = GuildRules.Standing(crawler, tick);
            if (trust < tuning.SponsorTrust || warmth < tuning.SponsorWarmth) return Check.NotYetTrusted;
            if (SkillSet.Lean(row.Lean, crawler.Personality) <= AgainstNatureAt) return Check.AgainstItsNature;
            return Check.Ok;
        }

        /// <summary>
        /// Sponsors <paramref name="technique"/> for <paramref name="crawlerId"/> as the rule allows: the price from the stall's
        /// treasury into its purse (counted with the wages' tally), the band's slot changed, and the chronicle told. Refused, it
        /// changes nothing and logs the reason.
        /// </summary>
        public static Check Sponsor(GuildService guild, WorkshopService workshops, IGuildHost host, EntityId crawlerId, Technique technique, uint tick)
        {
            var check = CheckSponsor(guild, workshops, host, crawlerId, technique, tick, out ushort price);
            if (check != Check.Ok)
            {
                SimLog.Economy.ZLogInformation($"Sponsor {TechniqueTable.NameOf(technique)} for {SimLog.Agent(crawlerId)} refused: {check}");
                return check;
            }
            host.TryGetCrawler(crawlerId, out var crawler);
            var stall = host.ActiveStalls[0];
            if (price > 0)
            {
                if (host.EconomyLedger != null) host.EconomyLedger.TryPayFromStall(stall.StallEntityId, ref stall, crawler.Inventory, price, StallPayment.Sponsorship);
                else
                {
                    stall = stall.WithTreasury(stall.TreasuryGold - price);
                    crawler.Inventory?.AddGold(price);
                }
                host.UpdateStall(stall);
            }
            var row = TechniqueTable.Get(technique);
            var traits = crawler.Traits;
            traits.SetTechnique(row.Skill, row.Band, technique);
            crawler.Traits = traits;
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(crawlerId)} took up {row.Name} for the patron's {price}g");
            host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.TechniqueSponsored, crawlerId, WorldRegistry.PlayerEntityId,
                host.GetSectorForPosition(crawler.Position), tick, ChronicleIntensity.Notable, (byte)technique));
            return Check.Ok;
        }

        /// <summary>
        /// What the dossier offers to sponsor (D18): the other technique at the highest band of the crawler's best skill that
        /// holds one (the highest level, then the table's display order), with the one it would replace; none when it holds none.
        /// </summary>
        public static Technique Offer(in SkillSet skills, in CrawlerTraits traits, out Technique replacing)
        {
            replacing = Technique.None;
            int bestLevel = -1;
            var shown = SkillTable.InDisplayOrder;
            for (int i = 0; i < shown.Count; i++)
            {
                int level = skills.Level(shown[i].Id);
                if (level <= bestLevel) continue;
                for (var band = SkillBand.Master; band >= SkillBand.Apprentice; band--)
                {
                    var held = traits.TechniqueIn(shown[i].Id, band);
                    if (held == Technique.None) continue;
                    replacing = held;
                    bestLevel = level;
                    break;
                }
            }
            return TechniqueTable.OtherThan(replacing);
        }

        /// <summary>A refusal in the player's words, for the dossier's button; empty for <see cref="Check.Ok"/>.</summary>
        public static string Describe(Check check, AgentState crawler, Technique technique, ushort price, in GuildTuning tuning, uint tick)
        {
            var row = TechniqueTable.Get(technique);
            string name = crawler?.GivenName ?? "It";
            switch (check)
            {
                case Check.Ok: return string.Empty;
                case Check.NoSuchCrawler: return "Nobody to sponsor.";
                case Check.NotOurs: return Invariant($"{name} isn't one of yours: only guild members and artisans take a patron's gold.");
                case Check.NoSuchTechnique: return "No such technique.";
                case Check.BandNotReached: return Invariant($"{name} isn't {(row != null ? SkillRules.NameOf(row.Band) : "there")} yet.");
                case Check.AlreadyHeld: return Invariant($"{name} already holds {row?.DisplayName}.");
                case Check.NoStall: return "There's no stall to pay from.";
                case Check.CannotPay: return Invariant($"The stall can't pay the {price}g.");
                case Check.NotYetTrusted:
                    // Only what stands under its line
                    var (trust, warmth) = crawler != null ? GuildRules.Standing(crawler, tick) : (0, 0);
                    string low = trust < tuning.SponsorTrust ? Invariant($"trust {trust} of {tuning.SponsorTrust}") : string.Empty;
                    if (warmth < tuning.SponsorWarmth) low += (low.Length > 0 ? ", " : string.Empty) + Invariant($"warmth {warmth} of {tuning.SponsorWarmth}");
                    return $"Not yet: {low}.";
                case Check.AgainstItsNature: return Invariant($"{name} won't: {row?.DisplayName} goes against their nature.");
                default: return check.ToString();
            }
        }
    }
}
