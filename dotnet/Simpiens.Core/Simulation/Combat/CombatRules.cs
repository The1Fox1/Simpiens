using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Combat
{
    /// <summary>What one swing did: whether it landed, at what chance, and the harm it does after the target's defense.</summary>
    public readonly struct SwingOutcome
    {
        public readonly bool Hit;

        /// <summary>The chance the swing had (for logs, the chronicle and tests).</summary>
        public readonly float Chance;

        /// <summary>The harm a landed swing does, the target's defense already taken off; 0 for a miss.</summary>
        public readonly float Damage;

        public SwingOutcome(bool hit, float chance, float damage)
        {
            Hit = hit;
            Chance = chance;
            Damage = hit ? damage : 0f;
        }

        public override string ToString() => Hit ? $"hit for {Damage:0.##} ({Chance:P0})" : $"miss ({Chance:P0})";
    }

    /// <summary>
    /// The combat rule (Epic 10 Part 3, K0.3; the producer's Q2: "melee first, then range. Seeded hit rolls"). One rule over
    /// both kinds of fighter, read through <see cref="Combatant"/> (seam A1):
    /// <list type="bullet">
    /// <item>a fighter on its feet swings at a foe within its <see cref="Combatant.Reach"/>, at most once every
    /// <see cref="Combatant.SwingInterval"/> ticks;</item>
    /// <item>one seeded roll decides hit or miss: the stream is <see cref="SimRandom"/>'s for (seed, attacker, tick,
    /// <see cref="SimRandomSalt.Combat"/>), so a swing lands or misses the same way on any thread and in any order of
    /// resolution;</item>
    /// <item>a hit does the attacker's <see cref="Combatant.Attack"/>, cut by the target's <see cref="Combatant.Defense"/> on a
    /// curve that never reaches zero;</item>
    /// <item>a landed hit wears a weapon that wears per use, one unit a hit.</item>
    /// </list>
    /// Combat is watched, so it runs in physical time: ticks and seconds, as walking does, never the day's minutes
    /// (docs/time-model.md). Pure functions with no state, so nothing here allocates or needs a clock: the pass that applies
    /// swings (K2.2) owns when each fighter may swing next.
    /// </summary>
    public static class CombatRules
    {
        /// <summary>Physical time: a tick is a tenth of a second (<see cref="Simpiens.Cognition.AgentState.FixedTickDeltaTime"/>).</summary>
        public const int TicksPerSecond = 10;

        /// <summary>A swing's chance to land before practice (Q2's "about 70%, until skills set it").</summary>
        public const float BaseHitChance = 0.7f;

        /// <summary>However clumsy the attacker, a swing can land; however practised, it can miss.</summary>
        public const float MinHitChance = 0.05f;
        public const float MaxHitChance = 0.95f;

        /// <summary>The defense that halves a blow: defense D stops D / (D + 10) of it (Q2).</summary>
        public const float MitigationScale = 10f;

        /// <summary>A bare-handed crawler's blow (Q2: "Unarmed, a crawler hits for 2").</summary>
        public const float UnarmedAttack = 2f;

        /// <summary>A bare-handed crawler's reach across the gap between two bodies: neighbours on adjacent tiles.</summary>
        public const float UnarmedReach = 0.5f;

        /// <summary>A bare-handed crawler's time between swings (Q2's "about 1.5 s").</summary>
        public const float UnarmedIntervalSeconds = 1.5f;

        /// <summary>Seconds of physical time as whole ticks, at least one: 1.5 s is 15 ticks.</summary>
        public static int SecondsToTicks(float seconds) => SimMath.Max(1, SimMath.RoundToInt((float)(seconds * TicksPerSecond)));

        /// <summary>The gap between two bodies: the distance between their centres less both radii, never below zero.</summary>
        public static float Gap(in Combatant a, in Combatant b) => SimMath.Max(0f, (float)(Vec2.Distance(a.Position, b.Position) - a.BodyRadius - b.BodyRadius));

        /// <summary>True when <paramref name="target"/>'s body is within <paramref name="attacker"/>'s reach (compared squared: no root).</summary>
        public static bool InReach(in Combatant attacker, in Combatant target)
        {
            float span = (float)((float)(attacker.BodyRadius + target.BodyRadius) + attacker.Reach);
            return (target.Position - attacker.Position).SqrMagnitude <= (float)(span * span);
        }

        /// <summary>True when a fighter whose next swing is due at <paramref name="readyAtTick"/> may swing at <paramref name="tick"/>.</summary>
        public static bool CanSwing(long tick, long readyAtTick) => tick >= readyAtTick;

        /// <summary>When a fighter that swings at <paramref name="tick"/> may swing again.</summary>
        public static long NextSwingAt(long tick, in Combatant attacker) => tick + SimMath.Max(1, attacker.SwingInterval);

        /// <summary>
        /// True when <paramref name="attacker"/> may swing at <paramref name="target"/> now: both on their feet, the target in reach,
        /// and the attacker's swing due.
        /// </summary>
        public static bool CanStrike(in Combatant attacker, in Combatant target, long tick, long readyAtTick) =>
            attacker.Standing && target.Standing && attacker.Id != target.Id && CanSwing(tick, readyAtTick) && InReach(attacker, target);

        /// <summary>The chance a swing by <paramref name="attacker"/> lands: the base plus its proficiency, kept within the bounds.</summary>
        public static float HitChance(in Combatant attacker) => SimMath.Clamp((float)(BaseHitChance + attacker.Proficiency), MinHitChance, MaxHitChance);

        /// <summary>
        /// The harm a blow of <paramref name="attack"/> does through <paramref name="defense"/>: attack × 10 / (10 + defense).
        /// Defense 10 halves it, 30 leaves a quarter, and no defense stops it all. Negative defense counts as none.
        /// </summary>
        public static float Mitigated(float attack, float defense)
        {
            if (attack <= 0f) return 0f;
            float guard = defense > 0f ? defense : 0f;
            return (float)(attack * (float)(MitigationScale / (float)(MitigationScale + guard)));
        }

        /// <summary>The random stream of <paramref name="attacker"/>'s swing at <paramref name="tick"/>. A swing that strikes several foes draws from it once a foe, in id order (the Gatekeeper's slam, K10).</summary>
        public static SimRandom RandomFor(ulong seed, EntityId attacker, long tick) => SimRandom.For(seed, attacker, tick, SimRandomSalt.Combat);

        /// <summary>One swing's outcome, drawing its roll from <paramref name="random"/>. Whether the swing may happen is <see cref="CanStrike"/>'s.</summary>
        public static SwingOutcome Swing(ref SimRandom random, in Combatant attacker, in Combatant target)
        {
            float chance = HitChance(attacker);
            bool hit = random.NextFloat() < chance;
            return new SwingOutcome(hit, chance, hit ? Mitigated(attacker.Attack, target.Defense) : 0f);
        }

        /// <summary>One swing's outcome, on its own stream: <paramref name="attacker"/>'s at <paramref name="tick"/>.</summary>
        public static SwingOutcome Swing(ulong seed, long tick, in Combatant attacker, in Combatant target)
        {
            var random = RandomFor(seed, attacker.Id, tick);
            return Swing(ref random, attacker, target);
        }

        /// <summary>
        /// Wears what the attacker wields for a swing that landed (Q10: weapons wear per hit; D7): one unit of every piece it
        /// wears by <see cref="WearRule.PerHit"/>, whichever slot it is in (Epic 10 Part 3, K1.3), and nothing for a miss, a
        /// bare hand or a piece that doesn't wear so. Returns true when a piece broke and is gone. Main thread only: it
        /// writes the inventory.
        /// </summary>
        public static bool WearOnHit(in SwingOutcome outcome, AgentInventory inventory) =>
            outcome.Hit && WearAll(inventory, WearRule.PerHit);

        /// <summary>
        /// Wears what the target wears against blows for a swing that landed on it (D7's armour, K1.3): one unit of every
        /// piece it wears by <see cref="WearRule.PerBlowTaken"/>; nothing for a miss. Returns true when a piece broke and is
        /// gone. Main thread only.
        /// </summary>
        public static bool WearOnBlowTaken(in SwingOutcome outcome, AgentInventory inventory) =>
            outcome.Hit && WearAll(inventory, WearRule.PerBlowTaken);

        private static bool WearAll(AgentInventory inventory, WearRule rule)
        {
            if (inventory == null) return false;
            System.Span<Simpiens.Simulation.Economy.ItemType> broke = stackalloc Simpiens.Simulation.Economy.ItemType[EquipmentSlots.Capacity];
            inventory.WearBy(rule, broke, out int brokeCount);
            return brokeCount > 0;
        }
    }
}
