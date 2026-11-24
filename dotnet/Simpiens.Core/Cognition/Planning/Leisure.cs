using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation;
using Simpiens.Simulation.Life;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Where a crawler would spend its free time (Epic LIFE-1 plan 2, L2.4, D15; the epic's A6): the venues the map holds,
    /// scored by utility, never by a planner fact per venue, as <see cref="ResourceTargeting"/> chooses a node. Recreate's
    /// base is fun's urgency times the best score.
    /// <list type="bullet">
    /// <item><b>The gates</b> (a score of 0): the venue closed (the snapshot's open mask), not for this crawler (the guild's
    /// yard), in a sector it hasn't charted, the activity not a recreation it offers, its cost beyond the purse or out of the
    /// stall's stock, too few others there for it (dice), or no seat free.</item>
    /// <item><b>The score:</b> the share of a bored crawler's fun it fills (a supper's meal adds the hunger it eases) × its
    /// lean on the crawler's nature × the walk there × a warm or trusted friend there (variety and curiosity: L2.7).</item>
    /// </list>
    /// Pure and allocation-free (a thread's own query buffer); reads only the context, so it runs off the main thread.
    /// </summary>
    public static class Leisure
    {
        /// <summary>Fun an hour that fills a whole share: dice's.</summary>
        public const float FullFunPerHour = 30f;

        /// <summary>How far an activity's lean bends the score: a trait of ±1 gives ×1.5 or ×0.5.</summary>
        public const float LeanStrength = 0.5f;

        /// <summary>The walk's discount: a venue this many units off scores half.</summary>
        public const float DistanceScale = 30f;

        /// <summary>A warm or trusted friend at the venue (warmth or trust from <see cref="FriendLine"/>, as the hearth's draw).</summary>
        public const float FriendsPresent = 1.25f;
        public const int FriendLine = 20;

        [System.ThreadStatic]
        private static List<EntitySnapshot> _nearby;

        /// <summary>
        /// How much <paramref name="activity"/> at the map's venue <paramref name="venueIndex"/> draws <paramref name="context"/>'s
        /// crawler now; 0 where a gate shuts it. <paramref name="seat"/> is the nearest free seat, or -1.
        /// </summary>
        public static float Score(AgentContext context, int venueIndex, Activity activity, out int seat)
        {
            seat = -1;
            var snapshot = context.Snapshot;
            var map = snapshot?.Map;
            if (map == null || venueIndex < 0 || venueIndex >= map.Venues.Count) return 0f;
            var venue = map.Venues[venueIndex];
            var row = ActivityTable.Get(activity);
            if (row == null || !row.IsRecreation || !venue.Offers(activity)) return 0f;
            if (!snapshot.IsVenueOpen(venueIndex) || !snapshot.IsSectorCharted(venue.SectorId)) return 0f;
            if (venue.Access == VenueAccess.GuildMembers && !context.IsGuildMember) return 0f;
            if (!Affordable(context, row)) return 0f;

            // Who is there: the others at the venue, a free seat, a friend
            float reach = venue.Radius;
            for (int s = 0; s < venue.Seats.Count; s++)
            {
                float d = Vec2.Distance(venue.Position, venue.Seats[s]) + VenueDefinition.SeatRadius;
                if (d > reach) reach = d;
            }
            var nearby = _nearby ??= new List<EntitySnapshot>(16);
            nearby.Clear();
            snapshot.GetEntitiesInRange(venue.Position, reach, nearby);
            int others = 0;
            bool friend = false;
            for (int i = 0; i < nearby.Count; i++)
            {
                var other = nearby[i];
                if (other.Id == context.AgentId || other.Type != EntityType.Pawn) continue;
                if (Vec2.Distance(other.Position, venue.Position) > venue.Radius) continue;
                others++;
                if (!friend && context.Memory != null)
                {
                    var affinity = context.Memory.GetAffinity(other.Id, context.CurrentTick);
                    friend = affinity.Warmth >= FriendLine || affinity.Trust >= FriendLine;
                }
            }
            if (row.CompanyMin > 0 && others < row.CompanyMin) return 0f;
            seat = FreeSeat(context, venue, nearby);
            if (seat < 0) return 0f;

            float share = row.FunPerHour / FullFunPerHour;
            if (row.Hunger < 0) share += SimMath.Min(context.Hunger, -row.Hunger) / 100f;
            if (share > 1f) share = 1f;
            float distance = 1f / (1f + Vec2.Distance(context.Position, venue.Position) / DistanceScale);
            return share * Lean(context.Personality, row.Lean) * distance * (friend ? FriendsPresent : 1f);
        }

        /// <summary>
        /// The best recreation <paramref name="context"/>'s crawler knows of: its venue, activity, seat and score; false when
        /// nothing scores above 0. Ties go to the earlier venue, then the lower activity id.
        /// </summary>
        public static bool TryChoose(AgentContext context, out int venueIndex, out Activity activity, out int seat, out float score)
        {
            venueIndex = -1;
            activity = Activity.None;
            seat = -1;
            score = 0f;
            var map = context.Snapshot?.Map;
            if (map == null) return false;
            for (int v = 0; v < map.Venues.Count; v++)
            {
                for (int id = 1; id < ActivityTable.Capacity; id++)
                {
                    if (!map.Venues[v].Offers((Activity)id)) continue;
                    float s = Score(context, v, (Activity)id, out int at);
                    if (s <= score) continue;
                    score = s;
                    venueIndex = v;
                    activity = (Activity)id;
                    seat = at;
                }
            }
            return venueIndex >= 0;
        }

        /// <summary>How <paramref name="personality"/> leans toward an activity of <paramref name="lean"/>: 1 for a neutral nature.</summary>
        public static float Lean(in AgentPersonality personality, ActivityLean lean) => lean switch
        {
            ActivityLean.Sociable => Considerations.TraitFactor(personality.Sociability, LeanStrength),
            ActivityLean.Solitary => Considerations.TraitFactor(-personality.Sociability, LeanStrength),
            ActivityLean.Greedy => Considerations.TraitFactor(personality.Greed, LeanStrength),
            ActivityLean.Bold => Considerations.TraitFactor(personality.RiskTolerance, LeanStrength),
            _ => 1f,
        };

        /// <summary>Whether the crawler can pay for <paramref name="row"/>: free, or its item in the stall's stock at a price its purse holds.</summary>
        private static bool Affordable(AgentContext context, ActivityDefinition row)
        {
            if (row.Cost == Simpiens.Simulation.Economy.ItemType.None) return true;
            var snapshot = context.Snapshot;
            if (snapshot.ActiveStallCount == 0 || context.Inventory == null) return false;
            var stall = snapshot.StallBuffer[0];
            ushort price = stall.SellPriceOf(row.Cost);
            return price > 0 && stall.SellStockOf(row.Cost) > 0 && context.Inventory.Gold >= price;
        }

        /// <summary>The free seat nearest the crawler (no other crawler within <see cref="VenueDefinition.SeatRadius"/>), or -1.</summary>
        private static int FreeSeat(AgentContext context, in VenueDefinition venue, List<EntitySnapshot> nearby)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int s = 0; s < venue.Seats.Count; s++)
            {
                var at = venue.Seats[s];
                bool taken = false;
                for (int i = 0; i < nearby.Count && !taken; i++)
                {
                    var other = nearby[i];
                    if (other.Id == context.AgentId || other.Type != EntityType.Pawn) continue;
                    taken = Vec2.Distance(other.Position, at) <= VenueDefinition.SeatRadius;
                }
                if (taken) continue;
                float d = Vec2.Distance(context.Position, at);
                if (d < bestDistance) { bestDistance = d; best = s; }
            }
            return best;
        }
    }
}
