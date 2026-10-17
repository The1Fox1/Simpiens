using UnityEngine;
using System;
using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.UI.Zones;

namespace Simpiens.Testing.UI
{
    /// <summary>
    /// Automated domain validation suite for Epic UI-1 Phase 1 (Facility Zone & Sector Mapping).
    /// Asserts mathematical correctness of zone containment, fallback behavior, defensive null safety,
    /// and zero-allocation performance in hot paths.
    /// </summary>
    public class FacilityZoneServiceValidator
    {
        private readonly IFacilityZoneService _zoneService;
        private readonly IWorldRegistry _registry;

        public FacilityZoneServiceValidator(IFacilityZoneService zoneService = null, IWorldRegistry registry = null)
        {
            _zoneService = zoneService ?? new FacilityZoneService(Simpiens.Testing.World.TestWorlds.DevMapSectors());
            _registry = registry;
        }

        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            TestLog.Log("<color=#38bdf8><b>[FacilityZoneServiceValidator]</b> >>> Starting Epic UI-1 Domain Validation Suite <<<</color>");

            ValidateZoneBoundsContainment();
            ValidateTransitFallbackZone();
            ValidateDefensiveNullSafety();
            ValidateZeroAllocationZoneOccupancyQuery();

            TestLog.Log("<color=#10b981><b>[FacilityZoneServiceValidator]</b> >>> ALL 4 DOMAIN TESTS PASSED CLEANLY! <<<</color>");
        }

        public void ValidateZoneBoundsContainment()
        {
            // Pen 1: Center (-8, 0)
            var zone1 = _zoneService.GetZoneForPosition(new Vector2(-8f, 0f));
            if (zone1.ZoneId != 1) throw new Exception($"Zone containment failed for Pen 1! Expected Zone 1, got {zone1.ZoneId}");

            // Pen 2: Center (-4, 0)
            var zone2 = _zoneService.GetZoneForPosition(new Vector2(-4f, 0f));
            if (zone2.ZoneId != 2) throw new Exception($"Zone containment failed for Pen 2! Expected Zone 2, got {zone2.ZoneId}");

            // Pen 3: Center (0, 0)
            var zone3 = _zoneService.GetZoneForPosition(new Vector2(0f, 0f));
            if (zone3.ZoneId != 3) throw new Exception($"Zone containment failed for Pen 3! Expected Zone 3, got {zone3.ZoneId}");

            // Pen 4: Center (4, 0)
            var zone4 = _zoneService.GetZoneForPosition(new Vector2(4f, 0f));
            if (zone4.ZoneId != 4) throw new Exception($"Zone containment failed for Pen 4! Expected Zone 4, got {zone4.ZoneId}");

            // Pen 5: Center (8, 0)
            var zone5 = _zoneService.GetZoneForPosition(new Vector2(8f, 0f));
            if (zone5.ZoneId != 5) throw new Exception($"Zone containment failed for Pen 5! Expected Zone 5, got {zone5.ZoneId}");

            // Row 2 (Epic 8)
            // Pen 6: Center (-8, -9)
            var zone6 = _zoneService.GetZoneForPosition(new Vector2(-8f, -9f));
            if (zone6.ZoneId != 6) throw new Exception($"Zone containment failed for Pen 6! Expected Zone 6, got {zone6.ZoneId}");

            // Pen 7: Center (-4, -9)
            var zone7 = _zoneService.GetZoneForPosition(new Vector2(-4f, -9f));
            if (zone7.ZoneId != 7) throw new Exception($"Zone containment failed for Pen 7! Expected Zone 7, got {zone7.ZoneId}");

            // Pen 8: Center (0, -9)
            var zone8 = _zoneService.GetZoneForPosition(new Vector2(0f, -9f));
            if (zone8.ZoneId != 8) throw new Exception($"Zone containment failed for Pen 8! Expected Zone 8, got {zone8.ZoneId}");

            // Pen 9: Center (4, -9)
            var zone9 = _zoneService.GetZoneForPosition(new Vector2(4f, -9f));
            if (zone9.ZoneId != 9) throw new Exception($"Zone containment failed for Pen 9! Expected Zone 9, got {zone9.ZoneId}");

            // Pen 10: Center (8, -9)
            var zone10 = _zoneService.GetZoneForPosition(new Vector2(8f, -9f));
            if (zone10.ZoneId != 10) throw new Exception($"Zone containment failed for Pen 10! Expected Zone 10, got {zone10.ZoneId}");

            TestLog.Log("  ✔ ValidateZoneBoundsContainment passed: All 10 demonstration sectors correctly identify coordinates.");
        }

        public void ValidateTransitFallbackZone()
        {
            // Point far north of the playpens
            var transitNorth = _zoneService.GetZoneForPosition(new Vector2(0f, 15f));
            if (transitNorth.ZoneId != 0) throw new Exception($"Expected fallback Transit Zone (0) for (0, 15), got {transitNorth.ZoneId}");

            // Querying non-existent zone ID
            var invalidZone = _zoneService.GetZoneById(999);
            if (invalidZone.ZoneId != 0) throw new Exception($"Expected fallback Transit Zone (0) for ID 999, got {invalidZone.ZoneId}");

            TestLog.Log("  ✔ ValidateTransitFallbackZone passed: Out-of-bounds positions gracefully resolve to Transit Zone 0.");
        }

        public void ValidateDefensiveNullSafety()
        {
            // Test null registry
            int occ = _zoneService.GetOccupancyCount(4, null);
            if (occ != 0) throw new Exception($"Expected 0 occupancy for null registry, got {occ}");

            // Test null buffer
            _zoneService.GetAgentsInZone(4, null, null);

            var buffer = new List<WorldEntity>();
            _zoneService.GetAgentsInZone(4, null, buffer);
            if (buffer.Count != 0) throw new Exception("Expected empty buffer for null registry query");

            TestLog.Log("  ✔ ValidateDefensiveNullSafety passed: Injected null dependencies safely handled without NRE.");
        }

        public void ValidateZeroAllocationZoneOccupancyQuery()
        {
            var buffer = new List<WorldEntity>(32);

            // Warmup JIT
            _zoneService.GetOccupancyCount(4, _registry);
            _zoneService.GetAgentsInZone(4, _registry, buffer);

            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                _zoneService.GetOccupancyCount(4, _registry);
                _zoneService.GetAgentsInZone(4, _registry, buffer);
            }

            long after = GC.GetAllocatedBytesForCurrentThread();
            long allocated = after - before;

            if (allocated > 0)
            {
                throw new Exception($"Zero-allocation breach in zone query loop! Allocated {allocated} bytes over 1,000 queries.");
            }

            TestLog.Log($"  ✔ ValidateZeroAllocationZoneOccupancyQuery passed: 1,000 zone occupancy checks produced exactly 0 GC bytes.");
        }
    }
}
