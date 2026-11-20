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

        /// <summary>
        /// The epithet of one who slays its kind (Epic 11, S6.1, D27): "Beetlebane", earned at the kill that brings a crawler's
        /// creatures slain to the epithet's count, or by a boss slain. Null for a kind that gives none.
        /// </summary>
        public string SlayerEpithet { get; init; }

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

        // Boss traits (Epic 10 Part 3, K10, Q13; seam A6: a boss is a row with traits, and the rule reads the traits, never a
        // kind's name)

        /// <summary>A boss: one of its kind, drawn with a health bar and named by the chronicle.</summary>
        public bool IsBoss { get; init; }

        /// <summary>
        /// The level a fight with this kind demands (Epic 11, S1.3, Q8, D5): a crawler above it learns less from its blows and
        /// shots (<see cref="Simpiens.Simulation.Skills.SkillRules.ChallengePercent"/>). A rat 2, a beetle 7, a boss 9 or 10.
        /// </summary>
        public byte Challenge { get; init; } = 5;

        /// <summary>Its swing strikes every crawler on its feet in its reach, one roll a foe in id order (the combat rule's D3).</summary>
        public bool Slam { get; init; }

        /// <summary>The share of its health under which it fights in fury (<see cref="FuryAttack"/>, <see cref="FuryInterval"/>); 0 for never.</summary>
        public float FuryBelow { get; init; }

        /// <summary>What fury multiplies its attack by.</summary>
        public float FuryAttack { get; init; } = 1f;

        /// <summary>What fury multiplies the time between its swings by (under 1: faster).</summary>
        public float FuryInterval { get; init; } = 1f;

        /// <summary>The share of its health under which, chasing, it calls its lair's brood to its quarry; 0 for never.</summary>
        public float CallsBroodBelow { get; init; }

        /// <summary>
        /// Game hours its lair's reach must stand empty of crawlers before it is whole again at once (Q13: the Gatekeeper heals
        /// between attempts); 0 for none. A kind with it usually heals nothing by resting (<see cref="HealHours"/> 0).
        /// </summary>
        public int WholeAfterHours { get; init; }

        /// <summary>True for a kind that fights in fury below a line.</summary>
        public bool HasFury => FuryBelow > 0f;

        /// <summary>True when one of this kind with <paramref name="health"/> fights in fury.</summary>
        public bool InFury(float health) => FuryBelow > 0f && health > 0f && health < (float)(FuryBelow * MaxHealth);

        /// <summary>True when one of this kind with <paramref name="health"/> calls its brood (if it chases).</summary>
        public bool CallsBrood(float health) => CallsBroodBelow > 0f && health > 0f && health < (float)(CallsBroodBelow * MaxHealth);

        /// <summary><see cref="WholeAfterHours"/> in ticks; 0 for none.</summary>
        public uint WholeAfterTicks => WholeAfterHours > 0 ? DayClock.Hours(WholeAfterHours) : 0u;

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
