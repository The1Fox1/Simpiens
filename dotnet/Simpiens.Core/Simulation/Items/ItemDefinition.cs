using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// What an item is, the same in every session: its groups, rarity, names and stack size (the item system refactor, 2026-10-26
    /// §3.3). How a session trades it (base value, the stall's and the caravan's terms) is the session's economy
    /// (EconomyTuning). Immutable: off-thread cognition reads it freely.
    /// </summary>
    public sealed class ItemDefinition
    {
        private readonly ConsumeEffect[] _consume;

        public ItemDefinition(ItemType id, string name, string displayName, ItemCategory categories, ItemRarity rarity,
            ushort maxStack = 99, string artKey = null, ConsumeEffect[] consume = null, EquipmentBlock equipment = null)
        {
            Equipment = equipment;
            _consume = consume != null ? (ConsumeEffect[])consume.Clone() : System.Array.Empty<ConsumeEffect>();
            Id = id;
            Name = name;
            DisplayName = displayName;
            Categories = categories;
            Rarity = rarity;
            MaxStack = maxStack;
            ArtKey = artKey ?? KebabCase(name);
        }

        public ItemType Id { get; }

        /// <summary>A code name, for logs: "Glowstone".</summary>
        public string Name { get; }

        /// <summary>The player's name: "Glowstone".</summary>
        public string DisplayName { get; }

        public ItemCategory Categories { get; }
        public ItemRarity Rarity { get; }

        /// <summary>Units in one pack slot.</summary>
        public ushort MaxStack { get; }

        /// <summary>
        /// The item's art key (graphics V3.4): a short, stable id the client finds its pictures by (its menu icon, its
        /// map sprite, the node that yields it). Presentation data, the same in every session; the name in kebab case
        /// unless the definition says otherwise ("IronOre": "iron-ore").
        /// </summary>
        public string ArtKey { get; }

        /// <summary>Its slot, stat modifiers and life when it is equipment (§3.7); null otherwise.</summary>
        public EquipmentBlock Equipment { get; }

        /// <summary>What consuming it does, in order; none for an item that isn't consumed (§3.6).</summary>
        public int ConsumeEffectCount => _consume.Length;
        public ConsumeEffect GetConsumeEffect(int index) => _consume[index];

        /// <summary>True when the item is in every group of <paramref name="categories"/>.</summary>
        public bool Is(ItemCategory categories) => (Categories & categories) == categories;

        public override string ToString() => Name;

        /// <summary>"IronOre" to "iron-ore": a dash before each capital after the first, all lower case.</summary>
        public static string KebabCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var text = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > 0) text.Append('-');
                text.Append(char.ToLowerInvariant(c));
            }
            return text.ToString();
        }
    }
}
