using System.Collections.Generic;
using Simpiens.Entities;

namespace Simpiens.Simulation
{
    public class WorldRegistry : IWorldRegistry
    {
        private readonly List<NodeController> _activeNodes = new List<NodeController>(128); // Pre-allocate some capacity

        public IReadOnlyList<NodeController> ActiveNodes => _activeNodes;

        public void RegisterNode(NodeController node)
        {
            if (!_activeNodes.Contains(node))
            {
                _activeNodes.Add(node);
            }
        }

        public void UnregisterNode(NodeController node)
        {
            _activeNodes.Remove(node);
        }
    }
}
