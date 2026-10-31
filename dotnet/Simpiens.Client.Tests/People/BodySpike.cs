using System;
using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation.People;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// Epic UI-Beta B0 spike, test-only (plan D1): a crawler's body on the map from its look, drawn on today's 24 pose
    /// frames. Each frame's pixels are read back into key letters by colour (skin, hair, outline, shirt, trousers); the head
    /// is found from the hair's pixels; then hair styles, facial hair, builds and the delvers' height are applied to the
    /// keys, and the keys are painted in the look's colours. What it learns (how many frames the head-finding gets wrong,
    /// which styles need art of their own) goes into the B0 design note; B2 draws the real kit.
    /// </summary>
    internal static class BodySpike
    {
        private const int Size = 16;
        private static readonly Rgba32 SkinKey = new Rgba32(245, 205, 175), OutlineKey = new Rgba32(20, 24, 33);
        private static readonly HashSet<Rgba32> Fixed = new HashSet<Rgba32>
        {
            new Rgba32(255, 255, 255), new Rgba32(148, 163, 184), new Rgba32(254, 240, 138), new Rgba32(192, 132, 252), new Rgba32(147, 197, 253),
        };

        private static readonly Dictionary<string, PixelSprite> Frames = BuildFrames();

        private static Dictionary<string, PixelSprite> BuildFrames()
        {
            var byName = new Dictionary<string, PixelSprite>();
            foreach (var sprite in ProceduralSprites.All) byName[sprite.Name] = sprite;
            return byName;
        }

        /// <summary>Hair styles the spike draws itself; the rest fall back to the nearest (listed in the design note).</summary>
        public static string DrawnAs(string hairStyle) => hairStyle switch
        {
            "shaved" or "crop" or "long" or "bun" or "mohawk" => hairStyle,
            "side-part" or "curls" or "coils" => "crop",
            "ponytail" or "braids" => "long",
            _ => "crop",
        };

        /// <summary>The body of <paramref name="look"/> in <paramref name="state"/>'s <paramref name="frame"/>, and whether its head was found.</summary>
        public static PixelSprite Bake(in CrawlerLook look, AgentVisualState state, int frame, out bool headFound)
        {
            string name = null;
            foreach (var (s, sprites) in CrawlerFrames.All) if (s == state) name = sprites[frame];
            var source = Frames[name];
            var keys = Keys(source, out var others);
            bool asleep = state == AgentVisualState.Sleeping;

            headFound = FindHead(keys, out var head);
            if (headFound)
            {
                Hair(keys, head, DrawnAs(LookCatalog.KeyOf(LookGene.HairStyle, look.HairStyle)));
                FacialHair(keys, head, LookCatalog.KeyOf(LookGene.FacialHair, look.FacialHair));
            }
            if (!asleep)
            {
                string build = LookCatalog.KeyOf(LookGene.Build, look.Build);
                int widen = build == "broad" ? 1 : build == "heavy" ? 2 : 0;
                for (int i = 0; i < widen; i++) Widen(keys, head.Bottom + 1);
                if (LookCatalog.KeyOf(LookGene.Lineage, look.Lineage) == "delver") Shorten(keys, head.Bottom + 1);
            }

            var skin = SpikePalettes.SkinOf(look);
            var hair = SpikePalettes.HairOf(look);
            var shirt = SpikePalettes.PrimaryOf(look);
            var trousers = SpriteKit.RampOf(SpikePalettes.SecondaryOf(look)).Shade;
            var pixels = new Rgba32[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = keys[i] switch
                {
                    '.' => Rgba32.Clear,
                    '#' => OutlineKey,
                    'K' => skin,
                    'H' => hair,
                    'C' => asleep ? others[i] : shirt,
                    'S' => asleep ? others[i] : trousers,
                    _ => others[i],
                };
            }
            return new PixelSprite($"{name}_{Describe(look)}", Size, Size, pixels);
        }

        private static string Describe(in CrawlerLook look) => string.Join("-", LookCatalog.KeyOf(LookGene.Lineage, look.Lineage),
            LookCatalog.KeyOf(LookGene.HairStyle, look.HairStyle), LookCatalog.KeyOf(LookGene.Build, look.Build));

        // The frame read back into keys: '.', '#', 'K' skin, 'H' hair, 'C' shirt (the lighter clothing colour), 'S' trousers, 'O' anything else
        private static char[] Keys(PixelSprite sprite, out Rgba32[] others)
        {
            others = sprite.Pixels;
            var clothes = new List<Rgba32>(2);
            foreach (var p in sprite.Pixels)
            {
                if (p.A == 0 || p == SkinKey || p == OutlineKey || p == ProceduralSprites.Hair || Fixed.Contains(p) || clothes.Contains(p)) continue;
                clothes.Add(p);
            }
            if (clothes.Count != 2) throw new InvalidOperationException($"{sprite.Name}: expected two clothing colours, found {clothes.Count}.");
            var shirt = Luma(clothes[0]) >= Luma(clothes[1]) ? clothes[0] : clothes[1];

            var keys = new char[Size * Size];
            for (int i = 0; i < keys.Length; i++)
            {
                var p = sprite.Pixels[i];
                keys[i] = p.A == 0 ? '.' : p == OutlineKey ? '#' : p == SkinKey ? 'K' : p == ProceduralSprites.Hair ? 'H'
                    : clothes.Contains(p) ? (p == shirt ? 'C' : 'S') : 'O';
            }
            return keys;
        }

        private static int Luma(Rgba32 c) => c.R * 299 + c.G * 587 + c.B * 114;

        private struct Head
        {
            public int Top, Left, Right, Eyes, Bottom; // hair's first row, hair's columns, the eyes' row, the face's last row
        }

        // The head from the hair's pixels: its first row and columns; the eyes are the first row below it with outline
        // between skin; the face ends at the last skin row before the clothes. False when any part is missing.
        private static bool FindHead(char[] keys, out Head head)
        {
            head = new Head { Top = -1, Left = Size, Right = -1, Eyes = -1, Bottom = -1 };
            for (int y = 0; y < Size && head.Top < 0; y++)
                for (int x = 0; x < Size; x++)
                    if (keys[y * Size + x] == 'H') { head.Top = y; break; }
            if (head.Top < 0) return false;
            for (int x = 0; x < Size; x++)
                if (keys[head.Top * Size + x] == 'H') { head.Left = Math.Min(head.Left, x); head.Right = Math.Max(head.Right, x); }
            for (int y = head.Top + 1; y < Math.Min(Size, head.Top + 5) && head.Eyes < 0; y++)
                for (int x = head.Left + 1; x < head.Right; x++)
                    if (keys[y * Size + x] == '#' && keys[y * Size + x - 1] == 'K') { head.Eyes = y; break; }
            if (head.Eyes < 0) return false;
            for (int y = head.Eyes; y < Size; y++)
            {
                bool skin = false;
                for (int x = head.Left; x <= head.Right; x++) skin |= keys[y * Size + x] == 'K';
                if (!skin) break;
                head.Bottom = y;
            }
            return head.Right - head.Left >= 3 && head.Right - head.Left <= 8 && head.Bottom > head.Eyes;
        }

        private static void Hair(char[] keys, Head head, string style)
        {
            switch (style)
            {
                case "shaved":
                    Replace(keys, 'H', 'K', head.Top, head.Eyes);
                    break;
                case "long":
                    for (int y = head.Top + 1; y <= Math.Min(Size - 2, head.Bottom + 2); y++)
                    {
                        Set(keys, head.Left - 1, y, 'H', over: "#.");
                        Set(keys, head.Right + 1, y, 'H', over: "#.");
                        Set(keys, head.Left - 2, y, '#', over: ".");
                        Set(keys, head.Right + 2, y, '#', over: ".");
                    }
                    break;
                case "bun":
                    int mid = (head.Left + head.Right) / 2;
                    Set(keys, mid, head.Top - 1, 'H', over: "#");
                    Set(keys, mid + 1, head.Top - 1, 'H', over: "#");
                    Set(keys, mid, head.Top - 2, 'H', over: ".#");
                    Set(keys, mid + 1, head.Top - 2, 'H', over: ".#");
                    break;
                case "mohawk":
                    int centre = (head.Left + head.Right) / 2;
                    Replace(keys, 'H', 'K', head.Top, head.Eyes);
                    Set(keys, centre, head.Top, 'H', over: "K");
                    Set(keys, centre + 1, head.Top, 'H', over: "K");
                    Set(keys, centre, head.Top - 1, 'H', over: "#");
                    Set(keys, centre + 1, head.Top - 1, 'H', over: "#");
                    break;
            }
        }

        private static void FacialHair(char[] keys, Head head, string kind)
        {
            int mouth = head.Eyes + 1;
            if (mouth > head.Bottom || kind == "none") return;
            // At 16 pixels the face is three rows: hair across a whole row reads as a mask, so a beard hangs below the chin
            int mid = (head.Left + head.Right) / 2;
            for (int x = mid; x <= mid + 1; x++)
                if (kind is "beard" or "moustache" || (kind == "stubble" && (x & 1) == 0)) Set(keys, x, mouth, 'H', over: "K");
            if (kind == "beard")
            {
                for (int x = mid - 1; x <= mid + 2; x++) Set(keys, x, head.Bottom + 1, 'H', over: "#C");
                for (int x = mid; x <= mid + 1; x++) Set(keys, x, head.Bottom + 2, 'H', over: "C");
            }
        }

        // The body a pixel wider: every row from fromRow down shifts its right half one to the right
        private static void Widen(char[] keys, int fromRow)
        {
            int mid = Size / 2;
            for (int y = Math.Max(0, fromRow); y < Size; y++)
            {
                if (keys[y * Size + Size - 1] != '.') continue; // no room
                for (int x = Size - 1; x > mid; x--) keys[y * Size + x] = keys[y * Size + x - 1];
            }
        }

        // The body a pixel shorter: the first trouser row goes, and everything above it moves down one
        private static void Shorten(char[] keys, int fromRow)
        {
            int legs = -1;
            for (int y = Math.Max(0, fromRow); y < Size && legs < 0; y++)
                for (int x = 0; x < Size; x++)
                    if (keys[y * Size + x] == 'S') { legs = y; break; }
            if (legs < 1) return;
            for (int y = legs; y > 0; y--) Array.Copy(keys, (y - 1) * Size, keys, y * Size, Size);
            for (int x = 0; x < Size; x++) keys[x] = '.';
        }

        private static void Replace(char[] keys, char from, char to, int firstRow, int lastRow)
        {
            for (int y = Math.Max(0, firstRow); y <= Math.Min(Size - 1, lastRow); y++)
                for (int x = 0; x < Size; x++)
                    if (keys[y * Size + x] == from) keys[y * Size + x] = to;
        }

        private static void Set(char[] keys, int x, int y, char key, string over)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            if (over.IndexOf(keys[y * Size + x]) >= 0) keys[y * Size + x] = key;
        }
    }
}
