using Simpiens.Simulation.World;

namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// What a bounty may target (Epic MAP Phase 4). The simulation enforces it when a bounty is registered (player
    /// posts and seeded bounties alike, before any escrow); the bounty board uses the same rule to explain a refusal.
    /// A future multiplayer server rejects the same commands.
    /// </summary>
    public static class BountyRules
    {
        public enum TargetCheck : byte
        {
            Ok = 0,
            /// <summary>No such sector on the loaded map.</summary>
            UnknownSector = 1,
            /// <summary>A Scout bounty on a sector that is already charted: there is nothing left to report.</summary>
            AlreadyCharted = 2
        }

        /// <summary>
        /// Checks a bounty's target sector against the loaded map. A Gather bounty may target an uncharted sector: it
        /// becomes claimable once a scout charts it. Without a registry or a loaded map (headless validators) nothing
        /// is checked.
        /// </summary>
        public static TargetCheck CheckTarget(BountyType type, int sectorId, ISectorRegistry sectors)
        {
            if (sectors == null || sectors.Map == null) return TargetCheck.Ok;
            if (!sectors.TryGetSector(sectorId, out _)) return TargetCheck.UnknownSector;
            if (type == BountyType.ScoutSector && sectors.IsExplored(sectorId)) return TargetCheck.AlreadyCharted;
            return TargetCheck.Ok;
        }

        /// <summary>A player-facing reason for a refusal (allocates; call only on the refusal path).</summary>
        public static string Describe(TargetCheck check, int sectorId, ISectorRegistry sectors)
        {
            string name = sectors != null && sectors.TryGetSector(sectorId, out var sector) ? sector.Name : $"Sector {sectorId}";
            switch (check)
            {
                case TargetCheck.UnknownSector: return $"There is no sector {sectorId} on this map.";
                case TargetCheck.AlreadyCharted: return $"{name} is already charted: there is nothing left to scout.";
                default: return string.Empty;
            }
        }
    }
}
