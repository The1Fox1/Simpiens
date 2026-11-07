using System.Collections.Generic;
using Simpiens.Cognition.Contracts;

namespace Simpiens.Simulation.Guild
{
    /// <summary>
    /// The guild's barracks (Epic 10 Part 2, W5.2, D62, D63): the bunks in the guildhall, map data the session spawns,
    /// one kept for each member from the first free one at signing (its place, from 1, in the contract), until it
    /// leaves. A member sleeps in its own wherever it is (<see cref="Simpiens.Cognition.Planning.BunkTargeting"/>), and
    /// pays a lodging fee from its wage each caravan visit. The bunks come from the map, so a load keeps them; only a
    /// respawn forgets them.
    /// </summary>
    public sealed partial class GuildService
    {
        private readonly List<(EntityId Id, Vec2 Position)> _barracks = new List<(EntityId, Vec2)>(8);

        /// <summary>The barracks' bunks, in the map's order.</summary>
        public IReadOnlyList<(EntityId Id, Vec2 Position)> Barracks => _barracks;

        /// <summary>A bunk in the barracks, as the session spawns the map (in its order).</summary>
        public void AddBarracksBunk(EntityId bunkId, Vec2 position) => _barracks.Add((bunkId, position));

        /// <summary>The bunk kept for <paramref name="crawlerId"/>, for cognition; default when it has none.</summary>
        public KeptBunk KeptBunkFor(EntityId crawlerId)
        {
            int i = IndexOf(crawlerId);
            if (i < 0) return default;
            int bunk = _members[i].Bunk;
            if (bunk == 0 || bunk > _barracks.Count) return default;
            var kept = _barracks[bunk - 1];
            return new KeptBunk(kept.Id, kept.Position);
        }

        /// <summary>The first bunk in the barracks no member keeps, from 1; 0 when every one is kept or there are none.</summary>
        private byte FreeBunk()
        {
            for (int bunk = 1; bunk <= _barracks.Count && bunk <= byte.MaxValue; bunk++)
            {
                bool kept = false;
                for (int i = 0; i < _members.Count && !kept; i++) kept = _members[i].Bunk == bunk;
                if (!kept) return (byte)bunk;
            }
            return 0;
        }

        /// <summary>What a member is paid of its wage this visit: the wage less the lodging fee if it keeps a bunk, never below nothing (D63).</summary>
        public static uint WagePaid(in GuildContract contract, in Simpiens.Simulation.Economy.GuildTuning tuning) =>
            contract.HasBunk ? (contract.Wage > tuning.LodgingFee ? (uint)(contract.Wage - tuning.LodgingFee) : 0u) : contract.Wage;
    }
}
