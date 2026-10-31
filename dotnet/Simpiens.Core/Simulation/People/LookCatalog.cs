using System;
using System.Collections.Generic;

namespace Simpiens.Simulation.People
{
    /// <summary>One option of a gene: its id (its place in <see cref="LookCatalog"/>'s list, from 1), its art key and its words.</summary>
    public sealed class LookOption
    {
        public LookOption(byte id, string key, string name)
        {
            Id = id;
            Key = key;
            Name = name;
        }

        public byte Id { get; }

        /// <summary>Kebab case, as art keys are: "side-part", "tone-3".</summary>
        public string Key { get; }

        public string Name { get; }
    }

    /// <summary>An option and how likely it is, against the other options of its table.</summary>
    public readonly struct LookOdds
    {
        public LookOdds(byte option, int weight)
        {
            Option = option;
            Weight = weight;
        }

        public byte Option { get; }
        public int Weight { get; }
    }

    /// <summary>
    /// A people (Q4, "racial body types"): how likely it is, and for each gene the options it offers and their odds. Hair
    /// style and facial hair have a table per body frame. Lineages are fantasy peoples, never real-world ones (Q6). They
    /// change no play in Epic UI-Beta; lineage traits are a design of their own, later (Q5), and would be fields here, not
    /// in the save.
    /// </summary>
    public sealed class LineageDefinition
    {
        private const int Frames = 3; // 0 = any frame, then female and male
        private readonly LookOdds[][] _odds = new LookOdds[CrawlerLook.GeneCount * Frames][];

        internal LineageDefinition(byte id, string key, string name, int weight)
        {
            Id = id;
            Key = key;
            Name = name;
            Weight = weight;
        }

        public byte Id { get; }
        public string Key { get; }
        public string Name { get; }

        /// <summary>How likely a crawler is of this lineage, against the other lineages.</summary>
        public int Weight { get; }

        /// <summary>The odds of <paramref name="gene"/>'s options for a crawler of <paramref name="frame"/>: the frame's own table if it has one, else the gene's.</summary>
        public IReadOnlyList<LookOdds> Odds(LookGene gene, byte frame)
        {
            if (frame > 0 && frame < Frames)
            {
                var own = _odds[(int)gene * Frames + frame];
                if (own != null) return own;
            }
            return _odds[(int)gene * Frames] ?? Array.Empty<LookOdds>();
        }

        /// <summary>True when a crawler of this lineage can be drawn with <paramref name="option"/> for <paramref name="gene"/> (in any frame).</summary>
        public bool Offers(LookGene gene, byte option)
        {
            if (gene == LookGene.Lineage) return option == Id;
            for (int f = 0; f < Frames; f++)
            {
                var table = _odds[(int)gene * Frames + f];
                if (table == null) continue;
                for (int i = 0; i < table.Length; i++)
                {
                    if (table[i].Option == option && table[i].Weight > 0) return true;
                }
            }
            return false;
        }

        /// <summary>True when a crawler of this lineage and <paramref name="frame"/> can be drawn with <paramref name="option"/> for <paramref name="gene"/>.</summary>
        public bool Allows(LookGene gene, byte frame, byte option)
        {
            if (gene == LookGene.Lineage) return option == Id;
            var table = Odds(gene, frame);
            for (int i = 0; i < table.Count; i++)
            {
                if (table[i].Option == option && table[i].Weight > 0) return true;
            }
            return false;
        }

        /// <summary>The options of <paramref name="gene"/> by key, each at <paramref name="weights"/>' weight in order; for one body frame when <paramref name="frameKey"/> is given.</summary>
        internal LineageDefinition Offer(LookGene gene, string frameKey, params (string Key, int Weight)[] weights)
        {
            int frame = frameKey == null ? 0 : LookCatalog.IdOf(LookGene.Frame, frameKey);
            var table = new LookOdds[weights.Length];
            for (int i = 0; i < weights.Length; i++) table[i] = new LookOdds(LookCatalog.IdOf(gene, weights[i].Key), weights[i].Weight);
            _odds[(int)gene * Frames + frame] = table;
            return this;
        }

        /// <summary>Every option of <paramref name="gene"/> in the catalogue at the same weight.</summary>
        internal LineageDefinition OfferEvenly(LookGene gene)
        {
            var options = LookCatalog.Options(gene);
            var table = new LookOdds[options.Count];
            for (int i = 0; i < options.Count; i++) table[i] = new LookOdds(options[i].Id, 1);
            _odds[(int)gene * Frames] = table;
            return this;
        }

        /// <summary>
        /// One of <paramref name="gene"/>'s options at its odds for <paramref name="frame"/>, never <paramref name="exclude"/>
        /// (a crawler's second clothing colour differs from its first). 0 when the lineage offers nothing.
        /// </summary>
        internal byte Draw(LookGene gene, byte frame, ref SimRandom rng, byte exclude = 0)
        {
            var table = Odds(gene, frame);
            long total = 0;
            for (int i = 0; i < table.Count; i++)
            {
                if (table[i].Option != exclude) total += table[i].Weight;
            }
            if (total <= 0) return 0;
            long pick = (long)(rng.NextULong() % (ulong)total);
            for (int i = 0; i < table.Count; i++)
            {
                if (table[i].Option == exclude) continue;
                pick -= table[i].Weight;
                if (pick < 0) return table[i].Option;
            }
            return 0;
        }
    }

    /// <summary>
    /// Every option a look can hold, and every lineage with its odds (Epic UI-Beta B1). What a look can be doesn't vary by
    /// session, so the catalogue is static and immutable, like <see cref="Items.ItemCatalog"/>: any code reads it, on any
    /// thread. The core holds ids, keys and odds; the client's art draws the keys.
    /// <para>
    /// An option's id is its place in its list below, from 1 (0 is unset). Never reorder or remove one: add at the end,
    /// and retire one by giving it no odds in any lineage, so a saved look always means what it meant.
    /// </para>
    /// </summary>
    public static class LookCatalog
    {
        private static readonly LookOption[][] _options = BuildOptions();
        private static readonly LineageDefinition[] _lineages = BuildLineages();

        private static LookOption[][] BuildOptions()
        {
            var options = new LookOption[CrawlerLook.GeneCount][];
            options[(int)LookGene.Lineage] = List("human", "delver");
            options[(int)LookGene.Frame] = List("female", "male");
            options[(int)LookGene.Build] = List("slight", "average", "broad", "heavy");
            options[(int)LookGene.SkinTone] = List("tone-1", "tone-2", "tone-3", "tone-4", "tone-5", "tone-6", "tone-7", "tone-8", "tone-9", "tone-10", "ruddy", "ash");
            options[(int)LookGene.HairStyle] = List("shaved", "crop", "side-part", "long", "ponytail", "bun", "braids", "curls", "coils", "mohawk");
            options[(int)LookGene.HairColour] = List("black", "dark-brown", "brown", "light-brown", "auburn", "red", "blond", "grey", "white", "teal", "violet", "rose");
            options[(int)LookGene.FacialHair] = List("none", "stubble", "moustache", "beard");
            options[(int)LookGene.EyeColour] = List("brown", "hazel", "green", "blue", "grey", "amber");
            options[(int)LookGene.FaceShape] = List("round", "oval", "square", "long");
            var clothes = List("ochre", "rust", "moss", "teal", "slate", "navy", "plum", "wine", "sand", "umber", "charcoal", "cream");
            options[(int)LookGene.ClothesPrimary] = clothes;
            options[(int)LookGene.ClothesSecondary] = clothes;
            options[(int)LookGene.Outfit] = List("tunic", "jerkin", "cloak", "hood", "robe", "furs", "pauldron", "scarf");
            options[(int)LookGene.Accessory] = List("none", "earring", "circlet", "feather", "scar", "warpaint");
            return options;
        }

        /// <summary>The lineages. Adding one: its key in the Lineage list above, then its odds here.</summary>
        private static LineageDefinition[] BuildLineages()
        {
            var human = Common(new LineageDefinition(1, "human", "Human", weight: 80))
                .Offer(LookGene.Build, null, ("slight", 20), ("average", 45), ("broad", 20), ("heavy", 15))
                .Offer(LookGene.SkinTone, null, ("tone-1", 1), ("tone-2", 1), ("tone-3", 1), ("tone-4", 1), ("tone-5", 1),
                    ("tone-6", 1), ("tone-7", 1), ("tone-8", 1), ("tone-9", 1), ("tone-10", 1))
                .Offer(LookGene.HairStyle, "female", ("shaved", 3), ("crop", 8), ("side-part", 8), ("long", 18), ("ponytail", 14),
                    ("bun", 14), ("braids", 12), ("curls", 10), ("coils", 10), ("mohawk", 3))
                .Offer(LookGene.HairStyle, "male", ("shaved", 12), ("crop", 22), ("side-part", 16), ("long", 8), ("ponytail", 6),
                    ("bun", 4), ("braids", 6), ("curls", 10), ("coils", 10), ("mohawk", 6))
                .Offer(LookGene.FacialHair, "male", ("none", 40), ("stubble", 20), ("moustache", 15), ("beard", 25));

            // Short and broad, with braided beards and two skin tones of their own (working name "delvers", Q4)
            var delver = Common(new LineageDefinition(2, "delver", "Delver", weight: 20))
                .Offer(LookGene.Build, null, ("average", 30), ("broad", 50), ("heavy", 20))
                .OfferEvenly(LookGene.SkinTone)
                .Offer(LookGene.HairStyle, "female", ("shaved", 4), ("crop", 8), ("long", 14), ("ponytail", 10), ("bun", 14),
                    ("braids", 30), ("curls", 10), ("coils", 10))
                .Offer(LookGene.HairStyle, "male", ("shaved", 14), ("crop", 18), ("long", 10), ("ponytail", 8), ("bun", 6),
                    ("braids", 24), ("curls", 8), ("coils", 8), ("mohawk", 4))
                .Offer(LookGene.FacialHair, "male", ("none", 10), ("stubble", 10), ("moustache", 10), ("beard", 70));

            return new[] { human, delver };
        }

        // What every lineage shares unless it says otherwise: female and male about half each, hair colour, eyes and
        // clothes independent of skin (Q6)
        private static LineageDefinition Common(LineageDefinition lineage) => lineage
            .Offer(LookGene.Frame, null, ("female", 1), ("male", 1))
            .Offer(LookGene.HairColour, null, ("black", 20), ("dark-brown", 20), ("brown", 15), ("light-brown", 10), ("auburn", 8),
                ("red", 5), ("blond", 10), ("grey", 6), ("white", 3), ("teal", 1), ("violet", 1), ("rose", 1))
            .Offer(LookGene.FacialHair, "female", ("none", 1))
            .Offer(LookGene.EyeColour, null, ("brown", 30), ("hazel", 15), ("green", 12), ("blue", 18), ("grey", 15), ("amber", 10))
            .OfferEvenly(LookGene.FaceShape)
            .OfferEvenly(LookGene.ClothesPrimary)
            .OfferEvenly(LookGene.ClothesSecondary)
            .OfferEvenly(LookGene.Outfit)
            .Offer(LookGene.Accessory, null, ("none", 50), ("earring", 12), ("circlet", 8), ("feather", 10), ("scar", 10), ("warpaint", 10));

        private static LookOption[] List(params string[] keys)
        {
            var list = new LookOption[keys.Length];
            for (int i = 0; i < keys.Length; i++) list[i] = new LookOption((byte)(i + 1), keys[i], Words(keys[i]));
            return list;
        }

        // "side-part" -> "side part", "tone-3" -> "tone 3"
        private static string Words(string key) => key.Replace('-', ' ');

        /// <summary>Every lineage, in id order.</summary>
        public static IReadOnlyList<LineageDefinition> Lineages => _lineages;

        /// <summary>The lineage with <paramref name="id"/>, or null.</summary>
        public static LineageDefinition Lineage(byte id) => id >= 1 && id <= _lineages.Length ? _lineages[id - 1] : null;

        /// <summary>Every option of <paramref name="gene"/>, in id order.</summary>
        public static IReadOnlyList<LookOption> Options(LookGene gene) => _options[(int)gene];

        /// <summary>The option of <paramref name="gene"/> with <paramref name="id"/>, or null (0 is unset).</summary>
        public static LookOption Option(LookGene gene, byte id)
        {
            var list = _options[(int)gene];
            return id >= 1 && id <= list.Length ? list[id - 1] : null;
        }

        /// <summary>The key of <paramref name="gene"/>'s option <paramref name="id"/>: "unset" for 0, "?<id>" for an id the catalogue lacks.</summary>
        public static string KeyOf(LookGene gene, byte id)
        {
            if (id == 0) return "unset";
            return Option(gene, id)?.Key ?? "?" + id;
        }

        /// <summary>The id of <paramref name="gene"/>'s option <paramref name="key"/>; throws on a key the catalogue lacks.</summary>
        public static byte IdOf(LookGene gene, string key)
        {
            var list = _options[(int)gene];
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Key == key) return list[i].Id;
            }
            throw new ArgumentException($"The look catalogue has no {gene} option '{key}'.", nameof(key));
        }

        /// <summary>True when every gene of <paramref name="look"/> holds an option its lineage allows for its body frame.</summary>
        public static bool IsValid(in CrawlerLook look)
        {
            var lineage = Lineage(look.Lineage);
            if (lineage == null) return false;
            for (int g = 0; g < CrawlerLook.GeneCount; g++)
            {
                var gene = (LookGene)g;
                if (!lineage.Allows(gene, look.Frame, look[gene])) return false;
            }
            return look.ClothesSecondary != look.ClothesPrimary;
        }

        internal static LineageDefinition DrawLineage(ref SimRandom rng)
        {
            long total = 0;
            for (int i = 0; i < _lineages.Length; i++) total += _lineages[i].Weight;
            long pick = (long)(rng.NextULong() % (ulong)total);
            for (int i = 0; i < _lineages.Length; i++)
            {
                pick -= _lineages[i].Weight;
                if (pick < 0) return _lineages[i];
            }
            return _lineages[0];
        }
    }
}
