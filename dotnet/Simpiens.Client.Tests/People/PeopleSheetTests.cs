using System;
using System.Collections.Generic;
using System.IO;
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
    /// The people sheet (Epic UI-Beta B0, B2, B3): bodies on the map from the body kit (<see cref="BodyKit"/>) beside
    /// Hearthlight portraits from the portrait kit (<see cref="PortraitKit"/>), for the producer's look. A row per gene
    /// with each option once, every mood, the outfits, the accessories, the frames by standing, the helm and the guild's
    /// tabard, every pose, then a crowd of 64 looks drawn from a fixed seed, the sample the pins use. Written on every run
    /// to render-output/people-sheet.png, with its counts in people-sheet.txt.
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
            public Rgba32[] Portrait; // framed, PortraitSide square
            public int PortraitSide = PortraitKit.FramedSize;
            public int PortraitScale = 1;
            public PixelSprite Body;
        }

        private readonly List<(string Title, List<Cell> Cells)> _sections = new List<(string, List<Cell>)>();

        [Fact]
        public void WriteThePeopleSheet()
        {
            Assert.True(LookPalettes.CoversCatalogue(), "Every colour option needs a colour.");
            var report = new List<string> { "Epic UI-Beta people sheet counts (B2 bodies, B3 portraits)", "" };

            var basis = Look(("lineage", "human"), ("frame", "male"), ("build", "average"), ("skin", "tone-4"), ("hair", "crop"),
                ("colour", "dark-brown"), ("facial", "none"), ("face", "oval"), ("outfit", "tunic"), ("accessory", "none"));

            Section("Skin tones (humans 1-10, then the delvers' own)", LookCatalog.Options(LookGene.SkinTone).Select(o =>
                Make(o.Key, o.Key is "ruddy" or "ash" ? Set(Set(basis, LookGene.SkinTone, o.Key), LookGene.Lineage, "delver") : Set(basis, LookGene.SkinTone, o.Key))));
            foreach (var frame in new[] { "female", "male" })
            {
                var framed = Set(basis, LookGene.Frame, frame);
                Section($"Hair styles, {frame}", LookCatalog.Options(LookGene.HairStyle).Select(o => Make(o.Key, Set(framed, LookGene.HairStyle, o.Key))));
            }
            Section("Hair colours", LookCatalog.Options(LookGene.HairColour).Select(o => Make(o.Key, Set(basis, LookGene.HairColour, o.Key))));
            Section("Facial hair and face shapes", LookCatalog.Options(LookGene.FacialHair).Select(o => Make(o.Key, Set(basis, LookGene.FacialHair, o.Key)))
                .Concat(LookCatalog.Options(LookGene.FaceShape).Select(o => Make(o.Key, Set(Set(basis, LookGene.FaceShape, o.Key), LookGene.Frame, "female")))));
            var delver = Set(Set(Set(basis, LookGene.Lineage, "delver"), LookGene.FacialHair, "beard"), LookGene.HairStyle, "braids");
            Section("Builds (humans), then delvers", LookCatalog.Options(LookGene.Build).Select(o => Make($"human {o.Key}", Set(basis, LookGene.Build, o.Key)))
                .Concat(new[] { "average", "broad", "heavy" }.Select(b => Make($"delver {b}", Set(delver, LookGene.Build, b))))
                .Concat(new[] { Make("delver female", Set(Set(Set(delver, LookGene.Frame, "female"), LookGene.FacialHair, "none"), LookGene.SkinTone, "ash")) }));
            foreach (var (who, l) in new[] { ("", basis), ("long hair", Set(Set(basis, LookGene.Frame, "female"), LookGene.HairStyle, "long")), ("delver", delver) })
                Section($"Moods at 2x{(who.Length > 0 ? ", " + who : "")}", Enum.GetValues<Mood>().Select(m => Make(m.ToString().ToLowerInvariant(), l, scale: 2, mood: m)));
            Section("Outfits", LookCatalog.Options(LookGene.Outfit).Select(o => Make(o.Key, Set(basis, LookGene.Outfit, o.Key))));
            Section("Accessories", LookCatalog.Options(LookGene.Accessory).Select(o => Make(o.Key, Set(basis, LookGene.Accessory, o.Key))));
            Section("Frames by standing, the helm and the tabard", new[]
            {
                Make("resident", basis), Make("drifter", basis, standing: CrawlerStanding.Drifter), Make("guild member", basis, standing: CrawlerStanding.GuildMember),
                Make("helm", basis, gear: ItemType.HazardGear), Make("helm, long hair", Set(basis, LookGene.HairStyle, "long"), gear: ItemType.HazardGear),
                Make("guild, helm", delver, standing: CrawlerStanding.GuildMember, gear: ItemType.HazardGear),
            });

            // Faces for the lists (B4, D1): the head cropped from the portrait, at 2x
            Section("Faces for the lists at 2x (B4)", Enumerable.Range(1, 12).Select(i =>
            {
                var look = CrawlerLook.Generate(CrowdSeed, new EntityId((uint)i, 0u, 0u, 0u));
                var framedPortrait = new Rgba32[PortraitKit.FramedSize * PortraitKit.FramedSize];
                var standing = (CrawlerStanding)(i % 3);
                PortraitKit.BakeFramed(new PortraitKey(look, (Mood)(i % 7), standing, ItemType.None), framedPortrait);
                var face = new Rgba32[PortraitKit.FaceSize * PortraitKit.FaceSize];
                PortraitKit.Face(framedPortrait, standing, face);
                return new Cell { Label = ((Mood)(i % 7)).ToString().ToLowerInvariant(), Portrait = face, PortraitSide = PortraitKit.FaceSize, PortraitScale = 2 };
            }));

            // The helm over every hair style (D4), and the guild's tabard on every build (D8)
            Section("The helm over each hair style", LookCatalog.Options(LookGene.HairStyle).Select(o =>
                BodyOnly(o.Key, Set(Set(basis, LookGene.Frame, "female"), LookGene.HairStyle, o.Key), AgentVisualState.Idle, 0, ItemType.HazardGear, tabard: false)));
            Section("The guild's tabard on each build, then a delver", LookCatalog.Options(LookGene.Build).Select(o =>
                BodyOnly(o.Key, Set(basis, LookGene.Build, o.Key), AgentVisualState.Idle, 0, ItemType.None, tabard: true))
                .Concat(new[] { BodyOnly("delver", delver, AgentVisualState.Idle, 0, ItemType.None, tabard: true), BodyOnly("helm, tabard", delver, AgentVisualState.Walking, 0, ItemType.HazardGear, tabard: true) }));

            // Every pose: do the overlays sit right at each frame's head anchor?
            foreach (var (title, look, gear, tabard) in new[]
            {
                ("long hair, broad", Set(Set(basis, LookGene.HairStyle, "long"), LookGene.Build, "broad"), ItemType.None, false),
                ("a delver with a bun", Set(delver, LookGene.HairStyle, "bun"), ItemType.None, false),
                ("coils, slight, a guild member", Set(Set(Set(basis, LookGene.HairStyle, "coils"), LookGene.Build, "slight"), LookGene.SkinTone, "tone-9"), ItemType.None, true),
                ("braids, heavy, in the helm", Set(Set(Set(basis, LookGene.HairStyle, "braids"), LookGene.Build, "heavy"), LookGene.Frame, "female"), ItemType.HazardGear, false),
            })
            {
                var posed = new List<Cell>();
                foreach (AgentVisualState state in Enum.GetValues<AgentVisualState>())
                    for (int f = 0; f < 2; f++) posed.Add(BodyOnly($"{state} {f}", look, state, f, gear, tabard));
                Section($"Every pose ({title})", posed);
            }

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
            int distinct = crowd.Select(c => string.Join(",", c.Body.Pixels)).Distinct().Count();
            report.Add($"Distinct bodies on the map in the crowd (idle, frame 0): {distinct} of {CrowdSize}.");

            Draw();
            File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "render-output", "people-sheet.txt"), report);
        }

        private Cell Make(string label, CrawlerLook look, int scale = 1, Mood mood = Mood.Content, CrawlerStanding standing = CrawlerStanding.Resident, ItemType gear = ItemType.None)
        {
            Assert.True(LookCatalog.IsValid(look), $"{label}: {look} is not a valid look.");
            var portrait = new Rgba32[PortraitKit.FramedSize * PortraitKit.FramedSize];
            PortraitKit.BakeFramed(new PortraitKey(look, mood, standing, gear), portrait);
            bool tabard = standing == CrawlerStanding.GuildMember;
            return new Cell
            {
                Label = label,
                Portrait = portrait,
                PortraitScale = scale,
                Body = Body(label, look, mood == Mood.Asleep ? AgentVisualState.Sleeping : AgentVisualState.Idle, 0, gear, tabard),
            };
        }

        private static Cell BodyOnly(string label, CrawlerLook look, AgentVisualState state, int frame, ItemType headGear, bool tabard)
            => new Cell { Label = label, Body = Body(label, look, state, frame, headGear, tabard) };

        private static PixelSprite Body(string label, CrawlerLook look, AgentVisualState state, int frame, ItemType headGear, bool tabard)
        {
            var pixels = new Rgba32[BodyKit.PixelsPerFrame];
            BodyKit.BakeFrame(look, headGear, tabard, state, frame, pixels);
            return new PixelSprite(label, BodyKit.Size, BodyKit.Size, pixels);
        }

        private void Section(string title, IEnumerable<Cell> cells) => _sections.Add((title, cells.ToList()));

        private void Draw()
        {
            var fonts = FontLibrary.LoadFrom(AppContext.BaseDirectory);
            var names = fonts[FontSet.Regular(10)];
            var titles = fonts[FontSet.Bold(12)];
            const int bodyScale = 3;

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
                    int framed = cell.PortraitSide;
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
                int framed = cell.PortraitSide;
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
