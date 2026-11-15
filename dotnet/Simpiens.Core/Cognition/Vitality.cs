using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.World;

namespace Simpiens.Cognition
{
    /// <summary>
    /// A crawler's health and the danger of the floor (Epic 10 G2; producer decisions Q2, no combat, and Q3, no death).
    /// Hazard sectors (map data, <see cref="SectorHazard"/>) take health while a crawler stands in them; at 0 it is downed
    /// and limps to a bunk, where it heals; a medicinal herb heals at once. Who goes into danger is a matter of
    /// personality and condition. Times are per second of simulation (ticks are a tenth of one).
    /// </summary>
    public static class Vitality
    {
        public const float MaxHealth = 100f;

        // Healing's rates, awake and in a bunk, are the session's pace (DayTuning, Epic 10 Part 2, T3.2a)

        /// <summary>A crawler under this eats a herb it carries.</summary>
        public const float TreatWoundsBelow = 60f;

        /// <summary>A crawler in a hazard sector under this gives up and heads for a bunk.</summary>
        public const float RetreatBelow = 40f;

        /// <summary>A downed crawler gets up again at this health.</summary>
        public const float RecoveredAt = 60f;

        /// <summary>A downed crawler limps at this share of its speed.</summary>
        public const float DownedSpeedFactor = 0.4f;

        /// <summary>A rescuer carries the fallen at half its pace (Epic 10 Part 3, K5.2).</summary>
        public const float CarrySpeedFactor = 0.5f;

        /// <summary>The share of its pace <paramref name="rescuer"/> carries at (Epic 10 Part 3, K9.3d): half, and more by its skill at tending the fallen, four fifths at the most.</summary>
        public static float CarrySpeed(AgentState rescuer) => rescuer == null ? CarrySpeedFactor
            : (float)(CarrySpeedFactor + Simpiens.Simulation.Skills.SkillRules.CarryBonus(rescuer.Skills.Level(Simpiens.Simulation.Skills.SkillTable.ForTending())));

        /// <summary>Only a crawler at least this bold (personality) and this healthy goes into a hazard sector for its salvage.</summary>
        public const float VentureRiskTolerance = 0.3f;
        public const int VentureRiskTolerancePercent = 30;
        public const float VentureHealth = 80f;

        /// <summary>True when this crawler would go into danger for salvage now.</summary>
        public static bool WouldVenture(in AgentContext context) =>
            context.Personality.RiskTolerancePercent >= VentureRiskTolerancePercent && context.Health >= VentureHealth && !context.IsDowned;

        /// <summary>
        /// The worst danger a crawler braves alone, in health a second (Epic 10 G5, D11): the Sump's 0.75 and the Glow
        /// Caves' 1, not the Drowned Gallery's 1.5, which only an expedition enters (a quarter of Part 1's figures since the
        /// 24-hour day, Epic 10 Part 2, T3.2a).
        /// </summary>
        public const float LoneHazardLimit = 1f;

        /// <summary>True when this crawler, alone, would go where <paramref name="hazard"/> is: anywhere safe, and danger within the lone limit, through the gear it wears, if it would venture.</summary>
        public static bool WouldEnter(in AgentContext context, in SectorHazard hazard) => WouldEnter(WouldVenture(context), hazard, context.Inventory);

        /// <summary>The same, for a crawler already known to venture or not, wearing <paramref name="worn"/> (none for null).</summary>
        public static bool WouldEnter(bool venture, in SectorHazard hazard, Simpiens.Cognition.Inventory.IAgentInventory worn = null) =>
            !hazard.IsHazardous || (venture && LoneDanger(hazard, worn) <= LoneHazardLimit);

        /// <summary>
        /// What a hazard leaves a crawler to brave alone (Epic 10 Part 2, W3, D51): its rate through gear made against its
        /// kind (a respirator leaves the Gallery's 1.5 at 0.375, within the lone limit). Hazard gear, against any hazard,
        /// softens the harm but not the judgment, so the Gallery stays an expedition's unless the player sells respirators.
        /// </summary>
        public static float LoneDanger(in SectorHazard hazard, Simpiens.Cognition.Inventory.IAgentInventory worn) =>
            LoneDanger(hazard, Simpiens.Simulation.Items.CrawlerStats.KindResistance(worn, hazard.Kind));

        /// <summary>The same, through a known resistance to the hazard's kind.</summary>
        public static float LoneDanger(in SectorHazard hazard, float kindResistance) =>
            kindResistance > 0f ? (float)(hazard.DamagePerSecond * (float)(1f - kindResistance)) : hazard.DamagePerSecond;

        /// <summary>
        /// The worst danger a charted lair puts a crawler in at <paramref name="position"/> (Epic 10 Part 3, K2.3, D18): the
        /// expected harm a second of the pack whose reach holds it, through the defense <paramref name="worn"/> gives; 0 outside
        /// every charted lair's reach. A lair nobody has charted is no danger to anyone's judgment, only to their health.
        /// </summary>
        public static float LairDangerAt(Simpiens.Simulation.Spatial.SharedWorldSnapshot snapshot, Vec2 position, Simpiens.Cognition.Inventory.IAgentInventory worn)
        {
            var map = snapshot?.Map;
            if (map == null || snapshot.ChartedLairMask == 0UL) return 0f;
            float worst = 0f, defense = -1f;
            var lairs = map.Lairs;
            for (int i = 0; i < lairs.Count; i++)
            {
                if (!snapshot.IsLairCharted(i) || !Simpiens.Simulation.Creatures.LairReach.Holds(lairs[i], position)) continue;
                if (defense < 0f) defense = Simpiens.Simulation.Items.CrawlerStats.Get(worn, Simpiens.Simulation.Items.StatKind.Defense);
                float danger = Simpiens.Simulation.Creatures.LairReach.Danger(lairs[i], defense);
                if (danger > worst) worst = danger;
            }
            return worst;
        }

        /// <summary>
        /// True when this crawler, alone, would go to <paramref name="position"/> (Epic 10 Part 3, K2.3): its sector's hazard
        /// judged as <see cref="WouldEnter(in AgentContext, in SectorHazard)"/>, and a charted lair's reach judged the same
        /// way, by its pack's danger against <see cref="LoneHazardLimit"/> (Q6: a known lair is judged as a hazard is).
        /// </summary>
        public static bool WouldEnterAt(in AgentContext context, Vec2 position)
        {
            bool venture = WouldVenture(context);
            if (!WouldEnter(venture, HazardAt(context.Snapshot?.Map, position), context.Inventory)) return false;
            float lair = LairDangerFor(context, position);
            return lair <= 0f || (venture && lair <= LoneHazardLimit);
        }

        /// <summary>
        /// As <see cref="LairDangerAt"/>, for this crawler: a charted lair it braves is no danger to it (Epic 10 Part 3, K3.3,
        /// D21: the bold and armed brave a lair whose pack they expect to slay, <see cref="BravesLair"/>).
        /// </summary>
        public static float LairDangerFor(in AgentContext context, Vec2 position)
        {
            var snapshot = context.Snapshot;
            var map = snapshot?.Map;
            if (map == null || snapshot.ChartedLairMask == 0UL) return 0f;
            float worst = 0f, defense = -1f;
            var lairs = map.Lairs;
            for (int i = 0; i < lairs.Count; i++)
            {
                if (!snapshot.IsLairCharted(i) || !Simpiens.Simulation.Creatures.LairReach.Holds(lairs[i], position)) continue;
                if (BravesLair(lairs[i], context.Inventory, context.Health, context.Personality.RiskTolerancePercent, context.IsDowned)) continue;
                if (defense < 0f) defense = Simpiens.Simulation.Items.CrawlerStats.Get(context.Inventory, Simpiens.Simulation.Items.StatKind.Defense);
                float danger = Simpiens.Simulation.Creatures.LairReach.Danger(lairs[i], defense);
                if (danger > worst) worst = danger;
            }
            return worst;
        }

        /// <summary>
        /// The harm a crawler wearing <paramref name="worn"/> expects to take slaying <paramref name="lair"/>'s smallest pack
        /// one by one (Epic 10 Part 3, K3.3, D21): each creature's harm a second through its defense, times what its own blows
        /// take to slay one (the combat rule's hit chance for its attack less the creature's defense, every swing), over the
        /// pack still standing: n, n - 1, ... 1 of them. Infinite for blows that can't hurt.
        /// </summary>
        public static float HarmToClear(in Simpiens.Simulation.World.LairDefinition lair, Simpiens.Cognition.Inventory.IAgentInventory worn)
        {
            var kind = Simpiens.Simulation.Creatures.CreatureCatalog.Get(lair.Kind);
            if (kind == null || kind.PackMin == 0) return 0f;
            float attack = Simpiens.Simulation.Items.CrawlerStats.Get(worn, Simpiens.Simulation.Items.StatKind.Attack, Simpiens.Simulation.Combat.CombatRules.UnarmedAttack);
            float interval = Simpiens.Simulation.Items.CrawlerStats.Get(worn, Simpiens.Simulation.Items.StatKind.AttackInterval, Simpiens.Simulation.Combat.CombatRules.UnarmedIntervalSeconds);
            float perSecond = (float)(Simpiens.Simulation.Combat.CombatRules.BaseHitChance * Simpiens.Simulation.Combat.CombatRules.Mitigated(attack, kind.Defense) / SimMath.Max(0.1f, interval));
            if (perSecond <= 0f) return float.MaxValue;
            float defense = Simpiens.Simulation.Items.CrawlerStats.Get(worn, Simpiens.Simulation.Items.StatKind.Defense);
            float eachHarm = Simpiens.Simulation.Creatures.LairReach.Danger(kind, defense) / kind.PackMin;
            float eachTime = kind.MaxHealth / perSecond;
            int n = kind.PackMin;
            return (float)(eachHarm * eachTime * (n * (n + 1) / 2f));
        }

        /// <summary>
        /// True when a crawler braves <paramref name="lair"/> alone (Epic 10 Part 3, K3.3, D21; Q6: a known lair is avoided
        /// "past what it would brave alone"): bold, armed, on its feet, and expecting to slay its smallest pack before its health
        /// falls to the retreat line.
        /// </summary>
        public static bool BravesLair(in Simpiens.Simulation.World.LairDefinition lair, Simpiens.Cognition.Inventory.IAgentInventory worn, float health, int riskTolerancePercent, bool downed) =>
            !downed && riskTolerancePercent >= VentureRiskTolerancePercent && IsArmed(worn) && HarmToClear(lair, worn) <= health - RetreatBelow;

        /// <summary>The same, for a crawler already known to venture or not, wearing <paramref name="worn"/>.</summary>
        public static bool WouldEnterAt(bool venture, Simpiens.Simulation.Spatial.SharedWorldSnapshot snapshot, Vec2 position, Simpiens.Cognition.Inventory.IAgentInventory worn)
        {
            if (!WouldEnter(venture, HazardAt(snapshot?.Map, position), worn)) return false;
            float lair = LairDangerAt(snapshot, position, worn);
            return lair <= 0f || (venture && lair <= LoneHazardLimit);
        }

        /// <summary>
        /// A crawler struck by a creature within this many seconds flees (Epic 10 Part 3, K2.3, D19), and runs on until none has
        /// struck it for as long. Physical time, as blows and walking are.
        /// </summary>
        public const float FlightSeconds = 2f;

        /// <summary><see cref="FlightSeconds"/> in ticks.</summary>
        public static uint FlightTicks => (uint)Simpiens.Simulation.Combat.CombatRules.SecondsToTicks(FlightSeconds);

        /// <summary>A fighting crawler lets a creature go that runs this far from it (Epic 10 Part 3, K3.3).</summary>
        public const float FightPursuit = 3f;

        /// <summary>
        /// True when this crawler, struck by a creature, fights back instead of fleeing (Epic 10 Part 3, K3.3, Q6): bold (as
        /// one who would venture into danger), armed (what it wields strikes harder than a bare hand) and on its feet above
        /// the retreat line.
        /// </summary>
        public static bool FightsBack(in AgentContext context) =>
            !context.IsDowned && context.Health >= FightLine(context.Expedition)
            && ((context.Personality.RiskTolerancePercent >= VentureRiskTolerancePercent && IsArmed(context.Inventory))
                || context.Expedition.IsFighting); // under a clear's orders every member fights, bold or not (Epic 10 Part 3, K7.2a)

        /// <summary>
        /// The health under which a crawler gives a fight up: its own <see cref="RetreatBelow"/>, or, under a clear's orders at
        /// the fight, none (Epic 10 Part 3, K7.2b, D41): the party holds together and its rule turns it back, so a member may
        /// fall where its mates carry it out.
        /// </summary>
        public static float FightLine(in Simpiens.Cognition.Contracts.ExpeditionOrder order) => order.IsFighting ? 0f : RetreatBelow;

        /// <summary>True when what <paramref name="worn"/> holds strikes harder than a bare hand.</summary>
        public static bool IsArmed(Simpiens.Cognition.Inventory.IAgentInventory worn) =>
            worn != null && Simpiens.Simulation.Items.CrawlerStats.Get(worn, Simpiens.Simulation.Items.StatKind.Attack, Simpiens.Simulation.Combat.CombatRules.UnarmedAttack) > Simpiens.Simulation.Combat.CombatRules.UnarmedAttack;

        /// <summary>How far past a lair's reach a fleeing crawler runs before it counts itself safe.</summary>
        public const float FlightClearance = 1f;

        /// <summary>True when a creature's blow (hit or miss) fell on the crawler within <see cref="FlightSeconds"/> of <paramref name="tick"/>.</summary>
        public static bool StruckRecently(uint struckAtTick, uint tick) => struckAtTick != 0 && tick >= struckAtTick && tick - struckAtTick <= FlightTicks;

        /// <summary>True when <paramref name="position"/> lies more than <see cref="FlightClearance"/> past every lair's reach on <paramref name="map"/>, charted or not.</summary>
        public static bool ClearOfLairs(MapDefinition map, Vec2 position)
        {
            if (map == null) return true;
            var lairs = map.Lairs;
            for (int i = 0; i < lairs.Count; i++)
            {
                float clear = Simpiens.Simulation.Creatures.LairReach.Of(lairs[i]) + FlightClearance;
                if ((position - lairs[i].Position).SqrMagnitude <= clear * clear) return false;
            }
            return true;
        }

        /// <summary>
        /// Where a hurt crawler heads: the floor plan's nearest bunk, free or not (the plan is common knowledge; a crawler
        /// that knew no free bunk stayed downed in a hazard sector all day). False on a map without bunks.
        /// </summary>
        public static bool TryGetRefuge(in AgentContext context, out Vec2 refuge) => TryGetRefugeFrom(context.Snapshot?.Map, context.Position, out refuge);

        /// <summary>The bunk nearest <paramref name="from"/> (a rescuer's way out from where the fallen lies, Epic 10 Part 3, K5.3).</summary>
        public static bool TryGetRefugeFrom(MapDefinition map, Vec2 from, out Vec2 refuge)
        {
            refuge = default;
            if (map == null) return false;
            float bestSqr = float.MaxValue;
            bool found = false;
            for (int i = 0; i < map.Pois.Count; i++)
            {
                var poi = map.Pois[i];
                if (poi.Kind != PoiKind.Bunk) continue;
                float sqr = (poi.Position - from).SqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                refuge = poi.Position;
                found = true;
            }
            return found;
        }

        /// <summary>The danger where <paramref name="position"/> lies, or none.</summary>
        public static SectorHazard HazardAt(MapDefinition map, Vec2 position) => map != null ? map.FindSector(position).Hazard : default;
    }
}
