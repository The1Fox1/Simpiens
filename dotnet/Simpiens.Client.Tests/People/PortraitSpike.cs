using System;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation.People;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// Epic UI-Beta B0 spike, test-only (plan D1): a 48×48 portrait bust in the Hearthlight look, from a look and a mood.
    /// Layers write a material and a tone (1 highlight to 4 deep) per pixel, in integer maths: each shape is lit as a dome
    /// from the top left and banded into four tones, skin more softly. The look's ramps (<see cref="SpriteKit.RampOf"/>)
    /// colour the tones, a one-pixel warm rim lights the far edge, and the outline is the map's in shadow and the
    /// material's deepest tone where lit. The stonework behind and the frame around are drawn outside the bake.
    /// </summary>
    internal static class PortraitSpike
    {
        public const int Size = 48;

        private enum Material : byte { None, Skin, Hair, Cloth, Trim, Iris, Sclera, Line, Lip }

        private static readonly Rgba32 ScleraColour = new Rgba32(236, 230, 218), Warm = new Rgba32(255, 176, 96);
        private static readonly Rgba32 Mortar = new Rgba32(46, 28, 30), BrickA = new Rgba32(124, 58, 44), BrickB = new Rgba32(106, 48, 38), Pillar = new Rgba32(48, 34, 40);

        private sealed class Canvas
        {
            public readonly Material[] Mat = new Material[Size * Size];
            public readonly byte[] Tone = new byte[Size * Size];

            public void Set(int x, int y, Material m, int tone)
            {
                if (x < 0 || y < 0 || x >= Size || y >= Size) return;
                Mat[y * Size + x] = m;
                Tone[y * Size + x] = (byte)tone;
            }

            public Material At(int x, int y) => x < 0 || y < 0 || x >= Size || y >= Size ? Material.None : Mat[y * Size + x];
        }

        /// <summary>The bust of <paramref name="look"/> showing <paramref name="mood"/> ("content", "tired"), on a clear background.</summary>
        public static Rgba32[] Bake(in CrawlerLook look, string mood)
        {
            var c = new Canvas();
            string lineage = LookCatalog.KeyOf(LookGene.Lineage, look.Lineage);
            string build = LookCatalog.KeyOf(LookGene.Build, look.Build);
            string face = LookCatalog.KeyOf(LookGene.FaceShape, look.FaceShape);
            string style = LookCatalog.KeyOf(LookGene.HairStyle, look.HairStyle);
            bool delver = lineage == "delver";

            // The head: centre and radii by face shape and lineage; the face turns to the viewer's right, toward the light's side
            int cx = 23, cy = 20;
            int rx = face switch { "round" => 10, "square" => 10, "long" => 9, _ => 9 } + (delver ? 1 : 0);
            int ry = face switch { "round" => 11, "long" => 13, _ => 12 } - (delver ? 1 : 0);

            // 1. Hair behind the head
            if (style is "long" or "braids") Dome(c, cx - 3, cy + 7, rx + 2, 15, Material.Hair, soft: false, maxY: 43, band: style == "braids");
            if (style == "ponytail") Dome(c, cx - 11, cy + 4, 3, 8, Material.Hair, soft: false);
            if (style == "bun") Dome(c, cx - 5, cy - 12, 4, 4, Material.Hair, soft: false);

            // 2. Shoulders and collar in the outfit's cut (the spike draws the tunic for every outfit)
            int shoulders = 22 + build switch { "slight" => -2, "broad" => 1, "heavy" => 2, _ => 0 } + (delver ? 2 : 0);
            Dome(c, 24, 51, shoulders, 15, Material.Cloth, soft: false);
            for (int y = 34; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (c.At(x, y) != Material.Cloth) continue;
                    bool collar = c.At(x, y - 2) == Material.None && Math.Abs(x - cx) < 10;
                    bool placket = y >= 41 && (x == cx + 1 || x == cx + 2);
                    if (collar || placket) c.Set(x, y, Material.Trim, collar ? Band(x - 24, y - 51, shoulders, 15, false) : 3);
                }

            // 3. Neck, shaded under the jaw
            for (int y = cy + 7; y < 40; y++)
                for (int x = cx - 4 - (delver ? 1 : 0); x <= cx + 3; x++)
                    if (c.At(x, y) == Material.None || y < 37) c.Set(x, y, Material.Skin, x < cx - 1 ? 2 : 3);

            // 4. Head; the jaw and chin forward, toward the face; the ear on the far side of the head (left), over it
            if (face == "square") Squircle(c, cx, cy, rx, ry, Material.Skin);
            else Dome(c, cx, cy, rx, ry, Material.Skin, soft: true);
            Dome(c, cx + 3, cy + 7, delver ? 8 : 6, 5, Material.Skin, soft: true);
            int ear = cx - rx + 4;
            for (int y = cy + 1; y <= cy + 5; y++)
            {
                c.Set(ear, y, Material.Skin, y == cy + 1 ? 2 : 3);
                c.Set(ear + 1, y, Material.Skin, y > cy + 1 && y < cy + 5 ? 4 : 3);
            }

            // 5. Eyes, brows, nose and mouth, by mood
            int ey = cy + 1, near = cx + 1, far = cx + 7;
            Eye(c, near, ey, look, mood, wide: true);
            Eye(c, far, ey, look, mood, wide: false);
            int browRows = delver ? 2 : 1;
            for (int r = 0; r < browRows; r++)
            {
                for (int x = near - 1; x <= near + 2; x++) c.Set(x, ey - 2 - r, r == 0 ? Material.Hair : Material.Skin, r == 0 ? 3 : 3);
                for (int x = far; x <= far + 1; x++) c.Set(x, ey - 2 - r, r == 0 ? Material.Hair : Material.Skin, 3);
            }
            for (int y = ey + 1; y <= ey + 4; y++) c.Set(cx + 4, y, Material.Skin, 3);
            c.Set(cx + 5, ey + 4, Material.Skin, 2);
            c.Set(cx + 6, ey + 4, Material.Skin, 2);
            c.Set(cx + 5, ey + 5, Material.Skin, 4);
            int my = ey + 7;
            for (int x = cx + 2; x <= cx + 5; x++) c.Set(x, my, Material.Lip, 4);
            if (mood == "content")
            {
                c.Set(cx + 1, my - 1, Material.Lip, 4);
                c.Set(cx + 6, my - 1, Material.Lip, 4);
            }

            // 6. Facial hair over the jaw
            string facial = LookCatalog.KeyOf(LookGene.FacialHair, look.FacialHair);
            for (int y = ey + 5; y < Size; y++)
                for (int x = cx - rx; x <= cx + rx + 2; x++)
                {
                    if (c.At(x, y) != Material.Skin || y >= cy + 13 + (delver ? 3 : 0)) continue;
                    bool jaw = y >= my - 1 && x >= cx - 6;
                    bool lip = y == my - 1 && x >= cx + 1 && x <= cx + 6;
                    if (facial == "beard" && (jaw || lip)) c.Set(x, y, Material.Hair, delver && (y % 3 == 0) ? 3 : 2);
                    else if (facial == "moustache" && lip) c.Set(x, y, Material.Hair, 2);
                    else if (facial == "stubble" && jaw && ((x + y) & 1) == 0) c.Set(x, y, Material.Skin, 3);
                }
            if (facial == "beard" && delver)
                for (int y = cy + 13; y < cy + 17; y++)
                    for (int x = cx + 1; x <= cx + 5; x++) c.Set(x, y, Material.Hair, y % 3 == 0 ? 3 : 2);

            // 7. Hair in front
            Front(c, cx, cy, rx, ry, style);

            return Paint(c, look);
        }

        private static void Eye(Canvas c, int x, int y, in CrawlerLook look, string mood, bool wide)
        {
            bool tired = mood == "tired";
            if (wide)
            {
                c.Set(x, y, tired ? Material.Skin : Material.Sclera, tired ? 3 : 2);
                c.Set(x + 1, y, tired ? Material.Skin : Material.Iris, tired ? 3 : 2);
                c.Set(x, y + 1, Material.Sclera, 2);
                c.Set(x + 1, y + 1, Material.Iris, 3);
                for (int lx = x - 1; lx <= x + 1; lx++) c.Set(lx, tired ? y : y - 1, Material.Line, 1);
                if (tired) c.Set(x, y + 2, Material.Skin, 3);
            }
            else
            {
                c.Set(x, y, tired ? Material.Skin : Material.Iris, tired ? 3 : 2);
                c.Set(x, y + 1, Material.Iris, 3);
                c.Set(x + 1, y + 1, Material.Sclera, 3);
                for (int lx = x; lx <= x + 1; lx++) c.Set(lx, tired ? y : y - 1, Material.Line, 1);
            }
        }

        // The hair over the head: a cap above a hairline that drops toward the back of the head (left) and sits high over the face
        private static void Front(Canvas c, int cx, int cy, int rx, int ry, string style)
        {
            if (style == "shaved") return;
            int grow = style switch { "curls" => 2, "coils" => 3, _ => 1 };
            for (int y = cy - ry - grow - 3; y <= cy + ry; y++)
                for (int x = cx - rx - grow - 1; x <= cx + rx + grow; x++)
                {
                    int dx = x - cx, dy = y - cy;
                    bool inCap = Inside(dx, dy, rx + grow, ry + grow);
                    if (style == "mohawk")
                    {
                        if (x < cx - 3 || x > cx + 1) continue;
                        inCap = Inside(dx, dy, rx, ry + 3);
                    }
                    if (!inCap) continue;
                    int hairline = x < cx - 2 ? (style is "long" or "braids" ? cy + 8 : x < cx - rx + 3 ? cy + 4 : cy + 1) : cy - 5 + ((x * 7) % 3 == 0 ? 1 : 0);
                    if (style is "curls" or "coils" && !Inside(dx, dy, rx + grow - 1, ry + grow - 1) && ((x + y) % 3 == 0)) continue;
                    if (y >= hairline) continue;
                    if (style == "side-part" && x == cx - 3 && y < cy - 6) { c.Set(x, y, Material.Skin, 2); continue; }
                    c.Set(x, y, Material.Hair, Band(dx, dy, rx + grow, ry + grow, false));
                }
        }

        private static void Dome(Canvas c, int cx, int cy, int rx, int ry, Material m, bool soft, int maxY = Size - 1, bool band = false)
        {
            for (int y = cy - ry; y <= Math.Min(cy + ry, maxY); y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    if (!Inside(x - cx, y - cy, rx, ry)) continue;
                    int tone = Band(x - cx, y - cy, rx, ry, soft);
                    if (band && y % 3 == 0) tone = Math.Min(4, tone + 1);
                    c.Set(x, y, m, tone);
                }
        }

        // A rounded square head: |dx/rx|^4 + |dy/ry|^4 <= 1
        private static void Squircle(Canvas c, int cx, int cy, int rx, int ry, Material m)
        {
            long rx4 = (long)rx * rx * rx * rx, ry4 = (long)ry * ry * ry * ry;
            for (int y = cy - ry; y <= cy + ry; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    long dx = x - cx, dy = y - cy;
                    if (dx * dx * dx * dx * ry4 + dy * dy * dy * dy * rx4 > rx4 * ry4) continue;
                    c.Set(x, y, m, Band(x - cx, y - cy, rx, ry, true));
                }
        }

        private static bool Inside(int dx, int dy, int rx, int ry) => (long)dx * dx * ry * ry + (long)dy * dy * rx * rx <= (long)rx * rx * ry * ry;

        // A dome's tone at (dx, dy) from its centre: its normal in 1/256ths against a light from the top left and in front,
        // (-0.5, -0.6, 0.62), banded into tones 1-4; skin (soft) only reaches tone 3
        private static int Band(int dx, int dy, int rx, int ry, bool soft)
        {
            int nx = dx * 256 / Math.Max(1, rx), ny = dy * 256 / Math.Max(1, ry);
            int nz = ISqrt(Math.Max(0, 65536 - nx * nx - ny * ny));
            int light = (-128 * nx - 154 * ny + 159 * nz) / 256;
            if (soft) return light >= 150 ? 1 : light >= 10 ? 2 : 3;
            return light >= 140 ? 1 : light >= 40 ? 2 : light >= -50 ? 3 : 4;
        }

        private static int ISqrt(int n)
        {
            if (n <= 0) return 0;
            int x = n, y = (x + 1) / 2;
            while (y < x)
            {
                x = y;
                y = (x + n / x) / 2;
            }
            return x;
        }

        private static Rgba32[] Paint(Canvas c, in CrawlerLook look)
        {
            var skin = SpriteKit.RampOf(SpikePalettes.SkinOf(look));
            var hair = SpriteKit.RampOf(SpikePalettes.HairOf(look));
            var cloth = SpriteKit.RampOf(SpikePalettes.PrimaryOf(look));
            var trim = SpriteKit.RampOf(SpikePalettes.SecondaryOf(look));
            var iris = SpriteKit.RampOf(SpikePalettes.EyesOf(look));
            Ramp RampFor(Material m) => m switch { Material.Hair => hair, Material.Cloth => cloth, Material.Trim => trim, Material.Iris => iris, _ => skin };

            // The hairline: hair just above skin takes its deepest tone, so the face reads at 1x
            for (int y = 1; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if (c.At(x, y - 1) == Material.Hair && c.At(x, y) == Material.Skin) c.Tone[(y - 1) * Size + x] = 4;

            var pixels = new Rgba32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    var m = c.Mat[i];
                    pixels[i] = m switch
                    {
                        Material.None => Rgba32.Clear,
                        Material.Sclera => ScleraColour,
                        Material.Line => SpriteKit.Outline,
                        Material.Lip => skin.Deep,
                        _ => RampFor(m)[c.Tone[i]],
                    };
                }

            // The warm rim: the far (right) edge of each row above the shoulders' foot, a pixel wide
            for (int y = 0; y < Size - 4; y++)
                for (int x = Size - 1; x >= 0; x--)
                {
                    var m = c.Mat[y * Size + x];
                    if (m == Material.None) continue;
                    if (m is Material.Skin or Material.Hair or Material.Cloth or Material.Trim) pixels[y * Size + x] = Mix(RampFor(m).Base, Warm);
                    break;
                }

            // The outline: the map's colour in shadow (right and below), the material's deepest tone where lit
            var outlined = (Rgba32[])pixels.Clone();
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (c.Mat[y * Size + x] != Material.None) continue;
                    Material lit = c.At(x + 1, y) != Material.None ? c.At(x + 1, y) : c.At(x, y + 1);
                    Material shadow = c.At(x - 1, y) != Material.None ? c.At(x - 1, y) : c.At(x, y - 1);
                    if (shadow != Material.None) outlined[y * Size + x] = SpriteKit.Outline;
                    else if (lit != Material.None) outlined[y * Size + x] = RampFor(lit).Deep;
                }
            return outlined;
        }

        private static Rgba32 Mix(Rgba32 a, Rgba32 b) => new Rgba32((byte)((a.R + b.R) / 2), (byte)((a.G + b.G) / 2), (byte)((a.B + b.B) / 2));

        /// <summary>The hub's stonework behind a bust (outside the bake): red brick in offset courses, a dark pillar on the left.</summary>
        public static Rgba32[] Backdrop()
        {
            var pixels = new Rgba32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int course = y / 5, shift = (course & 1) * 5;
                    bool mortar = y % 5 == 4 || (x + shift) % 10 == 9;
                    int brick = ((x + shift) / 10 * 7 + course * 3) % 4;
                    var colour = x < 10 ? Pillar : mortar ? Mortar : brick == 0 ? BrickB : BrickA;
                    if (x == 10) colour = Mortar;
                    pixels[y * Size + x] = colour;
                }
            return pixels;
        }

        /// <summary>The bust over its backdrop in a 2-pixel iron frame (a resident's): (Size + 4) square.</summary>
        public static Rgba32[] Framed(Rgba32[] bust)
        {
            const int framed = Size + 4;
            var outer = new Rgba32(58, 62, 72);
            var inner = new Rgba32(132, 138, 150);
            var back = Backdrop();
            var pixels = new Rgba32[framed * framed];
            for (int y = 0; y < framed; y++)
                for (int x = 0; x < framed; x++)
                {
                    int i = y * framed + x;
                    if (x == 0 || y == 0 || x == framed - 1 || y == framed - 1) { pixels[i] = outer; continue; }
                    if (x == 1 || y == 1 || x == framed - 2 || y == framed - 2) { pixels[i] = inner; continue; }
                    int b = (y - 2) * Size + (x - 2);
                    pixels[i] = bust[b].A != 0 ? bust[b] : back[b];
                }
            return pixels;
        }
    }
}
