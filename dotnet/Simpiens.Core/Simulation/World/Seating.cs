using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;

namespace Simpiens.Simulation.World
{
    /// <summary>
    /// Who sits where (Epic PLACES plan 1, F.6): a venue's seat holds one crawler at a sitting spell, as a bunk holds one sleeper
    /// (<see cref="Lodging"/>). A crawler claims its seat as it arrives; a claim never outlives the spell. Main thread; not saved
    /// (work in progress, as bunk claims are: crawlers decide afresh after a load).
    /// </summary>
    internal sealed class Seating
    {
        private readonly Dictionary<long, EntityId> _claims = new Dictionary<long, EntityId>(16);
        private readonly List<long> _scratch = new List<long>(4);

        private static long Key(int venue, int seat) => ((long)venue << 32) | (uint)seat;

        /// <summary>Claims seat <paramref name="seat"/> of venue <paramref name="venue"/> for <paramref name="agentId"/>: false when another holds it.</summary>
        public bool TryClaim(int venue, int seat, EntityId agentId)
        {
            long key = Key(venue, seat);
            if (_claims.TryGetValue(key, out var holder) && holder != agentId) return false;
            _claims[key] = agentId;
            return true;
        }

        /// <summary>Who holds the seat, if anyone.</summary>
        public bool TryGetHolder(int venue, int seat, out EntityId holder) => _claims.TryGetValue(Key(venue, seat), out holder);

        public int Count => _claims.Count;

        /// <summary>Releases every seat <paramref name="agentId"/> holds (a crawler leaving the outpost).</summary>
        public void ReleaseAllForAgent(EntityId agentId)
        {
            _scratch.Clear();
            foreach (var kvp in _claims) if (kvp.Value == agentId) _scratch.Add(kvp.Key);
            for (int i = 0; i < _scratch.Count; i++) _claims.Remove(_scratch[i]);
        }

        /// <summary>
        /// Drops every claim whose holder is no longer at a spell on that seat (finished, cut short, removed), so a seat never stays
        /// taken by someone who left it. Runs every tick before the snapshot. Which claims drop doesn't depend on their order.
        /// </summary>
        public void Validate(IReadOnlyDictionary<EntityId, ActiveIntentState> activeIntents)
        {
            if (_claims.Count == 0) return;
            _scratch.Clear();
            foreach (var kvp in _claims)
            {
                bool seated = activeIntents != null && activeIntents.TryGetValue(kvp.Value, out var state)
                    && state.Intent is RecreateIntent recreate && Key(recreate.VenueIndex, recreate.Seat) == kvp.Key;
                if (!seated) _scratch.Add(kvp.Key);
            }
            for (int i = 0; i < _scratch.Count; i++) _claims.Remove(_scratch[i]);
        }

        public void Reset() => _claims.Clear();
    }
}
