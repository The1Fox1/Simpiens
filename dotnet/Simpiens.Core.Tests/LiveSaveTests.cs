using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;
using Simpiens.Testing.Environment;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The live outpost captured whole and loaded back exactly, headless (Epic CLIENT M2.8; Unity's SessionCaptureScenario
    /// and SessionRestoreScenario). The validators check the blocks in isolation; these check them on a running session
    /// with nothing injected.
    /// </summary>
    public class LiveSaveTests
    {
        private const long SaveTick = 600;

        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }

        /// <summary>Two captures between the same two ticks are the same bytes, and every part is there.</summary>
        [Fact]
        public void TheLiveOutpostIsCapturedWhole()
        {
            using var sim = Outpost();
            sim.RunTo(SaveTick);
            var save = sim.Host.Capture();
            byte[] first = SaveSerializer.Serialize(save);
            byte[] second = SaveSerializer.Serialize(sim.Host.Capture());
            Assert.Equal(first, second);

            var h = save.Header;
            Assert.Equal(SessionSaveHeader.CurrentFormatVersion, h.FormatVersion);
            Assert.Equal(OutpostSession.SessionId, h.SessionId);
            Assert.Equal(SaveFingerprint.Of(Floor1Layout.Definition), h.MapFingerprint);
            Assert.Equal(SimulationSettings.DefaultSeed, h.Seed);
            Assert.Equal(SaveTick, h.Tick);
            Assert.True(h.EntityIdCounter > 0, "The id allocator's counter.");
            Assert.False(string.IsNullOrEmpty(h.BuildVersion), "Build version.");

            // Crawlers, in spawn order (no drifter has arrived by t600)
            var states = sim.Host.CrawlerStates;
            Assert.Equal(OutpostSession.DefaultCrawlerCount, save.Crawlers.Length);
            Assert.Equal(states.Count, save.Crawlers.Length);
            int sightings = 0, crawlerGold = 0;
            for (int i = 0; i < save.Crawlers.Length; i++)
            {
                var c = save.Crawlers[i];
                Assert.Equal($"SwarmAgent_{i}", c.Name);
                Assert.Equal($"Crawler{i}", c.Label);
                Assert.Equal(states[i].AgentId, c.Record.Id);
                Assert.Equal(states[i].Position, c.Record.Position);
                Assert.Equal(4, c.Inventory.Slots.Length);
                Assert.Equal(states[i].Inventory.Gold, c.Inventory.Gold);
                Assert.Equal(64, c.Memory.SectorVisits.Length);
                sightings += c.Memory.Sightings.Length;
                crawlerGold += c.Inventory.Gold;
            }
            Assert.True(sightings > 0, "Crawlers remember something after 600 ticks.");

            // The economy, and the ledger balances on the captured data alone
            var economy = save.Economy;
            Assert.Single(economy.Stalls);
            Assert.True(economy.Bounties.Length >= 2, "The seeded bounties.");
            var ledger = economy.Ledger;
            int seed = Array.IndexOf(ledger.FaucetNames, "InitialSeed");
            Assert.True(seed >= 0 && ledger.FaucetTotals[seed] > 0u, "The InitialSeed faucet holds the starting gold.");
            long held = economy.Stalls[0].TreasuryGold + (long)crawlerGold + ledger.Totals.Escrow;
            Assert.Equal((long)ledger.Totals.Fauceted - ledger.Totals.Sunk, held);

            // The world, the groups and the chronicle
            var world = save.World;
            Assert.True(world.Resources.Length > 0 && world.Resources.Length <= Floor1Layout.Definition.ResourceNodes.Count, "Resource nodes.");
            Assert.NotNull(world.Supply?.Dormant);
            Assert.Equal(256, world.ExploredSectors.Length);
            int stallKey = Array.IndexOf(world.EntityKeys, Floor1Layout.Stall);
            Assert.True(stallKey >= 0, "The stall's key.");
            Assert.Equal(economy.Stalls[0].StallEntityId, world.EntityIds[stallKey]);
            Assert.NotNull(save.Groups?.Groups);
            Assert.True(save.Chronicle.Events.Length > 0, "The chronicle has events after 600 ticks.");
            Assert.True(save.Chronicle.Sequence >= (ulong)save.Chronicle.Events.Length, "The chronicle's sequence.");
        }

        /// <summary>
        /// The capture is loaded over a fresh session: the same bytes and world hash, the clock and id counter where they
        /// were; broken saves are refused with a reason and change nothing; then every crawler decides afresh.
        /// </summary>
        [Fact]
        public void TheLiveOutpostLoadsBackExactly()
        {
            using var sim = Outpost();
            var ids = sim.Services.GetRequiredService<IEntityIdAllocator>();
            sim.RunTo(SaveTick);
            long savedCounter = ids.Counter;
            byte[] saved = SaveSerializer.Serialize(sim.Host.Capture());
            var savedHash = WorldStateHash.Capture(SaveTick, sim.Host, sim.Simulation, sim.Sectors, includeWork: false);

            sim.Respawn(OutpostSession.SessionId);
            sim.RunTo(50);

            // Broken saves are refused with a reason, and the running session is left as it was
            byte[] before = SaveSerializer.Serialize(sim.Host.Capture());
            var refusals = new List<string>();
            void TryBroken(string what, Action<SessionSave> breakIt)
            {
                var broken = SaveSerializer.Deserialize<SessionSave>(saved);
                breakIt(broken);
                Assert.False(sim.Host.TryRestore(broken, out string reason), $"A save with {what} was loaded.");
                Assert.False(string.IsNullOrEmpty(reason), $"A save with {what} was refused without a reason.");
                refusals.Add($"{what}: {reason}");
            }
            TryBroken("another format version", s => s.Header.FormatVersion = SessionSaveHeader.CurrentFormatVersion + 1);
            TryBroken("an unknown session", s => s.Header.SessionId = "no-such-session");
            TryBroken("another seed", s => s.Header.Seed = s.Header.Seed + 1);
            TryBroken("another map", s => s.Header.MapFingerprint = s.Header.MapFingerprint ^ 0xFFUL);
            TryBroken("a missing block", s => s.World = null);
            TryBroken("an item the game doesn't have", s => s.Crawlers[0].Inventory.Slots[0] = new ItemInstance(1, (ItemType)77, 1, 1));
            Assert.Equal(50, sim.Clock.CurrentTick);
            Assert.Equal(before, SaveSerializer.Serialize(sim.Host.Capture()));

            Assert.True(sim.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), $"The save was refused: {refusal}");
            Assert.Equal(SaveTick, sim.Clock.CurrentTick);
            Assert.Equal(savedCounter, ids.Counter);
            Assert.Equal(saved, SaveSerializer.Serialize(sim.Host.Capture()));
            var loadedHash = WorldStateHash.Capture(SaveTick, sim.Host, sim.Simulation, sim.Sectors, includeWork: false);
            Assert.Null(savedHash.DescribeDifference(loadedHash));

            // The loaded outpost plays on: every crawler decides afresh
            sim.Step(200);
            int afresh = 0;
            foreach (var state in sim.Host.CrawlerStates) if (state.LastDecision.Tick > (uint)SaveTick) afresh++;
            TestContext.Current.TestOutputHelper?.WriteLine($"refusals: {string.Join(" | ", refusals)}; {afresh}/{sim.Host.CrawlerStates.Count} decided afresh");
            Assert.Equal(sim.Host.CrawlerStates.Count, afresh);
        }
    }
}
