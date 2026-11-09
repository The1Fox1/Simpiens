namespace Simpiens.Simulation.Creatures
{
    /// <summary>
    /// A creature as plain data (Epic 10 Part 3, K2.1; Q5: "a new kind of entity, plain data, saved"): its kind, its lair
    /// and its place there, where it is and its health. Its <see cref="WorldEntity"/> reads its position from here, as a
    /// crawler's reads its <see cref="Simpiens.Cognition.AgentState"/>'s. Main thread only: the creatures' phase writes it
    /// (<see cref="CreatureService"/>), views read it through their frames. What it does each tick is K2.2's.
    /// </summary>
    public sealed class CreatureState
    {
        public CreatureState(EntityId id, CreatureKind kind, int lairIndex, byte slot, Vec2 home)
        {
            Id = id;
            Kind = kind;
            LairIndex = lairIndex;
            Slot = slot;
            Home = home;
            Position = home;
            Health = CreatureCatalog.Get(kind)?.MaxHealth ?? 0f;
        }

        public EntityId Id { get; }

        public CreatureKind Kind { get; }

        /// <summary>Its kind's definition: what it brings to a fight, how it hunts and heals.</summary>
        public CreatureDefinition Definition => CreatureCatalog.Get(Kind);

        /// <summary>Its lair, by its index in the map's lairs.</summary>
        public int LairIndex { get; }

        /// <summary>Its place in its lair's pack: a slain member returns to the same one.</summary>
        public byte Slot { get; }

        /// <summary>Where it rests in its lair.</summary>
        public Vec2 Home { get; }

        /// <summary>Where it is now (its entity's position).</summary>
        public Vec2 Position { get; set; }

        public float Health { get; set; }

        public bool IsAlive => Health > 0f;

        public override string ToString() => $"{CreatureCatalog.NameOf(Kind)} {Id} of lair {LairIndex}.{Slot} at {Position}, health {Health:0.#}";
    }
}
