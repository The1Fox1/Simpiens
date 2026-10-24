namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// Byte-backed enumeration classifying all portable items in the simulation.
    /// </summary>
    public enum ItemType : byte
    {
        None          = 0,
        Ration        = 1, // Edible provisions; satisfies agent Hunger
        IronOre       = 2, // Raw mineral salvage harvested from deep nodes; sold to outpost
        MedicinalHerb = 3, // Biological salvage; sold to outpost or used for comfort
        ToolPickaxe   = 4, // Harvesting implement; enhances harvest yield
        BasicWeapon   = 5, // Equipped for hazard defense
        Glowstone     = 6  // Rare salvage, found only in hazard sectors; the caravan pays a premium (Epic 10 G2; placeholder name)
    }
}
