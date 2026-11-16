using Simpiens.Simulation.Combat;
using Simpiens.Simulation.World;

namespace Simpiens.Simulation.Creatures
{
    /// <summary>
    /// The ground a lair holds and what it costs a crawler to stand there (Epic 10 Part 3, K2.3; the epic's D16 and D18):
    /// pure functions of the map, the creature catalogue and the combat rule, so cognition reads them off the main thread
    /// as it reads a sector's hazard.
    /// <list type="bullet">
    /// <item><b>Reach</b> (D16): a disc of its kind's leash around its heart. A creature chases only quarry within its leash
    /// of the heart (D10), so outside the disc nobody is chased.</item>
    /// <item><b>Danger</b> (D18): a pack's expected harm a second, in a hazard's unit, so a known lair is judged against
    /// <c>Vitality.LoneHazardLimit</c> as a hazard is: its kind's smallest pack, each swinging at the combat rule's hit
    /// chance for its attack less the crawler's defense (D / (D + 10)), every swing interval.</item>
    /// </list>
    /// </summary>
    public static class LairReach
    {
        /// <summary>
        /// How far from its heart <paramref name="lair"/> holds: its kind's leash, or its boss's if longer (K10); 0 for a kind
        /// the catalogue lacks.
        /// </summary>
        public static float Of(in LairDefinition lair)
        {
            float reach = CreatureCatalog.Get(lair.Kind)?.Leash ?? 0f;
            if (!lair.HasBoss) return reach;
            float boss = CreatureCatalog.Get(lair.Boss)?.Leash ?? 0f;
            return boss > reach ? boss : reach;
        }

        /// <summary>True when <paramref name="position"/> lies within <paramref name="lair"/>'s reach.</summary>
        public static bool Holds(in LairDefinition lair, Vec2 position)
        {
            float reach = Of(lair);
            return reach > 0f && (position - lair.Position).SqrMagnitude <= reach * reach;
        }

        /// <summary>The first of <paramref name="map"/>'s lairs, in its order, whose reach holds <paramref name="position"/>; -1 for none.</summary>
        public static int LairAt(MapDefinition map, Vec2 position)
        {
            if (map == null) return -1;
            var lairs = map.Lairs;
            for (int i = 0; i < lairs.Count; i++)
            {
                if (Holds(lairs[i], position)) return i;
            }
            return -1;
        }

        /// <summary>
        /// A pack of <paramref name="kind"/>'s expected harm a second on a crawler of <paramref name="defense"/>: its smallest
        /// pack, each at the hit chance for its mitigated attack every swing interval. 0 for no kind.
        /// </summary>
        public static float Danger(CreatureDefinition kind, float defense)
        {
            if (kind == null || kind.SwingIntervalSeconds <= 0f) return 0f;
            var fighter = Combatant.ForCreature(kind, default, default, kind.MaxHealth);
            float perSwing = (float)(CombatRules.HitChance(fighter) * CombatRules.Mitigated(kind.Attack, defense));
            return (float)(kind.PackMin * perSwing / kind.SwingIntervalSeconds);
        }

        /// <summary>
        /// One of <paramref name="kind"/>'s expected harm a second on a crawler of <paramref name="defense"/> (K10: a boss's
        /// share of its lair's danger). 0 for no kind.
        /// </summary>
        public static float HarmPerSecond(CreatureDefinition kind, float defense)
        {
            if (kind == null || kind.SwingIntervalSeconds <= 0f) return 0f;
            var fighter = Combatant.ForCreature(kind, default, default, kind.MaxHealth);
            return (float)(CombatRules.HitChance(fighter) * CombatRules.Mitigated(kind.Attack, defense) / kind.SwingIntervalSeconds);
        }

        /// <summary>
        /// The danger of <paramref name="lair"/>'s pack on a crawler of <paramref name="defense"/>, its boss's harm with it while
        /// <paramref name="bossAlive"/> (K10, D63: a slain Matriarch is gone for a week, and her warren is judged as her brood).
        /// </summary>
        public static float Danger(in LairDefinition lair, float defense, bool bossAlive)
        {
            float pack = Danger(CreatureCatalog.Get(lair.Kind), defense);
            return lair.HasBoss && bossAlive ? (float)(pack + HarmPerSecond(CreatureCatalog.Get(lair.Boss), defense)) : pack;
        }
    }
}
