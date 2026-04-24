using System.Collections.Generic;
using Simpiens.Entities;

namespace Simpiens.Simulation
{
    public class WorldRegistry : IWorldRegistry
    {
        private readonly List<NodeController> _activeNodes = new List<NodeController>(128); // Pre-allocate some capacity
        private readonly Dictionary<UnityEngine.GUID, NodeController> _nodeLookup = new Dictionary<UnityEngine.GUID, NodeController>(128);

        public IReadOnlyList<NodeController> ActiveNodes => _activeNodes;

        public void RegisterNode(NodeController node)
        {
            if (!_activeNodes.Contains(node))
            {
                _activeNodes.Add(node);
                _nodeLookup[node.Id] = node;
            }
        }

        public void UnregisterNode(NodeController node)
        {
            if (_activeNodes.Remove(node))
            {
                _nodeLookup.Remove(node.Id);
            }
        }

        public NodeController GetNode(UnityEngine.GUID id)
        {
            _nodeLookup.TryGetValue(id, out var node);
            return node;
        }
    }
}
