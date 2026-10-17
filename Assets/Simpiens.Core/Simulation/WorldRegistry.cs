using System;
using System.Collections.Generic;

namespace Simpiens.Simulation
{
    public class WorldRegistry : IWorldRegistry
    {
        public static readonly EntityId PlayerEntityId = new EntityId("00000000000000000000000000000001");

        private readonly List<WorldEntity> _activeEntities = new List<WorldEntity>(128); // Pre-allocate some capacity
        private readonly Dictionary<EntityId, WorldEntity> _lookup = new Dictionary<EntityId, WorldEntity>(128);

        public IReadOnlyList<WorldEntity> ActiveEntities => _activeEntities;

        public event Action<WorldEntity> EntityRegistered;
        public event Action<WorldEntity> EntityUnregistered;

        public void Register(WorldEntity entity)
        {
            if (entity == null) return;
            if (!_activeEntities.Contains(entity))
            {
                _activeEntities.Add(entity);
                _lookup[entity.Id] = entity;
                EntityRegistered?.Invoke(entity);
            }
        }

        public void Unregister(WorldEntity entity)
        {
            if (entity == null) return;
            if (_activeEntities.Remove(entity))
            {
                if (_lookup.TryGetValue(entity.Id, out var current) && current == entity) _lookup.Remove(entity.Id);
                EntityUnregistered?.Invoke(entity);
            }
        }

        public void Clear()
        {
            _activeEntities.Clear();
            _lookup.Clear();
        }

        public WorldEntity GetEntity(EntityId id)
        {
            return _lookup.TryGetValue(id, out var entity) ? entity : null;
        }
    }
}
