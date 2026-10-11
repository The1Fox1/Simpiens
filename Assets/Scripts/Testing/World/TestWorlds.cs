using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation.World;
using Simpiens.UI.Zones;

namespace Simpiens.Testing.World
{
    /// <summary>Shared fixtures: sector registries and zone services loaded with a known map.</summary>
    public static class TestWorlds
    {
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
    }
}
