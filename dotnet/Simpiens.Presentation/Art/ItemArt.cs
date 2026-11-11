using System.Collections.Generic;
using System.Linq;

namespace Simpiens.Presentation.Art
{
    /// <summary>Where an item shows when a crawler wears it (graphics V5): nowhere, or over the hair band, as a helmet.</summary>
    public enum Worn : byte
    {
        None = 0,
        Head = 1,
        /// <summary>Over the lower face: a respirator's mask (Epic 10 Part 2, W3, D52).</summary>
        Face = 2,
        /// <summary>Over the shirt, as a coat: hazard gear's suit, worn on the body (Epic 10 Part 3, K1.5).</summary>
        Torso = 3,
        /// <summary>In the right hand, its blade rising up and out: a weapon (Epic 10 Part 3, K3.4).</summary>
        Hand = 4,
    }

    /// <summary>How an item looks: its shape, its primary and secondary colours, and how it is worn.</summary>
    public readonly struct ItemLook
    {
        public ItemLook(string shape, Rgba32 primary, Rgba32 secondary, Worn worn = Worn.None)
        {
            Shape = shape;
            Primary = primary;
            Secondary = secondary;
            Worn = worn;
        }

        public string Shape { get; }
        public Rgba32 Primary { get; }
        public Rgba32 Secondary { get; }
        public Worn Worn { get; }
    }

    /// <summary>
    /// Every item's look, by its art key (graphics V3.6): one line an item. The sprite kit makes its menu icon, its map
    /// sprite and its carried marker from it (<see cref="ItemSprites"/>). Keyed by art key, so no item is named in code.
    /// The colours follow the nodes they come from (ore's slate and rust, herbs' teal and lilac, glowstone's amber and cyan).
    /// </summary>
    public static class ItemArt
    {
        private static readonly Dictionary<string, ItemLook> _looks = new Dictionary<string, ItemLook>(System.StringComparer.Ordinal)
        {
            ["ration"] = new ItemLook(ItemShapes.Pack, new Rgba32(194, 140, 84), new Rgba32(220, 38, 28)),
            ["iron-ore"] = new ItemLook(ItemShapes.Chunk, new Rgba32(100, 116, 139), new Rgba32(234, 88, 12)),
            ["medicinal-herb"] = new ItemLook(ItemShapes.Sprig, new Rgba32(45, 212, 191), new Rgba32(216, 180, 254)),
            ["pickaxe"] = new ItemLook(ItemShapes.Pick, new Rgba32(148, 163, 184), new Rgba32(120, 72, 32)),
            // A suit in hazard yellow with silver bands, worn on the body (Epic 10 Part 3, K1.5: a helmet until the body slot took it)
            ["hazard-gear"] = new ItemLook(ItemShapes.Vest, new Rgba32(250, 204, 21), new Rgba32(203, 213, 225), Worn.Torso),
            ["glowstone"] = new ItemLook(ItemShapes.Chunk, new Rgba32(251, 191, 36), new Rgba32(103, 232, 249)),
            ["healing-draught"] = new ItemLook(ItemShapes.Flask, new Rgba32(225, 29, 72), new Rgba32(161, 112, 66)), // ruby in glass, a cork
            // Protection by kind (Epic 10 Part 2, W3, D52): the respirator a mask over the mouth, its filters teal; the hard hat
            // a steel helm on the head
            ["respirator"] = new ItemLook(ItemShapes.Mask, new Rgba32(71, 85, 105), new Rgba32(45, 212, 191), Worn.Face),
            ["hard-hat"] = new ItemLook(ItemShapes.Helmet, new Rgba32(148, 163, 184), new Rgba32(30, 41, 59), Worn.Head),
            // Melee arms and armour (Epic 10 Part 3, K3.1, K3.2): the cudgel's oak and iron studs; the blade's steel and its
            // leather grip, the edge's glowstone amber on a dark grip, each held in the hand (K3.4); the helm dark iron with a
            // black visor; the ring coat steel
            ["cudgel"] = new ItemLook(ItemShapes.Club, new Rgba32(146, 98, 56), new Rgba32(120, 128, 140), Worn.Hand),
            ["iron-blade"] = new ItemLook(ItemShapes.Blade, new Rgba32(203, 213, 225), new Rgba32(120, 72, 32), Worn.Hand),
            ["glowstone-edge"] = new ItemLook(ItemShapes.Blade, new Rgba32(251, 191, 36), new Rgba32(51, 65, 85), Worn.Hand),
            ["iron-helm"] = new ItemLook(ItemShapes.Helmet, new Rgba32(100, 110, 124), new Rgba32(15, 18, 24), Worn.Head),
            ["ring-coat"] = new ItemLook(ItemShapes.Mail, new Rgba32(160, 170, 182), new Rgba32(84, 92, 104), Worn.Torso),
        };

        public static bool TryGet(string artKey, out ItemLook look) => _looks.TryGetValue(artKey ?? string.Empty, out look);

        /// <summary>Where <paramref name="item"/> shows when worn, by its art key; None for an item with no look, or one that doesn't show.</summary>
        public static Worn WornOf(Simpiens.Simulation.Economy.ItemType item)
            => item != Simpiens.Simulation.Economy.ItemType.None && TryGet(Simpiens.Simulation.Items.ItemCatalog.Get(item)?.ArtKey, out var look) ? look.Worn : Worn.None;

        /// <summary>Every look, by art key in ordinal order.</summary>
        public static IEnumerable<KeyValuePair<string, ItemLook>> All => _looks.OrderBy(p => p.Key, System.StringComparer.Ordinal);
    }
}
