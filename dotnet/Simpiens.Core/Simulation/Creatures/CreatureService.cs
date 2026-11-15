using System.Collections.Generic;
using Simpiens.Diagnostics;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using ZLogger;

namespace Simpiens.Simulation.Creatures
{
    /// <summary>
    /// The world as the creatures' phase sees it (the simulation): where a creature's entity enters and leaves (its registry,
    /// so its spatial grid and its views follow), what it sees and walks on, and where a blow lands on a crawler. Main thread.
    /// </summary>
    public interface ICreatureWorld : Simpiens.Simulation.Skills.ISkillPractice
    {
        void AddCreature(WorldEntity entity);
        void RemoveCreature(EntityId id);

        /// <summary>The live snapshot: line of sight and the nav grid for paths; null in a test without one.</summary>
        Simpiens.Simulation.Spatial.SharedWorldSnapshot Snapshot { get; }

        /// <summary>Every entity in the world, the crawlers among them.</summary>
        IReadOnlyList<WorldEntity> Entities { get; }

        /// <summary>A creature's swing at a crawler (K2.2, D13): the harm if it landed, the red coat, the armour's wear, a downing.</summary>
        void Strike(WorldEntity crawler, EntityId attacker, in Simpiens.Simulation.Combat.SwingOutcome outcome, uint tick);

        /// <summary>
        /// A crawler slew a creature (K3.3): the log and the chronicle, and its <paramref name="drops"/> (K4.2) in a pile at its
        /// place, claimed for the slayer. The creature is already out of the world.
        /// </summary>
        void CreatureSlain(EntityId slayer, CreatureState creature, System.ReadOnlySpan<Simpiens.Simulation.Economy.ItemInstance> drops, uint tick);

        /// <summary>A crawler's blow landed on a creature for <paramref name="damage"/> (Epic 10 Part 3, K6.1: a hunter's share).</summary>
        void CreatureHit(EntityId crawler, CreatureState creature, float damage);
    }

    /// <summary>
    /// One lair at play (Epic 10 Part 3, K2.1): its map definition, the size of its pack (drawn from the session's seed),
    /// who holds each place in it, and when a slain member returns to its place.
    /// </summary>
    public sealed class LairState
    {
        private EntityId[] _members = System.Array.Empty<EntityId>();
        private uint[] _returnsAt = System.Array.Empty<uint>();

        internal LairState(int index, in LairDefinition definition)
        {
            Index = index;
            Definition = definition;
        }

        public int Index { get; }
        public LairDefinition Definition { get; }

        /// <summary>How many it holds when full.</summary>
        public int Pack => _members.Length;

        /// <summary>The creature in <paramref name="slot"/>; empty while the place is empty.</summary>
        public EntityId MemberIn(int slot) => _members[slot];

        /// <summary>When <paramref name="slot"/>'s slain member returns; 0 when none is due.</summary>
        public uint ReturnAt(int slot) => _returnsAt[slot];

        /// <summary>How many of its places are held.</summary>
        public int Alive
        {
            get
            {
                int alive = 0;
                for (int i = 0; i < _members.Length; i++)
                {
                    if (_members[i] != default) alive++;
                }
                return alive;
            }
        }

        internal void SetPack(int pack)
        {
            _members = new EntityId[pack];
            _returnsAt = new uint[pack];
        }

        internal void Hold(int slot, EntityId id) => _members[slot] = id;

        internal void Free(int slot, uint returnsAt)
        {
            _members[slot] = default;
            _returnsAt[slot] = returnsAt;
        }

        internal bool Due(int slot, uint tick) => _members[slot] == default && _returnsAt[slot] != 0 && tick >= _returnsAt[slot];

        internal void ClearReturn(int slot) => _returnsAt[slot] = 0;

        internal void Set(int slot, EntityId member, uint returnsAt)
        {
            _members[slot] = member;
            _returnsAt[slot] = returnsAt;
        }
    }

    /// <summary>
    /// The creatures of the session (Epic 10 Part 3, K2; seam A4): the map's lairs and the creatures that live in them.
    /// <list type="bullet">
    /// <item><b>Spawns on the seed</b> (K2.1): at the session's start every lair is filled to a pack drawn from the seed within
    /// its kind's range, each member in its place around the lair's heart.</item>
    /// <item><b>Its own ids:</b> a creature's id comes from a seeded sequence of its own (<see cref="SimRandomSalt.CreatureId"/>),
    /// not the entities' (<see cref="EntityIdAllocator"/>), so creatures coming and going never change who the next crawler
    /// or drifter is. The sequence's counter is saved (K2.4).</item>
    /// <item><b>Refilling on the day's time</b> (Q5): a slain member returns to its place after its kind's
    /// <see cref="CreatureDefinition.RespawnHours"/>; a cleared lair is whole again 2 game days on.</item>
    /// <item><b>Its own phase</b> (A4): <see cref="Tick"/> runs after the crawlers' runtime, every creature in id order.</item>
    /// </list>
    /// Main thread only. Owned by the simulation (<see cref="SimulationManager.Creatures"/>), which follows the loaded map.
    /// </summary>
    public sealed partial class CreatureService
    {
        /// <summary>The most a lair holds: the places around its heart.</summary>
        public const int MaxPack = 8;

        // A pack's places around its lair's heart, about a body apart
        private static readonly Vec2[] Places =
        {
            new Vec2(0f, 0f), new Vec2(0.8f, 0f), new Vec2(-0.8f, 0.4f), new Vec2(0.4f, -0.8f),
            new Vec2(-0.4f, -0.8f), new Vec2(0.8f, 0.8f), new Vec2(-0.8f, -0.4f), new Vec2(0f, 0.9f),
        };

        private MapDefinition _map;
        private readonly List<LairState> _lairs = new List<LairState>(4);
        private readonly List<CreatureState> _creatures = new List<CreatureState>(16);
        private ulong _seed;
        private long _idCounter;
        private ulong _charted; // bit i: lair i is known to everyone (K2.3, D17)

        /// <summary>Every creature in the world, in id order (the phase's order, A4).</summary>
        public IReadOnlyList<CreatureState> Creatures => _creatures;

        /// <summary>The map's lairs at play, in definition order.</summary>
        public IReadOnlyList<LairState> Lairs => _lairs;

        /// <summary>Creature ids handed out since the session started: with the seed, all a save needs to continue the sequence (K2.4).</summary>
        public long IdCounter => _idCounter;

        /// <summary>
        /// Which lairs are charted (K2.3, D17): bit i set when lair i is known to everyone, since a crawler saw one of its
        /// creatures or was struck by one. The simulation publishes it in every snapshot, for cognition.
        /// </summary>
        public ulong ChartedMask => _charted;

        /// <summary>True when lair <paramref name="lairIndex"/> is charted.</summary>
        public bool IsCharted(int lairIndex) => lairIndex >= 0 && lairIndex < 64 && (_charted & (1UL << lairIndex)) != 0;

        /// <summary>
        /// Which lairs have their boss alive (Epic 10 Part 3, K10.2a, D63): bit i set while lair i's boss holds its first place.
        /// The simulation publishes it in every snapshot, so a lair is judged with its boss only while she lives.
        /// </summary>
        public ulong BossMask
        {
            get
            {
                ulong mask = 0UL;
                for (int l = 0; l < _lairs.Count && l < 64; l++)
                    if (IsBossAlive(l)) mask |= 1UL << l;
                return mask;
            }
        }

        /// <summary>
        /// True when lair <paramref name="lairIndex"/> guards the gate and is emptied for good (Epic 10 Part 3, K10.4): none of its
        /// places held, and none due to refill (its keeper never returns, K10.3d).
        /// </summary>
        public bool GuardsGateEmptyForGood(int lairIndex)
        {
            if (lairIndex < 0 || lairIndex >= _lairs.Count || !_lairs[lairIndex].Definition.GuardsGate) return false;
            var lair = _lairs[lairIndex];
            for (int slot = 0; slot < lair.Pack; slot++)
                if (lair.MemberIn(slot) != default || lair.ReturnAt(slot) != 0) return false;
            return lair.Pack > 0;
        }

        /// <summary>True while lair <paramref name="lairIndex"/>'s boss holds its place.</summary>
        public bool IsBossAlive(int lairIndex) =>
            lairIndex >= 0 && lairIndex < _lairs.Count && _lairs[lairIndex].Definition.HasBoss && _lairs[lairIndex].Pack > 0 && _lairs[lairIndex].MemberIn(0) != default;

        /// <summary>Lays out <paramref name="map"/>'s lairs, empty, when the map changes (a respawn or a load); nothing otherwise.</summary>
        public CreatureService Follow(MapDefinition map)
        {
            if (ReferenceEquals(map, _map)) return this;
            _map = map;
            _lairs.Clear();
            _creatures.Clear();
            _charted = 0UL;
            if (map == null) return this;
            for (int i = 0; i < map.Lairs.Count; i++) _lairs.Add(new LairState(i, map.Lairs[i]));
            return this;
        }

        /// <summary>Forgets every lair and creature (a respawn or a load tears the world down); the next <see cref="Follow"/> lays the map's out afresh.</summary>
        public void Reset()
        {
            _map = null;
            _lairs.Clear();
            _creatures.Clear();
            _idCounter = 0;
            _charted = 0UL;
            _asked.Clear();
            Kills = 0;
        }

        /// <summary>The creature with <paramref name="id"/>; false for none.</summary>
        public bool TryGet(EntityId id, out CreatureState creature)
        {
            int index = IndexOf(id);
            creature = index >= 0 ? _creatures[index] : null;
            return index >= 0;
        }

        /// <summary>
        /// Fills every lair at the session's start (K2.1): a pack drawn from <paramref name="seed"/> and the lair's place in the
        /// map, within its kind's range (at most <see cref="MaxPack"/>), each member at full health in its place.
        /// </summary>
        public void Populate(ICreatureWorld world, ulong seed)
        {
            _seed = seed;
            _idCounter = 0;
            _charted = 0UL;
            for (int i = 0; i < _creatures.Count; i++) world?.RemoveCreature(_creatures[i].Id);
            _creatures.Clear();
            for (int l = 0; l < _lairs.Count; l++)
            {
                var lair = _lairs[l];
                var kind = CreatureCatalog.Get(lair.Definition.Kind);
                if (kind == null)
                {
                    lair.SetPack(0);
                    continue;
                }
                lair.SetPack(PackOf(seed, l, lair.Definition));
                for (int slot = 0; slot < lair.Pack; slot++) Spawn(world, lair, (byte)slot, 0u);
                SimLog.Bootstrap.ZLogInformation($"Lair {lair.Definition.Key}: {lair.Pack} {kind.Name}{(lair.Definition.HasBoss ? ", headed by " + CreatureCatalog.NameOf(lair.Definition.Boss) : "")}");
            }
        }

        /// <summary>
        /// The creatures' phase (seam A4), after the crawlers' runtime: a slain member whose time has come returns to its place
        /// (K2.1); then every creature's rule, in id order, and every swing decided from where all stand, then all the harm
        /// (K2.2, <see cref="Rule"/>). Allocates nothing once warm.
        /// </summary>
        public void Tick(ICreatureWorld world, uint tick)
        {
            Returns(world, tick);
            Rule(world, tick);
        }

        private void Returns(ICreatureWorld world, uint tick)
        {
            for (int l = 0; l < _lairs.Count; l++)
            {
                var lair = _lairs[l];
                for (int slot = 0; slot < lair.Pack; slot++)
                {
                    if (!lair.Due(slot, tick)) continue;
                    lair.ClearReturn(slot);
                    var creature = Spawn(world, lair, (byte)slot, tick);
                    SimLog.Simulation.ZLogInformation($"A {CreatureCatalog.NameOf(creature.Kind)} came back to {lair.Definition.Key}");
                }
            }
        }

        /// <summary>
        /// A creature slain (K3's blows; a test): out of the world, and back in its place after its kind's
        /// <see cref="CreatureDefinition.RespawnHours"/> of the day (Q5), or never for a kind that doesn't return. False for none.
        /// </summary>
        public bool Slay(ICreatureWorld world, EntityId id, uint tick)
        {
            int index = IndexOf(id);
            if (index < 0) return false;
            var creature = _creatures[index];
            _creatures.RemoveAt(index);
            creature.Health = 0f;
            world?.RemoveCreature(id);
            var kind = creature.Definition;
            uint returnsAt = kind != null && kind.Respawns ? tick + DayClock.Hours(kind.RespawnHours) : 0u;
            if (creature.LairIndex >= 0 && creature.LairIndex < _lairs.Count) _lairs[creature.LairIndex].Free(creature.Slot, returnsAt);
            return true;
        }

        /// <summary>The pack <paramref name="kind"/>'s lair at <paramref name="lairIndex"/> holds on <paramref name="seed"/>: within its range, at most <see cref="MaxPack"/>.</summary>
        public static int PackSize(ulong seed, int lairIndex, CreatureDefinition kind)
        {
            int min = kind.PackMin, max = kind.PackMax < kind.PackMin ? kind.PackMin : kind.PackMax;
            var rng = SimRandom.For(seed, default, lairIndex, SimRandomSalt.Lair);
            int pack = min + (int)(rng.NextULong() % (ulong)(max - min + 1));
            return pack < MaxPack ? pack : MaxPack;
        }

        /// <summary>
        /// The places <paramref name="lair"/> holds on <paramref name="seed"/> (K10): its boss's first, if it has one the
        /// catalogue knows, then its pack's (<see cref="PackSize"/>), at most <see cref="MaxPack"/> in all.
        /// </summary>
        public static int PackOf(ulong seed, int lairIndex, in LairDefinition lair)
        {
            var kind = CreatureCatalog.Get(lair.Kind);
            if (kind == null) return 0;
            int boss = lair.HasBoss && CreatureCatalog.IsDefined(lair.Boss) ? 1 : 0;
            int pack = boss + PackSize(seed, lairIndex, kind);
            return pack < MaxPack ? pack : MaxPack;
        }

        /// <summary>Where <paramref name="slot"/> rests around a lair's heart at <paramref name="heart"/>.</summary>
        public static Vec2 PlaceOf(Vec2 heart, int slot) => heart + Places[slot % Places.Length];

        private CreatureState Spawn(ICreatureWorld world, LairState lair, byte slot, uint tick)
        {
            var kind = CreatureCatalog.Get(lair.Definition.KindIn(slot));
            var creature = new CreatureState(NextId(), kind.Id, lair.Index, slot, PlaceOf(lair.Definition.Position, slot));
            creature.RestUntil = tick + RestTicks(creature.Id, tick);
            lair.Hold(slot, creature.Id);
            int at = ~IndexOf(creature.Id);
            _creatures.Insert(at < 0 ? _creatures.Count : at, creature);
            Enter(world, creature, kind);
            return creature;
        }

        // Its entity into the world, where it stands (its spatial grid and its views follow the registry)
        private static void Enter(ICreatureWorld world, CreatureState creature, CreatureDefinition kind)
        {
            creature.Entity = new WorldEntity(creature.Id, EntityType.Creature, creature.Position, kind.BodyRadius, kind.Speed,
                name: $"{kind.Name}_{creature.LairIndex}.{creature.Slot}", creature: creature);
            world?.AddCreature(creature.Entity);
        }

        /// <summary>
        /// The creatures for a save (K2.4): the id counter, the charted lairs, each lair's pack and places, every creature
        /// in id order. Main thread, between ticks.
        /// </summary>
        public Simpiens.Simulation.Persistence.CreatureSave Capture()
        {
            var packs = new byte[_lairs.Count];
            int places = 0;
            for (int l = 0; l < _lairs.Count; l++)
            {
                packs[l] = (byte)_lairs[l].Pack;
                places += _lairs[l].Pack;
            }
            var slots = new Simpiens.Simulation.Persistence.LairSlotRecord[places];
            int n = 0;
            for (int l = 0; l < _lairs.Count; l++)
                for (int slot = 0; slot < _lairs[l].Pack; slot++)
                    slots[n++] = new Simpiens.Simulation.Persistence.LairSlotRecord((byte)l, (byte)slot, _lairs[l].MemberIn(slot), _lairs[l].ReturnAt(slot));
            var creatures = new Simpiens.Simulation.Persistence.CreatureRecord[_creatures.Count];
            for (int i = 0; i < _creatures.Count; i++) creatures[i] = new Simpiens.Simulation.Persistence.CreatureRecord(_creatures[i]);
            return new Simpiens.Simulation.Persistence.CreatureSave { IdCounter = _idCounter, ChartedLairs = _charted, Packs = packs, Slots = slots, Creatures = creatures };
        }

        /// <summary>
        /// Why <paramref name="save"/>'s creatures can't live on <paramref name="map"/> (player-facing text), or null when
        /// they can: a lair the map doesn't have, a place past its pack or the most a lair holds, a kind this game doesn't
        /// know or another than its lair's, creatures out of id order. Checked before a load changes anything.
        /// </summary>
        public static string Check(Simpiens.Simulation.Persistence.CreatureSave save, MapDefinition map)
        {
            if (save == null) return null; // a save from before the creatures were saved: the lairs fill afresh
            int lairs = map?.Lairs.Count ?? 0;
            if (save.Packs == null || save.Slots == null || save.Creatures == null) return "its creatures are incomplete";
            if (save.Packs.Length != lairs) return $"it has {save.Packs.Length} lairs where the map has {lairs}";
            for (int l = 0; l < save.Packs.Length; l++)
                if (save.Packs[l] > MaxPack) return $"lair {l} holds {save.Packs[l]}, more than {MaxPack}";
            for (int i = 0; i < save.Slots.Length; i++)
            {
                var slot = save.Slots[i];
                if (slot.Lair >= lairs || slot.Slot >= save.Packs[slot.Lair]) return $"a place in lair {slot.Lair} that doesn't exist";
            }
            for (int i = 0; i < save.Creatures.Length; i++)
            {
                var c = save.Creatures[i];
                if (!CreatureCatalog.IsDefined(c.Kind)) return $"a creature of kind {(byte)c.Kind}";
                if (c.Lair >= lairs || c.Slot >= save.Packs[c.Lair] || map.Lairs[c.Lair].KindIn(c.Slot) != c.Kind) return $"a {CreatureCatalog.NameOf(c.Kind)} out of its lair";
                if (i > 0 && save.Creatures[i - 1].Id.CompareTo(c.Id) >= 0) return "creatures out of order";
            }
            return null;
        }

        /// <summary>
        /// Puts a save's creatures back (a load, K2.4; after <see cref="Check"/>): the id counter and the charted lairs, each
        /// lair's pack and places, and every creature where it stood, doing what it did, its entity in the world in id order.
        /// A save without creatures fills the lairs afresh, as a session's start does. Main thread, between ticks.
        /// </summary>
        public void Restore(ICreatureWorld world, Simpiens.Simulation.Persistence.CreatureSave save, ulong seed)
        {
            if (save == null)
            {
                Populate(world, seed);
                return;
            }
            _seed = seed;
            for (int i = 0; i < _creatures.Count; i++) world?.RemoveCreature(_creatures[i].Id);
            _creatures.Clear();
            _idCounter = save.IdCounter;
            _charted = save.ChartedLairs;
            for (int l = 0; l < _lairs.Count; l++) _lairs[l].SetPack(l < save.Packs.Length ? save.Packs[l] : 0);
            for (int i = 0; i < save.Slots.Length; i++)
            {
                var slot = save.Slots[i];
                if (slot.Lair < _lairs.Count && slot.Slot < _lairs[slot.Lair].Pack) _lairs[slot.Lair].Set(slot.Slot, slot.Member, slot.ReturnAt);
            }
            for (int i = 0; i < save.Creatures.Length; i++)
            {
                var r = save.Creatures[i];
                var kind = CreatureCatalog.Get(r.Kind);
                if (kind == null || r.Lair >= _lairs.Count) continue;
                var creature = new CreatureState(r.Id, r.Kind, r.Lair, r.Slot, PlaceOf(_lairs[r.Lair].Definition.Position, r.Slot))
                {
                    Position = r.Position,
                    Health = r.Health,
                    Mode = r.Mode,
                    Target = r.Target,
                    NextSwingAt = r.NextSwingAt,
                    RestUntil = r.RestUntil,
                    Goal = r.Goal,
                    LastVisitAt = r.LastVisitAt,
                };
                _creatures.Add(creature);
                Enter(world, creature, kind);
            }
        }

        // The creatures' own seeded sequence: the n-th id depends only on the seed and n
        private EntityId NextId()
        {
            while (true)
            {
                var rng = SimRandom.For(_seed, default, _idCounter++, SimRandomSalt.CreatureId);
                ulong hi = rng.NextULong();
                ulong lo = rng.NextULong();
                var id = new EntityId((uint)(hi >> 32), (uint)hi, (uint)(lo >> 32), (uint)lo);
                if (id != default && id != WorldRegistry.PlayerEntityId) return id;
            }
        }

        // Binary search by id: the index, or the complement of where it would go
        private int IndexOf(EntityId id)
        {
            int lo = 0, hi = _creatures.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int c = _creatures[mid].Id.CompareTo(id);
                if (c == 0) return mid;
                if (c < 0) lo = mid + 1;
                else hi = mid - 1;
            }
            return ~lo;
        }
    }
}
