using System;
using Simpiens.Simulation.Economy;
using UnityEngine;

namespace Simpiens.Simulation.Spatial
{
    /// <summary>
    /// A lightweight, stack-allocated data representation of an entity at a specific point in time.
    /// Used by the cognitive engine to read world state without triggering locks or GC on the main thread.
    /// </summary>
    public readonly struct EntitySnapshot
    {
        public readonly GUID Id;
        public readonly Vector2 Position;
        public readonly Vector2 Velocity;
        public readonly float Radius;
        public readonly EntityType Type;
        public readonly bool IsCollapsed;
        public readonly float Hunger;
        public readonly ItemType ResourceKind;

        public EntitySnapshot(GUID id, Vector2 position, Vector2 velocity, float radius, EntityType type)
            : this(id, position, velocity, radius, type, false, 0f)
        {
        }

        public EntitySnapshot(GUID id, Vector2 position, Vector2 velocity, float radius, EntityType type, bool isCollapsed, float hunger)
            : this(id, position, velocity, radius, type, isCollapsed, hunger, ItemType.None)
        {
        }

        public EntitySnapshot(GUID id, Vector2 position, Vector2 velocity, float radius, EntityType type, bool isCollapsed, float hunger, ItemType resourceKind)
        {
            ResourceKind = resourceKind;
            Id = id;
            Position = position;
            Velocity = velocity;
            Radius = radius;
            Type = type;
            IsCollapsed = isCollapsed;
            Hunger = hunger;
        }
    }
}
