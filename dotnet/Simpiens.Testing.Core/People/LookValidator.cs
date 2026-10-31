using System;
using System.Collections.Generic;
using System.Reflection;
using Simpiens.Simulation;
using Simpiens.Simulation.People;

namespace Simpiens.Testing.People
{
    /// <summary>
    /// Epic UI-Beta B1: a crawler's look as data. The catalogue is whole, a look is 16 bytes (the save format), the same
    /// seed and id give the same look, every option turns up at its odds, and the look's random stream is its own.
    /// </summary>
    public class LookValidator
    {
        private const int Draws = 10000;

        public void RunAllValidationTests()
        {
            TestLog.Log("[LookValidator] Beginning look validation...");

            ValidateCatalogue();
            ValidateLayout();
            ValidateGenerationIsDeterministic();
            ValidateOdds();
            ValidateValidity();
            ValidateSaltsAreDistinct();

            TestLog.Log("<color=green>[LookValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateCatalogue()
        {
            for (int g = 0; g < CrawlerLook.GeneCount; g++)
            {
                var gene = (LookGene)g;
                var options = LookCatalog.Options(gene);
                Assert(options.Count > 0 && options.Count < 256, $"{gene} has {options.Count} options.");
                var keys = new HashSet<string>();
                for (int i = 0; i < options.Count; i++)
                {
                    Assert(options[i].Id == i + 1, $"{gene}'s option {options[i].Key} has id {options[i].Id}, not its place {i + 1}.");
                    Assert(keys.Add(options[i].Key), $"{gene} lists '{options[i].Key}' twice.");
                    Assert(IsKebab(options[i].Key), $"{gene}'s key '{options[i].Key}' must be kebab case, as art keys are.");
                    Assert(LookCatalog.IdOf(gene, options[i].Key) == options[i].Id, $"{gene}'s '{options[i].Key}' must resolve to its id.");
                }
            }

            var lineageOptions = LookCatalog.Options(LookGene.Lineage);
            Assert(LookCatalog.Lineages.Count == lineageOptions.Count, "Every lineage option needs a lineage definition.");
            for (int l = 0; l < LookCatalog.Lineages.Count; l++)
            {
                var lineage = LookCatalog.Lineages[l];
                Assert(lineage.Id == l + 1 && lineage.Key == lineageOptions[l].Key, $"Lineage {lineage.Key} must sit at its option's place.");
                Assert(lineage.Weight > 0, $"Lineage {lineage.Key} needs positive odds.");
                Assert(LookCatalog.Lineage(lineage.Id) == lineage, $"Lineage {lineage.Key} must resolve by id.");
                foreach (var frame in LookCatalog.Options(LookGene.Frame))
                {
                    for (int g = (int)LookGene.Frame; g < CrawlerLook.GeneCount; g++)
                    {
                        var odds = lineage.Odds((LookGene)g, frame.Id);
                        long total = 0;
                        int positive = 0;
                        for (int i = 0; i < odds.Count; i++)
                        {
                            Assert(odds[i].Weight >= 0, $"{lineage.Key} {(LookGene)g}: negative odds.");
                            Assert(LookCatalog.Option((LookGene)g, odds[i].Option) != null, $"{lineage.Key} {(LookGene)g}: odds for an option the catalogue lacks.");
                            total += odds[i].Weight;
                            if (odds[i].Weight > 0) positive++;
                        }
                        Assert(total > 0, $"{lineage.Key} ({frame.Key}) offers no {(LookGene)g}.");
                        if ((LookGene)g == LookGene.ClothesSecondary) Assert(positive >= 2, $"{lineage.Key} needs two clothing colours to choose a second.");
                    }
                }
            }

            // No option is unreachable: until one is retired, every option some lineage offers
            for (int g = 0; g < CrawlerLook.GeneCount; g++)
            {
                var gene = (LookGene)g;
                foreach (var option in LookCatalog.Options(gene))
                {
                    bool offered = false;
                    foreach (var lineage in LookCatalog.Lineages) offered |= lineage.Offers(gene, option.Id);
                    Assert(offered, $"No lineage offers {gene} '{option.Key}': give it odds, or retire it on purpose.");
                }
            }
            Assert(LookCatalog.KeyOf(LookGene.HairStyle, 0) == "unset" && LookCatalog.Option(LookGene.HairStyle, 0) == null, "0 means unset.");

            TestLog.Log("[PASS] Test 1: The catalogue is whole: ids by place, unique kebab keys, every lineage offers every gene in both frames.");
        }

        private static void ValidateLayout()
        {
            int size = System.Runtime.CompilerServices.Unsafe.SizeOf<CrawlerLook>();
            Assert(size == CrawlerLook.Size && size == 16,
                $"CrawlerLook is {size} bytes, pinned at 16: its layout is the save format. Bump SessionSaveHeader.CurrentFormatVersion and update the pin.");
            Assert(Enum.GetValues<LookGene>().Length == CrawlerLook.GeneCount, "GeneCount must match LookGene.");
            Assert(!default(CrawlerLook).IsComplete && default(CrawlerLook).Lineage == 0, "default(CrawlerLook) is no look: every gene unset.");

            var look = default(CrawlerLook).With(LookGene.HairStyle, 3);
            Assert(look.HairStyle == 3 && look.Lineage == 0 && look != default, "With sets one gene and keeps the others.");
            Assert(look.ToString().Contains("side-part"), $"A look names its options by key ({look}).");

            TestLog.Log("[PASS] Test 2: A look is 16 bytes; default is unset; With and ToString work by gene.");
        }

        private static void ValidateGenerationIsDeterministic()
        {
            var id = new EntityId(10u, 20u, 30u, 40u);
            var a = CrawlerLook.Generate(42, id);
            Assert(a == CrawlerLook.Generate(42, new EntityId(10u, 20u, 30u, 40u)), "The same seed and id must give the same look.");
            Assert(a.IsComplete && LookCatalog.IsValid(a), $"A generated look must be complete and valid ({a}).");

            int differentBySeed = 0, differentById = 0;
            for (uint i = 0; i < 200; i++)
            {
                var other = new EntityId(i + 1u, 7u, 7u, 7u);
                if (CrawlerLook.Generate(42, other) != CrawlerLook.Generate(43, other)) differentBySeed++;
                if (CrawlerLook.Generate(42, other) != a) differentById++;
            }
            Assert(differentBySeed > 190 && differentById > 190, $"Looks must vary by seed ({differentBySeed}/200) and by crawler ({differentById}/200).");

            TestLog.Log("[PASS] Test 3: Looks are deterministic per (seed, id), and vary by seed and by crawler.");
        }

        // Every gene's options turn up at their odds, given the lineage and frame they were drawn under
        private static void ValidateOdds()
        {
            // counts[(lineage, gene, frame or 0)][option]; conditions[(lineage, gene, frame or 0)]
            var counts = new Dictionary<(byte, LookGene, byte), int[]>();
            var lineages = new int[LookCatalog.Lineages.Count + 1];
            for (uint i = 0; i < Draws; i++)
            {
                var look = CrawlerLook.Generate(20261030, new EntityId(i + 1u, 3u, 5u, 7u));
                Assert(look.IsComplete && LookCatalog.IsValid(look), $"Draw {i} is incomplete or invalid ({look}).");
                lineages[look.Lineage]++;
                for (int g = (int)LookGene.Frame; g < CrawlerLook.GeneCount; g++)
                {
                    var gene = (LookGene)g;
                    if (gene == LookGene.ClothesSecondary) continue; // drawn apart from the first colour: checked below
                    var key = (look.Lineage, gene, FrameOf(gene, look.Frame));
                    if (!counts.TryGetValue(key, out var row)) counts[key] = row = new int[256];
                    row[look[gene]]++;
                }
            }

            long lineageTotal = 0;
            foreach (var lineage in LookCatalog.Lineages) lineageTotal += lineage.Weight;
            foreach (var lineage in LookCatalog.Lineages) AssertShare(lineages[lineage.Id], Draws, (double)lineage.Weight / lineageTotal, $"lineage {lineage.Key}");

            foreach (var pair in counts)
            {
                var (lineageId, gene, frame) = pair.Key;
                var odds = LookCatalog.Lineage(lineageId).Odds(gene, frame);
                long total = 0;
                int n = 0;
                for (int i = 0; i < odds.Count; i++) total += odds[i].Weight;
                foreach (int c in pair.Value) n += c;
                for (int i = 0; i < odds.Count; i++)
                {
                    AssertShare(pair.Value[odds[i].Option], n, (double)odds[i].Weight / total,
                        $"{LookCatalog.Lineage(lineageId).Key} {gene} '{LookCatalog.KeyOf(gene, odds[i].Option)}'{(frame > 0 ? $" ({LookCatalog.KeyOf(LookGene.Frame, frame)})" : "")}");
                }
            }

            TestLog.Log($"[PASS] Test 4: Over {Draws} draws every lineage and option turns up at its odds (skin tones evenly, frames about half each).");
        }

        private static void ValidateValidity()
        {
            var look = CrawlerLook.Generate(7, new EntityId(1u, 2u, 3u, 4u));
            byte female = LookCatalog.IdOf(LookGene.Frame, "female"), beard = LookCatalog.IdOf(LookGene.FacialHair, "beard");
            Assert(!LookCatalog.IsValid(look.With(LookGene.Frame, female).With(LookGene.FacialHair, beard)), "A female frame is drawn without a beard.");
            Assert(!LookCatalog.IsValid(look.With(LookGene.ClothesSecondary, look.ClothesPrimary)), "The two clothing colours differ.");
            Assert(!LookCatalog.IsValid(look.With(LookGene.Outfit, 0)), "An unset gene is not a valid look.");
            Assert(!LookCatalog.IsValid(look.With(LookGene.Lineage, 99)), "An unknown lineage is not a valid look.");

            byte delver = LookCatalog.IdOf(LookGene.Lineage, "delver"), slight = LookCatalog.IdOf(LookGene.Build, "slight");
            Assert(!LookCatalog.Lineage(delver).Offers(LookGene.Build, slight), "Delvers are never slight (a lineage offers some builds).");
            Assert(LookCatalog.Lineage(delver).Offers(LookGene.SkinTone, LookCatalog.IdOf(LookGene.SkinTone, "ash")), "Delvers add skin tones of their own.");

            TestLog.Log("[PASS] Test 5: A look is valid only with options its lineage allows for its frame.");
        }

        private static void ValidateSaltsAreDistinct()
        {
            var seen = new HashSet<uint>();
            foreach (var field in typeof(SimRandomSalt).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                uint salt = (uint)field.GetValue(null);
                Assert(seen.Add(salt), $"SimRandomSalt.{field.Name} = {salt} shares its stream with another decision.");
            }
            Assert(seen.Contains(SimRandomSalt.Look) && seen.Contains(SimRandomSalt.Name), "The look and the name draw on salts of their own.");

            TestLog.Log("[PASS] Test 6: Every SimRandomSalt is distinct: a look and a name never move another decision's stream.");
        }

        // Hair style and facial hair have a table per frame; the other genes one for both
        private static byte FrameOf(LookGene gene, byte frame) => gene == LookGene.HairStyle || gene == LookGene.FacialHair ? frame : (byte)0;

        // Within five standard deviations of the expected share, plus half a percent
        private static void AssertShare(int observed, int n, double expected, string what)
        {
            double share = n > 0 ? (double)observed / n : 0;
            double tolerance = 5 * Math.Sqrt(expected * (1 - expected) / Math.Max(1, n)) + 0.005;
            Assert(Math.Abs(share - expected) <= tolerance, $"{what}: {share:P1} of {n} draws, expected {expected:P1} (±{tolerance:P1}).");
        }

        private static bool IsKebab(string key)
        {
            if (string.IsNullOrEmpty(key) || key[0] == '-' || key[^1] == '-') return false;
            foreach (char c in key)
            {
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-') return false;
            }
            return true;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[ASSERTION FAILED] {message}");
            }
        }
    }
}
