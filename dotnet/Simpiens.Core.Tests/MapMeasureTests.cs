using Simpiens.Core.Session.Layouts;
using Simpiens.Testing.World;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Floor 1's walkable ground, measured (Epic PLACES plan 1, P.1): its walkable cells, each zone's, the doors and the walks
    /// from the hub. Explicit: the baseline in docs/places-model.md, re-run after a step that changes walls or furniture.
    /// </summary>
    public sealed class MapMeasureTests
    {
        [Fact(Explicit = true)]
        public void MeasureTheMap()
        {
            var report = MapMeasure.Measure(Floor1Layout.Definition, Floor1Layout.HubSectorId);
            TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());
        }

        /// <summary>Every zone of Floor 1 can be walked to from the hub.</summary>
        [Fact]
        public void EveryZoneIsWalkedToFromTheHub()
        {
            var report = MapMeasure.Measure(Floor1Layout.Definition, Floor1Layout.HubSectorId);
            foreach (var zone in report.Zones)
                if (zone.Id != 0) Assert.True(zone.Walk >= 0f, $"{zone.Name} can be walked to from the hub.");
        }
    }
}
