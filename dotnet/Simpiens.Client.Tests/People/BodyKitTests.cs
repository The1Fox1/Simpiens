using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Entities;
using Simpiens.Presentation;
using Simpiens.Presentation.Art.People;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.People;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// The body kit (Epic UI-Beta B2): the same look bakes the same pixels; every option the map shows changes the body
    /// and the genes it doesn't show change nothing; the overlays never cover the marks over the head or the tool; the
    /// helm, the tabard and the sleeper's blanket are where D4, D5 and D8 put them; a bake into a buffer allocates nothing.
    /// </summary>
    public class BodyKitTests
    {
        private static readonly CrawlerLook Basis = CrawlerLook.Generate(PeopleSheetTests.CrowdSeed, new EntityId(1u, 0u, 0u, 0u))
            .With(LookGene.Lineage, LookCatalog.IdOf(LookGene.Lineage, "human"))
            .With(LookGene.Frame, LookCatalog.IdOf(LookGene.Frame, "male"))
            .With(LookGene.Build, LookCatalog.IdOf(LookGene.Build, "average"))
            .With(LookGene.HairStyle, LookCatalog.IdOf(LookGene.HairStyle, "crop"))
            .With(LookGene.FacialHair, LookCatalog.IdOf(LookGene.FacialHair, "none"));

        private static readonly char[] Marks = { 'W', 'T', '!', '?', 'Z' };

        [Fact]
        public void TheSameLookBakesTheSamePixels()
        {
            var a = Bake(Basis);
            var b = Bake(Basis);
            Assert.True(a.AsSpan().SequenceEqual(b));
            for (uint i = 0; i < 16; i++)
            {
                var look = CrawlerLook.Generate(PeopleSheetTests.CrowdSeed, new EntityId(i + 1u, 0u, 0u, 0u));
                Assert.True(Bake(look, ItemType.HazardGear, tabard: true).AsSpan().SequenceEqual(Bake(look, ItemType.HazardGear, tabard: true)));
            }
        }

        [Theory]
        [InlineData(LookGene.Lineage)]
        [InlineData(LookGene.Build)]
        [InlineData(LookGene.SkinTone)]
        [InlineData(LookGene.HairStyle)]
        [InlineData(LookGene.HairColour)]
        [InlineData(LookGene.FacialHair)]
        [InlineData(LookGene.ClothesPrimary)]
        [InlineData(LookGene.ClothesSecondary)]
        public void EveryOptionTheMapShowsChangesTheBody(LookGene gene)
        {
            var bodies = new Dictionary<string, string>();
            foreach (var option in LookCatalog.Options(gene))
            {
                var look = Basis.With(gene, option.Id);
                if (gene == LookGene.ClothesSecondary && look.ClothesSecondary == look.ClothesPrimary) continue;
                if (gene == LookGene.ClothesPrimary && look.ClothesSecondary == look.ClothesPrimary) look = look.With(LookGene.ClothesSecondary, look.ClothesPrimary == 1 ? (byte)2 : (byte)1);
                string pixels = string.Join(",", Bake(look));
                Assert.False(bodies.ContainsValue(pixels), $"{gene} {option.Key} bakes the same body as {bodies.FirstOrDefault(p => p.Value == pixels).Key}.");
                bodies[option.Key] = pixels;
            }
        }

        [Theory]
        [InlineData(LookGene.Frame)]
        [InlineData(LookGene.EyeColour)]
        [InlineData(LookGene.FaceShape)]
        [InlineData(LookGene.Outfit)]
        [InlineData(LookGene.Accessory)]
        public void GenesTheMapDoesntShowChangeNothing(LookGene gene)
        {
            // §3.1: the frame shows only through its odds; eyes, face, outfit and accessory are too small at 16 pixels
            var basis = Bake(Basis);
            foreach (var option in LookCatalog.Options(gene)) Assert.True(Bake(Basis.With(gene, option.Id)).AsSpan().SequenceEqual(basis), $"{gene} {option.Key} changed the body.");
        }

        [Fact]
        public void NoOverlayCoversTheMarksOrTheTool()
        {
            foreach (var look in EveryShape())
            {
                foreach (var gear in new[] { ItemType.None, ItemType.HazardGear })
                {
                    var body = Bake(look, gear, tabard: true);
                    foreach (AgentVisualState state in Enum.GetValues<AgentVisualState>())
                    {
                        for (int f = 0; f < 2; f++)
                        {
                            var keys = ProceduralSprites.CrawlerKeys(state, f);
                            var frame = body.AsSpan(BodyKit.FrameIndex(state, f) * BodyKit.PixelsPerFrame, BodyKit.PixelsPerFrame);
                            foreach (char mark in Marks)
                            {
                                int drawn = keys.Sum(row => row.Count(c => c == mark));
                                var colour = ProceduralSprites.CrawlerKeyColour(mark);
                                int baked = 0;
                                foreach (var p in frame) if (p == colour) baked++;
                                Assert.True(baked >= drawn, $"{Describe(look)} {gear} {state} {f}: '{mark}' drawn {drawn} times, baked {baked}.");
                            }
                        }
                    }
                }
            }
        }

        [Fact]
        public void TheHelmCoversTheCrownInEveryPose()
        {
            ItemArtLook(out var helm, out var lamp);
            foreach (var style in LookCatalog.Options(LookGene.HairStyle))
            {
                var body = Bake(Basis.With(LookGene.HairStyle, style.Id), ItemType.HazardGear, tabard: false);
                foreach (AgentVisualState state in Enum.GetValues<AgentVisualState>())
                {
                    for (int f = 0; f < 2; f++)
                    {
                        var head = HeadAnchors.Of(state, f);
                        var frame = body.AsSpan(BodyKit.FrameIndex(state, f) * BodyKit.PixelsPerFrame, BodyKit.PixelsPerFrame);
                        for (int x = HeadAnchor.Left; x <= HeadAnchor.Right; x++)
                        {
                            var p = frame[head.HairTop * BodyKit.Size + x];
                            Assert.True(p == helm || p == lamp, $"{style.Key} {state} {f}: ({x}, {head.HairTop}) isn't the helm.");
                        }
                    }
                }
            }
        }

        [Fact]
        public void TheTabardIsWornAwakeAndTheBlanketKeptAsleep()
        {
            foreach (var look in EveryShape())
            {
                var body = Bake(look, ItemType.None, tabard: true);
                foreach (AgentVisualState state in Enum.GetValues<AgentVisualState>())
                {
                    for (int f = 0; f < 2; f++)
                    {
                        var frame = body.AsSpan(BodyKit.FrameIndex(state, f) * BodyKit.PixelsPerFrame, BodyKit.PixelsPerFrame);
                        int tabard = 0;
                        foreach (var p in frame) if (p == BodyKit.TabardColour || p == BodyKit.TabardDevice) tabard++;
                        if (state == AgentVisualState.Sleeping)
                        {
                            Assert.Equal(0, tabard);
                            // D5: the blanket is the bunk's, whatever the look
                            var today = ProceduralSprites.GetAgentSprite(state, f).Pixels;
                            var head = HeadAnchors.Of(state, f);
                            for (int i = (head.Chin + 2) * BodyKit.Size; i < BodyKit.PixelsPerFrame; i++) Assert.Equal(today[i], frame[i]);
                        }
                        else Assert.True(tabard >= 2, $"{Describe(look)} {state} {f}: no tabard."); // a beard may cover all but its device
                    }
                }
            }
        }

        [Fact]
        public void ABakeIntoABufferAllocatesNothing()
        {
            var into = new Rgba32[BodyKit.PixelsPerBody];
            BodyKit.Bake(Basis, ItemType.HazardGear, true, into); // warm
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++) BodyKit.Bake(Basis, ItemType.HazardGear, i % 2 == 0, into);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        // Every hair style, build and lineage, and every facial hair, at least once
        private static IEnumerable<CrawlerLook> EveryShape()
        {
            foreach (var lineage in LookCatalog.Options(LookGene.Lineage))
                foreach (var build in LookCatalog.Options(LookGene.Build))
                    foreach (var style in LookCatalog.Options(LookGene.HairStyle))
                    {
                        var facial = LookCatalog.Options(LookGene.FacialHair);
                        yield return Basis.With(LookGene.Lineage, lineage.Id).With(LookGene.Build, build.Id).With(LookGene.HairStyle, style.Id)
                            .With(LookGene.FacialHair, facial[(style.Id + build.Id) % facial.Count].Id);
                    }
        }

        private static string Describe(in CrawlerLook look) => string.Join(" ", LookCatalog.KeyOf(LookGene.Lineage, look.Lineage), LookCatalog.KeyOf(LookGene.Build, look.Build),
            LookCatalog.KeyOf(LookGene.HairStyle, look.HairStyle), LookCatalog.KeyOf(LookGene.FacialHair, look.FacialHair));

        private static void ItemArtLook(out Rgba32 helm, out Rgba32 lamp)
        {
            Assert.True(Simpiens.Presentation.Art.ItemArt.TryGet(Simpiens.Simulation.Items.ItemCatalog.Get(ItemType.HazardGear).ArtKey, out var look));
            helm = look.Primary;
            lamp = look.Secondary;
        }

        private static Rgba32[] Bake(in CrawlerLook look, ItemType gear = ItemType.None, bool tabard = false)
        {
            var pixels = new Rgba32[BodyKit.PixelsPerBody];
            BodyKit.Bake(look, gear, tabard, pixels);
            return pixels;
        }
    }
}
