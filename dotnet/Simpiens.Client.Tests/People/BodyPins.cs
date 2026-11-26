using System.Collections.Generic;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Presentation.Art.People;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.People;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// The bodies the art pins hold (Epic UI-Beta B2, D10), each as one sprite of its 26 frames stacked (24 until the seated pose), by name:
    /// every option the map shows on a base look, the helm over each hair style, the tabard on each build, and the people
    /// sheet's crowd of 64. A changed body fails by name (<see cref="ArtPinTests"/>).
    /// </summary>
    internal static class BodyPins
    {
        private static readonly CrawlerLook Basis = Look(("lineage", "human"), ("frame", "male"), ("build", "average"), ("skin", "tone-4"),
            ("hair", "crop"), ("colour", "dark-brown"), ("facial", "none"));

        public static IEnumerable<PixelSprite> All()
        {
            foreach (var gene in new[] { LookGene.Lineage, LookGene.Build, LookGene.SkinTone, LookGene.HairStyle, LookGene.HairColour, LookGene.FacialHair, LookGene.ClothesPrimary, LookGene.ClothesSecondary })
            {
                foreach (var option in LookCatalog.Options(gene))
                {
                    var look = Basis.With(gene, option.Id);
                    if (look.ClothesPrimary == look.ClothesSecondary) look = look.With(gene == LookGene.ClothesPrimary ? LookGene.ClothesSecondary : LookGene.ClothesPrimary, option.Id == 1 ? (byte)2 : (byte)1);
                    yield return Body($"Body/{gene}/{option.Key}", look, default, false);
                }
            }
            foreach (var style in LookCatalog.Options(LookGene.HairStyle)) yield return Body($"Body/helm/{style.Key}", Basis.With(LookGene.HairStyle, style.Id), WornLook.Wearing(ItemType.HardHat), false);
            foreach (var build in LookCatalog.Options(LookGene.Build)) yield return Body($"Body/tabard/{build.Key}", Basis.With(LookGene.Build, build.Id), default, true);
            // A coat on the body (Epic 10 Part 3, K1.5): hazard gear by build, and with a hard hat and the tabard over it
            foreach (var build in LookCatalog.Options(LookGene.Build)) yield return Body($"Body/coat/{build.Key}", Basis.With(LookGene.Build, build.Id), WornLook.Wearing(ItemType.HazardGear), false);
            yield return Body("Body/hat-and-coat", Basis, WornLook.Wearing(ItemType.HardHat).And(Simpiens.Simulation.Items.EquipmentSlot.Body, ItemType.HazardGear), true);
            // Scars (Epic 11, S6.7): all four places, on the basis and on the lightest and darkest skin
            var everywhere = ScarMarks.With(Simpiens.Simulation.Skills.ScarShape.Cut, Simpiens.Simulation.Skills.ScarPlace.LeftCheek, Simpiens.Simulation.Skills.ScarPlace.Brow, Simpiens.Simulation.Skills.ScarPlace.RightCheek, Simpiens.Simulation.Skills.ScarPlace.Chin);
            yield return Body("Body/scar/all", Basis, default, false, everywhere);
            yield return Body("Body/scar/tone-1", Basis.With(LookGene.SkinTone, LookCatalog.IdOf(LookGene.SkinTone, "tone-1")), default, false, everywhere);
            yield return Body("Body/scar/tone-10", Basis.With(LookGene.SkinTone, LookCatalog.IdOf(LookGene.SkinTone, "tone-10")), default, false, everywhere);
            for (uint i = 0; i < PeopleSheetTests.CrowdSize; i++)
                yield return Body($"Body/crowd/{i:D2}", CrawlerLook.Generate(PeopleSheetTests.CrowdSeed, new EntityId(i + 1u, 0u, 0u, 0u)), default, false);
        }

        private static PixelSprite Body(string name, CrawlerLook look, WornLook worn, bool tabard, ScarMarks scars = default)
        {
            var pixels = new Rgba32[BodyKit.PixelsPerBody];
            BodyKit.Bake(look, worn, tabard, pixels, scars);
            return new PixelSprite(name, BodyKit.Size, BodyKit.Size * BodyKit.FrameCount, pixels); // the frames stacked
        }

        private static CrawlerLook Look(params (string Gene, string Key)[] genes)
        {
            var look = CrawlerLook.Generate(1, new EntityId(1u, 0u, 0u, 0u));
            foreach (var (gene, key) in genes)
            {
                var g = gene switch
                {
                    "lineage" => LookGene.Lineage, "frame" => LookGene.Frame, "build" => LookGene.Build, "skin" => LookGene.SkinTone,
                    "hair" => LookGene.HairStyle, "colour" => LookGene.HairColour, "facial" => LookGene.FacialHair, _ => throw new System.ArgumentException(gene),
                };
                look = look.With(g, LookCatalog.IdOf(g, key));
            }
            return look;
        }
    }
}
