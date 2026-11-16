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
    /// <item>a ranged fighter strikes a foe within its <see cref="Combatant.Range"/> too (Epic 10 Part 3, K8.1, D45): its
    /// chance falls past half its range, to <see cref="RangeFalloff"/> of the base at full range, and its weapon wears per
    /// shot, hit or miss. The clear line a shot needs is the caller's, which holds the map.</item>
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

        /// <summary>At full range a shot's chance is this share of its chance at half range or nearer (Epic 10 Part 3, K8.1, D45).</summary>
        public const float RangeFalloff = 0.5f;

        /// <summary>What a shooter keeps past its foe's reach, at the least (Epic 10 Part 3, K8.2b, D46; game units).</summary>
        public const float KeepMargin = 1.5f;

        /// <summary>A shooter closes to this share of its range, so a step of its foe's doesn't put it out of range (K8.2b).</summary>
        public const float CloseShare = 0.8f;

        /// <summary>
        /// The gap a shooter with <paramref name="range"/> keeps from a foe of <paramref name="foeReach"/> (K8.2b, D46): the foe's
        /// reach and <see cref="KeepMargin"/>, at least half the range and at most <see cref="CloseShare"/> of it. A crawler's bow
        /// and a spinner's spit alike.
        /// </summary>
        public static float KeepAway(float range, float foeReach) => SimMath.Min((float)(range * CloseShare), SimMath.Max((float)(range * 0.5f), (float)(foeReach + KeepMargin)));

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

        /// <summary>True when <paramref name="attacker"/> has a ranged attack and <paramref name="target"/>'s body is within its range (K8.1; compared squared).</summary>
        public static bool InRange(in Combatant attacker, in Combatant target)
        {
            if (!attacker.IsRanged) return false;
            float span = (float)((float)(attacker.BodyRadius + target.BodyRadius) + attacker.Range);
            return (target.Position - attacker.Position).SqrMagnitude <= (float)(span * span);
        }

        /// <summary>True when <paramref name="attacker"/> can strike <paramref name="target"/> from where they stand: in reach, or in range (a shot, which also needs a clear line).</summary>
        public static bool InStrikingDistance(in Combatant attacker, in Combatant target) => InReach(attacker, target) || InRange(attacker, target);

        /// <summary>True when a fighter whose next swing is due at <paramref name="readyAtTick"/> may swing at <paramref name="tick"/>.</summary>
        public static bool CanSwing(long tick, long readyAtTick) => tick >= readyAtTick;

        /// <summary>When a fighter that swings at <paramref name="tick"/> may swing again.</summary>
        public static long NextSwingAt(long tick, in Combatant attacker) => tick + SimMath.Max(1, attacker.SwingInterval);

        /// <summary>
        /// True when <paramref name="attacker"/> may swing at <paramref name="target"/> now: both on their feet, the target in reach
        /// or, for a ranged fighter, in range (K8.1), and the attacker's swing due.
        /// </summary>
        public static bool CanStrike(in Combatant attacker, in Combatant target, long tick, long readyAtTick) =>
            attacker.Standing && target.Standing && attacker.Id != target.Id && CanSwing(tick, readyAtTick) && InStrikingDistance(attacker, target);

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

        /// <summary>
        /// The chance a ranged fighter's shot at <paramref name="target"/> lands (K8.1, D45): <see cref="HitChance(in Combatant)"/>
        /// out to half its range, falling evenly past it to <see cref="RangeFalloff"/> of that at full range (and no lower beyond).
        /// A fighter with no range has its swing's chance.
        /// </summary>
        public static float ShotChance(in Combatant attacker, in Combatant target)
        {
            float chance = HitChance(attacker);
            if (!attacker.IsRanged) return chance;
            float half = (float)(attacker.Range * 0.5f);
            float past = (float)(Gap(attacker, target) - half);
            if (past <= 0f) return chance;
            float share = SimMath.Min(1f, (float)(past / half));
            return SimMath.Max(MinHitChance, (float)(chance * (float)(1f - (float)((1f - RangeFalloff) * share))));
        }

        /// <summary>One swing's outcome, drawing its roll from <paramref name="random"/>. Whether the swing may happen is <see cref="CanStrike"/>'s.</summary>
        public static SwingOutcome Swing(ref SimRandom random, in Combatant attacker, in Combatant target)
        {
            float chance = attacker.IsRanged ? ShotChance(attacker, target) : HitChance(attacker); // melee's chance as it was
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
        /// One swing over several foes (Epic 10 Part 3, K10.3a; D3: the Gatekeeper's slam): a roll a foe from the attacker's
        /// stream at <paramref name="tick"/>, in the order <paramref name="foes"/> are given (the caller gives them in id order),
        /// so the first foe's outcome is what a lone swing at it would be. Writes an outcome a foe, as many as both spans hold,
        /// and returns how many. Which foes are struck (those in reach) is the caller's.
        /// </summary>
        public static int Slam(ulong seed, long tick, in Combatant attacker, System.ReadOnlySpan<Combatant> foes, System.Span<SwingOutcome> outcomes)
        {
            var random = RandomFor(seed, attacker.Id, tick);
            int count = foes.Length < outcomes.Length ? foes.Length : outcomes.Length;
            for (int i = 0; i < count; i++) outcomes[i] = Swing(ref random, attacker, foes[i]);
            return count;
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

        /// <summary>
        /// Wears what the attacker wields for a shot loosed, hit or miss (Q16, K8.1: a ranged weapon wears per shot): one unit of
        /// every piece it wears by <see cref="WearRule.PerShot"/>; nothing for a melee weapon. Returns true when a piece broke and
        /// is gone. Main thread only.
        /// </summary>
        public static bool WearOnShot(AgentInventory inventory) => WearAll(inventory, WearRule.PerShot);

        private static bool WearAll(AgentInventory inventory, WearRule rule)
        {
            if (inventory == null) return false;
            System.Span<Simpiens.Simulation.Economy.ItemType> broke = stackalloc Simpiens.Simulation.Economy.ItemType[EquipmentSlots.Capacity];
            inventory.WearBy(rule, broke, out int brokeCount);
            return brokeCount > 0;
        }
    }
}
