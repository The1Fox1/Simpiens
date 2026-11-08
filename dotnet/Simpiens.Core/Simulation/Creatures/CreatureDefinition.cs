using Simpiens.Simulation.Combat;

namespace Simpiens.Simulation.Creatures
{
    /// <summary>
    /// What one kind of creature is (Epic 10 Part 3, K0.4; seam A6): its body, what it brings to a fight, how it hunts and
    /// how it heals and returns. Immutable, written once in <see cref="CreatureCatalog"/>.
    /// <para>
    /// Two kinds of time, as everywhere (docs/time-model.md):
    /// <list type="bullet">
    /// <item>what is watched is physical: speed in world units a second, the swing's interval in seconds;</item>
    /// <item>what lasts across the day is in game hours: healing at its lair, and returning after it is slain.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class CreatureDefinition
    {
        public CreatureDefinition(CreatureKind id, string name, string displayName, string artKey = null)
        {
            Id = id;
            Name = name;
            DisplayName = displayName;
            ArtKey = artKey ?? Simpiens.Simulation.Items.ItemDefinition.KebabCase(name);
        }

        public CreatureKind Id { get; }

        /// <summary>A code name, for logs: "BurrowRat".</summary>
        public string Name { get; }

        /// <summary>The player's name: "Burrow Rat".</summary>
        public string DisplayName { get; }

        /// <summary>Its art's key in kebab case ("burrow-rat"), as an item's is; the art tables come with the client's creatures (K2.5).</summary>
        public string ArtKey { get; }

        /// <summary>Health when whole.</summary>
        public float MaxHealth { get; init; }

        /// <summary>A landed blow's harm before the target's defense (the combat rule's <see cref="Combatant.Attack"/>).</summary>
        public float Attack { get; init; }

        /// <summary>What cuts a blow it takes: defense D stops D / (D + 10) of it.</summary>
        public float Defense { get; init; }

        /// <summary>How far its blow reaches across the gap between two bodies, in world units.</summary>
        public float Reach { get; init; }

        /// <summary>Seconds between two swings.</summary>
        public float SwingIntervalSeconds { get; init; }

        /// <summary>How far its shot carries, in world units; 0 for a kind that only strikes in reach (shots come with K8).</summary>
        public float Range { get; init; }

        /// <summary>How fast it moves, in world units a second (a crawler walks at 2).</summary>
        public float Speed { get; init; }

        /// <summary>Its body's radius.</summary>
        public float BodyRadius { get; init; }

        /// <summary>How near a crawler must come for it to notice, in world units.</summary>
        public float Sight { get; init; }

        /// <summary>How far from its lair it chases before it gives up and goes home, in world units.</summary>
        public float Leash { get; init; }

        /// <summary>The share of its health under which it runs for its lair; 0 for a kind that never flees.</summary>
        public float FleeBelow { get; init; }

        /// <summary>How many of it a lair holds when full (spawns come with lairs, K2.1).</summary>
        public byte PackMin { get; init; }
        public byte PackMax { get; init; }

        /// <summary>Game hours to heal from nothing to whole while it rests at its lair.</summary>
        public int HealHours { get; init; }

        /// <summary>Game hours until a slain one returns to its lair (Q5: a cleared lair refills after 2 game days); 0 for never.</summary>
        public int RespawnHours { get; init; }

        /// <summary>Its kind's loot table (K4); 0 until loot is in play.</summary>
        public ushort LootTableId { get; init; }

        /// <summary>The swing's interval in whole ticks of physical time.</summary>
        public int SwingIntervalTicks => CombatRules.SecondsToTicks(SwingIntervalSeconds);

        /// <summary>True for a kind that runs when hurt.</summary>
        public bool Flees => FleeBelow > 0f;

        /// <summary>True for a kind that strikes from range (K8).</summary>
        public bool IsRanged => Range > 0f;

        /// <summary>True for a kind that returns after it is slain.</summary>
        public bool Respawns => RespawnHours > 0;

        /// <summary>Health it regains each tick at its lair: whole again over <see cref="HealHours"/> of the day.</summary>
        public float HealPerTick => HealHours > 0 ? (float)(MaxHealth / (float)(HealHours * DayClock.TicksPerHour)) : 0f;

        /// <summary>Ticks until a slain one returns.</summary>
        public uint RespawnTicks => DayClock.Hours(RespawnHours);

        public override string ToString() => $"{Name} ({(byte)Id})";
    }
}
