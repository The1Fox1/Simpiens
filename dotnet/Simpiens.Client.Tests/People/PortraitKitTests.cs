using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Presentation;
using Simpiens.Presentation.Art.People;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.People;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// The portrait kit (Epic UI-Beta B3): the same key bakes the same pixels; every option of every gene changes the
    /// portrait (the portrait shows them all, where the map body shows some); every mood is its own face; the frame says
    /// the standing; the helm and the tabard show; a bake into a buffer allocates nothing.
    /// </summary>
    public class PortraitKitTests
    {
        private static readonly CrawlerLook Basis = CrawlerLook.Generate(PeopleSheetTests.CrowdSeed, new EntityId(3u, 0u, 0u, 0u))
            .With(LookGene.Lineage, LookCatalog.IdOf(LookGene.Lineage, "human"))
            .With(LookGene.Frame, LookCatalog.IdOf(LookGene.Frame, "male"))
            .With(LookGene.HairStyle, LookCatalog.IdOf(LookGene.HairStyle, "crop"))
            .With(LookGene.FacialHair, LookCatalog.IdOf(LookGene.FacialHair, "none"))
            .With(LookGene.Outfit, LookCatalog.IdOf(LookGene.Outfit, "tunic"))
            .With(LookGene.Accessory, LookCatalog.IdOf(LookGene.Accessory, "none"));

        [Fact]
        public void TheSameKeyBakesTheSamePixels()
        {
            for (uint i = 0; i < 16; i++)
            {
                var key = new PortraitKey(CrawlerLook.Generate(PeopleSheetTests.CrowdSeed, new EntityId(i + 1u, 0u, 0u, 0u)), (Mood)(i % 7), (CrawlerStanding)(i % 3), i % 2 == 0 ? ItemType.HazardGear : ItemType.None);
                Assert.True(Framed(key).AsSpan().SequenceEqual(Framed(key)));
            }
        }

        [Theory]
        [InlineData(LookGene.Lineage)]
        [InlineData(LookGene.Frame)]
        [InlineData(LookGene.Build)]
        [InlineData(LookGene.SkinTone)]
        [InlineData(LookGene.HairStyle)]
        [InlineData(LookGene.HairColour)]
        [InlineData(LookGene.FacialHair)]
        [InlineData(LookGene.EyeColour)]
        [InlineData(LookGene.FaceShape)]
        [InlineData(LookGene.ClothesPrimary)]
        [InlineData(LookGene.ClothesSecondary)]
        [InlineData(LookGene.Outfit)]
        [InlineData(LookGene.Accessory)]
        public void EveryOptionChangesThePortrait(LookGene gene)
        {
            var seen = new Dictionary<string, string>();
            foreach (var option in LookCatalog.Options(gene))
            {
                var look = Basis.With(gene, option.Id);
                if (look.ClothesPrimary == look.ClothesSecondary) look = look.With(gene == LookGene.ClothesPrimary ? LookGene.ClothesSecondary : LookGene.ClothesPrimary, option.Id == 1 ? (byte)2 : (byte)1);
                string pixels = string.Join(",", Framed(new PortraitKey(look, Mood.Content, CrawlerStanding.Resident, ItemType.None)));
                Assert.False(seen.ContainsValue(pixels), $"{gene} {option.Key} draws the same portrait as {seen.FirstOrDefault(p => p.Value == pixels).Key}.");
                seen[option.Key] = pixels;
            }
        }

        [Fact]
        public void EveryMoodIsItsOwnFaceAndEveryStandingItsOwnFrame()
        {
            var faces = Enum.GetValues<Mood>().Select(m => string.Join(",", Framed(new PortraitKey(Basis, m, CrawlerStanding.Resident, ItemType.None)))).ToList();
            Assert.Equal(faces.Count, faces.Distinct().Count());
            var frames = Enum.GetValues<CrawlerStanding>().Select(s => PortraitKit.FrameOf(s)).ToList();
            Assert.Equal(frames.Count, frames.Distinct().Count());
            foreach (var standing in Enum.GetValues<CrawlerStanding>())
                Assert.Equal(PortraitKit.FrameOf(standing).Outer, Framed(new PortraitKey(Basis, Mood.Content, standing, ItemType.None))[0]);
        }

        [Fact]
        public void TheHelmAndTheTabardShow()
        {
            Assert.True(Simpiens.Presentation.Art.ItemArt.TryGet(Simpiens.Simulation.Items.ItemCatalog.Get(ItemType.HazardGear).ArtKey, out var helm));
            var bare = Framed(new PortraitKey(Basis, Mood.Content, CrawlerStanding.Resident, ItemType.None));
            var helmed = Framed(new PortraitKey(Basis, Mood.Content, CrawlerStanding.Resident, ItemType.HazardGear));
            Assert.Contains(helm.Secondary, helmed); // the lamp
            Assert.DoesNotContain(helm.Secondary, bare);
            var member = Framed(new PortraitKey(Basis, Mood.Content, CrawlerStanding.GuildMember, ItemType.None));
            var tabard = Simpiens.Presentation.Art.SpriteKit.RampOf(BodyKit.TabardColour);
            Assert.True(member.Count(p => p == tabard.Base || p == tabard.Shade) >= 20, "No tabard on a guild member's chest.");
            Assert.True(bare.Count(p => p == tabard.Base || p == tabard.Shade) == 0, "A tabard on a resident.");
        }

        [Fact]
        public void ABakeIntoABufferAllocatesNothing()
        {
            var into = new Rgba32[PortraitKit.FramedSize * PortraitKit.FramedSize];
            var key = new PortraitKey(Basis, Mood.Scared, CrawlerStanding.GuildMember, ItemType.HazardGear);
            PortraitKit.BakeFramed(key, into); // warm
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++) PortraitKit.BakeFramed(key, into);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        private static Rgba32[] Framed(in PortraitKey key)
        {
            var pixels = new Rgba32[PortraitKit.FramedSize * PortraitKit.FramedSize];
            PortraitKit.BakeFramed(key, pixels);
            return pixels;
        }
    }
}
