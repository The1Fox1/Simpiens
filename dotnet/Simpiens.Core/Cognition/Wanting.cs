using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Life;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

namespace Simpiens.Cognition
{
    /// <summary>
    /// Where wants come from (Epic LIFE-1 plan 2, W.2, D20): on waking from a bunk's sleep, a crawler whose state meets a want's
    /// trigger may want it, one roll a game day on its own salt (<see cref="SimRandomSalt.Wants"/>): a <see cref="WantChance"/>
    /// that one arises, then which, among the wants whose triggers hold, in the table's order. Main thread, through the
    /// crawler's one entry (<c>AgentState.Want</c>).
    /// </summary>
    public static class Wanting
    {
        /// <summary>The chance a want arises on waking, when any trigger holds.</summary>
        public const float WantChance = 0.6f;

        /// <summary>The fun under which the sociable want dice and the quiet a walk.</summary>
        public const float LowFun = 50f;

        /// <summary>The curiosity over which a crawler wants somewhere new.</summary>
        public const float CuriousOver = 0.3f;

        /// <summary>The warmth or trust that makes a crawler a friend to want to see.</summary>
        public const int FriendLine = 20;

        public static void OnWaking(AgentState state, SharedWorldSnapshot snapshot, EconomyTuning economy, ulong seed, uint tick)
        {
            if (state == null || state.IsDrifter) return;
            var map = snapshot?.Map;
            System.Span<int> eligible = stackalloc int[WantTable.Capacity];
            int n = 0;
            int newVenue = -1;
            EntityId friend = default;
            foreach (var row in WantTable.All)
            {
                if (WantRules.Holds(state.Life, row.Id, tick)) continue;
                bool holds = row.Trigger switch
                {
                    WantTrigger.SpareGold => state.Inventory != null && state.Inventory.Gold >= economy.ComfortablePurse / 2 && Offered(map, snapshot, row.Activity),
                    WantTrigger.LowFunSociable => state.Life.Fun < LowFun && state.Personality.Sociability > 0f && Offered(map, snapshot, row.Activity),
                    WantTrigger.LowFunQuiet => state.Life.Fun < LowFun && state.Personality.Sociability <= 0f && Offered(map, snapshot, row.Activity),
                    WantTrigger.FriendUnseen => TryFriendUnseen(state, tick, out friend),
                    WantTrigger.Curious => state.Life.Curiosity > CuriousOver && TryNewVenue(state, map, snapshot, out newVenue),
                    _ => false,
                };
                if (holds) eligible[n++] = (ushort)row.Id;
            }
            if (n == 0) return;
            var rng = SimRandom.For(seed, state.AgentId, DayClock.Day(tick), SimRandomSalt.Wants);
            if (rng.NextFloat() >= WantChance) return;
            var want = WantTable.Get((Want)eligible[System.Math.Min(n - 1, (int)(rng.NextFloat() * n))]);
            state.Want(want.Id, tick, want.Activity, want.Target == WantTarget.Venue ? newVenue : -1, want.Target == WantTarget.Friend ? friend : default);
        }

        /// <summary>Whether a venue on the map offers <paramref name="activity"/> with its station (if any) standing.</summary>
        private static bool Offered(MapDefinition map, SharedWorldSnapshot snapshot, Activity activity)
        {
            if (map == null) return false;
            for (int v = 0; v < map.Venues.Count; v++)
                if (map.Venues[v].Offers(activity) && Stands(map, snapshot, map.Venues[v])) return true;
            return false;
        }

        private static bool Stands(MapDefinition map, SharedWorldSnapshot snapshot, in VenueDefinition venue)
        {
            if (venue.Station == null) return true;
            for (int s = 0; s < map.Stations.Count; s++) if (map.Stations[s].Key == venue.Station) return snapshot.IsStationBuilt(s);
            return false;
        }

        /// <summary>The warmest friend not met for a game day, if any.</summary>
        private static bool TryFriendUnseen(AgentState state, uint tick, out EntityId friend)
        {
            friend = default;
            var memory = state.Memory;
            if (memory?.AffinityMap == null) return false;
            int warmest = int.MinValue;
            foreach (var pair in memory.AffinityMap)
            {
                if (pair.Key == WorldRegistry.PlayerEntityId) continue;
                var record = memory.GetAffinity(pair.Key, tick);
                if (record.Warmth < FriendLine && record.Trust < FriendLine) continue;
                if (record.LastInteractionTick + DayClock.TicksPerDay > tick) continue;
                if (record.Warmth > warmest || (record.Warmth == warmest && pair.Key.CompareTo(friend) < 0)) { warmest = record.Warmth; friend = pair.Key; }
            }
            return friend != default;
        }

        /// <summary>The first venue (of the first eight) the crawler hasn't tried that offers a recreation, its station standing.</summary>
        private static bool TryNewVenue(AgentState state, MapDefinition map, SharedWorldSnapshot snapshot, out int venue)
        {
            venue = -1;
            if (map == null) return false;
            for (int v = 0; v < map.Venues.Count && v < 8; v++)
            {
                if (state.Life.HasTried(v) || map.Venues[v].Access != VenueAccess.Anyone || !Stands(map, snapshot, map.Venues[v])) continue;
                for (int a = 1; a < ActivityTable.Capacity; a++)
                {
                    var row = ActivityTable.Get((Activity)a);
                    if (row != null && row.IsRecreation && map.Venues[v].Offers((Activity)a)) { venue = v; return true; }
                }
            }
            return false;
        }
    }
}
