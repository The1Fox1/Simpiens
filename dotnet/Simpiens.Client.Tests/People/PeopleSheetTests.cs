using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Simpiens.Entities;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Simpiens.Simulation.People;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// The people sheet (Epic UI-Beta B0): the spike's bodies and Hearthlight portraits for the producer's look. A row per
    /// gene with each option once, two moods, every pose, then a crowd of 64 looks drawn from a fixed seed, the sample B2's
    /// and B3's pins will use. Written on every run to render-output/people-sheet.png, with the spike's counts in
    /// people-sheet.txt; not pinned (B0 records no art pins).
    /// </summary>
    public class PeopleSheetTests
    {
        public const ulong CrowdSeed = 20261030;
        public const int CrowdSize = 64;

        private const int SheetWidth = 1200, Margin = 16, Gap = 12;
        private static readonly Rgba32 Background = new Rgba32(30, 33, 40), CheckerLight = new Rgba32(72, 76, 86), CheckerDark = new Rgba32(58, 62, 70);
        private static readonly Rgba32 NameColour = new Rgba32(226, 232, 240), TitleColour = new Rgba32(250, 204, 21);

        private sealed class Cell
        {
            public string Label;
            public Rgba32[] Portrait; // framed, PortraitSpike.Size + 4 square
            public int PortraitScale = 1;
            public PixelSprite Body;
        }

        private readonly List<(string Title, List<Cell> Cells)> _sections = new List<(string, List<Cell>)>();

        [Fact]
        public void WriteThePeopleSheet()
        {
            Assert.True(SpikePalettes.CoversCatalogue(), "Every colour option needs a spike colour.");
            var report = new List<string> { "Epic UI-Beta B0 spike: people sheet counts", "" };

            var basis = Look(("lineage", "human"), ("frame", "male"), ("build", "average"), ("skin", "tone-4"), ("hair", "crop"),
                ("colour", "dark-brown"), ("facial", "none"), ("face", "oval"), ("outfit", "tunic"), ("accessory", "none"));

            Section("Skin tones (humans 1-10, then the delvers' own)", LookCatalog.Options(LookGene.SkinTone).Select(o =>
                Make(o.Key, o.Key is "ruddy" or "ash" ? Set(Set(basis, LookGene.SkinTone, o.Key), LookGene.Lineage, "delver") : Set(basis, LookGene.SkinTone, o.Key))));
            foreach (var frame in new[] { "female", "male" })
            {
                var framed = Set(basis, LookGene.Frame, frame);
                Section($"Hair styles, {frame} (spike draws shaved, crop, long, bun, mohawk; the rest fall back)", LookCatalog.Options(LookGene.HairStyle).Select(o =>
                    Make($"{o.Key} ({BodySpike.DrawnAs(o.Key)})", Set(framed, LookGene.HairStyle, o.Key))));
            }
            Section("Hair colours", LookCatalog.Options(LookGene.HairColour).Select(o => Make(o.Key, Set(basis, LookGene.HairColour, o.Key))));
            Section("Facial hair and face shapes", LookCatalog.Options(LookGene.FacialHair).Select(o => Make(o.Key, Set(basis, LookGene.FacialHair, o.Key)))
                .Concat(LookCatalog.Options(LookGene.FaceShape).Select(o => Make(o.Key, Set(Set(basis, LookGene.FaceShape, o.Key), LookGene.Frame, "female")))));
            var delver = Set(Set(Set(basis, LookGene.Lineage, "delver"), LookGene.FacialHair, "beard"), LookGene.HairStyle, "braids");
            Section("Builds (humans), then delvers", LookCatalog.Options(LookGene.Build).Select(o => Make($"human {o.Key}", Set(basis, LookGene.Build, o.Key)))
                .Concat(new[] { "average", "broad", "heavy" }.Select(b => Make($"delver {b}", Set(delver, LookGene.Build, b))))
                .Concat(new[] { Make("delver female", Set(Set(Set(delver, LookGene.Frame, "female"), LookGene.FacialHair, "none"), LookGene.SkinTone, "ash")) }));
            Section("Moods at 2x: content, tired", new[] { basis, Set(Set(basis, LookGene.Frame, "female"), LookGene.HairStyle, "long"), delver }
                .SelectMany(l => new[] { Make("content", l, scale: 2), Make("tired", l, scale: 2, mood: "tired") }));

            // Every pose: does the head-finding hold, and do the overlays sit right?
            var posed = new List<Cell>();
            int found = 0, frames = 0;
            var misses = new List<string>();
            foreach (var look in new[] { Set(Set(basis, LookGene.HairStyle, "long"), LookGene.Build, "broad"), Set(delver, LookGene.HairStyle, "bun") })
                foreach (AgentVisualState state in Enum.GetValues<AgentVisualState>())
                    for (int f = 0; f < 2; f++)
                    {
                        var body = BodySpike.Bake(look, state, f, out bool head);
                        frames++;
                        if (head) found++;
                        else if (!misses.Contains($"{state} {f}")) misses.Add($"{state} {f}");
                        posed.Add(new Cell { Label = $"{state} {f}", Body = body });
                    }
            Section("Every pose (long hair, broad; a delver with a bun)", posed);
            report.Add($"Head found by the hair's pixels: {found} of {frames} frames (two looks x 12 states x 2 frames).");
            report.Add($"Frames needing a hand-placed anchor: {(misses.Count == 0 ? "none" : string.Join(", ", misses))}.");

            // The crowd: the sample B2's and B3's pins will use
            var crowd = new List<Cell>();
            var counts = new Dictionary<string, int>();
            for (uint i = 0; i < CrowdSize; i++)
            {
                var look = CrawlerLook.Generate(CrowdSeed, new EntityId(i + 1u, 0u, 0u, 0u));
                Assert.True(LookCatalog.IsValid(look), $"Crowd look {i} is invalid ({look}).");
                string key = $"{LookCatalog.KeyOf(LookGene.Lineage, look.Lineage)} {LookCatalog.KeyOf(LookGene.Frame, look.Frame)}";
                counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
                crowd.Add(Make($"{key[0]}{key[key.IndexOf(' ') + 1]} {LookCatalog.KeyOf(LookGene.HairStyle, look.HairStyle)}", look));
            }
            Section($"A crowd of {CrowdSize} looks, seed {CrowdSeed} (h/d human or delver, f/m frame; then hair style)", crowd);
            report.Add($"Crowd of {CrowdSize}: {string.Join(", ", counts.OrderBy(p => p.Key).Select(p => $"{p.Key} {p.Value}"))}.");

            Draw();
            File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "render-output", "people-sheet.txt"), report);
        }

        private Cell Make(string label, CrawlerLook look, int scale = 1, string mood = "content")
        {
            Assert.True(LookCatalog.IsValid(look), $"{label}: {look} is not a valid look.");
            return new Cell
            {
                Label = label,
                Portrait = PortraitSpike.Framed(PortraitSpike.Bake(look, mood)),
                PortraitScale = scale,
                Body = BodySpike.Bake(look, AgentVisualState.Idle, 0, out _),
            };
        }

        private void Section(string title, IEnumerable<Cell> cells) => _sections.Add((title, cells.ToList()));

        private void Draw()
        {
            var fonts = FontLibrary.LoadFrom(AppContext.BaseDirectory);
            var names = fonts[FontSet.Regular(10)];
            var titles = fonts[FontSet.Bold(12)];
            const int bodyScale = 3, framed = PortraitSpike.Size + 4;

            // Lay out first, then draw
            var placed = new List<(Cell Cell, int X, int Y, int Width)>();
            var titlesAt = new List<(string, int, int)>();
            int y = Margin;
            foreach (var (title, cells) in _sections)
            {
                titlesAt.Add((title, Margin, y));
                y += titles.LineHeight + 6;
                int x = Margin, rowHeight = 0;
                foreach (var cell in cells)
                {
                    int picture = (cell.Portrait != null ? framed * cell.PortraitScale + 4 : 0) + (cell.Body != null ? 16 * bodyScale : 0);
                    int width = Math.Max(picture, names.Measure(cell.Label));
                    int height = Math.Max(cell.Portrait != null ? framed * cell.PortraitScale : 0, 16 * bodyScale) + 4 + names.LineHeight;
                    if (x + width > SheetWidth - Margin && x > Margin)
                    {
                        x = Margin;
                        y += rowHeight + Gap;
                        rowHeight = 0;
                    }
                    placed.Add((cell, x, y, width));
                    x += width + Gap;
                    rowHeight = Math.Max(rowHeight, height);
                }
                y += rowHeight + Gap * 2;
            }

            var canvas = new ReviewCanvas(SheetWidth, y + Margin - Gap, Background);
            foreach (var (cell, x, top, width) in placed)
            {
                int pen = x, pictureHeight = 16 * bodyScale;
                if (cell.Portrait != null)
                {
                    canvas.Blit(cell.Portrait, framed, framed, pen, top, cell.PortraitScale);
                    pen += framed * cell.PortraitScale + 4;
                    pictureHeight = Math.Max(pictureHeight, framed * cell.PortraitScale);
                }
                if (cell.Body != null) canvas.Blit(cell.Body, pen, top + pictureHeight - 16 * bodyScale, bodyScale, (CheckerLight, CheckerDark));
                canvas.Write(names, cell.Label, x, top + pictureHeight + 4, NameColour);
            }
            foreach (var (text, tx, ty) in titlesAt) canvas.Write(titles, text, tx, ty, TitleColour);
            canvas.Save("people-sheet.png");
        }

        // A look by option keys over a generated one, so every gene is set
        private static CrawlerLook Look(params (string Gene, string Key)[] genes)
        {
            var look = CrawlerLook.Generate(1, new EntityId(1u, 0u, 0u, 0u));
            foreach (var (gene, key) in genes)
            {
                var g = gene switch
                {
                    "lineage" => LookGene.Lineage, "frame" => LookGene.Frame, "build" => LookGene.Build, "skin" => LookGene.SkinTone,
                    "hair" => LookGene.HairStyle, "colour" => LookGene.HairColour, "facial" => LookGene.FacialHair, "face" => LookGene.FaceShape,
                    "outfit" => LookGene.Outfit, "accessory" => LookGene.Accessory, _ => throw new ArgumentException(gene),
                };
                look = Set(look, g, key);
            }
            return look;
        }

        private static CrawlerLook Set(CrawlerLook look, LookGene gene, string key) => look.With(gene, LookCatalog.IdOf(gene, key));
    }
}
