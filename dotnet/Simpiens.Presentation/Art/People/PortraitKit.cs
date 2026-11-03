using System;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.People;

namespace Simpiens.Presentation.Art.People
{
    /// <summary>
    /// A crawler's portrait (Epic UI-Beta B3, design §3.3): a 48×48 bust in the Hearthlight look, from its look, its mood
    /// and what it wears. Layers write a material and a tone (1 highlight to 4 deep) per pixel, back to front: hair behind
    /// the head, the shoulders in the outfit's cut, the neck, the head and ear, the face by mood, facial hair, hair in
    /// front, then the accessory, the helm and the tabard. Each shape is lit as a dome from the top left and banded into
    /// four tones (skin more softly); the look's ramps (<see cref="SpriteKit.RampOf"/>) colour them, a one-pixel warm rim
    /// lights the far edge, and the outline is the map's colour in shadow and the material's deepest tone where lit. All
    /// integer maths on the stack: the same key bakes the same pixels, and a bake into a buffer allocates nothing. The
    /// stonework behind and the frame around (<see cref="Framed"/>) are outside the bust. Grown from B0's spike.
    /// </summary>
    public static class PortraitKit
    {
        public const int Size = 48;

        /// <summary>A framed portrait's side: the bust and a two-pixel frame each side.</summary>
        public const int FramedSize = Size + 4;

        private const byte None = 0, Skin = 1, Hair = 2, Cloth = 3, Trim = 4, Iris = 5, Sclera = 6, Line = 7, Lip = 8,
            Gold = 9, Steel = 10, Helm = 11, Lamp = 12, Tabard = 13, Warpaint = 14, Bandage = 15;

        private static readonly Rgba32 ScleraColour = new Rgba32(236, 230, 218), Warm = new Rgba32(255, 176, 96);
        private static readonly Rgba32 GoldColour = new Rgba32(212, 160, 40), SteelColour = new Rgba32(148, 163, 184);
        private static readonly Rgba32 PaintColour = new Rgba32(170, 70, 40), BandageColour = new Rgba32(232, 226, 210);
        private static readonly Rgba32 Mortar = new Rgba32(46, 28, 30), BrickA = new Rgba32(124, 58, 44), BrickB = new Rgba32(106, 48, 38), Pillar = new Rgba32(48, 34, 40);

        /// <summary>The frame's outer and inner edge by standing (D6): iron, bronze, blued steel.</summary>
        public static (Rgba32 Outer, Rgba32 Inner) FrameOf(CrawlerStanding standing) => standing switch
        {
            CrawlerStanding.Drifter => (new Rgba32(92, 58, 30), new Rgba32(205, 127, 50)),
            CrawlerStanding.GuildMember => (new Rgba32(30, 42, 70), new Rgba32(96, 130, 190)),
            _ => (new Rgba32(58, 62, 72), new Rgba32(132, 138, 150)),
        };

        private ref struct Canvas
        {
            public Span<byte> Mat;
            public Span<byte> Tone;

            public void Set(int x, int y, byte m, int tone)
            {
                if ((uint)x >= Size || (uint)y >= Size) return;
                Mat[y * Size + x] = m;
                Tone[y * Size + x] = (byte)tone;
            }

            public byte At(int x, int y) => (uint)x >= Size || (uint)y >= Size ? None : Mat[y * Size + x];
        }

        /// <summary>The framed portrait of <paramref name="key"/> (<see cref="FramedSize"/> square) into <paramref name="into"/>.</summary>
        public static void BakeFramed(in PortraitKey key, Span<Rgba32> into)
        {
            Span<Rgba32> bust = stackalloc Rgba32[Size * Size];
            Bake(key.Look, key.Mood, key.HeadGear, key.Standing == CrawlerStanding.GuildMember, bust);
            Framed(bust, key.Standing, into);
        }

        /// <summary>
        /// The bust of <paramref name="look"/> showing <paramref name="mood"/> (<see cref="Size"/> square, clear behind),
        /// wearing <paramref name="headGear"/> when it is worn on the head, and the guild's tabard when <paramref name="tabard"/>.
        /// </summary>
        public static void Bake(in CrawlerLook look, Mood mood, ItemType headGear, bool tabard, Span<Rgba32> into)
        {
            if (into.Length < Size * Size) throw new ArgumentException($"A portrait needs {Size * Size} pixels.", nameof(into));
            Span<byte> mat = stackalloc byte[Size * Size];
            Span<byte> tone = stackalloc byte[Size * Size];
            mat.Clear();
            tone.Clear();
            var c = new Canvas { Mat = mat, Tone = tone };
            string lineage = LookCatalog.KeyOf(LookGene.Lineage, look.Lineage);
            string build = LookCatalog.KeyOf(LookGene.Build, look.Build);
            string face = LookCatalog.KeyOf(LookGene.FaceShape, look.FaceShape);
            string style = LookCatalog.KeyOf(LookGene.HairStyle, look.HairStyle);
            string outfit = LookCatalog.KeyOf(LookGene.Outfit, look.Outfit);
            string accessory = LookCatalog.KeyOf(LookGene.Accessory, look.Accessory);
            bool delver = lineage == "delver", female = LookCatalog.KeyOf(LookGene.Frame, look.Frame) == "female";
            ItemLook gear = default;
            bool helm = headGear != ItemType.None && ItemArt.TryGet(ItemCatalog.Get(headGear)?.ArtKey, out gear) && gear.Worn == Worn.Head;

            // The head: centre and radii by face shape and lineage; the face turns to the viewer's right, toward the light's side
            int cx = 23, cy = 20;
            int rx = face switch { "round" => 10, "square" => 10, "long" => 9, _ => 9 } + (delver ? 1 : 0);
            int ry = face switch { "round" => 11, "long" => 13, _ => 12 } - (delver ? 1 : 0);

            // 1. Hair behind the head, and a hood lowered behind the neck
            if (style is "long" or "braids") Dome(ref c, cx - 3, cy + 7, rx + 2, 15, Hair, soft: false, maxY: 43, band: style == "braids");
            if (style == "ponytail")
            {
                Dome(ref c, cx - 11, cy + 5, 3, 9, Hair, soft: false);
                for (int x = cx - 13; x <= cx - 10; x++) c.Set(x, cy - 2, Trim, 3); // its tie
            }
            if (style == "bun") Dome(ref c, cx - 5, cy - 12, 4, 4, Hair, soft: false);
            if (outfit == "hood") Dome(ref c, cx - 3, cy + 13, rx + 4, 7, Trim, soft: false);

            // 2. Shoulders and collar in the outfit's cut
            int shoulders = 22 + build switch { "slight" => -2, "broad" => 1, "heavy" => 2, _ => 0 } + (delver ? 2 : 0);
            Dome(ref c, 24, 51, shoulders, 15, Cloth, soft: false);
            Outfit(ref c, cx, outfit, shoulders);

            // 3. Neck, shaded under the jaw
            for (int y = cy + 7; y < 40; y++)
                for (int x = cx - 4 - (delver ? 1 : 0); x <= cx + 3; x++)
                    if (c.At(x, y) == None || y < 37) c.Set(x, y, Skin, x < cx - 1 ? 2 : 3);
            if (outfit == "scarf")
                for (int y = 35; y <= 38; y++)
                    for (int x = cx - 6; x <= cx + 5; x++) c.Set(x, y, Trim, y == 35 ? 2 : (x + y) % 4 == 0 ? 4 : 3);
            if (outfit == "robe") // the wrap's high collar
                for (int y = 36; y <= 38; y++)
                    for (int x = cx - 6; x <= cx + 5; x++) if (c.At(x, y) == Skin && (x < cx - 3 || x > cx + 2)) c.Set(x, y, Trim, 3);

            // 4. Head; the jaw and chin forward, toward the face; the ear on the far side of the head (left), over it
            if (face == "square") Squircle(ref c, cx, cy, rx, ry, Skin);
            else Dome(ref c, cx, cy, rx, ry, Skin, soft: true);
            Dome(ref c, cx + 3, cy + 7, delver ? 8 : 6, 5, Skin, soft: true);
            int ear = cx - rx + 4;
            for (int y = cy + 1; y <= cy + 5; y++)
            {
                c.Set(ear, y, Skin, y == cy + 1 ? 2 : 3);
                c.Set(ear + 1, y, Skin, y > cy + 1 && y < cy + 5 ? 4 : 3);
            }

            // 5. The face by mood (D2): eyes, brows, nose, mouth
            int ey = cy + 1, near = cx + 1, far = cx + 7, my = ey + 7;
            Eyes(ref c, near, far, ey, mood);
            Brows(ref c, near, far, ey, mood, delver ? 2 : 1, female);
            for (int y = ey + 1; y <= ey + 4; y++) c.Set(cx + 4, y, Skin, 3);
            c.Set(cx + 5, ey + 4, Skin, 2);
            c.Set(cx + 6, ey + 4, Skin, 2);
            c.Set(cx + 5, ey + 5, Skin, 4);
            Mouth(ref c, cx, my, mood);

            // 6. Facial hair over the jaw
            string facial = LookCatalog.KeyOf(LookGene.FacialHair, look.FacialHair);
            for (int y = ey + 5; y < Size; y++)
                for (int x = cx - rx; x <= cx + rx + 2; x++)
                {
                    if (c.At(x, y) != Skin || y >= cy + 13 + (delver ? 3 : 0)) continue;
                    bool jaw = y >= my - 1 && x >= cx - 6;
                    bool lip = y == my - 1 && x >= cx + 1 && x <= cx + 6;
                    if (facial == "beard" && (jaw || lip)) c.Set(x, y, Hair, delver && (y % 3 == 0) ? 3 : 2);
                    else if (facial == "moustache" && lip) c.Set(x, y, Hair, 2);
                    else if (facial == "stubble" && jaw && ((x + y) & 1) == 0) c.Set(x, y, Skin, 3);
                }
            if (facial == "beard" && delver)
                for (int y = cy + 13; y < cy + 17; y++)
                    for (int x = cx + 1; x <= cx + 5; x++) c.Set(x, y, Hair, y % 3 == 0 ? 3 : 2);

            // 7. Hair in front; under a helm only what hangs below its brim
            if (!helm || style is "long" or "braids" or "ponytail") Front(ref c, cx, cy, rx, ry, helm ? "crop" : style);

            // 8. The accessory (D4), the hurt's bandage, the helm and the tabard (D5)
            Accessory(ref c, cx, cy, rx, ry, ey, near, ear, accessory);
            if (mood == Mood.Hurt)
                for (int y = cy - 6; y <= cy - 5; y++)
                    for (int x = cx - rx + 2; x <= cx + rx; x++) if (c.At(x, y) is Skin or Hair) c.Set(x, y, Bandage, y == cy - 6 ? 1 : 2);
            if (helm) HelmOn(ref c, cx, cy, rx, ry);
            if (tabard)
                for (int y = 40; y < Size; y++)
                    for (int x = cx - 2; x <= cx + 3; x++)
                        if (c.At(x, y) is Cloth or Trim) c.Set(x, y, (y is 43 or 44) && (x == cx || x == cx + 1) ? Gold : Tabard, x < cx ? 2 : 3);

            Paint(ref c, look, gear, into);
        }

        private static void Outfit(ref Canvas c, int cx, string outfit, int shoulders)
        {
            for (int y = 34; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (c.At(x, y) != Cloth) continue;
                    bool edge = c.At(x, y - 2) == None; // the shoulders' top
                    int dx = x - cx;
                    switch (outfit)
                    {
                        case "jerkin": // a laced V
                            if (y <= 44 && Math.Abs(dx - 1) <= (44 - y) / 2) c.Set(x, y, Math.Abs(dx - 1) == (44 - y) / 2 ? Trim : Skin, 3);
                            else if (edge && Math.Abs(dx) < 10) c.Set(x, y, Trim, 3);
                            break;
                        case "cloak": // over the shoulders, a clasp at the collar
                            if (edge || c.At(x, y - 4) == None || Math.Abs(dx) > shoulders - 8) c.Set(x, y, Trim, Band(x - 24, y - 51, shoulders, 15, false));
                            break;
                        case "hood": // the hood lies behind; a round collar in front
                        case "tunic":
                            if ((edge && Math.Abs(dx) < 10) || (y >= 41 && (x == cx + 1 || x == cx + 2))) c.Set(x, y, Trim, edge ? Band(x - 24, y - 51, shoulders, 15, false) : 3);
                            break;
                        case "robe": // a crossed wrap: the near panel over the far
                            if (y >= 38 && (dx + 2) * 3 == (y - 38) * 2 + 0 || (y >= 38 && (dx + 3) * 3 == (y - 38) * 2)) c.Set(x, y, Trim, 3);
                            break;
                        case "furs": // a shaggy collar
                            if ((edge || c.At(x, y - 3) == None) && ((x * 3 + y) % 4 != 0)) c.Set(x, y, Trim, (x + y) % 3 == 0 ? 2 : 3);
                            break;
                        case "pauldron":
                            if (edge && Math.Abs(dx) < 10) c.Set(x, y, Trim, 3);
                            break;
                        case "scarf":
                            break;
                    }
                }
            if (outfit == "cloak")
                for (int y = 40; y <= 41; y++)
                    for (int x = cx - 5; x <= cx - 4; x++) c.Set(x, y, Gold, y == 40 ? 1 : 3);
            if (outfit == "pauldron") // a steel plate on the near shoulder, riveted
            {
                for (int y = 34; y <= 42; y++)
                    for (int x = cx + 9; x <= cx + 22; x++)
                        if (Inside(x - (cx + 15), y - 41, 7, 6) && c.At(x, y) != None) c.Set(x, y, Steel, Band(x - (cx + 15), y - 41, 7, 6, false));
                c.Set(cx + 15, 37, Line, 1);
            }
            if (outfit == "scarf") // its tail hanging at the near side
                for (int y = 39; y <= 45; y++)
                    for (int x = cx + 4; x <= cx + 6; x++) c.Set(x, y, Trim, x == cx + 4 ? 2 : 3);
        }

        private static void Eyes(ref Canvas c, int near, int far, int y, Mood mood)
        {
            switch (mood)
            {
                case Mood.Tired: // half-lidded
                    Lid(ref c, near - 1, near + 1, y);
                    c.Set(near, y + 1, Sclera, 2);
                    c.Set(near + 1, y + 1, Iris, 3);
                    Lid(ref c, far, far + 1, y);
                    c.Set(far, y + 1, Iris, 3);
                    c.Set(far + 1, y + 1, Sclera, 3);
                    c.Set(near, y + 2, Skin, 3);
                    break;
                case Mood.Asleep: // closed
                    for (int x = near - 1; x <= near + 1; x++) c.Set(x, y + 1, Line, 1);
                    for (int x = far; x <= far + 1; x++) c.Set(x, y + 1, Line, 1);
                    break;
                case Mood.Hurt: // the near eye squeezed shut, the far open
                    c.Set(near - 1, y, Line, 1);
                    c.Set(near, y + 1, Line, 1);
                    c.Set(near + 1, y, Line, 1);
                    Open(ref c, far, y, wide: false, startled: false);
                    break;
                case Mood.Scared: // wide: white all round the iris
                    Open(ref c, near, y, wide: true, startled: true);
                    Open(ref c, far, y, wide: false, startled: true);
                    break;
                default:
                    Open(ref c, near, y, wide: true, startled: false);
                    Open(ref c, far, y, wide: false, startled: false);
                    break;
            }
        }

        // A heavy lid: the lash line drawn down over the eye's top row
        private static void Lid(ref Canvas c, int from, int to, int y)
        {
            for (int x = from; x <= to; x++) c.Set(x, y, Line, 1);
        }

        // An open eye: the near eye two pixels wide (white, iris), the far one narrower, a lash line above
        private static void Open(ref Canvas c, int x, int y, bool wide, bool startled)
        {
            if (wide)
            {
                c.Set(x, y, Sclera, 2);
                c.Set(x + 1, y, Iris, 2);
                c.Set(x, y + 1, Sclera, 2);
                c.Set(x + 1, y + 1, Iris, 3);
                if (startled) { c.Set(x + 2, y, Sclera, 2); c.Set(x + 2, y + 1, Sclera, 3); }
                for (int lx = x - 1; lx <= x + (startled ? 2 : 1); lx++) c.Set(lx, y - 1, Line, 1);
            }
            else
            {
                c.Set(x, y, Iris, 2);
                c.Set(x, y + 1, Iris, 3);
                c.Set(x + 1, y + 1, Sclera, 3);
                if (startled) c.Set(x + 1, y, Sclera, 2);
                for (int lx = x; lx <= x + 1; lx++) c.Set(lx, y - 1, Line, 1);
            }
        }

        private static void Brows(ref Canvas c, int near, int far, int ey, Mood mood, int rows, bool female)
        {
            int lift = mood is Mood.Hungry or Mood.Scared ? 1 : 0;
            bool knit = mood is Mood.Frustrated or Mood.Hurt; // their inner ends drawn down toward the nose
            for (int r = 0; r < rows; r++)
            {
                byte m = r == 0 ? Hair : Skin;
                int top = ey - 2 - r - lift;
                for (int x = near - 1; x <= near + 2; x++)
                {
                    if (female && r == 0 && x == near + 2) continue; // finer brows
                    c.Set(x, top + (knit && x <= near ? 1 : 0), m, 3);
                }
                for (int x = far; x <= far + 1; x++) c.Set(x, top + (knit && x == far ? 1 : 0), m, 3);
            }
        }

        private static void Mouth(ref Canvas c, int cx, int my, Mood mood)
        {
            switch (mood)
            {
                case Mood.Content: // a soft smile
                    for (int x = cx + 2; x <= cx + 5; x++) c.Set(x, my, Lip, 4);
                    c.Set(cx + 1, my - 1, Lip, 4);
                    c.Set(cx + 6, my - 1, Lip, 4);
                    break;
                case Mood.Hungry: // a small open mouth
                    for (int x = cx + 3; x <= cx + 4; x++)
                    {
                        c.Set(x, my - 1, Lip, 4);
                        c.Set(x, my, Line, 1);
                        c.Set(x, my + 1, Lip, 4);
                    }
                    break;
                case Mood.Frustrated:
                case Mood.Hurt: // a frown
                    for (int x = cx + 2; x <= cx + 5; x++) c.Set(x, my, Lip, 4);
                    c.Set(cx + 1, my + 1, Lip, 4);
                    c.Set(cx + 6, my + 1, Lip, 4);
                    break;
                case Mood.Scared: // open wide
                    for (int x = cx + 3; x <= cx + 4; x++) c.Set(x, my - 1, Line, 1);
                    for (int x = cx + 2; x <= cx + 5; x++) c.Set(x, my, Line, 1);
                    for (int x = cx + 3; x <= cx + 4; x++) c.Set(x, my + 1, Lip, 4);
                    break;
                case Mood.Asleep: // slack
                    for (int x = cx + 3; x <= cx + 4; x++) c.Set(x, my, Lip, 4);
                    break;
                default: // flat
                    for (int x = cx + 2; x <= cx + 5; x++) c.Set(x, my, Lip, 4);
                    break;
            }
        }

        private static void Accessory(ref Canvas c, int cx, int cy, int rx, int ry, int ey, int near, int ear, string accessory)
        {
            switch (accessory)
            {
                case "earring":
                    c.Set(ear, cy + 6, Gold, 1);
                    c.Set(ear, cy + 7, Gold, 3);
                    break;
                case "circlet": // across the brow, over the hair
                    for (int x = cx - rx + 1; x <= cx + rx; x++)
                    {
                        int y = cy - 6 + (x < cx - 3 ? 1 : 0);
                        if (c.At(x, y) != None) c.Set(x, y, Gold, x < cx ? 1 : 3);
                    }
                    break;
                case "feather": // tucked behind the ear, rising
                    for (int i = 0; i < 12; i++)
                    {
                        int x = ear - 1 - i / 3, y = cy - 1 - i;
                        c.Set(x, y, Trim, i < 2 ? 3 : 2);
                        c.Set(x - 1, y, Trim, 3);
                    }
                    break;
                case "scar": // a pale line across the near cheek
                    for (int i = 0; i < 4; i++) if (c.At(near + 2 + i, ey + 2 + i) == Skin) c.Set(near + 2 + i, ey + 2 + i, Skin, 1);
                    break;
                case "warpaint": // two stripes under the near eye
                    for (int x = near - 1; x <= near + 1; x++)
                    {
                        c.Set(x, ey + 3, Warpaint, 2);
                        c.Set(x + 1, ey + 5, Warpaint, 3);
                    }
                    break;
            }
        }

        // The miner's helm (D5): a dome over the crown, a brim at the brow reaching forward, the lamp lit at its front
        private static void HelmOn(ref Canvas c, int cx, int cy, int rx, int ry)
        {
            int brim = cy - 5;
            for (int y = cy - ry - 3; y < brim; y++)
                for (int x = cx - rx - 2; x <= cx + rx + 2; x++)
                    if (Inside(x - cx, y - brim, rx + 2, ry + 3 - (cy - brim) + 3)) c.Set(x, y, Helm, Band(x - cx, y - brim, rx + 2, 9, false));
            for (int x = cx - rx - 2; x <= cx + rx + 4; x++) c.Set(x, brim, Helm, x < cx ? 3 : 4);
            for (int y = brim - 6; y <= brim - 3; y++)
                for (int x = cx + 5; x <= cx + 8; x++) c.Set(x, y, Lamp, (x == cx + 6 && y == brim - 5) ? 1 : 2);
        }

        // The hair over the head: a cap above a hairline that drops toward the back of the head (left) and sits high over the face
        private static void Front(ref Canvas c, int cx, int cy, int rx, int ry, string style)
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
                    if (style == "side-part" && x == cx - 3 && y < cy - 6) { c.Set(x, y, Skin, 2); continue; }
                    int tone = Band(dx, dy, rx + grow, ry + grow, false);
                    if (style is "curls" or "coils" && ((x * 3 + y * 5) % 7 == 0)) tone = Math.Min(4, tone + 1); // curl shadows
                    c.Set(x, y, Hair, tone);
                }
        }

        private static void Dome(ref Canvas c, int cx, int cy, int rx, int ry, byte m, bool soft, int maxY = Size - 1, bool band = false)
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
        private static void Squircle(ref Canvas c, int cx, int cy, int rx, int ry, byte m)
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

        private static void Paint(ref Canvas c, in CrawlerLook look, in ItemLook gear, Span<Rgba32> into)
        {
            var skin = SpriteKit.RampOf(LookPalettes.SkinOf(look));
            var hair = SpriteKit.RampOf(LookPalettes.HairOf(look));
            var cloth = SpriteKit.RampOf(LookPalettes.PrimaryOf(look));
            var trim = SpriteKit.RampOf(LookPalettes.SecondaryOf(look));
            var iris = SpriteKit.RampOf(LookPalettes.EyesOf(look));
            var gold = SpriteKit.RampOf(GoldColour);
            var steel = SpriteKit.RampOf(SteelColour);
            var helm = SpriteKit.RampOf(gear.Primary);
            var tabard = SpriteKit.RampOf(BodyKit.TabardColour);
            var paint = SpriteKit.RampOf(PaintColour);
            var bandage = SpriteKit.RampOf(BandageColour);

            // The hairline: hair just above skin takes its deepest tone, so the face reads at 1x
            for (int y = 1; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if (c.At(x, y - 1) == Hair && c.At(x, y) == Skin) c.Tone[(y - 1) * Size + x] = 4;

            for (int i = 0; i < Size * Size; i++)
            {
                byte m = c.Mat[i];
                int t = c.Tone[i];
                into[i] = m switch
                {
                    None => Rgba32.Clear,
                    Sclera => ScleraColour,
                    Line => SpriteKit.Outline,
                    Lip => skin.Deep,
                    Lamp => t == 1 ? new Rgba32(255, 255, 230) : gear.Secondary,
                    _ => RampFor(m, skin, hair, cloth, trim, iris, gold, steel, helm, tabard, paint, bandage)[t],
                };
            }

            // The warm rim: the far (right) edge of each row above the shoulders' foot, a pixel wide
            for (int y = 0; y < Size - 4; y++)
                for (int x = Size - 1; x >= 0; x--)
                {
                    byte m = c.Mat[y * Size + x];
                    if (m == None) continue;
                    if (m is Skin or Hair or Cloth or Trim or Helm or Tabard) into[y * Size + x] = Mix(RampFor(m, skin, hair, cloth, trim, iris, gold, steel, helm, tabard, paint, bandage).Base, Warm);
                    break;
                }

            // The outline, into clear pixels only: the map's colour in shadow (right and below), the material's deepest tone where lit
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    if (c.Mat[y * Size + x] != None) continue;
                    byte lit = c.At(x + 1, y) != None ? c.At(x + 1, y) : c.At(x, y + 1);
                    byte shadow = c.At(x - 1, y) != None ? c.At(x - 1, y) : c.At(x, y - 1);
                    if (shadow != None) into[y * Size + x] = SpriteKit.Outline;
                    else if (lit != None) into[y * Size + x] = lit is Sclera or Line or Lip or Lamp ? SpriteKit.Outline : RampFor(lit, skin, hair, cloth, trim, iris, gold, steel, helm, tabard, paint, bandage).Deep;
                }
        }

        private static Ramp RampFor(byte m, in Ramp skin, in Ramp hair, in Ramp cloth, in Ramp trim, in Ramp iris, in Ramp gold, in Ramp steel, in Ramp helm, in Ramp tabard, in Ramp paint, in Ramp bandage) => m switch
        {
            Hair => hair,
            Cloth => cloth,
            Trim => trim,
            Iris => iris,
            Gold => gold,
            Steel => steel,
            Helm => helm,
            Tabard => tabard,
            Warpaint => paint,
            Bandage => bandage,
            _ => skin,
        };

        private static Rgba32 Mix(Rgba32 a, Rgba32 b) => new Rgba32((byte)((a.R + b.R) / 2), (byte)((a.G + b.G) / 2), (byte)((a.B + b.B) / 2));

        /// <summary>The hub's stonework behind a bust at (x, y) (outside the bake): red brick in offset courses, a dark pillar on the left.</summary>
        public static Rgba32 Backdrop(int x, int y)
        {
            int course = y / 5, shift = (course & 1) * 5;
            bool mortar = y % 5 == 4 || (x + shift) % 10 == 9;
            int brick = ((x + shift) / 10 * 7 + course * 3) % 4;
            if (x == 10) return Mortar;
            return x < 10 ? Pillar : mortar ? Mortar : brick == 0 ? BrickB : BrickA;
        }

        /// <summary>
        /// <paramref name="bust"/> over the stonework in its two-pixel frame by <paramref name="standing"/> (D6), into
        /// <paramref name="into"/> (<see cref="FramedSize"/> square).
        /// </summary>
        public static void Framed(ReadOnlySpan<Rgba32> bust, CrawlerStanding standing, Span<Rgba32> into)
        {
            var (outer, inner) = FrameOf(standing);
            for (int y = 0; y < FramedSize; y++)
                for (int x = 0; x < FramedSize; x++)
                {
                    int i = y * FramedSize + x;
                    if (x == 0 || y == 0 || x == FramedSize - 1 || y == FramedSize - 1) { into[i] = outer; continue; }
                    if (x == 1 || y == 1 || x == FramedSize - 2 || y == FramedSize - 2) { into[i] = inner; continue; }
                    var p = bust[(y - 2) * Size + (x - 2)];
                    into[i] = p.A != 0 ? p : Backdrop(x - 2, y - 2);
                }
        }

        /// <summary><paramref name="source"/> (<paramref name="side"/> square) magnified by <paramref name="scale"/>, each pixel a block, into <paramref name="into"/>.</summary>
        public static void Magnify(ReadOnlySpan<Rgba32> source, int side, int scale, Span<Rgba32> into)
        {
            int big = side * scale;
            for (int y = 0; y < big; y++)
                for (int x = 0; x < big; x++)
                    into[y * big + x] = source[(y / scale) * side + x / scale];
        }
    }

    /// <summary>A portrait: a look with its mood, its standing (the frame, the tabard) and what it wears on its head.</summary>
    public readonly struct PortraitKey : IEquatable<PortraitKey>
    {
        public PortraitKey(in CrawlerLook look, Mood mood, CrawlerStanding standing, ItemType headGear)
        {
            Look = look;
            Mood = mood;
            Standing = standing;
            HeadGear = headGear;
        }

        public CrawlerLook Look { get; }
        public Mood Mood { get; }
        public CrawlerStanding Standing { get; }

        /// <summary>What it wears on its head; None for nothing, or for gear not worn on the head.</summary>
        public ItemType HeadGear { get; }

        public bool Equals(PortraitKey other) => Look.Equals(other.Look) && Mood == other.Mood && Standing == other.Standing && HeadGear == other.HeadGear;

        public override bool Equals(object obj) => obj is PortraitKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Look.GetHashCode(), (int)Mood, (int)Standing, (int)HeadGear);
    }
}
