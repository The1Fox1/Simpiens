using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation.World;
using Simpiens.Testing.Sessions;
using Simpiens.UI.Zones;

namespace Simpiens.Testing.World
{
    /// <summary>Shared fixtures: sector registries and zone services loaded with a known map.</summary>
    public static class TestWorlds
    {
        /// <summary>The pens-only Dev Map (sectors 1-10).</summary>
        public static SectorRegistry DevMapSectors()
        {
            var sectors = new SectorRegistry();
            sectors.Load(DevMapLayout.Definition);
            return sectors;
        }

        public static FacilityZoneService DevMapZones()
        {
            return new FacilityZoneService(DevMapSectors());
        }

        /// <summary>The open Commons yard fixture (Outpost Market, Ore Ridge, Herb Hollow).</summary>
        public static SectorRegistry CommonsYardSectors()
        {
            var sectors = new SectorRegistry();
            sectors.Load(CommonsYardFixture.Definition);
            return sectors;
        }
    }
}
