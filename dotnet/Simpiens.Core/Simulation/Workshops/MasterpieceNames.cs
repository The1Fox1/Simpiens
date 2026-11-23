using System.Collections.Generic;
using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Workshops
{
    /// <summary>What a piece is, for its name's noun (Epic 11, S8.2, D42): a blade, a bow, a tool, a coat, a helm.</summary>
    public enum MasterpieceKind : byte
    {
        Blade = 0,
        Bow = 1,
        Tool = 2,
        Coat = 3,
        Helm = 4
    }

    /// <summary>
    /// The words of masterpieces' names (Epic 11, S8.2, D42): adjectives for "the {adjective} {noun}", and nouns by what the
    /// piece is. A record holds the words' indices, so a list only grows at its end and no word is reordered or removed
    /// (a retired word keeps its place). Latin-1 only: the client's fonts hold no more.
    /// </summary>
    public static class MasterpieceNames
    {
        /// <summary>The longest a word may be, so "{a friend}'s {noun}" and "the {adjective} {noun}" fit the dossier's gear line.</summary>
        public const int MaxWordLength = 9;

        public static readonly IReadOnlyList<string> Adjectives = new[]
        {
            "Quiet", "Patient", "Ember", "Hollow", "Last", "Bright", "Grey", "Long", "Deep", "Iron",
            "True", "Faithful", "Lantern", "Silent", "Stubborn", "Low", "Sleepless", "Ashen"
        };

        private static readonly string[][] Nouns =
        {
            new[] { "Fang", "Edge", "Thorn", "Oath", "Promise", "Answer", "Mercy", "Tooth" }, // a blade
            new[] { "Reach", "Whisper", "Sting", "Word", "Errand", "Patience" },              // a bow
            new[] { "Delving", "Seeker", "Knock", "Vein", "Hearth", "Labour" },           // a tool
            new[] { "Shell", "Bulwark", "Ward", "Keep", "Coat", "Vow" },                      // a coat
            new[] { "Crown", "Brow", "Watch", "Lid", "Lantern", "Vigil" }                     // a helm
        };

        /// <summary>The nouns for <paramref name="kind"/>.</summary>
        public static IReadOnlyList<string> NounsFor(MasterpieceKind kind) => Nouns[(int)kind <= Nouns.Length - 1 ? (int)kind : 0];

        /// <summary>
        /// What a piece is, from its groups and where it is worn (never its name or a slot's): a weapon with a range is a bow,
        /// without one a blade; a tool; protection worn on the head a helm, elsewhere a coat.
        /// </summary>
        public static MasterpieceKind KindOf(Simpiens.Simulation.Economy.ItemType item)
        {
            var equipment = ItemCatalog.Get(item)?.Equipment;
            if (equipment == null) return MasterpieceKind.Blade;
            if (ItemCatalog.Is(item, ItemCategory.Weapon)) return HasRange(equipment) ? MasterpieceKind.Bow : MasterpieceKind.Blade;
            if (ItemCatalog.Is(item, ItemCategory.Tool)) return MasterpieceKind.Tool;
            var slots = EquipmentSlots.All;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Fits(item)) return slots[i].Region == BodyRegion.Head ? MasterpieceKind.Helm : MasterpieceKind.Coat;
            }
            return MasterpieceKind.Coat;
        }

        private static bool HasRange(EquipmentBlock equipment)
        {
            for (int i = 0; i < equipment.ModifierCount; i++) if (equipment.GetModifier(i).Stat == StatKind.Range) return true;
            return false;
        }

        /// <summary>
        /// A masterpiece's name from its record: "the Quiet Fang", or, made in grief, "Ama's Oath" (<paramref name="nameOf"/>
        /// gives a crawler's given name, the departed's too).
        /// </summary>
        public static string NameOf(in MasterpieceRecord record, System.Func<EntityId, string> nameOf)
            => NameOf(record.Item, record.Form, record.Adjective, record.Noun, record.Form == MasterpieceForm.Grief ? nameOf?.Invoke(record.NamedFor) : null);

        /// <summary>A masterpiece's name from its parts; a grief's name needs <paramref name="friend"/>, and without it reads plain.</summary>
        public static string NameOf(Simpiens.Simulation.Economy.ItemType item, MasterpieceForm form, byte adjective, byte noun, string friend)
        {
            var nouns = NounsFor(KindOf(item));
            string word = nouns[noun < nouns.Count ? noun : 0];
            if (form == MasterpieceForm.Grief && !string.IsNullOrEmpty(friend)) return $"{friend}'s {word}";
            return $"the {Adjectives[adjective < Adjectives.Count ? adjective : 0]} {word}";
        }

        /// <summary>
        /// A name's parts packed for the chronicle's extra (a float, exact to 2^24): the item, then the adjective (under 32),
        /// the noun (under 16) and the form.
        /// </summary>
        public static int Pack(in MasterpieceRecord record) => (byte)record.Item + 256 * (record.Adjective & 31) + 8192 * (record.Noun & 15) + 131072 * ((byte)record.Form & 1);

        /// <summary>The parts <see cref="Pack"/> packed.</summary>
        public static void Unpack(int packed, out Simpiens.Simulation.Economy.ItemType item, out MasterpieceForm form, out byte adjective, out byte noun)
        {
            item = (Simpiens.Simulation.Economy.ItemType)(packed & 255);
            adjective = (byte)((packed >> 8) & 31);
            noun = (byte)((packed >> 13) & 15);
            form = (MasterpieceForm)((packed >> 17) & 1);
        }
    }
}
