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
        public static Combatant ForCrawler(AgentState state, float bodyRadius) =>
            ForCrawler(state.AgentId, state.Position, bodyRadius, state.Health, standing: !state.IsDowned && state.Health > 0f, state.Skills, state.Inventory, state.Traits);

        /// <summary>
        /// As <see cref="ForCrawler(AgentState, float)"/>, from what a crawler is made of rather than its state: its skills,
        /// what it wears and its techniques, which cognition's copy holds too (Epic 10 Part 3, K9.8b: a lair is judged by the
        /// fighter the fight would build).
        /// </summary>
        public static Combatant ForCrawler(EntityId id, Vec2 position, float bodyRadius, float health, bool standing,
            in Simpiens.Simulation.Skills.SkillSet skills, Simpiens.Cognition.Inventory.IAgentInventory worn, in Simpiens.Simulation.Skills.CrawlerTraits traits)
        {
            // Its skill at the fight it fights (Epic 10 Part 3, K9.3a, Q20): ranged with a weapon that has a range, melee
            // otherwise; the hit chance and a little damage. Unskilled, both are the numbers before skills
            float range = CrawlerStats.Get(worn, StatKind.Range);
            var work = Simpiens.Simulation.Skills.SkillWork.Fight(range > 0f);
            int level = Simpiens.Simulation.Skills.CrawlerSkills.Level(skills, worn, work);
            float attack = (float)(CrawlerStats.Get(worn, StatKind.Attack, CombatRules.UnarmedAttack) * Simpiens.Simulation.Skills.SkillRules.DamageFactor(level));
            float defense = CrawlerStats.Get(worn, StatKind.Defense);
            float interval = CrawlerStats.Get(worn, StatKind.AttackInterval, CombatRules.UnarmedIntervalSeconds);
            float proficiency = Simpiens.Simulation.Skills.SkillRules.HitBonus(level);

            // Its techniques at that fight (Epic 11, S4.3, D14), through the one door: the harm of a blow, and more of it below
            // half health; defense, never below none; the hit chance; how often it swings; how far it shoots. None held, the
            // numbers are untouched
            int attackPercent = Simpiens.Simulation.Skills.CrawlerTechniques.Get(traits, skills, work, Simpiens.Simulation.Skills.TechniqueStat.Attack);
            if (attackPercent != 0) attack = (float)(attack * Simpiens.Simulation.Skills.CrawlerTechniques.Factor(attackPercent));
            if (health < LastStandBelow)
            {
                int lastStand = Simpiens.Simulation.Skills.CrawlerTechniques.Get(traits, skills, work, Simpiens.Simulation.Skills.TechniqueStat.LastStand);
                if (lastStand != 0) attack = (float)(attack * Simpiens.Simulation.Skills.CrawlerTechniques.Factor(lastStand));
            }
            int guard = Simpiens.Simulation.Skills.CrawlerTechniques.Get(traits, skills, work, Simpiens.Simulation.Skills.TechniqueStat.Defense);
            if (guard != 0) defense = SimMath.Max(0f, (float)(defense + guard));
            int hit = Simpiens.Simulation.Skills.CrawlerTechniques.Get(traits, skills, work, Simpiens.Simulation.Skills.TechniqueStat.Hit);
            if (hit != 0) proficiency = (float)(proficiency + (float)(hit / 100f));
            int pace = Simpiens.Simulation.Skills.CrawlerTechniques.Get(traits, skills, work, Simpiens.Simulation.Skills.TechniqueStat.Pace);
            if (pace != 0) interval = (float)(interval / Simpiens.Simulation.Skills.CrawlerTechniques.Factor(pace));
            if (range > 0f)
            {
                int reach = Simpiens.Simulation.Skills.CrawlerTechniques.Get(traits, skills, work, Simpiens.Simulation.Skills.TechniqueStat.Range);
                if (reach != 0) range = (float)(range * Simpiens.Simulation.Skills.CrawlerTechniques.Factor(reach));
            }
            return new Combatant(id, position, bodyRadius, health, standing,
                attack: attack,
                defense: defense,
                reach: CrawlerStats.Get(worn, StatKind.Reach, CombatRules.UnarmedReach),
                swingInterval: CombatRules.SecondsToTicks(interval),
                range: range,
                proficiency: proficiency);
        }

        /// <summary>A last stand's harm counts below this health: half a crawler's (Epic 11, S4.3).</summary>
        public const float LastStandBelow = Simpiens.Cognition.Vitality.MaxHealth * 0.5f;

        /// <summary>
        /// A creature as a fighter (K0.4): what it brings from its kind, its health from its own record (K2). On its feet while
        /// it has health; practice is a crawler's alone.
        /// </summary>
        public static Combatant ForCreature(Simpiens.Simulation.Creatures.CreatureDefinition kind, EntityId id, Vec2 position, float health)
        {
            // Fury (Epic 10 Part 3, K10.3b, Q13): below its line a kind strikes harder and faster; it follows health, so no state
            if (kind.InFury(health))
                return new Combatant(id, position, kind.BodyRadius, health, standing: true, (float)(kind.Attack * kind.FuryAttack), kind.Defense, kind.Reach,
                    CombatRules.SecondsToTicks((float)(kind.SwingIntervalSeconds * kind.FuryInterval)), kind.Range);
            return new Combatant(id, position, kind.BodyRadius, health, standing: health > 0f, kind.Attack, kind.Defense, kind.Reach, kind.SwingIntervalTicks, kind.Range);
        }

        public override string ToString() =>
            $"{Id} at {Position}: health {Health:0.#}{(Standing ? string.Empty : " (down)")}, attack {Attack:0.##}, defense {Defense:0.##}, reach {Reach:0.##}, every {SwingInterval} ticks{(IsRanged ? $", range {Range:0.#}" : string.Empty)}";
    }
}
