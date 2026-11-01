using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using Simpiens.Narrative;
using Simpiens.Simulation.Economy;
using ZLogger;

namespace Simpiens.Simulation
{
    public partial class SimulationManager
    {
        // --- Rest anchor: bunks (Epic MAP Phase 3) ---
        /// <summary>Slack on top of BunkTargeting.ReachDistance for a sleeper's final step onto the bunk.</summary>
        public const float BunkClaimTolerance = 0.25f;

        /// <summary>A sleeper this close to the bunk's position is lying in it.</summary>
        public const float BunkSettleDistance = 0.02f;

        private readonly Dictionary<EntityId, Vec2> _bunks = new Dictionary<EntityId, Vec2>(16);
        private readonly Dictionary<EntityId, EntityId> _bunkOccupants = new Dictionary<EntityId, EntityId>(16);
        private readonly List<EntityId> _occupiedBunks = new List<EntityId>(16);
        private readonly Dictionary<EntityId, int> _bunkSleeps = new Dictionary<EntityId, int>(16);
        private readonly List<EntityId> _bunkScratch = new List<EntityId>(16);

        private readonly List<ushort> _departingClaims = new List<ushort>(2);
        private readonly List<EntityId> _departingBunks = new List<EntityId>(1);

        public void RegisterBunk(EntityId id, Vec2 position) => _bunks[id] = position;

        /// <summary>Bunk id to sleeper id, for the bunks taken right now.</summary>
        public IReadOnlyDictionary<EntityId, EntityId> BunkOccupants => _bunkOccupants;

        /// <summary>Completed bunk sleeps per agent this session.</summary>
        public IReadOnlyDictionary<EntityId, int> BunkSleeps => _bunkSleeps;

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

            Vec2 candidate = Vec2.MoveTowards(node.Position, bunkPosition, node.BaseSpeed * FixedTickDeltaTime);
            // Someone still standing in it (a sleeper just up): wait, within the handler's bounded approach, rather than
            // lie down on them (Epic 10 G0: walkers reach occupied bunks now, and stacked there)
            if (IsCrowdedMove(node, candidate)) return IntentResult.InProgress;
            if (!TryDisplacePawn(node, candidate, respectPawns: false)) return IntentResult.PathBlocked;
            return (bunkPosition - candidate).SqrMagnitude <= BunkSettleDistance * BunkSettleDistance ? IntentResult.Success : IntentResult.InProgress;
        }

        public void DepartOutpost(WorldEntity node)
        {
            var agent = node?.Agent;
            if (agent == null || agent.HasLeft) return;
            uint tick = _clock != null ? (uint)_clock.CurrentTick : 0;

            var inventory = agent.Inventory;
            int gold = inventory != null ? inventory.Gold : 0;
            if (gold > 0)
            {
                _economyLedger?.RecordSink((uint)gold, "DrifterDepartures");
                inventory.Gold = 0;
            }

            _departingClaims.Clear();
            for (int i = 0; i < _activeBountiesBuffer.Count; i++)
            {
                var bounty = _activeBountiesBuffer[i];
                if (bounty.Status == BountyStatus.Claimed && bounty.ClaimantId == node.Id) _departingClaims.Add(bounty.BountyId);
            }
            for (int i = 0; i < _departingClaims.Count; i++)
            {
                if (_bounties.TryGetValue(_departingClaims[i], out var claimed)) UpdateBounty(claimed.WithClaimant(default, BountyStatus.Open));
            }

            _departingBunks.Clear();
            foreach (var kvp in _bunkOccupants)
            {
                if (kvp.Value == node.Id) _departingBunks.Add(kvp.Key);
            }
            for (int i = 0; i < _departingBunks.Count; i++) ReleaseBunk(_departingBunks[i], node.Id);

            // Leaving the outpost leaves every group (Epic 10 G4.4): no departed crawler stays on a roster, nor in the guild
            if (_groupRegistry != null)
            {
                _groupRegistry.LeaveAllGroups(node.Id);
                agent.Memberships = _groupRegistry.GetMemberships(node.Id);
            }
            Guild.Forget(node.Id);

            agent.HasLeft = true;
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(node.Id)} left by the Road Gate with {gold}g ({_departingClaims.Count} claim(s) released)");
            _chronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.DrifterLeft, node.Id, default, GetSectorForPosition(node.Position), tick));
        }

        public void ReleaseBunk(EntityId bunkId, EntityId agentId)
        {
            if (_bunkOccupants.TryGetValue(bunkId, out var occupant) && occupant == agentId)
            {
                _bunkOccupants.Remove(bunkId);
                RebuildOccupiedBunks();
            }
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
        internal void ValidateBunkOccupancy()
        {
            if (_bunkOccupants.Count == 0) return;
            _bunkScratch.Clear();
            foreach (var kvp in _bunkOccupants)
            {
                bool sleeping = _activeIntents.TryGetValue(kvp.Value, out var state)
                    && state.Intent is SleepAtBunkIntent sleep && sleep.BunkId == kvp.Key;
                if (!sleeping) _bunkScratch.Add(kvp.Key);
            }
            if (_bunkScratch.Count == 0) return;
            for (int i = 0; i < _bunkScratch.Count; i++)
            {
                // A sleeper pulled out of bed (preempted) gets up, unless its new intent already set its own look
                if (_agentRegistry != null && _agentRegistry.TryGetAgent(_bunkOccupants[_bunkScratch[i]], out var sleeper)
                    && sleeper.VisualState == Simpiens.Entities.AgentVisualState.Sleeping)
                {
                    sleeper.VisualState = Simpiens.Entities.AgentVisualState.Idle;
                }
                _bunkOccupants.Remove(_bunkScratch[i]);
            }
            RebuildOccupiedBunks();
        }

        private void RebuildOccupiedBunks()
        {
            _occupiedBunks.Clear();
            foreach (var kvp in _bunkOccupants) _occupiedBunks.Add(kvp.Key);
        }
    }
}
