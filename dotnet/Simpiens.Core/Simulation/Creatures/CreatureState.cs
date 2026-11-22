namespace Simpiens.Simulation.Creatures
{
    /// <summary>What a creature is doing (Epic 10 Part 3, K2.2; Q5): the creature's rule moves it from one to the next.</summary>
    public enum CreatureMode : byte
    {
        /// <summary>At its place in its lair, healing; it roams again when rested.</summary>
        Rest = 0,
        /// <summary>Walking to a point near its lair, in its sector.</summary>
        Roam = 1,
        /// <summary>After a crawler it saw, within its leash; it strikes once in reach.</summary>
        Chase = 2,
        /// <summary>Hurt past its kind's line: running for its place, heeding nothing.</summary>
        Flee = 3,
        /// <summary>Walking back to its place: its quarry lost, or past its leash.</summary>
        Home = 4,
    }

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

        /// <summary>What it is doing (K2.2).</summary>
        public CreatureMode Mode { get; set; }

        /// <summary>The crawler it chases; empty unless it chases.</summary>
        public EntityId Target { get; set; }

        /// <summary>The first tick it may swing again (the combat rule's D8: the swing's timer is its owner's).</summary>
        public long NextSwingAt { get; set; }

        /// <summary>
        /// The last tick a living crawler stood in its lair's reach (Epic 10 Part 3, K10.3c), for a kind that stands whole again once
        /// its lair has been empty long enough; 0 for never. Saved: a save between attempts keeps the clock.
        /// </summary>
        public uint LastVisitAt { get; set; }

        /// <summary>
        /// The crawler whose downing named it (Epic 11, S7.1, D31): "{its name}'s Bane", by its id while it lives and the departed
        /// list's name after. Empty for an unnamed creature. Saved.
        /// </summary>
        public EntityId Victim { get; set; }

        /// <summary>Its rank (D31): 0 unnamed, then 1 to <see cref="CreatureTitleTable.MaxRank"/> by the crawlers it has downed. Saved.</summary>
        public byte Rank { get; set; }

        /// <summary>How many crawlers it has downed since it was named, the first among them (D31). Saved.</summary>
        public byte Downed { get; set; }

        /// <summary>The last crawler that struck it and lived (Epic 11, S7.3, D33): it sees its foe nearer. Empty for none. Saved.</summary>
        public EntityId Foe { get; set; }

        /// <summary>True once it has a name (D31).</summary>
        public bool IsNamed => Rank > 0;

        /// <summary>Its health when whole: its kind's, grown by its rank (Epic 11, S7.2, D32).</summary>
        public float MaxHealth => Rank == 0 ? (Definition?.MaxHealth ?? 0f) : (float)((Definition?.MaxHealth ?? 0f) * CreatureTitleTable.HealthFactor(Rank));

        /// <summary>What it heals a tick resting at its place: its kind's, grown with its health, so it is whole in its kind's hours.</summary>
        public float HealPerTick => Rank == 0 ? (Definition?.HealPerTick ?? 0f) : (float)((Definition?.HealPerTick ?? 0f) * CreatureTitleTable.HealthFactor(Rank));

        /// <summary>When it last swung, hit or miss (K2.5's strike pose); 0 for never. Unsaved, as a crawler's <c>StruckAtTick</c> is.</summary>
        public uint SwungAtTick { get; set; }

        /// <summary>Until when it rests before it roams (physical time, ticks).</summary>
        public long RestUntil { get; set; }

        /// <summary>Where it walks: a roam's point, its place, its quarry.</summary>
        public Vec2 Goal { get; set; }

        // Its entity, moved through SetPosition so views can draw it between ticks
        internal WorldEntity Entity { get; set; }

        // Its path to the goal: work in progress, planned again after a load
        internal readonly Vec2[] Path = new Vec2[CreatureService.MaxPath];
        internal int PathLength;
        internal int PathCursor;
        internal long PlannedAt = -1;
        internal Vec2 PlannedFor;

        internal void ForgetPath()
        {
            PathLength = 0;
            PathCursor = 0;
            PlannedAt = -1;
        }

        public override string ToString() => $"{CreatureCatalog.NameOf(Kind)} {Id} of lair {LairIndex}.{Slot} at {Position}, health {Health:0.#}, {Mode}";
    }
}
