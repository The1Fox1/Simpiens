using System;
using System.Collections.Generic;
using Simpiens.Narrative;
using Simpiens.UI;
using Simpiens.UI.Zones;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Simpiens.Testing.UI
{
    /// <summary>
    /// Automated domain validation suite for Epic UI-Alpha: The Humanized Narrative Chronicle & Story Feed.
    /// Validates bounded ring buffer wrapping, strict chronological ordering, deterministic identity generation,
    /// sector query isolation, defensive null safety, and zero-allocation performance in hot paths.
    /// </summary>
    public class NarrativeChronicleValidator : IStartable
    {
        private readonly INarrativeChronicleService _chronicleService;
        private readonly IAgentIdentityService _identityService;
        private readonly INarrativeFormatter _formatter;
        private readonly IFacilityZoneService _zoneService;

        [Inject]
        public NarrativeChronicleValidator(
            INarrativeChronicleService chronicleService = null,
            IAgentIdentityService identityService = null,
            INarrativeFormatter formatter = null,
            IFacilityZoneService zoneService = null)
        {
            _chronicleService = chronicleService;
            _identityService = identityService;
            _formatter = formatter;
            _zoneService = zoneService;
        }

        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            Debug.Log("<color=#38bdf8><b>[NarrativeChronicleValidator]</b> >>> Starting Epic UI-Alpha Domain Validation Suite <<<</color>");

            ValidateRingBufferWrappingAndChronologicalOrder();
            ValidateAgentIdentityDeterminism();
            ValidateSectorEventFiltering();
            ValidateZeroAllocationQuery();
            ValidateRelativeTimeFormatting();
            ValidateDefensiveNullSafety();
            ValidateSectorPersistenceAndCapacityLimit();
            ValidateFiveMinuteRetention();

            Debug.Log("<color=#10b981><b>[NarrativeChronicleValidator]</b> >>> ALL 8 NARRATIVE TESTS PASSED CLEANLY! <<<</color>");
        }

        public void ValidateRingBufferWrappingAndChronologicalOrder()
        {
            var service = new NarrativeChronicleService(capacity: 5);
            var buffer = new List<ChronicleEvent>(8);

            // Record 8 events into a buffer of capacity 5
            for (uint i = 1; i <= 8; i++)
            {
                service.RecordEvent(new ChronicleEvent(
                    ChronicleEventType.CampfireGossip,
                    actorAId: default,
                    actorBId: default,
                    sectorId: 4,
                    simulationTick: i * 10,
                    intensity: ChronicleIntensity.Notable));
            }

            if (service.TotalEventCount != 5)
            {
                throw new Exception($"Expected ring buffer count 5 after overflow, got {service.TotalEventCount}");
            }

            service.GetAllRecentEvents(buffer, maxCount: 5);
            if (buffer.Count != 5)
            {
                throw new Exception($"Expected 5 recent events returned, got {buffer.Count}");
            }

            // Must be strictly ordered newest-to-oldest: 80, 70, 60, 50, 40
            uint expectedTick = 80;
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].SimulationTick != expectedTick)
                {
                    throw new Exception($"Chronological order violation at index {i}! Expected tick {expectedTick}, got {buffer[i].SimulationTick}");
                }
                expectedTick -= 10;
            }

            // Monotonic sequence verification: EventId of tick 80 must be 8
            if (buffer[0].EventId != 8)
            {
                throw new Exception($"Expected EventId 8 on newest event, got {buffer[0].EventId}");
            }

            Debug.Log("  ✔ ValidateRingBufferWrappingAndChronologicalOrder passed: Bounded ring buffer correctly wraps and preserves newest-first ordering.");
        }

        public void ValidateAgentIdentityDeterminism()
        {
            var identityService = new AgentIdentityService();

            var guidA = new GUID("11111111111111111111111111111111");
            var guidB = new GUID("22222222222222222222222222222222");
            var emptyGuid = default(GUID);

            string nameA1 = identityService.GetFriendlyName(guidA);
            string nameB1 = identityService.GetFriendlyName(guidB);

            // 1. Repeat calls must return exact same name
            for (int i = 0; i < 50; i++)
            {
                if (identityService.GetFriendlyName(guidA) != nameA1)
                {
                    throw new Exception($"Determinism failure for guidA! Changed at iteration {i}");
                }
                if (identityService.GetFriendlyName(guidB) != nameB1)
                {
                    throw new Exception($"Determinism failure for guidB! Changed at iteration {i}");
                }
            }

            // 2. Default GUID must return Unknown
            if (identityService.GetFriendlyName(emptyGuid) != "Unknown")
            {
                throw new Exception($"Expected 'Unknown' for default GUID, got {identityService.GetFriendlyName(emptyGuid)}");
            }

            // 3. Custom rename override
            identityService.RegisterCustomName(guidA, "Commander Shepard");
            if (identityService.GetFriendlyName(guidA) != "Commander Shepard")
            {
                throw new Exception($"Custom name override failed! Got {identityService.GetFriendlyName(guidA)}");
            }

            // 4. Formatted display name check
            string formatted = identityService.GetFormattedDisplayName(guidA, "Agent_1");
            if (formatted != "Commander Shepard [Agent_1]")
            {
                throw new Exception($"Formatted display name mismatch! Expected 'Commander Shepard [Agent_1]', got '{formatted}'");
            }

            Debug.Log("  ✔ ValidateAgentIdentityDeterminism passed: 100% deterministic naming, custom overrides, and fallback display tags verified.");
        }

        public void ValidateSectorEventFiltering()
        {
            var service = new NarrativeChronicleService(capacity: 32);
            var buffer = new List<ChronicleEvent>(16);

            // Record events in Sector 2 (Berry Orchard) and Sector 4 (Common Hearth)
            service.RecordEvent(new ChronicleEvent(ChronicleEventType.CampfireGossip, default, default, sectorId: 4, simulationTick: 100));
            service.RecordEvent(new ChronicleEvent(ChronicleEventType.ResourceYielded, default, default, sectorId: 2, simulationTick: 110));
            service.RecordEvent(new ChronicleEvent(ChronicleEventType.CampfireCompanionship, default, default, sectorId: 4, simulationTick: 120));
            service.RecordEvent(new ChronicleEvent(ChronicleEventType.ResourceClaimed, default, default, sectorId: 2, simulationTick: 130));
            service.RecordEvent(new ChronicleEvent(ChronicleEventType.AltruisticFoodShare, default, default, sectorId: 4, simulationTick: 140));

            // Query Sector 4
            service.GetSectorEvents(4, buffer, maxCount: 10);
            if (buffer.Count != 3)
            {
                throw new Exception($"Expected 3 events in Sector 4, got {buffer.Count}");
            }

            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].SectorId != 4)
                {
                    throw new Exception($"Sector leak detected! Event has SectorId {buffer[i].SectorId} instead of 4");
                }
            }

            // Newest first in Sector 4: 140, 120, 100
            if (buffer[0].SimulationTick != 140 || buffer[1].SimulationTick != 120 || buffer[2].SimulationTick != 100)
            {
                throw new Exception("Sector query failed newest-to-oldest chronological sort!");
            }

            Debug.Log("  ✔ ValidateSectorEventFiltering passed: Sector isolation verified without cross-room data leakage.");
        }

        public void ValidateZeroAllocationQuery()
        {
            var service = new NarrativeChronicleService(capacity: 64);
            var buffer = new List<ChronicleEvent>(16);

            for (uint i = 0; i < 50; i++)
            {
                service.RecordEvent(new ChronicleEvent(
                    ChronicleEventType.CampfireGossip,
                    default,
                    default,
                    sectorId: (int)(i % 5),
                    simulationTick: i * 50));
            }

            // Warmup query
            service.GetSectorEvents(4, buffer, 10);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long memoryBefore = GC.GetTotalMemory(true);

            // Execute 1,000 queries in a hot loop
            for (int i = 0; i < 1000; i++)
            {
                service.GetSectorEvents(4, buffer, 10);
            }

            long memoryAfter = GC.GetTotalMemory(false);
            long allocated = memoryAfter - memoryBefore;

            if (allocated > 0)
            {
                throw new Exception($"Hot-path zero-allocation assertion failed! Allocated {allocated} bytes across 1,000 queries.");
            }

            Debug.Log("  ✔ ValidateZeroAllocationQuery passed: 1,000 sector queries produced exactly 0 bytes of heap garbage.");
        }

        public void ValidateRelativeTimeFormatting()
        {
            // Event at tick 5000, current tick 5000 (0s delta) -> [Just now]
            string t0 = RelativeTimeFormatter.Format(5000, 5000, 50f);
            if (t0 != "[Just now]") throw new Exception($"Expected '[Just now]', got '{t0}'");

            // Event at tick 4750, current tick 5000 (5s delta) -> [Just now]
            string t5 = RelativeTimeFormatter.Format(4750, 5000, 50f);
            if (t5 != "[Just now]") throw new Exception($"Expected '[Just now]', got '{t5}'");

            // Event at tick 3750, current tick 5000 (25s delta) -> [25s ago]
            string t25 = RelativeTimeFormatter.Format(3750, 5000, 50f);
            if (t25 != "[25s ago]") throw new Exception($"Expected '[25s ago]', got '{t25}'");

            // Event at tick 2000, current tick 5000 (60s delta) -> [1m ago]
            string t60 = RelativeTimeFormatter.Format(2000, 5000, 50f);
            if (t60 != "[1m ago]") throw new Exception($"Expected '[1m ago]', got '{t60}'");

            // Event 12 minutes ago (36,000 ticks) -> [12m ago]
            string t12m = RelativeTimeFormatter.Format(0, 36000, 50f);
            if (t12m != "[12m ago]") throw new Exception($"Expected '[12m ago]', got '{t12m}'");

            Debug.Log("  ✔ ValidateRelativeTimeFormatting passed: Mathematical tick-to-human time translation verified.");
        }

        public void ValidateDefensiveNullSafety()
        {
            // 1. TemplateNarrativeFormatter with null dependencies
            var formatter = new TemplateNarrativeFormatter(null, null);
            var testEvent = new ChronicleEvent(ChronicleEventType.CampfireGossip, default, default, 99, 100);
            string formatted = formatter.FormatEvent(testEvent);
            if (string.IsNullOrEmpty(formatted))
            {
                throw new Exception("TemplateNarrativeFormatter returned null/empty with null dependencies!");
            }

            // 2. Querying with null buffer must not throw
            var service = new NarrativeChronicleService(16);
            service.GetSectorEvents(1, null);
            service.GetActorEvents(default, null);
            service.GetAllRecentEvents(null);

            // 3. Passthrough enricher with null dependencies
            var enricher = new PassthroughNarrativeEnricher(null);
            var task = enricher.EnrichStoryBeatAsync(testEvent);
            if (string.IsNullOrEmpty(task.GetAwaiter().GetResult()))
            {
                throw new Exception("PassthroughNarrativeEnricher returned empty result!");
            }

            Debug.Log("  ✔ ValidateDefensiveNullSafety passed: Headless isolation and null guards confirmed robust.");
        }

        public void ValidateSectorPersistenceAndCapacityLimit()
        {
            var service = new NarrativeChronicleService(globalCapacity: 1024, sectorCapacity: 100);
            var buffer = new List<ChronicleEvent>(128);

            // 1. Record 150 events in Sector 4 (exceeding the 100-event capacity limit)
            for (uint i = 1; i <= 150; i++)
            {
                service.RecordEvent(new ChronicleEvent(
                    ChronicleEventType.CampfireGossip,
                    actorAId: default,
                    actorBId: default,
                    sectorId: 4,
                    simulationTick: i * 10));
            }

            service.GetSectorEvents(4, buffer, maxCount: 150);
            if (buffer.Count != 100)
            {
                throw new Exception($"Expected Sector 4 count to be capped at 100, got {buffer.Count}");
            }

            // Must be newest first: tick 1500 down to tick 510
            if (buffer[0].SimulationTick != 1500)
            {
                throw new Exception($"Expected newest tick 1500, got {buffer[0].SimulationTick}");
            }
            if (buffer[99].SimulationTick != 510)
            {
                throw new Exception($"Expected oldest retained tick 510, got {buffer[99].SimulationTick}");
            }

            // 2. Record 10 events in Sector 2 (Berry Orchard)
            for (uint i = 1; i <= 10; i++)
            {
                service.RecordEvent(new ChronicleEvent(
                    ChronicleEventType.ResourceYielded,
                    actorAId: default,
                    actorBId: default,
                    sectorId: 2,
                    simulationTick: 2000 + i * 10));
            }

            // 3. Flood Sector 4 with 200 more events
            for (uint i = 1; i <= 200; i++)
            {
                service.RecordEvent(new ChronicleEvent(
                    ChronicleEventType.CampfireCompanionship,
                    actorAId: default,
                    actorBId: default,
                    sectorId: 4,
                    simulationTick: 3000 + i * 10));
            }

            // 4. Sector 2 must still retain all 10 of its events completely intact (Cross-Sector Isolation)
            service.GetSectorEvents(2, buffer, maxCount: 100);
            if (buffer.Count != 10)
            {
                throw new Exception($"Cross-sector eviction detected! Sector 2 expected 10 events, got {buffer.Count}");
            }
            if (buffer[0].SimulationTick != 2100 || buffer[9].SimulationTick != 2010)
            {
                throw new Exception("Sector 2 event data corrupted after Sector 4 flood!");
            }

            Debug.Log("  ✔ ValidateSectorPersistenceAndCapacityLimit passed: Sector 100-event cap and strict cross-sector isolation confirmed.");
        }

        public void ValidateFiveMinuteRetention()
        {
            var service = new NarrativeChronicleService(globalCapacity: 1024, sectorCapacity: 100);
            var buffer = new List<ChronicleEvent>(128);

            // At 10 ticks/second, 5 minutes = 300 seconds = 3000 ticks.
            // Record 50 events spaced across 5 minutes (every 60 ticks = 6 seconds).
            uint startTick = 1000;
            for (uint i = 0; i < 50; i++)
            {
                service.RecordEvent(new ChronicleEvent(
                    ChronicleEventType.CampfireGossip,
                    actorAId: default,
                    actorBId: default,
                    sectorId: 1,
                    simulationTick: startTick + i * 60));
            }

            service.GetSectorEvents(1, buffer, maxCount: 100);
            if (buffer.Count != 50)
            {
                throw new Exception($"Expected all 50 events within the 5-minute window to be preserved, got {buffer.Count}");
            }

            // Ensure oldest (tick 1000) and newest (tick 3940) are both retained
            if (buffer[0].SimulationTick != startTick + 49 * 60)
            {
                throw new Exception($"Newest event tick mismatch! Expected {startTick + 49 * 60}, got {buffer[0].SimulationTick}");
            }
            if (buffer[49].SimulationTick != startTick)
            {
                throw new Exception($"Oldest event tick mismatch! Expected {startTick}, got {buffer[49].SimulationTick}");
            }

            Debug.Log("  ✔ ValidateFiveMinuteRetention passed: Full 5-minute event history persisted without premature eviction.");
        }
    }
}
