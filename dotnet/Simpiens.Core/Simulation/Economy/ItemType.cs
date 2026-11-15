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
        ToolPickaxe   = 4, // Mining tool, made at the forge (Epic 10 Part 2, W1)
        HazardGear    = 5, // Worn protection: cuts hazard damage and wears out (Epic 10 G3; placeholder name; Epic 9 called it BasicWeapon)
        Glowstone     = 6, // Rare salvage, found only in hazard sectors; the caravan pays a premium (Epic 10 G2; placeholder name)
        HealingDraught = 7, // Made at the still from herbs; heals at once, carried into danger (Epic 10 Part 2, W2; placeholder name)
        Respirator    = 8, // Worn protection against the air hazards, made at the forge (Epic 10 Part 2, W3; placeholder name)
        HardHat       = 9, // Worn protection against falling rock, made at the forge (Epic 10 Part 2, W3; placeholder name)
        Cudgel        = 10, // A melee weapon the caravan supplies cheaply (Epic 10 Part 3, K3.1, Q10; placeholder name)
        IronBlade     = 11, // A melee weapon made at the forge from ore (K3.1; placeholder name)
        GlowstoneEdge = 12, // The best melee weapon, made at the forge from ore and glowstone (K3.1; placeholder name)
        IronHelm      = 13, // Armour for the head, made at the forge from ore (K3.2, Q11; placeholder name)
        RingCoat      = 14, // Armour for the body, made at the forge from ore (K3.2; placeholder name)
        Hide          = 15, // A rat's hide, dropped at the kill (K4.2, Q12; placeholder name)
        Chitin        = 16, // A beetle's shell plates, dropped at the kill (K4.2; placeholder name)
        LeatherJerkin = 17, // Armour for the body, made at the forge from hides (K4.4; placeholder name)
        ChitinPlate   = 18, // Armour for the body, made at the forge from chitin and ore (K4.4; placeholder name)
        FangKnife     = 19, // A rare weapon a rat now and then drops (K4.2, K4.4; placeholder name)
        StoneheartCoat = 20, // A rare coat a beetle now and then drops (K4.2, K4.4; placeholder name)
        Sling         = 21, // A ranged weapon the caravan supplies cheaply (Epic 10 Part 3, K8.1, Q15; placeholder name)
        Crossbow      = 22, // A ranged weapon made at the forge from ore and a hide (K8.1, Q15; placeholder name)
        MatriarchPelt = 23, // The Matriarch's trophy, sure at her slaying (Epic 10 Part 3, K10, Q12; placeholder name)
        KeeperStone   = 24 // The Gatekeeper's trophy, sure at its slaying (K10, Q12; placeholder name)
    }
}
