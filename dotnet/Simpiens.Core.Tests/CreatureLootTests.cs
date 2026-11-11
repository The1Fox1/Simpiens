using System.Collections.Generic;
using System.Linq;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// What each kind drops (Epic 10 Part 3, K4.2, Q12): every kind names a table that exists; over many kills a rat drops its
    /// hide about 7 in 10, a beetle chitin, each now and then a rare piece; the same kill drops the same.
    /// </summary>
    public class CreatureLootTests
    {
        [Fact]
        public void EveryKindHasATable()
        {
            foreach (var kind in CreatureCatalog.All) Assert.True(CreatureLoot.Tables.TryGet(kind.LootTableId, out _), $"{kind.Name}'s table {kind.LootTableId}.");
        }

        [Theory]
        [InlineData(CreatureKind.BurrowRat, ItemType.Hide, 0.6, 0.8, ItemType.FangKnife)]
        [InlineData(CreatureKind.StoneBeetle, ItemType.Chitin, 0.75, 0.95, ItemType.StoneheartCoat)] // one or two rolls: 82% measured
        public void AKindDropsItsMaterialAndNowAndThenARarePiece(CreatureKind kind, ItemType material, double least, double most, ItemType rare)
        {
            var table = CreatureCatalog.Get(kind).LootTableId;
            var drops = new ItemInstance[CreatureLoot.MaxDrops];
            int withMaterial = 0, rares = 0;
            const int kills = 4000;
            for (int k = 0; k < kills; k++)
            {
                int n = LootRoller.Roll(CreatureLoot.Tables, table, 424242UL, new EntityId(1u, 2u, 3u, (uint)k), 1000 + k, drops);
                if (drops.Take(n).Any(d => d.Type == material)) withMaterial++;
                if (drops.Take(n).Any(d => d.Type == rare)) rares++;
            }
            double share = withMaterial / (double)kills;
            TestContext.Current.TestOutputHelper?.WriteLine($"{kind}: {material} in {share:P0} of kills, {rares} rare pieces in {kills}");
            Assert.InRange(share, least, most);
            Assert.InRange(rares, 1, kills / 15);
        }

        [Fact]
        public void TheSameKillDropsTheSame()
        {
            var a = new ItemInstance[CreatureLoot.MaxDrops];
            var b = new ItemInstance[CreatureLoot.MaxDrops];
            var who = new EntityId(9u, 8u, 7u, 6u);
            int na = LootRoller.Roll(CreatureLoot.Tables, CreatureLoot.Beetle, 7UL, who, 4242, a);
            int nb = LootRoller.Roll(CreatureLoot.Tables, CreatureLoot.Beetle, 7UL, who, 4242, b);
            Assert.Equal(na, nb);
            Assert.Equal(a.Take(na), b.Take(nb));
        }
    }
}
