using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation;
using Simpiens.Simulation.Persistence;
using Simpiens.Testing.Diagnostics;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Milestone 1 completion plan, step 2: the live outpost is captured whole. After <see cref="CaptureTick"/> ticks with
    /// nothing injected, two captures taken between the same two ticks serialize to the same bytes, and every part is
    /// there: the header (session, map, seed, tick, id counter); all crawlers with their names, packs and memories; the
    /// stall, the bounties and a ledger that balances; the resource nodes, the fog and the keyed entities; the chronicle.
    /// </summary>
    public class SessionCaptureScenario : ISimulationScenario
    {
        public const long CaptureTick = 600;
        private const float WallClockTimeoutSeconds = 60f;

        public string Name => "SessionCaptureScenario";
        public string SessionId => OutpostSession.SessionId;
        public float DurationSeconds => 0f;
        public float TimeMultiplier => 10f;

        private SessionSave _save;
        private byte[] _firstBytes;
        private byte[] _secondBytes;
        private long _tickBefore = -1;
        private long _tickAfter = -1;
        private ulong _seed;
        private readonly List<GUID> _agentIds = new List<GUID>(16);
        private readonly List<Vector2> _agentPositions = new List<Vector2>(16);
        private readonly List<int> _agentGold = new List<int>(16);

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            var spawner = resolver.Resolve<SwarmSpawner>();
            var settings = resolver.Resolve<SimulationSettings>();
            Assert.IsNotNull(clock, "ISimulationClock not registered.");
            Assert.IsNotNull(spawner, "SwarmSpawner not registered.");
            _seed = settings != null ? settings.Seed : SimulationSettings.DefaultSeed;
            clock.SetTimeMultiplier(TimeMultiplier);

            float elapsed = 0f;
            while (clock.CurrentTick < CaptureTick && elapsed < WallClockTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // A coroutine resumes after the frame's simulation ticks: this is between two ticks
            _tickBefore = clock.CurrentTick;
            _save = spawner.Capture();
            _firstBytes = SaveSerializer.Serialize(_save);
            _secondBytes = SaveSerializer.Serialize(spawner.Capture());
            _tickAfter = clock.CurrentTick;

            var agents = spawner.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var state = agents[i] != null ? agents[i].State : null;
                if (state == null) continue;
                _agentIds.Add(state.AgentId);
                _agentPositions.Add(state.Position);
                _agentGold.Add(state.Inventory != null ? state.Inventory.Gold : 0);
            }
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Assert.IsNotNull(_save, $"No capture: the outpost didn't reach t{CaptureTick} in {WallClockTimeoutSeconds} s.");
            Assert.AreEqual(_tickBefore, _tickAfter, "A tick ran between the two captures.");

            Assert.AreEqual(_firstBytes.Length, _secondBytes.Length, "Two captures of the same state differ in size.");
            for (int i = 0; i < _firstBytes.Length; i++)
            {
                if (_firstBytes[i] != _secondBytes[i]) Assert.Fail($"Two captures of the same state differ at byte {i} of {_firstBytes.Length}.");
            }

            var h = _save.Header;
            Assert.AreEqual(SessionSaveHeader.CurrentFormatVersion, h.FormatVersion, "Format version.");
            Assert.AreEqual(OutpostSession.SessionId, h.SessionId, "Session id.");
            Assert.AreEqual(SaveFingerprint.Of(Floor1Layout.Definition), h.MapFingerprint, "Map fingerprint.");
            Assert.AreEqual(_seed, h.Seed, "Seed.");
            Assert.AreEqual(_tickBefore, h.Tick, "Tick.");
            Assert.Greater(h.EntityIdCounter, 0, "The id allocator's counter.");
            Assert.IsFalse(string.IsNullOrEmpty(h.BuildVersion), "Build version.");

            // Crawlers, in spawn order
            Assert.AreEqual(OutpostSession.DefaultCrawlerCount, _save.Crawlers.Length, "Every crawler is captured.");
            Assert.AreEqual(_agentIds.Count, _save.Crawlers.Length, "Crawler count matches the spawner.");
            int sightings = 0;
            int crawlerGold = 0;
            for (int i = 0; i < _save.Crawlers.Length; i++)
            {
                var c = _save.Crawlers[i];
                Assert.AreEqual($"SwarmAgent_{i}", c.Name, $"Crawler {i}'s view name.");
                Assert.AreEqual($"Crawler{i}", c.Label, $"Crawler {i}'s log label.");
                Assert.AreEqual(_agentIds[i], c.Record.Id, $"Crawler {i}'s id (spawn order).");
                Assert.AreEqual(_agentPositions[i], c.Record.Position, $"Crawler {i}'s position.");
                Assert.IsNotNull(c.Inventory?.Slots, $"Crawler {i}'s pack.");
                Assert.AreEqual(4, c.Inventory.Slots.Length, $"Crawler {i}'s pack has every slot.");
                Assert.AreEqual(_agentGold[i], c.Inventory.Gold, $"Crawler {i}'s purse.");
                Assert.IsNotNull(c.Memory?.Sightings, $"Crawler {i}'s memory.");
                Assert.AreEqual(64, c.Memory.SectorVisits.Length, $"Crawler {i}'s sector visits.");
                sightings += c.Memory.Sightings.Length;
                crawlerGold += c.Inventory.Gold;
            }
            Assert.Greater(sightings, 0, "Crawlers remember something after 600 ticks.");

            // The economy, and the ledger balances on the captured data alone
            var economy = _save.Economy;
            Assert.AreEqual(1, economy.Stalls.Length, "The stall.");
            Assert.GreaterOrEqual(economy.Bounties.Length, 2, "The seeded bounties.");
            var ledger = economy.Ledger;
            int seed = System.Array.IndexOf(ledger.FaucetNames, "InitialSeed");
            Assert.GreaterOrEqual(seed, 0, "The ledger's InitialSeed faucet.");
            Assert.Greater(ledger.FaucetTotals[seed], 0u, "The InitialSeed faucet holds the starting gold.");
            long held = economy.Stalls[0].TreasuryGold + (long)crawlerGold + ledger.Totals.Escrow;
            long expected = (long)ledger.Totals.Fauceted - ledger.Totals.Sunk;
            Assert.AreEqual(expected, held, "The captured gold balances: stall + crawlers + escrow = faucets - sinks.");

            // The world
            var world = _save.World;
            Assert.IsNotNull(world.Resources, "Resource nodes.");
            Assert.Greater(world.Resources.Length, 0, "Resource nodes.");
            Assert.LessOrEqual(world.Resources.Length, Floor1Layout.Definition.ResourceNodes.Count, "No more resource nodes than the map has.");
            Assert.IsNotNull(world.Supply?.Dormant, "The regrowth queue.");
            Assert.AreEqual(256, world.ExploredSectors.Length, "The fog, one flag per sector id.");
            int stallKey = System.Array.IndexOf(world.EntityKeys, Floor1Layout.Stall);
            Assert.GreaterOrEqual(stallKey, 0, "The stall's key.");
            Assert.AreEqual(economy.Stalls[0].StallEntityId, world.EntityIds[stallKey], "The stall's key points at the stall.");

            Assert.IsNotNull(_save.Groups?.Groups, "The group registry.");
            Assert.IsNotNull(_save.Chronicle?.Events, "The chronicle.");
            Assert.Greater(_save.Chronicle.Events.Length, 0, "The chronicle has events after 600 ticks.");
            Assert.GreaterOrEqual(_save.Chronicle.Sequence, (ulong)_save.Chronicle.Events.Length, "The chronicle's sequence.");

            Debug.Log($"[SessionCaptureScenario] t{h.Tick}: {_firstBytes.Length} bytes; {_save.Crawlers.Length} crawlers ({sightings} sightings), " +
                      $"{economy.Bounties.Length} bounties, {world.Resources.Length} resource nodes, {world.Supply.Dormant.Length} dormant, " +
                      $"{_save.Groups.Groups.Length} groups, {_save.Chronicle.Events.Length} chronicle events; two captures identical");
        }
    }
}
