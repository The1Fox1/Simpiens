using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Simpiens.Simulation.People
{
    /// <summary>A crawler's genes (Epic UI-Beta B1), in the order they are drawn. A gene added later goes last, so the genes before it keep their values on every seed.</summary>
    public enum LookGene : byte
    {
        Lineage = 0,
        Frame = 1,
        Build = 2,
        SkinTone = 3,
        HairStyle = 4,
        HairColour = 5,
        FacialHair = 6,
        EyeColour = 7,
        FaceShape = 8,
        ClothesPrimary = 9,
        ClothesSecondary = 10,
        Outfit = 11,
        Accessory = 12
    }

    /// <summary>A look's genes, one option id per byte (unmanaged, saved as raw memory); the bytes past the last gene stay 0.</summary>
    [InlineArray(CrawlerLook.Size)]
    public struct LookGenes
    {
        private byte _gene;
    }

    /// <summary>
    /// What a crawler looks like (Epic UI-Beta B1): one option id per <see cref="LookGene"/>, ids from
    /// <see cref="LookCatalog"/>. The core holds ids and odds, never colours or pixels; the client draws them. A look is
    /// drawn once at spawn from the session's seed on its own random salt, so it changes no decision, and it is saved with
    /// the crawler. 0 means unset in every gene: <c>default</c> is no look at all, which the live checks refuse.
    /// Blittable, 16 bytes: its layout is the save format.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct CrawlerLook : IEquatable<CrawlerLook>
    {
        /// <summary>Bytes in a look: the genes, then spare bytes kept at 0 for genes added later.</summary>
        public const int Size = 16;

        /// <summary>Genes in a look (<see cref="LookGene"/>'s values).</summary>
        public const int GeneCount = 13;

        private readonly LookGenes _genes;

        /// <summary>A look from its genes in <see cref="LookGene"/> order; missing genes are unset.</summary>
        public CrawlerLook(ReadOnlySpan<byte> genes)
        {
            if (genes.Length > GeneCount) throw new ArgumentException($"A look has {GeneCount} genes, not {genes.Length}.", nameof(genes));
            var copy = new LookGenes();
            genes.CopyTo(copy);
            _genes = copy;
        }

        /// <summary>The option id of <paramref name="gene"/>, 0 when unset.</summary>
        public byte this[LookGene gene] => _genes[(int)gene];

        public byte Lineage => this[LookGene.Lineage];
        public byte Frame => this[LookGene.Frame];
        public byte Build => this[LookGene.Build];
        public byte SkinTone => this[LookGene.SkinTone];
        public byte HairStyle => this[LookGene.HairStyle];
        public byte HairColour => this[LookGene.HairColour];
        public byte FacialHair => this[LookGene.FacialHair];
        public byte EyeColour => this[LookGene.EyeColour];
        public byte FaceShape => this[LookGene.FaceShape];
        public byte ClothesPrimary => this[LookGene.ClothesPrimary];
        public byte ClothesSecondary => this[LookGene.ClothesSecondary];
        public byte Outfit => this[LookGene.Outfit];
        public byte Accessory => this[LookGene.Accessory];

        /// <summary>True when every gene holds an option.</summary>
        public bool IsComplete
        {
            get
            {
                for (int g = 0; g < GeneCount; g++)
                {
                    if (_genes[g] == 0) return false;
                }
                return true;
            }
        }

        /// <summary>This look with <paramref name="gene"/> set to <paramref name="option"/> (a fixed look for a cutscene or a test).</summary>
        public CrawlerLook With(LookGene gene, byte option)
        {
            Span<byte> genes = stackalloc byte[GeneCount];
            ReadOnlySpan<byte> current = _genes;
            current.Slice(0, GeneCount).CopyTo(genes);
            genes[(int)gene] = option;
            return new CrawlerLook(genes);
        }

        /// <summary>
        /// The look of <paramref name="entityId"/> under a session seed: lineage first, then each gene from what that
        /// lineage offers, at its odds (the frame's odds for hair style and facial hair), on <see cref="SimRandomSalt.Look"/>.
        /// </summary>
        public static CrawlerLook Generate(ulong seed, EntityId entityId)
        {
            var rng = SimRandom.For(seed, entityId, 0, SimRandomSalt.Look);
            Span<byte> genes = stackalloc byte[GeneCount];
            var lineage = LookCatalog.DrawLineage(ref rng);
            genes[(int)LookGene.Lineage] = lineage.Id;
            byte frame = lineage.Draw(LookGene.Frame, 0, ref rng);
            genes[(int)LookGene.Frame] = frame;
            for (int g = (int)LookGene.Build; g < GeneCount; g++)
            {
                var gene = (LookGene)g;
                byte exclude = gene == LookGene.ClothesSecondary ? genes[(int)LookGene.ClothesPrimary] : (byte)0;
                genes[g] = lineage.Draw(gene, frame, ref rng, exclude);
            }
            return new CrawlerLook(genes);
        }

        public bool Equals(CrawlerLook other)
        {
            ReadOnlySpan<byte> a = _genes, b = other._genes;
            return a.SequenceEqual(b);
        }

        public override bool Equals(object obj) => obj is CrawlerLook other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            for (int g = 0; g < GeneCount; g++) hash.Add(_genes[g]);
            return hash.ToHashCode();
        }

        public static bool operator ==(CrawlerLook a, CrawlerLook b) => a.Equals(b);
        public static bool operator !=(CrawlerLook a, CrawlerLook b) => !a.Equals(b);

        /// <summary>The genes by option key, for logs and review sheets: "human female average tone-3 crop …".</summary>
        public override string ToString()
        {
            var parts = new string[GeneCount];
            for (int g = 0; g < GeneCount; g++) parts[g] = LookCatalog.KeyOf((LookGene)g, _genes[g]);
            return string.Join(" ", parts);
        }
    }
}
