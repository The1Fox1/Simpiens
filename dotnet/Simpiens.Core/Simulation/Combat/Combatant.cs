using Simpiens.Cognition;
using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Combat
{
    /// <summary>
    /// A fighter as the combat rule reads it (Epic 10 Part 3, K0.3, seam A1): who it is, where its body is, its health, and
    /// what it brings to a fight. A crawler's comes from its state and its equipment (<see cref="ForCrawler"/>); a creature's
    /// comes from its kind and its record (K0.4, K2). <see cref="CombatRules"/> reads nothing else, so there is one rule over
    /// both kinds of fighter, one validator and one place to tune.
    /// <para>
    /// A value read on the main thread when a swing is due, used and dropped: never stored, never saved. Range and
    /// proficiency are in it from the start, so ranged combat (K8) and skills (K9) fill fields rather than reshape it.
    /// </para>
    /// </summary>
    public readonly struct Combatant
    {
        public readonly EntityId Id;
        public readonly Vec2 Position;

        /// <summary>The body's radius: a blow's reach is measured across the gap between two bodies, not between their centres.</summary>
        public readonly float BodyRadius;

        public readonly float Health;

        /// <summary>False for a fighter that can neither swing nor be fought as a foe on its feet: a downed crawler, a dead creature.</summary>
        public readonly bool Standing;

        /// <summary>A landed blow's harm before the target's <see cref="Defense"/>. Unarmed, a crawler's is <see cref="CombatRules.UnarmedAttack"/>.</summary>
        public readonly float Attack;

        /// <summary>What cuts a blow: the share Defense / (Defense + <see cref="CombatRules.MitigationScale"/>), never all of it.</summary>
        public readonly float Defense;

        /// <summary>How far a blow reaches across the gap between two bodies, in world units (a tile is one).</summary>
        public readonly float Reach;

        /// <summary>Ticks between two swings: physical time, as walking is (15 ticks are 1.5 seconds).</summary>
        public readonly int SwingInterval;

        /// <summary>How far a shot carries, in world units; 0 for a fighter with no ranged attack, which until K8 is everyone.</summary>
        public readonly float Range;

        /// <summary>What practice adds to the hit chance: 0 until skills fill it (K9), negative for the clumsy.</summary>
        public readonly float Proficiency;

        public Combatant(EntityId id, Vec2 position, float bodyRadius, float health, bool standing, float attack, float defense, float reach,
            int swingInterval, float range = 0f, float proficiency = 0f)
        {
            Id = id;
            Position = position;
            BodyRadius = bodyRadius;
            Health = health;
            Standing = standing;
            Attack = attack;
            Defense = defense;
            Reach = reach;
            SwingInterval = swingInterval;
            Range = range;
            Proficiency = proficiency;
        }

        /// <summary>True for a fighter with a ranged attack (K8).</summary>
        public bool IsRanged => Range > 0f;

        /// <summary>
        /// A crawler as a fighter: its health and whether it is on its feet from its state, and what it brings from its
        /// equipment through <see cref="CrawlerStats"/>. Bare-handed, that is the base of each stat: <see cref="CombatRules.UnarmedAttack"/>,
        /// no defense, <see cref="CombatRules.UnarmedReach"/> and a swing every <see cref="CombatRules.UnarmedIntervalSeconds"/>; a
        /// weapon or armour adds to or multiplies the base, as a pickaxe multiplies harvest speed.
        /// </summary>
        public static Combatant ForCrawler(AgentState state, float bodyRadius)
        {
            var worn = state.Inventory;
            return new Combatant(state.AgentId, state.Position, bodyRadius, state.Health, standing: !state.IsDowned && state.Health > 0f,
                attack: CrawlerStats.Get(worn, StatKind.Attack, CombatRules.UnarmedAttack),
                defense: CrawlerStats.Get(worn, StatKind.Defense),
                reach: CrawlerStats.Get(worn, StatKind.Reach, CombatRules.UnarmedReach),
                swingInterval: CombatRules.SecondsToTicks(CrawlerStats.Get(worn, StatKind.AttackInterval, CombatRules.UnarmedIntervalSeconds)),
                range: CrawlerStats.Get(worn, StatKind.Range),
                // Skills fill it at K9
                proficiency: 0f);
        }

        public override string ToString() =>
            $"{Id} at {Position}: health {Health:0.#}{(Standing ? string.Empty : " (down)")}, attack {Attack:0.##}, defense {Defense:0.##}, reach {Reach:0.##}, every {SwingInterval} ticks{(IsRanged ? $", range {Range:0.#}" : string.Empty)}";
    }
}
