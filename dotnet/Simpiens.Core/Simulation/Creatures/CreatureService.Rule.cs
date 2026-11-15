using System.Collections.Generic;
using Simpiens.Diagnostics;
using ZLogger;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.Combat;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Simulation.Creatures
{
    /// <summary>
    /// The creature's rule (Epic 10 Part 3, K2.2; Q5; the epic's D9–D15): a short state machine on the main thread, not a
    /// planner. A creature rests at its place and heals; rested, it roams near its lair; it chases a crawler it sees within its
    /// leash and strikes once in reach; hurt past its kind's line it flees home; its quarry lost, it goes home. Movement is
    /// physical time (its speed a second, rests in seconds); healing is the day's (its kind's hours).
    /// </summary>
    public sealed partial class CreatureService
    {
        /// <summary>The waypoints a creature keeps of a path: past them it plans again.</summary>
        public const int MaxPath = 64;

        /// <summary>Seconds a creature rests at its place before it roams: from this to twice it, drawn (D15).</summary>
        public const float RestSeconds = 20f;

        /// <summary>How far from its lair's heart it roams: this share of its leash (D15).</summary>
        public const float RoamShareOfLeash = 0.5f;

        /// <summary>The nearest a roam takes it from the heart.</summary>
        public const float RoamMinimum = 0.5f;

        /// <summary>How near its goal counts as there.</summary>
        public const float Arrived = 0.25f;

        /// <summary>Its goal moved this far from where its path ends: it plans again (a quarry on the move, D11).</summary>
        public const float ReplanDistance = 1f;


        private enum Walk : byte { There, Walking, Stuck }

        private struct Swing
        {
            public CreatureState Attacker;
            public WorldEntity Target;
            public SwingOutcome Outcome;
        }

        // A crawler's swing at a creature (K3.3): asked for by its fight, decided in the same pass as the creatures' (D6)
        private struct CrawlerSwing
        {
            public WorldEntity Crawler;
            public EntityId Target;
            public SwingOutcome Outcome;
            public CreatureState Creature;
            public bool Ranged; // a shot: practice in the ranged skill, hit or miss (K9.2)
        }

        private readonly List<CrawlerSwing> _asked = new List<CrawlerSwing>(8);
        private readonly List<ShotRecord> _shots = new List<ShotRecord>(8);

        /// <summary>
        /// The shots loosed in the last creatures' pass, crawlers' and creatures' alike, in the order decided (Epic 10 Part 3,
        /// K8.4a): what the views draw flying between ticks. Unsaved, as a hurt is: presentation's feed, rebuilt each pass.
        /// </summary>
        public IReadOnlyList<ShotRecord> ShotsThisTick => _shots;
        private readonly List<CrawlerSwing> _blows = new List<CrawlerSwing>(8);

        /// <summary>Creatures slain by crawlers since the session started (K3.3; unsaved, for the day's checks).</summary>
        public int Kills { get; private set; }

        /// <summary>
        /// A crawler in a fight asks to swing at <paramref name="creature"/> this tick (K3.3, from its intent, before the
        /// creatures' phase); the phase decides it with every creature's swing, from where all stand, before any harm lands.
        /// </summary>
        public void AskSwing(WorldEntity crawler, EntityId creature)
        {
            if (crawler?.Agent == null || creature == default) return;
            for (int i = 0; i < _asked.Count; i++) if (_asked[i].Crawler == crawler) return; // one a tick
            _asked.Add(new CrawlerSwing { Crawler = crawler, Target = creature });
        }

        private readonly GridAStarSearcher _searcher = new GridAStarSearcher();
        private readonly PathResponse _path = NewPathBuffer();
        private readonly List<Swing> _swings = new List<Swing>(16);

        private static PathResponse NewPathBuffer()
        {
            var response = new PathResponse();
            response.Initialize(200, null);
            return response;
        }

        /// <summary>
        /// D12: every creature in id order decides and moves; then every due swing is decided from where all stand after
        /// moving (the combat rule's D6: the rolls are keyed by attacker and tick); then all the harm lands. A creature swings
        /// at most once a tick.
        /// </summary>
        private void Rule(ICreatureWorld world, uint tick)
        {
            _shots.Clear(); // this pass's shots only, though no creature is left to fight (K8.4a)
            if (world == null || _creatures.Count == 0)
            {
                _asked.Clear(); // nothing to swing at: an asked swing doesn't wait for the next creature
                return;
            }
            var snapshot = world.Snapshot;
            var entities = world.Entities;
            WholeAgain(entities, tick);
            for (int i = 0; i < _creatures.Count; i++) Step(_creatures[i], snapshot, entities, tick);

            _swings.Clear();
            for (int i = 0; i < _creatures.Count; i++) DecideSwing(_creatures[i], snapshot, entities, tick);
            DecideCrawlerSwings(snapshot, tick);
            for (int i = 0; i < _swings.Count; i++) world.Strike(_swings[i].Target, _swings[i].Attacker.Id, _swings[i].Outcome, tick);
            for (int i = 0; i < _blows.Count; i++) Land(world, _blows[i], tick);

            // Who knows of a lair now (K2.3, D17): a crawler struck by one of its creatures, or one who sees one
            for (int i = 0; i < _swings.Count; i++) Chart(_swings[i].Attacker.LairIndex, _swings[i].Target.Id, struck: true);
            Sightings(snapshot, entities);
            Landmarks(snapshot);
        }

        // A lair that guards the descent gate is charted with the sector it stands in (Epic 10 Part 3, K10.1b): the gate and its
        // keeper are the room's landmark, which a scout's report shows (D17 keeps every other lair to sight or a blow)
        private void Landmarks(SharedWorldSnapshot snapshot)
        {
            if (snapshot == null) return;
            for (int l = 0; l < _lairs.Count && l < 64; l++)
            {
                var lair = _lairs[l].Definition;
                if (!lair.GuardsGate || IsCharted(l) || !snapshot.IsCharted(lair.Position)) continue;
                _charted |= 1UL << l;
                SimLog.Simulation.ZLogInformation($"{lair.Key} is charted with its sector: the gate's keeper is the room's landmark");
            }
        }

        // The crawlers' asked swings, in the order they were asked (the intents' order): each on its feet, at a creature alive
        // and in its reach, or in its range with a clear line (a shot, K8.1-K8.2a), its own timer ready; rolled on the attacker's
        // stream as a creature's are (D3)
        private void DecideCrawlerSwings(SharedWorldSnapshot snapshot, uint tick)
        {
            _blows.Clear();
            for (int i = 0; i < _asked.Count; i++)
            {
                var ask = _asked[i];
                var agent = ask.Crawler.Agent;
                if (agent == null || agent.IsDowned || agent.Health <= 0f || agent.HasLeft) continue;
                if (!TryGet(ask.Target, out var creature) || !creature.IsAlive || creature.Definition == null) continue;
                var me = Combatant.ForCrawler(agent, ask.Crawler.Radius);
                var them = Combatant.ForCreature(creature.Definition, creature.Id, creature.Position, creature.Health);
                if (!CombatRules.CanStrike(me, them, tick, agent.NextSwingAt)) continue;
                if (!CombatRules.InReach(me, them) && snapshot != null && !snapshot.HasLineOfSight(me.Position, them.Position)) continue; // a shot needs a clear line (K8.2a)
                ask.Outcome = CombatRules.Swing(_seed, tick, me, them);
                ask.Creature = creature;
                ask.Ranged = me.IsRanged;
                if (me.IsRanged) _shots.Add(new ShotRecord(me.Id, them.Id, me.Position, them.Position, ask.Outcome.Hit, creature: false));
                agent.NextSwingAt = CombatRules.NextSwingAt(tick, me);
                _blows.Add(ask);
            }
            _asked.Clear();
        }

        // A crawler's blow on a creature: its weapon wears by the landed hit (D7); the creature turns on its attacker if it
        // was minding its own business; at no health it is slain, out of the world, and returns to its place in its time (Q5)
        private void Land(ICreatureWorld world, in CrawlerSwing blow, uint tick)
        {
            var creature = blow.Creature;
            var agent = blow.Crawler.Agent;
            if (creature == null || !creature.IsAlive) return;
            // Practice (K9.2): a shot, hit or miss; a blow in reach that landed
            if (blow.Ranged) world.Practise(agent, Simpiens.Simulation.Skills.SkillWork.Fight(ranged: true), Simpiens.Simulation.Skills.SkillRules.ShotExperience, Simpiens.Simulation.Skills.PracticeSource.Work);
            else if (blow.Outcome.Hit) world.Practise(agent, Simpiens.Simulation.Skills.SkillWork.Fight(ranged: false), Simpiens.Simulation.Skills.SkillRules.HitExperience, Simpiens.Simulation.Skills.PracticeSource.Work);
            if (agent?.Inventory != null && CombatRules.WearOnShot(agent.Inventory)) // a ranged weapon wears per shot, hit or miss (K8.1)
                SimLog.Simulation.ZLogInformation($"{SimLog.Agent(agent.AgentId)}'s ranged weapon broke shooting at {CreatureCatalog.NameOf(creature.Kind)} {creature.Id}");
            if (!blow.Outcome.Hit)
            {
                Provoke(creature, blow.Crawler);
                return;
            }
            if (agent?.Inventory != null && CombatRules.WearOnHit(blow.Outcome, agent.Inventory))
                SimLog.Simulation.ZLogInformation($"{SimLog.Agent(agent.AgentId)}'s weapon broke on {CreatureCatalog.NameOf(creature.Kind)} {creature.Id}");
            float dealt = SimMath.Min(creature.Health, (float)blow.Outcome.Damage);
            creature.Health = SimMath.Max(0f, (float)(creature.Health - blow.Outcome.Damage));
            world.CreatureHit(blow.Crawler.Id, creature, dealt); // a hunter's share (K6.1)
            if (creature.Health > 0f)
            {
                Provoke(creature, blow.Crawler);
                return;
            }
            Kills++;
            Slay(world, creature.Id, tick);
            // Its loot (K4.2), rolled on (seed, the creature, the tick of its death) from its kind's table
            System.Span<Simpiens.Simulation.Economy.ItemInstance> drops = stackalloc Simpiens.Simulation.Economy.ItemInstance[CreatureLoot.MaxDrops];
            int dropped = creature.Definition != null ? LootRoller.Roll(CreatureLoot.Tables, creature.Definition.LootTableId, _seed, creature.Id, tick, drops) : 0;
            world.CreatureSlain(blow.Crawler.Id, creature, drops.Slice(0, dropped), tick);
        }

        // Struck while resting, roaming or going home, a creature turns on whoever struck it, within its leash (a fleeing one
        // runs on)
        private void Provoke(CreatureState creature, WorldEntity attacker)
        {
            if (creature.Mode == CreatureMode.Chase || creature.Mode == CreatureMode.Flee) return;
            var kind = creature.Definition;
            if (kind == null || creature.LairIndex < 0 || creature.LairIndex >= _lairs.Count) return;
            if (!IsQuarry(attacker, kind, _lairs[creature.LairIndex].Definition.Position)) return;
            creature.Mode = CreatureMode.Chase;
            creature.Target = attacker.Id;
            creature.ForgetPath();
        }

        /// <summary>How far a crawler sees a creature (its vision, as it sees everything else).</summary>
        public const float SightingDistance = Simpiens.Cognition.AgentRuntime.DefaultVisionRadius;

        // D17: an uncharted lair is charted when a crawler on its feet sees one of its creatures in a charted sector (nothing
        // inside the fog is seen), within its vision and with nothing walling it off; one sighting a creature, the first crawler
        private void Sightings(SharedWorldSnapshot snapshot, IReadOnlyList<WorldEntity> entities)
        {
            if (entities == null) return;
            for (int i = 0; i < _creatures.Count; i++)
            {
                var c = _creatures[i];
                if (!c.IsAlive || IsCharted(c.LairIndex)) continue;
                if (snapshot != null && !snapshot.IsCharted(c.Position)) continue;
                for (int e = 0; e < entities.Count; e++)
                {
                    var agent = entities[e].Agent;
                    if (agent == null || agent.IsDowned || agent.HasLeft) continue;
                    if ((entities[e].Position - c.Position).SqrMagnitude > SightingDistance * SightingDistance) continue;
                    if (snapshot != null && !snapshot.HasLineOfSight(entities[e].Position, c.Position)) continue;
                    Chart(c.LairIndex, entities[e].Id, struck: false);
                    break;
                }
            }
        }

        private void Chart(int lairIndex, EntityId by, bool struck)
        {
            if (lairIndex < 0 || lairIndex >= 64 || lairIndex >= _lairs.Count || IsCharted(lairIndex)) return;
            _charted |= 1UL << lairIndex;
            SimLog.Simulation.ZLogInformation($"{SimLog.Agent(by)} {(struck ? "was struck from" : "sighted")} {_lairs[lairIndex].Definition.Key}: the lair is charted");
        }

        private void Step(CreatureState c, SharedWorldSnapshot snapshot, IReadOnlyList<WorldEntity> entities, uint tick)
        {
            var kind = c.Definition;
            if (kind == null || !c.IsAlive || c.LairIndex < 0 || c.LairIndex >= _lairs.Count) return;
            var heart = _lairs[c.LairIndex].Definition.Position;
            var at = c.Position;

            // Hurt past its line, a kind that flees runs for its place and heeds nothing (D9)
            if (kind.Flees && Wounded(c, kind) && c.Mode != CreatureMode.Flee && c.Mode != CreatureMode.Rest)
            {
                c.Mode = CreatureMode.Flee;
                c.Target = default;
                c.ForgetPath();
            }

            switch (c.Mode)
            {
                case CreatureMode.Flee:
                    if (WalkTo(c, kind, c.Home, snapshot, tick) != Walk.Walking) Rest(c, tick);
                    break;

                case CreatureMode.Chase:
                    var quarry = FindCrawler(entities, c.Target);
                    if (!IsQuarry(quarry, kind, heart))
                    {
                        GoHome(c);
                        if (WalkTo(c, kind, c.Home, snapshot, tick) != Walk.Walking) Rest(c, tick);
                        break;
                    }
                    if (kind.CallsBrood(c.Health)) CallBrood(c, quarry); // a boss calls her brood to her quarry (K10.2b, Q13)
                    var me = Combatant.ForCreature(kind, c.Id, c.Position, c.Health);
                    var them = Combatant.ForCrawler(quarry.Agent, quarry.Radius);
                    if (me.IsRanged && Keep(c, kind, me, them, heart, snapshot)) break; // a spitter keeps its distance (K8.3)
                    if (CombatRules.Gap(me, them) <= kind.Reach) break; // in reach: it stands and strikes
                    if (WalkTo(c, kind, quarry.Position, snapshot, tick) == Walk.Stuck) GoHome(c);
                    break;

                case CreatureMode.Home:
                    if (Notice(c, kind, heart, snapshot, entities)) break;
                    if (WalkTo(c, kind, c.Home, snapshot, tick) != Walk.Walking) Rest(c, tick);
                    break;

                case CreatureMode.Roam:
                    if (Notice(c, kind, heart, snapshot, entities)) break;
                    if (WalkTo(c, kind, c.Goal, snapshot, tick) != Walk.Walking) Rest(c, tick);
                    break;

                default: // Rest: heal at its place; rested and not wounded, it roams
                    if (Vec2.Distance(c.Position, c.Home) <= Arrived && c.Health < kind.MaxHealth)
                        c.Health = SimMath.Min(kind.MaxHealth, (float)(c.Health + kind.HealPerTick));
                    if (Notice(c, kind, heart, snapshot, entities)) break;
                    if (tick >= c.RestUntil && !Wounded(c, kind) && TryRoamPoint(c, kind, heart, snapshot, tick, out var point))
                    {
                        c.Mode = CreatureMode.Roam;
                        c.Goal = point;
                        c.ForgetPath();
                    }
                    break;
            }

            // Every tick, moved or not, through its entity, so a view draws it between ticks from where it was (D11)
            if (c.Entity != null)
            {
                var moved = c.Position;
                c.Position = at;
                c.Entity.SetPosition(moved);
            }
        }

        // D10: a crawler on its feet, in its sight with nothing walling it off, within its leash of the lair's heart; the
        // nearest, the lower id on a tie. Wounded past its line, a fleeing kind notices nothing
        private bool Notice(CreatureState c, CreatureDefinition kind, Vec2 heart, SharedWorldSnapshot snapshot, IReadOnlyList<WorldEntity> entities)
        {
            if ((kind.Flees && Wounded(c, kind)) || entities == null) return false;
            WorldEntity best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (!IsQuarry(e, kind, heart)) continue;
                float d = Vec2.Distance(c.Position, e.Position);
                if (d > kind.Sight) continue;
                if (snapshot != null && !snapshot.HasLineOfSight(c.Position, e.Position)) continue;
                if (d < bestDistance || (d == bestDistance && e.Id.CompareTo(best.Id) < 0))
                {
                    best = e;
                    bestDistance = d;
                }
            }
            if (best == null) return false;
            c.Mode = CreatureMode.Chase;
            c.Target = best.Id;
            c.ForgetPath();
            return true;
        }

        // The call (Epic 10 Part 3, K10.2b, Q13: the Matriarch "calls her brood to her at half health"): while a boss chases under
        // its call's line, every one of its lair's brood that rests, roams or goes home, and isn't wounded past its own line,
        // turns on the boss's quarry within its leash. Stateless: it holds while she is under the line, so a load changes nothing.
        // The brood after her in id order answer this tick, those before her the next
        private void CallBrood(CreatureState boss, WorldEntity quarry)
        {
            if (boss.LairIndex < 0 || boss.LairIndex >= _lairs.Count) return;
            var heart = _lairs[boss.LairIndex].Definition.Position;
            for (int i = 0; i < _creatures.Count; i++)
            {
                var mate = _creatures[i];
                if (mate == boss || mate.LairIndex != boss.LairIndex || !mate.IsAlive) continue;
                if (mate.Mode == CreatureMode.Chase || mate.Mode == CreatureMode.Flee) continue;
                var kind = mate.Definition;
                if (kind == null || Wounded(mate, kind) || !IsQuarry(quarry, kind, heart)) continue;
                mate.Mode = CreatureMode.Chase;
                mate.Target = quarry.Id;
                mate.ForgetPath();
            }
        }

        // A crawler worth chasing: on its feet (until K5 a downed crawler limps out, as from a hazard), and within the leash
        private static bool IsQuarry(WorldEntity e, CreatureDefinition kind, Vec2 heart)
        {
            var agent = e?.Agent;
            return agent != null && !agent.IsDowned && agent.Health > 0f && !agent.HasLeft && Vec2.Distance(e.Position, heart) <= kind.Leash;
        }

        private void DecideSwing(CreatureState c, SharedWorldSnapshot snapshot, IReadOnlyList<WorldEntity> entities, uint tick)
        {
            if (c.Mode != CreatureMode.Chase || !c.IsAlive) return;
            var kind = c.Definition;
            var quarry = FindCrawler(entities, c.Target);
            if (kind == null || quarry?.Agent == null) return;
            var me = Combatant.ForCreature(kind, c.Id, c.Position, c.Health);
            var them = Combatant.ForCrawler(quarry.Agent, quarry.Radius);
            if (!CombatRules.CanStrike(me, them, tick, c.NextSwingAt)) return;
            if (kind.Slam && CombatRules.InReach(me, them))
            {
                SlamAll(c, me, entities, tick);
                return;
            }
            if (!CombatRules.InReach(me, them) && snapshot != null && !snapshot.HasLineOfSight(me.Position, them.Position)) return; // a spit needs a clear line (K8.2a)
            var outcome = CombatRules.Swing(_seed, tick, me, them);
            _swings.Add(new Swing { Attacker = c, Target = quarry, Outcome = outcome });
            if (me.IsRanged) _shots.Add(new ShotRecord(me.Id, them.Id, me.Position, them.Position, outcome.Hit, creature: true));
            c.NextSwingAt = CombatRules.NextSwingAt(tick, me);
            c.SwungAtTick = tick;
        }

        /// <summary>The most crawlers one slam strikes.</summary>
        public const int MaxSlammed = 8;

        private readonly WorldEntity[] _slammed = new WorldEntity[MaxSlammed];
        private readonly Combatant[] _slamFoes = new Combatant[MaxSlammed];
        private readonly SwingOutcome[] _slamOutcomes = new SwingOutcome[MaxSlammed];

        // The slam (Epic 10 Part 3, K10.3a, Q13: "a slam that strikes everyone in reach"): its quarry in reach and its swing due,
        // a slamming kind strikes every crawler on its feet in its reach, the nearest eight, one roll a foe in id order (D3), all
        // decided now with every other swing of the tick (D6); a miss on a foe is still a blow that fell on it
        private void SlamAll(CreatureState c, in Combatant me, IReadOnlyList<WorldEntity> entities, uint tick)
        {
            int count = 0;
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                var agent = e.Agent;
                if (agent == null || agent.IsDowned || agent.Health <= 0f || agent.HasLeft) continue;
                var foe = Combatant.ForCrawler(agent, e.Radius);
                if (!CombatRules.InReach(me, foe)) continue;
                if (count == MaxSlammed)
                {
                    // Full: keep the nearest eight (rare: eight crawlers in one boss's reach)
                    int far = 0;
                    for (int k = 1; k < count; k++) if (CombatRules.Gap(me, _slamFoes[k]) > CombatRules.Gap(me, _slamFoes[far])) far = k;
                    if (CombatRules.Gap(me, foe) >= CombatRules.Gap(me, _slamFoes[far])) continue;
                    _slammed[far] = e;
                    _slamFoes[far] = foe;
                    continue;
                }
                _slammed[count] = e;
                _slamFoes[count] = foe;
                count++;
            }
            // In id order: the rolls follow the foes' ids, never the registry's order
            for (int i = 1; i < count; i++)
            {
                var e = _slammed[i];
                var foe = _slamFoes[i];
                int j = i - 1;
                while (j >= 0 && _slammed[j].Id.CompareTo(e.Id) > 0)
                {
                    _slammed[j + 1] = _slammed[j];
                    _slamFoes[j + 1] = _slamFoes[j];
                    j--;
                }
                _slammed[j + 1] = e;
                _slamFoes[j + 1] = foe;
            }
            int rolled = CombatRules.Slam(_seed, tick, me, new System.ReadOnlySpan<Combatant>(_slamFoes, 0, count), _slamOutcomes);
            for (int i = 0; i < rolled; i++) _swings.Add(new Swing { Attacker = c, Target = _slammed[i], Outcome = _slamOutcomes[i] });
            for (int i = 0; i < count; i++) _slammed[i] = null;
            c.NextSwingAt = CombatRules.NextSwingAt(tick, me);
            c.SwungAtTick = tick;
        }

        // Whole between attempts (Epic 10 Part 3, K10.3c, Q13: the Gatekeeper "heals fully once its lair has been empty for a game
        // hour"): a kind with WholeAfterHours notes the last tick a living crawler stood in its lair's reach (downed ones too: a
        // fallen crawler there is the fight not over), and once its lair has stood empty that long, it is whole again at once
        private void WholeAgain(IReadOnlyList<WorldEntity> entities, uint tick)
        {
            for (int i = 0; i < _creatures.Count; i++)
            {
                var c = _creatures[i];
                var kind = c.Definition;
                if (kind == null || kind.WholeAfterHours <= 0 || !c.IsAlive || c.LairIndex < 0 || c.LairIndex >= _lairs.Count) continue;
                var lair = _lairs[c.LairIndex].Definition;
                bool visited = false;
                for (int e = 0; e < entities.Count && !visited; e++)
                {
                    var agent = entities[e].Agent;
                    visited = agent != null && !agent.HasLeft && (agent.Health > 0f || agent.IsDowned) && LairReach.Holds(lair, entities[e].Position);
                }
                if (visited)
                {
                    c.LastVisitAt = tick;
                    continue;
                }
                if (c.Health < kind.MaxHealth && c.LastVisitAt != 0 && tick - c.LastVisitAt >= kind.WholeAfterTicks)
                {
                    c.Health = kind.MaxHealth;
                    SimLog.Simulation.ZLogInformation($"{CreatureCatalog.NameOf(c.Kind)} {c.Id} stands whole again: its lair has been empty for {kind.WholeAfterHours} game hour(s)");
                }
            }
        }

        // A ranged creature's ground (K8.3, D48, as a shooting crawler's, K8.2b): inside its keep-away line it steps back from its
        // quarry, wall-checked and within its leash; in range with a clear line it holds and spits. True when it stays put or
        // stepped back; false to close as a biter does
        private static bool Keep(CreatureState c, CreatureDefinition kind, in Combatant me, in Combatant them, Vec2 heart, SharedWorldSnapshot snapshot)
        {
            float gap = CombatRules.Gap(me, them);
            bool clear = snapshot == null || snapshot.HasLineOfSight(me.Position, them.Position);
            if (gap < CombatRules.KeepAway(me.Range, them.Reach))
            {
                var away = (c.Position - them.Position).Normalized;
                if (away == Vec2.Zero) away = Vec2.Up;
                var candidate = c.Position + away * (float)(kind.Speed * SimulationClock.FixedTickDeltaTime);
                if ((candidate - heart).SqrMagnitude <= kind.Leash * kind.Leash
                    && (snapshot == null || (snapshot.IsStaticWalkable(candidate) && snapshot.IsSegmentStaticClear(c.Position, candidate))))
                {
                    c.Position = candidate;
                    c.ForgetPath();
                }
                return true; // cornered or at its leash's end, it stands and spits
            }
            return clear && gap <= (float)(me.Range * CombatRules.CloseShare);
        }

        // Toward the goal along its path (D11): planned through the A* when it has none, when the goal has moved more than
        // a cell from where the path ends, or when the path ran out short of it
        private Walk WalkTo(CreatureState c, CreatureDefinition kind, Vec2 goal, SharedWorldSnapshot snapshot, uint tick)
        {
            if (Vec2.Distance(c.Position, goal) <= Arrived) return Walk.There;
            bool stale = c.PlannedAt < 0 || c.PathCursor >= c.PathLength || Vec2.Distance(goal, c.PlannedFor) > ReplanDistance;
            if (stale && !Plan(c, goal, snapshot, tick)) return Walk.Stuck;

            float step = (float)(kind.Speed * SimulationClock.FixedTickDeltaTime);
            var position = c.Position;
            while (step > 0f && c.PathCursor < c.PathLength)
            {
                var waypoint = c.Path[c.PathCursor];
                float d = Vec2.Distance(position, waypoint);
                if (d <= step)
                {
                    position = waypoint;
                    step = (float)(step - d);
                    c.PathCursor++;
                    continue;
                }
                position = position + (waypoint - position) * (float)(step / d);
                step = 0f;
            }
            c.Position = position;
            return Vec2.Distance(position, goal) <= Arrived ? Walk.There : Walk.Walking;
        }

        private bool Plan(CreatureState c, Vec2 goal, SharedWorldSnapshot snapshot, uint tick)
        {
            c.ForgetPath();
            c.PlannedAt = tick;
            c.PlannedFor = goal;
            if (snapshot == null)
            {
                c.Path[0] = goal;
                c.PathLength = 1;
                return true;
            }
            _path.Clear();
            var request = new PathRequest { StartPosition = c.Position, TargetPosition = goal, RequesterId = c.Id };
            if (!_searcher.Search(request, snapshot, _path) || !_path.IsValid || _path.Length == 0) return false;
            int length = _path.Length < MaxPath ? _path.Length : MaxPath;
            for (int i = 0; i < length; i++) c.Path[i] = _path.Waypoints[i];
            c.PathLength = length;
            c.PathCursor = 0;
            return true;
        }

        // A point near the lair's heart, in its sector and walkable, drawn from (seed, creature, tick); false after four misses
        private bool TryRoamPoint(CreatureState c, CreatureDefinition kind, Vec2 heart, SharedWorldSnapshot snapshot, uint tick, out Vec2 point)
        {
            var rng = SimRandom.For(_seed, c.Id, tick, SimRandomSalt.CreatureRoam);
            float reach = (float)(kind.Leash * RoamShareOfLeash);
            var map = _map;
            byte sector = map != null ? map.FindSector(heart).Id : (byte)0;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float angle = rng.Range(0f, (float)(2f * SimMath.PI));
                float distance = rng.Range(RoamMinimum, reach > RoamMinimum ? reach : RoamMinimum);
                point = new Vec2((float)(heart.X + (float)(SimMath.Cos(angle) * distance)), (float)(heart.Y + (float)(SimMath.Sin(angle) * distance)));
                if (map != null && map.FindSector(point).Id != sector) continue;
                if (snapshot?.StaticGrid != null && (!snapshot.StaticGrid.WorldToCell(point, out int x, out int y) || !snapshot.StaticGrid.IsCellWalkable(x, y))) continue;
                return true;
            }
            point = default;
            return false;
        }

        private void Rest(CreatureState c, uint tick)
        {
            c.Mode = CreatureMode.Rest;
            c.Target = default;
            c.RestUntil = tick + RestTicks(c.Id, tick);
            c.ForgetPath();
        }

        private static void GoHome(CreatureState c)
        {
            c.Mode = CreatureMode.Home;
            c.Target = default;
            c.ForgetPath();
        }

        private static bool Wounded(CreatureState c, CreatureDefinition kind) => kind.Flees && c.Health < (float)(kind.FleeBelow * kind.MaxHealth);

        // How long a rest lasts: RestSeconds to twice it, drawn from (seed, creature, tick)
        private uint RestTicks(EntityId id, uint tick)
        {
            var rng = SimRandom.For(_seed, id, tick, SimRandomSalt.CreatureRest);
            float seconds = rng.Range(RestSeconds, (float)(2f * RestSeconds));
            return (uint)CombatRules.SecondsToTicks(seconds);
        }

        private static WorldEntity FindCrawler(IReadOnlyList<WorldEntity> entities, EntityId id)
        {
            if (entities == null || id == default) return null;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Id == id) return entities[i].Agent != null ? entities[i] : null;
            }
            return null;
        }
    }
}
