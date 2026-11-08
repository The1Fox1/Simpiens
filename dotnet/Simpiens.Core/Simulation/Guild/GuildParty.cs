using System;
using System.Collections.Generic;
using Simpiens.Intervention;

namespace Simpiens.Simulation.Guild
{
    /// <summary>
    /// One of the guild's parties (Epic 10 Part 2, W5, D60): its slot, its group (a
    /// <see cref="Simpiens.Cognition.Groups.GroupType.TemporaryParty"/> inside the guild, led by the patron), its roster in
    /// the order its members were sent, each with its part, and its expedition. <see cref="GuildService"/> owns and moves
    /// it on; everything else reads it. Main thread.
    /// </summary>
    public sealed class GuildParty
    {
        internal readonly List<ExpeditionMember> Roster = new List<ExpeditionMember>(DispatchExpeditionCommand.MaxMembers);
        internal ExpeditionRecord Record;

        internal GuildParty(byte slot)
        {
            Slot = slot;
        }

        /// <summary>Its number in the guild, from 1.</summary>
        public byte Slot { get; }

        /// <summary>Its group, or 0 while it has none.</summary>
        public byte GroupId { get; internal set; }

        /// <summary>Its expedition (<see cref="ExpeditionRecord.IsActive"/> while out).</summary>
        public ExpeditionRecord Expedition => Record;

        public bool IsOut => Record.IsActive;

        /// <summary>Its members, in the order they were sent, with each one's part.</summary>
        public IReadOnlyList<ExpeditionMember> Members => Roster;

        /// <summary>What the members with the party gathered of the haul's item since they were sent (the tracker's haul).</summary>
        public int Gathered(IGuildHost host)
        {
            int gathered = 0;
            for (int i = 0; i < Roster.Count; i++)
            {
                var member = Roster[i];
                if (!member.IsWithParty || !host.TryGetCrawler(member.CrawlerId, out var crawler) || crawler.Inventory == null) continue;
                gathered += Math.Max(0, crawler.Inventory.GetItemCount(Record.HaulItem) - member.CarriedAtDispatch);
            }
            return gathered;
        }

        internal int IndexOf(EntityId crawlerId)
        {
            for (int i = 0; i < Roster.Count; i++)
            {
                if (Roster[i].CrawlerId == crawlerId) return i;
            }
            return -1;
        }

        internal int CountWithParty()
        {
            int n = 0;
            for (int i = 0; i < Roster.Count; i++)
            {
                if (Roster[i].IsWithParty) n++;
            }
            return n;
        }

        internal EntityId FirstWithParty()
        {
            for (int i = 0; i < Roster.Count; i++)
            {
                if (Roster[i].IsWithParty) return Roster[i].CrawlerId;
            }
            return Roster.Count > 0 ? Roster[0].CrawlerId : default;
        }

        internal void Clear()
        {
            Record = default;
            Roster.Clear();
            GroupId = 0;
        }
    }
}
