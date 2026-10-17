using Simpiens.Cognition;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Simulation
{
    /// <summary>
    /// A simulation entity as plain data (Epic CORE C5): a crawler, a resource node, a hearth, a bunk, an injected wall.
    /// This, not a scene object, is what the simulation, the intent handlers and the spatial grid work with; views read
    /// it and never write it (ROADMAP §1A.1 rule 5). A crawler's position is its <see cref="AgentState.Position"/>, so
    /// there is one source of truth for it.
    /// </summary>
    public sealed class WorldEntity
    {
        /// <summary>Speed when none is configured (WorldEntity's old default).</summary>
        public const float DefaultBaseSpeed = 2f;

        private Vec2 _position;
        private readonly float _baseSpeed;

        public WorldEntity(EntityId id, EntityType type, Vec2 position, float radius, float baseSpeed = 0f, AgentState agent = null, string name = null)
        {
            Id = id;
            Type = type;
            Radius = radius;
            _baseSpeed = baseSpeed;
            Agent = agent;
            Name = name ?? string.Empty;
            if (agent != null) agent.Position = position;
            else _position = position;
            PreviousPosition = position;
        }

        public EntityId Id { get; }
        public EntityType Type { get; set; }

        /// <summary>The crawler's state; null for props (resources, hearths, bunks, walls).</summary>
        public AgentState Agent { get; }

        /// <summary>The authoritative logical position (a crawler's is its AgentState's).</summary>
        public Vec2 Position => Agent != null ? Agent.Position : _position;

        /// <summary>The position before the last move, for views that interpolate between ticks.</summary>
        public Vec2 PreviousPosition { get; private set; }

        /// <summary>Moves the entity. Main thread (the simulation), never a view.</summary>
        public void SetPosition(Vec2 position)
        {
            PreviousPosition = Position;
            if (Agent != null) Agent.Position = position;
            else _position = position;
        }

        /// <summary>Body radius for the spatial grid (crawler 0.35, resource 0.4, hearth 0.35, bunk 0.5).</summary>
        public float Radius { get; set; }

        public float BaseSpeed => _baseSpeed > 0f ? _baseSpeed : DefaultBaseSpeed;

        /// <summary>Always zero: every entity is kinematic or static, moved only by the simulation.</summary>
        public Vec2 Velocity => Vec2.Zero;

        /// <summary>Spawn-time yield kind of a resource node, mirrored into its snapshot (the yield state is ResourceData).</summary>
        public ItemType ResourceKind { get; set; }

        /// <summary>A label for logs and views ("SwarmAgent_3", "IronOreNode_-16_4").</summary>
        public string Name { get; set; }

        public override string ToString() => $"{Name} ({Type}, {Id})";
    }
}
