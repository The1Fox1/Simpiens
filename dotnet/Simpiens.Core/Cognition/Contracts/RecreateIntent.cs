using Simpiens.Simulation.Life;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// A spell of free time (Epic LIFE-1 plan 2, L2.5, D14): <see cref="Activity"/> at the map's venue <see cref="VenueIndex"/>,
    /// at its seat <see cref="Seat"/>, walking there on <see cref="Path"/> first. <see cref="Begun"/> and <see cref="Stop"/>
    /// are work in progress (the spell started; the walk's next spot).
    /// </summary>
    public class RecreateIntent : AgentIntent
    {
        public RecreateIntent(EntityId agentId, int venueIndex, Activity activity, int seat, Simpiens.Cognition.Pathfinding.PathResponse path) : base(agentId)
        {
            VenueIndex = venueIndex;
            Activity = activity;
            Seat = seat;
            Path = path;
            Stop = seat;
        }

        public int VenueIndex { get; }
        public Activity Activity { get; }
        public int Seat { get; }
        public Simpiens.Cognition.Pathfinding.PathResponse Path { get; }

        /// <summary>Whether the spell has begun (its cost paid, a meal eaten): the walk there is over.</summary>
        public bool Begun { get; set; }

        /// <summary>A walk's next spot among the venue's seats; the seat for any other activity.</summary>
        public int Stop { get; set; }
    }
}
