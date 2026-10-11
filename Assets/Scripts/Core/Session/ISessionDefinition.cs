using Simpiens.Simulation.World;

namespace Simpiens.Core.Session
{
    /// <summary>
    /// One playable (or test) session: a map plus the content spawned onto it. <see cref="SwarmSpawner"/> builds the
    /// world from <see cref="Map"/> (partition, walls, sectors, resource nodes, hearths) and then calls
    /// <see cref="Populate"/> for everything else: agents, relationships, groups, stalls and seeded bounties.
    /// The live game uses the outpost session; the test harness adds the Dev Map pen canaries (Epic MAP Phase 2).
    /// </summary>
    public interface ISessionDefinition
    {
        /// <summary>Stable id used to select the session (tests, the session switch).</summary>
        string Id { get; }

        /// <summary>Name shown on the session switch.</summary>
        string DisplayName { get; }

        MapDefinition Map { get; }

        /// <summary>Spawns the session's content. Called once per respawn, after the map's entities exist.</summary>
        void Populate(SessionBuilder session);
    }
}
