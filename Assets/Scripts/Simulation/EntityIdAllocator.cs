using UnityEngine;

namespace Simpiens.Simulation
{
    /// <summary>
    /// Issues entity ids for simulation-owned entities. Ids feed tie-breaks, exploration hashes and
    /// <see cref="SimRandom"/> streams, so they must be reproducible for a given seed (ROADMAP §1A.1 rule 4).
    /// </summary>
    public interface IEntityIdAllocator
    {
        GUID Next();

        /// <summary>Restart the sequence (call when the world is rebuilt so a respawn reproduces the same ids).</summary>
        void Reset();

        /// <summary>Ids handed out since the last reset: with the seed, all a save needs to continue the sequence.</summary>
        long Counter { get; }
    }

    /// <summary>
    /// Deterministic id sequence: the n-th id after <see cref="Reset"/> depends only on the seed and n.
    /// Main thread only (entities are created by the simulation thread).
    /// </summary>
    public sealed class EntityIdAllocator : IEntityIdAllocator
    {
        private readonly ulong _seed;
        private long _counter;

        public long Counter => _counter;

        public EntityIdAllocator(SimulationSettings settings = null)
        {
            _seed = settings != null ? settings.Seed : SimulationSettings.DefaultSeed;
        }

        public GUID Next()
        {
            while (true)
            {
                var rng = SimRandom.For(_seed, default, _counter++, SimRandomSalt.EntityId);
                ulong hi = rng.NextULong();
                ulong lo = rng.NextULong();
                var id = new GUID((uint)(hi >> 32), (uint)hi, (uint)(lo >> 32), (uint)lo);
                // Never hand out the empty id or the reserved player id
                if (id != default && id != WorldRegistry.PlayerEntityId) return id;
            }
        }

        public void Reset()
        {
            _counter = 0;
        }
    }
}
