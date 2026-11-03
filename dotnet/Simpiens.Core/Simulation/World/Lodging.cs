using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Movement;
using Simpiens.Simulation.Persistence;

namespace Simpiens.Simulation.World
{
    /// <summary>
    /// The rest anchor's bunks (Epic MAP Phase 3): where they are, who sleeps in which, and how many sleeps each crawler
    /// had. A bunk holds one sleeper, and a claim never outlives the sleep. Main thread.
    /// </summary>
    internal sealed class Lodging
    {
        /// <summary>Slack on top of BunkTargeting.ReachDistance for a sleeper's final step onto the bunk.</summary>
        public const float BunkClaimTolerance = 0.25f;

        /// <summary>A sleeper this close to the bunk's position is lying in it.</summary>
        public const float BunkSettleDistance = 0.02f;

        private readonly Locomotion _locomotion;

        private readonly Dictionary<EntityId, Vec2> _bunks = new Dictionary<EntityId, Vec2>(16);
        private readonly Dictionary<EntityId, EntityId> _bunkOccupants = new Dictionary<EntityId, EntityId>(16);
        private readonly List<EntityId> _occupiedBunks = new List<EntityId>(16);
        private readonly Dictionary<EntityId, int> _bunkSleeps = new Dictionary<EntityId, int>(16);
        private readonly List<EntityId> _bunkScratch = new List<EntityId>(16);

        private readonly List<EntityId> _departingBunks = new List<EntityId>(1);

        public Lodging(Locomotion locomotion = null)
        {
            _locomotion = locomotion ?? new Locomotion();
        }

        public void RegisterBunk(EntityId id, Vec2 position) => _bunks[id] = position;

        /// <summary>Bunk id to sleeper id, for the bunks taken right now.</summary>
        public IReadOnlyDictionary<EntityId, EntityId> BunkOccupants => _bunkOccupants;

        /// <summary>Completed bunk sleeps per agent this session.</summary>
        public IReadOnlyDictionary<EntityId, int> BunkSleeps => _bunkSleeps;

        /// <summary>The bunks taken right now. Always the same list: the spatial partition keeps it.</summary>
        public IReadOnlyList<EntityId> OccupiedBunks => _occupiedBunks;

        public bool TryClaimBunk(EntityId bunkId, EntityId agentId, Vec2 agentPosition)
        {
            if (!_bunks.TryGetValue(bunkId, out var bunkPosition)) return false;
            float reach = Simpiens.Cognition.Planning.BunkTargeting.ReachDistance + BunkClaimTolerance;
            if ((bunkPosition - agentPosition).SqrMagnitude > reach * reach) return false;
            if (_bunkOccupants.TryGetValue(bunkId, out var occupant) && occupant != agentId) return false;
            _bunkOccupants[bunkId] = agentId;
            RebuildOccupiedBunks();
            return true;
        }

        public IntentResult StepOntoBunk(WorldEntity node, EntityId bunkId)
        {
            if (node == null || !_bunks.TryGetValue(bunkId, out var bunkPosition)) return IntentResult.TargetMissing;
            if (_bunkOccupants.TryGetValue(bunkId, out var occupant) && occupant != node.Id) return IntentResult.TargetMissing;

            // Walk to the bunk's exact position (at walking speed, so the view never sees a jump): the sleeper lies in it
            if ((bunkPosition - (Vec2)node.Position).SqrMagnitude <= BunkSettleDistance * BunkSettleDistance) return IntentResult.Success;

            Vec2 candidate = Vec2.MoveTowards(node.Position, bunkPosition, node.BaseSpeed * SimulationManager.FixedTickDeltaTime);
            // Someone still standing in it (a sleeper just up): wait, within the handler's bounded approach, rather than
            // lie down on them (Epic 10 G0: walkers reach occupied bunks now, and stacked there)
            if (_locomotion.IsCrowdedMove(node, candidate)) return IntentResult.InProgress;
            if (!_locomotion.TryDisplacePawn(node, candidate, respectPawns: false)) return IntentResult.PathBlocked;
            return (bunkPosition - candidate).SqrMagnitude <= BunkSettleDistance * BunkSettleDistance ? IntentResult.Success : IntentResult.InProgress;
        }

        public void ReleaseBunk(EntityId bunkId, EntityId agentId)
        {
            if (_bunkOccupants.TryGetValue(bunkId, out var occupant) && occupant == agentId)
            {
                _bunkOccupants.Remove(bunkId);
                RebuildOccupiedBunks();
            }
        }

        /// <summary>Releases every bunk <paramref name="agentId"/> holds (a crawler leaving the outpost).</summary>
        public void ReleaseAllForAgent(EntityId agentId)
        {
            _departingBunks.Clear();
            foreach (var kvp in _bunkOccupants)
            {
                if (kvp.Value == agentId) _departingBunks.Add(kvp.Key);
            }
            for (int i = 0; i < _departingBunks.Count; i++) ReleaseBunk(_departingBunks[i], agentId);
        }

        public void RecordBunkSleep(EntityId agentId)
        {
            _bunkSleeps.TryGetValue(agentId, out int n);
            _bunkSleeps[agentId] = n + 1;
        }

        /// <summary>
        /// Drops every claim whose sleeper is no longer sleeping in that bunk (preempted, cancelled or removed), so a
        /// bunk can never stay taken by someone who left it. Runs every tick before the snapshot.
        /// </summary>
        public void ValidateOccupancy(IReadOnlyDictionary<EntityId, ActiveIntentState> activeIntents, IAgentRegistry agentRegistry)
        {
            if (_bunkOccupants.Count == 0) return;
            _bunkScratch.Clear();
            foreach (var kvp in _bunkOccupants)
            {
                bool sleeping = activeIntents != null && activeIntents.TryGetValue(kvp.Value, out var state)
                    && state.Intent is SleepAtBunkIntent sleep && sleep.BunkId == kvp.Key;
                if (!sleeping) _bunkScratch.Add(kvp.Key);
            }
            if (_bunkScratch.Count == 0) return;
            for (int i = 0; i < _bunkScratch.Count; i++)
            {
                // A sleeper pulled out of bed (preempted) gets up, unless its new intent already set its own look
                if (agentRegistry != null && agentRegistry.TryGetAgent(_bunkOccupants[_bunkScratch[i]], out var sleeper)
                    && sleeper.VisualState == Simpiens.Entities.AgentVisualState.Sleeping)
                {
                    sleeper.VisualState = Simpiens.Entities.AgentVisualState.Idle;
                }
                _bunkOccupants.Remove(_bunkScratch[i]);
            }
            RebuildOccupiedBunks();
        }

        /// <summary>The bunk sleeps for the save's world block, sorted by agent id.</summary>
        public IdCountRecord[] CaptureSleeps()
        {
            var sleeps = new IdCountRecord[_bunkSleeps.Count];
            int n = 0;
            foreach (var kvp in _bunkSleeps) sleeps[n++] = new IdCountRecord { Id = kvp.Key, Count = kvp.Value };
            System.Array.Sort(sleeps, (a, b) => a.Id.CompareTo(b.Id));
            return sleeps;
        }

        public void RestoreSleeps(IdCountRecord[] sleeps)
        {
            _bunkSleeps.Clear();
            if (sleeps != null)
            {
                for (int i = 0; i < sleeps.Length; i++) _bunkSleeps[sleeps[i].Id] = sleeps[i].Count;
            }
        }

        public void Reset()
        {
            _bunks.Clear();
            _bunkOccupants.Clear();
            _occupiedBunks.Clear();
            _bunkSleeps.Clear();
        }

        private void RebuildOccupiedBunks()
        {
            _occupiedBunks.Clear();
            foreach (var kvp in _bunkOccupants) _occupiedBunks.Add(kvp.Key);
        }
    }
}
