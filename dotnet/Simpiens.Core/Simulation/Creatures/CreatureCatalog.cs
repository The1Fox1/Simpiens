using System.Collections.Generic;

namespace Simpiens.Simulation.Creatures
{
    /// <summary>
    /// Every kind of creature the game knows, by id (Epic 10 Part 3, K0.4; seam A6: kinds as data, as items are). What a
    /// kind is doesn't vary by session, so the catalogue is static and immutable: any code reads it, on any thread, without
    /// being handed it. Where creatures live is the map's (lairs, K2.1).
    /// <para>
    /// A new kind is a <see cref="CreatureKind"/> value (never reused) and a row below. Its lairs (K2.1) and its art (K2.5)
    /// follow. A boss is a row too, with the mechanics no row can hold written apart (K10).
    /// </para>
    /// </summary>
    public static class CreatureCatalog
    {
        public const int Capacity = 256;

        /// <summary>The tables, built once and swapped whole (only by a test's <see cref="ExtendForTest"/>), so readers never see a half-built one.</summary>
        private sealed class Tables
        {
            public readonly CreatureDefinition[] ById = new CreatureDefinition[Capacity];
            public readonly List<CreatureDefinition> All = new List<CreatureDefinition>(8);

            public Tables(IEnumerable<CreatureDefinition> definitions)
            {
                foreach (var definition in definitions)
                {
                    if (definition.Id == CreatureKind.None) throw new System.ArgumentException($"{definition.Name}: kind 0 means none.");
                    if (ById[(byte)definition.Id] != null) throw new System.ArgumentException($"Two kinds have id {(byte)definition.Id}.");
                    ById[(byte)definition.Id] = definition;
                    int index = All.Count;
                    while (index > 0 && All[index - 1].Id > definition.Id) index--;
                    All.Insert(index, definition);
                }
            }
        }

        private static volatile Tables _tables = new Tables(Definitions());

        /// <summary>
        /// The game's creatures (placeholder names and numbers, the draft's Q5; K2 and K3 tune them by measurement). A crawler
        /// walks at 2, has 100 health and hits for 2 bare-handed, a swing every 1.5 s.
        /// </summary>
        private static CreatureDefinition[] Definitions() => new[]
        {
            // Weak and quick, in packs of three or four that wear a lone crawler down; each runs for the warren under 40%. A leash of
            // 6 (K2.3, D20): the warren holds overgrowth.3 and leaves the Overgrowth's other patches and its survey point clear
            new CreatureDefinition(CreatureKind.BurrowRat, "BurrowRat", "Burrow Rat")
            {
                MaxHealth = 20f, Attack = 3f, Defense = 0f, Reach = 0.3f, SwingIntervalSeconds = 1f,
                Speed = 2.2f, BodyRadius = 0.25f, Sight = 5f, Leash = 6f, FleeBelow = 0.4f,
                PackMin = 3, PackMax = 4, HealHours = 12, RespawnHours = 48, LootTableId = CreatureLoot.Rat
            },
            // Armoured and slow: a bare hand barely scratches one (defense 8 stops 44% of a blow), a crawler can walk away
            // from one, and a weapon decides the fight
            new CreatureDefinition(CreatureKind.StoneBeetle, "StoneBeetle", "Stone Beetle")
            {
                MaxHealth = 60f, Attack = 7f, Defense = 8f, Reach = 0.4f, SwingIntervalSeconds = 2f,
                Speed = 1.2f, BodyRadius = 0.4f, Sight = 4f, Leash = 6f,
                PackMin = 1, PackMax = 2, HealHours = 12, RespawnHours = 48, LootTableId = CreatureLoot.Beetle
            },
            // Spits from range on the gate's approach (shots came with K8, the approach with K10); runs under 30%. Sight and leash 6
            // (from 7 and 9, K10.1b): the Old Stair is a sector of the upper block's size, so its web holds the way to the gate and
            // leaves the survey point inside the door out of its reach and sight
            new CreatureDefinition(CreatureKind.CaveSpinner, "CaveSpinner", "Cave Spinner")
            {
                MaxHealth = 30f, Attack = 4f, Defense = 2f, Reach = 0.4f, SwingIntervalSeconds = 1.5f, Range = 5f,
                Speed = 1.6f, BodyRadius = 0.35f, Sight = 6f, Leash = 6f, FleeBelow = 0.3f,
                PackMin = 2, PackMax = 3, HealHours = 12, RespawnHours = 48, LootTableId = CreatureLoot.Spinner
            },
            // The bosses (Epic 10 Part 3, K10, Q13; placeholder numbers, tuned by K10.6's measurement). The Matriarch heads the
            // warren (its lair's boss): four rats' worth of health, a rat's quickness and a harder bite; she never flees, calls
            // her brood to her quarry at half health, and returns after a game week
            new CreatureDefinition(CreatureKind.Matriarch, "Matriarch", "The Matriarch")
            {
                MaxHealth = 80f, Attack = 6f, Defense = 2f, Reach = 0.4f, SwingIntervalSeconds = 1.2f,
                Speed = 2.1f, BodyRadius = 0.35f, Sight = 5f, Leash = 6f,
                PackMin = 1, PackMax = 1, HealHours = 12, RespawnHours = 7 * 24, LootTableId = CreatureLoot.Matriarch,
                IsBoss = true, CallsBroodBelow = 0.5f
            },
            // The Gatekeeper holds the sealed gate: a stone warden, slow and heavily armoured, whose slam strikes everyone in
            // its reach and who fights in fury under a quarter of its health. It heals nothing by resting, but stands whole again
            // once its lair has been empty of crawlers for a game hour, and it never returns once beaten
            new CreatureDefinition(CreatureKind.Gatekeeper, "Gatekeeper", "The Gatekeeper")
            {
                MaxHealth = 320f, Attack = 12f, Defense = 8f, Reach = 1f, SwingIntervalSeconds = 2.5f,
                Speed = 1f, BodyRadius = 0.9f, Sight = 4f, Leash = 4f,
                PackMin = 1, PackMax = 1, HealHours = 0, RespawnHours = 0, LootTableId = CreatureLoot.Gatekeeper,
                IsBoss = true, Slam = true, FuryBelow = 0.25f, FuryAttack = 1.5f, FuryInterval = 0.6f, WholeAfterHours = 1
            }
        };

        /// <summary>
        /// Tests only: the catalogue with <paramref name="extra"/> added, until the returned scope is disposed (the proof that
        /// a kind needs no code beyond its row). The tables are swapped whole, between ticks; tests that use it run serially.
        /// </summary>
        internal static System.IDisposable ExtendForTest(params CreatureDefinition[] extra)
        {
            var previous = _tables;
            var definitions = new List<CreatureDefinition>(previous.All);
            definitions.AddRange(extra);
            _tables = new Tables(definitions);
            return new Restore(previous);
        }

        private sealed class Restore : System.IDisposable
        {
            private readonly Tables _previous;
            public Restore(Tables previous) => _previous = previous;
            public void Dispose() => _tables = _previous;
        }

        /// <summary>Every defined kind, in id order.</summary>
        public static IReadOnlyList<CreatureDefinition> All => _tables.All;

        public static bool IsDefined(CreatureKind id) => _tables.ById[(byte)id] != null;

        /// <summary>The kind's definition, or null for <see cref="CreatureKind.None"/> and undefined ids.</summary>
        public static CreatureDefinition Get(CreatureKind id) => _tables.ById[(byte)id];

        /// <summary>A code name for logs: the definition's, or the raw id for an undefined one.</summary>
        public static string NameOf(CreatureKind id) => Get(id)?.Name ?? $"Creature{(byte)id}";
    }
}
