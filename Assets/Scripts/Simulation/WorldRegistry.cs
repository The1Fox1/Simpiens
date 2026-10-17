using System.Collections.Generic;
using Simpiens.Entities;

namespace Simpiens.Simulation
{
    public class WorldRegistry : IWorldRegistry
    {
        public static readonly EntityId PlayerEntityId = new EntityId("00000000000000000000000000000001");

        private readonly List<NodeController> _activeNodes = new List<NodeController>(128); // Pre-allocate some capacity
        private readonly Dictionary<EntityId, NodeController> _nodeLookup = new Dictionary<EntityId, NodeController>(128);

        public IReadOnlyList<NodeController> ActiveNodes => _activeNodes;

        public void RegisterNode(NodeController node)
        {
            if (node == null) return;
            if (!_activeNodes.Contains(node))
            {
                _activeNodes.Add(node);
                _nodeLookup[node.Id] = node;
                node.SetRegistry(this);
            }
        }

        public void UnregisterNode(NodeController node)
        {
            if (node == null) return;
            if (_activeNodes.Remove(node))
            {
                // Safely remove from lookup without accessing properties on potentially destroyed Unity object
                EntityId keyToRemove = default;
                bool found = false;
                foreach (var kvp in _nodeLookup)
                {
                    if (kvp.Value == node)
                    {
                        keyToRemove = kvp.Key;
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    _nodeLookup.Remove(keyToRemove);
                }
            }
        }

        public void PruneDestroyed()
        {
            for (int i = _activeNodes.Count - 1; i >= 0; i--)
            {
                if (_activeNodes[i] == null)
                {
                    _activeNodes.RemoveAt(i);
                }
            }

            List<EntityId> keysToRemove = null;
            foreach (var kvp in _nodeLookup)
            {
                if (kvp.Value == null)
                {
                    keysToRemove ??= new List<EntityId>();
                    keysToRemove.Add(kvp.Key);
                }
            }
            if (keysToRemove != null)
            {
                for (int i = 0; i < keysToRemove.Count; i++)
                {
                    _nodeLookup.Remove(keysToRemove[i]);
                }
            }
        }

        public void Clear()
        {
            _activeNodes.Clear();
            _nodeLookup.Clear();
        }

        public NodeController GetNode(EntityId id)
        {
            if (_nodeLookup.TryGetValue(id, out var node))
            {
                if (node != null) return node;
                _nodeLookup.Remove(id);
            }
            return null;
        }
    }
}
