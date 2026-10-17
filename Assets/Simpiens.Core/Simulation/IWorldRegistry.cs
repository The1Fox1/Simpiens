using System;
using System.Collections.Generic;

namespace Simpiens.Simulation
{
    /// <summary>
    /// The entities present in the world, in registration order (which is the snapshot's entity order, and so the
    /// order cognition sees them in). Views follow <see cref="EntityRegistered"/> and <see cref="EntityUnregistered"/>
    /// to show and hide themselves; they never register or remove entities.
    /// </summary>
    public interface IWorldRegistry
    {
        IReadOnlyList<WorldEntity> ActiveEntities { get; }

        /// <summary>Adds an entity at the end of the order (a regrown node comes back at the end). Main thread.</summary>
        void Register(WorldEntity entity);

        /// <summary>Removes an entity (a depleted node, a removed wall). Main thread.</summary>
        void Unregister(WorldEntity entity);

        /// <summary>Forgets every entity without raising events (a respawn or load tears the world down).</summary>
        void Clear();

        WorldEntity GetEntity(EntityId id);

        event Action<WorldEntity> EntityRegistered;
        event Action<WorldEntity> EntityUnregistered;
    }
}
