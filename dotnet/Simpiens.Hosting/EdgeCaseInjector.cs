using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Hosting
{
    /// <summary>
    /// Drops an obstacle into the running world (Epic CLIENT M2.8; Unity's <c>EdgeCaseInjector</c>, without its view or
    /// physics): a wall entity registered like any other, which the next snapshot shows to pathing and movement. Test
    /// harness only: the scenario chooses the spot.
    /// </summary>
    public sealed class EdgeCaseInjector
    {
        public const float WallRadius = 0.5f;

        private readonly IWorldRegistry _registry;
        private readonly IEntityIdAllocator _ids;

        public EdgeCaseInjector(IWorldRegistry registry, IEntityIdAllocator ids = null)
        {
            _registry = registry;
            _ids = ids;
        }

        public static EdgeCaseInjector For(LocalSimulation sim) =>
            new EdgeCaseInjector(sim.Registry, (IEntityIdAllocator)sim.Services.GetService(typeof(IEntityIdAllocator)));

        /// <summary>Registers a wall at <paramref name="position"/> and returns it.</summary>
        public WorldEntity InjectWall(Vec2 position)
        {
            var wall = new WorldEntity(_ids != null ? _ids.Next() : EntityId.Generate(), EntityType.Wall, position, WallRadius, name: "InjectedWall");
            _registry.Register(wall);
            return wall;
        }
    }
}
