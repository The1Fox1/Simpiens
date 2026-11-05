using System;
using System.Collections.Generic;
using Simpiens.Presentation;
using Simpiens.Presentation.Art.People;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.People;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// The portraits the art pins hold (Epic UI-Beta B3, D10), framed, by name: every option of every gene on a base look,
    /// every mood, every standing's frame, the helm, and the people sheet's crowd of 64, content. A changed portrait fails
    /// by name (<see cref="ArtPinTests"/>).
    /// </summary>
    internal static class PortraitPins
    {
        private static readonly CrawlerLook Basis = CrawlerLook.Generate(1, new EntityId(1u, 0u, 0u, 0u))
            .With(LookGene.Lineage, LookCatalog.IdOf(LookGene.Lineage, "human"))
            .With(LookGene.Frame, LookCatalog.IdOf(LookGene.Frame, "male"))
            .With(LookGene.HairStyle, LookCatalog.IdOf(LookGene.HairStyle, "crop"))
            .With(LookGene.FacialHair, LookCatalog.IdOf(LookGene.FacialHair, "none"))
            .With(LookGene.Outfit, LookCatalog.IdOf(LookGene.Outfit, "tunic"))
            .With(LookGene.Accessory, LookCatalog.IdOf(LookGene.Accessory, "none"));

        public static IEnumerable<PixelSprite> All()
        {
            for (int g = 0; g < CrawlerLook.GeneCount; g++)
            {
                var gene = (LookGene)g;
                foreach (var option in LookCatalog.Options(gene))
                {
                    var look = Basis.With(gene, option.Id);
                    if (look.ClothesPrimary == look.ClothesSecondary) look = look.With(gene == LookGene.ClothesPrimary ? LookGene.ClothesSecondary : LookGene.ClothesPrimary, option.Id == 1 ? (byte)2 : (byte)1);
                    yield return Portrait($"Portrait/{gene}/{option.Key}", new PortraitKey(look, Mood.Content, CrawlerStanding.Resident, ItemType.None));
                }
            }
            foreach (var mood in Enum.GetValues<Mood>()) yield return Portrait($"Portrait/mood/{mood}", new PortraitKey(Basis, mood, CrawlerStanding.Resident, ItemType.None));
            foreach (var standing in Enum.GetValues<CrawlerStanding>()) yield return Portrait($"Portrait/standing/{standing}", new PortraitKey(Basis, Mood.Content, standing, ItemType.None));
            yield return Portrait("Portrait/helm", new PortraitKey(Basis, Mood.Content, CrawlerStanding.Resident, ItemType.HazardGear));
            for (uint i = 0; i < PeopleSheetTests.CrowdSize; i++)
                yield return Portrait($"Portrait/crowd/{i:D2}", new PortraitKey(CrawlerLook.Generate(PeopleSheetTests.CrowdSeed, new EntityId(i + 1u, 0u, 0u, 0u)), Mood.Content, CrawlerStanding.Resident, ItemType.None));
        }

        private static PixelSprite Portrait(string name, in PortraitKey key)
        {
            var pixels = new Rgba32[PortraitKit.FramedSize * PortraitKit.FramedSize];
            PortraitKit.BakeFramed(key, pixels);
            return new PixelSprite(name, PortraitKit.FramedSize, PortraitKit.FramedSize, pixels);
        }
    }
}
