using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using Simpiens.Narrative;
using Simpiens.Simulation.World;
using ZLogger;

namespace Simpiens.Simulation
{
    public partial class SimulationManager
    {
        // --- Rest anchor: bunks (Epic MAP Phase 3), kept by Lodging ---
        /// <summary>Slack on top of BunkTargeting.ReachDistance for a sleeper's final step onto the bunk.</summary>
        public const float BunkClaimTolerance = Lodging.BunkClaimTolerance;

        /// <summary>A sleeper this close to the bunk's position is lying in it.</summary>
        public const float BunkSettleDistance = Lodging.BunkSettleDistance;

        public void RegisterBunk(EntityId id, Vec2 position) => _lodging.RegisterBunk(id, position);

        /// <summary>Bunk id to sleeper id, for the bunks taken right now.</summary>
        public IReadOnlyDictionary<EntityId, EntityId> BunkOccupants => _lodging.BunkOccupants;

        /// <summary>Completed bunk sleeps per agent this session.</summary>
        public IReadOnlyDictionary<EntityId, int> BunkSleeps => _lodging.BunkSleeps;

        public bool TryClaimBunk(EntityId bunkId, EntityId agentId, Vec2 agentPosition) => _lodging.TryClaimBunk(bunkId, agentId, agentPosition);

        public IntentResult StepOntoBunk(WorldEntity node, EntityId bunkId) => _lodging.StepOntoBunk(node, bunkId);

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

            int releasedClaims = _market.ReleaseClaimsForAgent(node.Id);
            _lodging.ReleaseAllForAgent(node.Id);

            // Leaving the outpost leaves every group (Epic 10 G4.4): no departed crawler stays on a roster, nor in the guild
            if (_groupRegistry != null)
            {
                _groupRegistry.LeaveAllGroups(node.Id);
                agent.Memberships = _groupRegistry.GetMemberships(node.Id);
            }
            Guild.Forget(node.Id);
            _workshops.Forget(node.Id);

            agent.HasLeft = true;
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(node.Id)} left by the Road Gate with {gold}g ({releasedClaims} claim(s) released)");
            _chronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.DrifterLeft, node.Id, default, GetSectorForPosition(node.Position), tick));
        }

        public void ReleaseBunk(EntityId bunkId, EntityId agentId) => _lodging.ReleaseBunk(bunkId, agentId);

        public void RecordBunkSleep(EntityId agentId) => _lodging.RecordBunkSleep(agentId);

        /// <summary>
        /// Drops every claim whose sleeper is no longer sleeping in that bunk (preempted, cancelled or removed), so a
        /// bunk can never stay taken by someone who left it. Runs every tick before the snapshot.
        /// </summary>
        internal void ValidateBunkOccupancy() => _lodging.ValidateOccupancy(_activeIntents, _agentRegistry);
    }
}
