using System.Collections.Generic;
using Simpiens.Diagnostics;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
using ZLogger;

namespace Simpiens.Simulation
{
    public partial class SimulationManager
    {
        private readonly Dictionary<EntityId, ResourceData> _resources = new Dictionary<EntityId, ResourceData>();
        private readonly Dictionary<EntityId, WorldEntity> _dormantNodes = new Dictionary<EntityId, WorldEntity>();
        private readonly List<Simpiens.Cognition.Memory.SpatialMemoryRecord> _surveyBuffer = new List<Simpiens.Cognition.Memory.SpatialMemoryRecord>(16);
        private readonly Dictionary<ItemType, int> _harvested = new Dictionary<ItemType, int>(4);

        public void RegisterResource(ResourceData data)
        {
            if (data.MaxYield <= 0) data.MaxYield = data.RemainingYield;
            _resources[data.EntityId] = data;
        }

        public bool TryGetResource(EntityId resourceId, out ResourceData resource) => _resources.TryGetValue(resourceId, out resource);

        public void RemoveResource(EntityId resourceId) => _resources.Remove(resourceId);

        public void DepleteResource(EntityId resourceId)
        {
            var node = _worldRegistry?.GetEntity(resourceId);

            if (_resources.TryGetValue(resourceId, out var resource) && resource.RegrowTicks > 0 && _supplyService != null)
            {
                // Dormant: hidden from perception and harvesting until the supply service regrows it
                uint tick = CurrentTick;
                resource.IsDormant = true;
                resource.RemainingYield = 0;
                resource.DepletedAtTick = tick;
                _supplyService.RegisterDormant(resourceId, tick + resource.RegrowTicks);
                SimLog.Simulation.ZLogDebug($"Resource {SimLog.Agent(resourceId)} ({resource.YieldItem}) depleted -> dormant until t{tick + resource.RegrowTicks}");

                if (node != null)
                {
                    _dormantNodes[resourceId] = node;
                    _worldRegistry.Unregister(node);
                }
                return;
            }

            _resources.Remove(resourceId);
            if (node != null)
            {
                _worldRegistry.Unregister(node);
            }
            SimLog.Simulation.ZLogDebug($"Resource {SimLog.Agent(resourceId)} depleted and removed permanently (no regrowth)");
        }

        public void ReactivateResource(EntityId resourceId)
        {
            if (_dormantNodes.TryGetValue(resourceId, out var node))
            {
                _dormantNodes.Remove(resourceId);
                if (node != null)
                {
                    _worldRegistry?.Register(node);
                }
                SimLog.Simulation.ZLogDebug($"Resource {SimLog.Agent(resourceId)} reactivated in the world");
            }
        }

        public void MarkSectorExplored(int sectorId)
        {
            _sectors?.SetExplored(sectorId, true);
            SimLog.Economy.ZLogInformation($"Sector {sectorId} marked explored");
        }

        /// <summary>
        /// A scout's successful report publishes its survey of the sector: everything it noted inside the fog goes
        /// into every agent's spatial memory (a newer record is kept), in entity id order. Main thread.
        /// </summary>
        public int PublishSurvey(EntityId scoutId, int sectorId)
        {
            var scoutMemory = FindMemory(scoutId);
            if (scoutMemory == null) return 0;

            _surveyBuffer.Clear();
            scoutMemory.TakeSurveyNotes((byte)sectorId, _surveyBuffer);
            if (_surveyBuffer.Count == 0) return 0;
            StableSort.Sort(_surveyBuffer, (a, b) => a.EntityId.CompareTo(b.EntityId));

            int recipients = 0;
            if (_agentRegistry != null)
            {
                var agents = _agentRegistry.AllAgents;
                for (int i = 0; i < agents.Count; i++)
                {
                    if (agents[i]?.Memory == null) continue;
                    agents[i].Memory.ReceiveSurvey(_surveyBuffer);
                    recipients++;
                }
            }
            else
            {
                scoutMemory.ReceiveSurvey(_surveyBuffer);
                recipients = 1;
            }

            SimLog.Economy.ZLogInformation($"{SimLog.Agent(scoutId)} charted sector {sectorId}: {_surveyBuffer.Count} landmarks shared with {recipients} agents");
            return _surveyBuffer.Count;
        }

        private Simpiens.Cognition.Memory.AgentMemory FindMemory(EntityId agentId)
        {
            if (_agentRegistry != null && _agentRegistry.TryGetAgent(agentId, out var state)) return state.Memory;
            var node = _worldRegistry?.GetEntity(agentId);
            if (node == null) return null;
            if (node.Agent != null) return node.Agent.Memory;
            return node.Agent != null ? node.Agent.Memory : null;
        }

        public void RecordHarvest(ItemType item)
        {
            _harvested.TryGetValue(item, out int n);
            _harvested[item] = n + 1;
        }

        /// <summary>Units of the item harvested this session.</summary>
        public int HarvestedUnits(ItemType item) => _harvested.TryGetValue(item, out int n) ? n : 0;
    }
}
