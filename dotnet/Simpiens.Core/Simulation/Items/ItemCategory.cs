using System;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// The groups an item belongs to, as flags: an item can be in several (a medicinal herb is a resource, a medicine and
    /// a consumable). Code asks for a group rather than naming items (the item system refactor, 2026-10-26). A flag test is
    /// free on the tick path, allocates nothing and is deterministic.
    /// </summary>
    [Flags]
    public enum ItemCategory : uint
    {
        None = 0,

        /// <summary>A raw material harvested from a node.</summary>
        Resource = 1u << 0,

        /// <summary>Used up for an effect.</summary>
        Consumable = 1u << 1,

        /// <summary>A consumable that eases hunger.</summary>
        Food = 1u << 2,

        /// <summary>A consumable that heals.</summary>
        Medicine = 1u << 3,

        /// <summary>Worn or wielded; exactly one of <see cref="Weapon"/>, <see cref="Protection"/> or <see cref="Tool"/>.</summary>
        Equipment = 1u << 4,
        Weapon = 1u << 5,
        Protection = 1u << 6,
        Tool = 1u << 7,

        /// <summary>Origin, for future rules (a pickaxe for minerals).</summary>
        Mineral = 1u << 8,
        Botanical = 1u << 9,

        /// <summary>Found only in hazard sectors.</summary>
        Hazardous = 1u << 10
    }
}
