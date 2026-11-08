using System;
using Simpiens.Entities;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.People;

namespace Simpiens.Presentation.Art.People
{
    /// <summary>
    /// A crawler's body on the map, baked from its look (Epic UI-Beta B2, design §3.2): today's 24 pose frames, read in
    /// their key letters (<see cref="ProceduralSprites.CrawlerKeys"/>), then the look applied to the keys in a fixed
    /// order (the hair style and facial hair at the frame's head anchor, the helm, the build, the delvers' height, the
    /// guild's tabard), then the keys painted in the look's colours (<see cref="LookPalettes"/>). Pure, in integer
    /// maths, on the stack: the same look always bakes the same pixels, and a bake allocates only what it returns.
    /// Asleep, the blanket keeps the bunk's colours and the body takes no build or height (B0's design note, §9).
    /// </summary>
    public static class BodyKit
    {
        public const int Size = 16;

        /// <summary>Frames in a body: two per <see cref="AgentVisualState"/>.</summary>
        public const int FrameCount = 24;

        public const int PixelsPerFrame = Size * Size;
        public const int PixelsPerBody = FrameCount * PixelsPerFrame;

        /// <summary>The guild's tabard (D8): deep blue, with a gold device at the breast.</summary>
        public static readonly Rgba32 TabardColour = new Rgba32(30, 58, 138), TabardDevice = new Rgba32(250, 204, 21);

        // The keys the kit adds to a pattern's: hair in shade, the helm and its brim, the helm's lamp, the tabard and its device
        private const char HairShade = 'h', Helm = 'M', Brim = 'm', Lamp = 'L', Tabard = 'G', Device = 'g', Mask = 'Q', Filter = 'q';

        // What a pose holds besides the body: the tool, the bubbles' white and the marks over the head
        private const string Carried = "TW!?Z";

        /// <summary>Where <paramref name="state"/>'s <paramref name="frame"/> sits in a body: state by state, two frames each.</summary>
        public static int FrameIndex(AgentVisualState state, int frame) => (int)state * 2 + (frame & 1);

        /// <summary>
        /// Bakes all 24 frames of a body into <paramref name="into"/> (<see cref="PixelsPerBody"/> pixels), frame after
        /// frame in <see cref="FrameIndex"/> order, each row by row from the top. <paramref name="headGear"/> is what the
        /// crawler wears on its head (None for nothing); <paramref name="tabard"/>, whether it wears the guild's tabard.
        /// The look must be complete: every gene an option the palettes colour.
        /// </summary>
        public static void Bake(in CrawlerLook look, ItemType headGear, bool tabard, Span<Rgba32> into)
        {
            if (into.Length < PixelsPerBody) throw new ArgumentException($"A body needs {PixelsPerBody} pixels.", nameof(into));
            var colours = new Colours(look, headGear);
            for (int s = 0; s < FrameCount / 2; s++)
            {
                for (int f = 0; f < 2; f++)
                {
                    var state = (AgentVisualState)s;
                    BakeFrame(look, colours, tabard, state, f, into.Slice(FrameIndex(state, f) * PixelsPerFrame, PixelsPerFrame));
                }
            }
        }

        /// <summary>One frame of a body (<see cref="PixelsPerFrame"/> pixels): see <see cref="Bake"/>.</summary>
        public static void BakeFrame(in CrawlerLook look, ItemType headGear, bool tabard, AgentVisualState state, int frame, Span<Rgba32> into)
            => BakeFrame(look, new Colours(look, headGear), tabard, state, frame, into);

        /// <summary>A body's frames as sprites named "<paramref name="name"/>_Agent_Walk_0" (review sheets and pins).</summary>
        public static PixelSprite[] Sprites(in CrawlerLook look, ItemType headGear, bool tabard, string name)
        {
            var pixels = new Rgba32[PixelsPerBody];
            Bake(look, headGear, tabard, pixels);
            var sprites = new PixelSprite[FrameCount];
            foreach (var (state, frames) in CrawlerFrames.All)
            {
                for (int f = 0; f < 2; f++)
                {
                    int index = FrameIndex(state, f);
                    sprites[index] = new PixelSprite(name + "_" + frames[f], Size, Size, pixels.AsSpan(index * PixelsPerFrame, PixelsPerFrame).ToArray());
                }
            }
            return sprites;
        }

        private static readonly bool[] _onHead = BuildWorn(Worn.Head);
        private static readonly bool[] _onFace = BuildWorn(Worn.Face);

        private static bool[] BuildWorn(Worn where)
        {
            var items = (ItemType[])Enum.GetValues(typeof(ItemType));
            int most = 0;
            foreach (var item in items) most = Math.Max(most, (int)item);
            var worn = new bool[most + 1];
            foreach (var item in items)
                worn[(int)item] = item != ItemType.None && ItemArt.TryGet(ItemCatalog.Get(item)?.ArtKey, out var art) && art.Worn == where;
            return worn;
        }

        /// <summary>Whether <paramref name="item"/> is worn on the head, and so drawn on a body and a portrait (a helm).</summary>
        public static bool IsWornOnHead(ItemType item) => (uint)item < (uint)_onHead.Length && _onHead[(int)item];

        /// <summary>Whether <paramref name="item"/> is worn over the face (a respirator's mask, Epic 10 Part 2, D52), and so drawn on a body and a portrait.</summary>
        public static bool IsWornOnFace(ItemType item) => (uint)item < (uint)_onFace.Length && _onFace[(int)item];

        /// <summary><paramref name="worn"/> if it is worn on the head or the face, else None: what shows of a crawler's protection.</summary>
        public static ItemType HeadGearOf(ItemType worn) => IsWornOnHead(worn) || IsWornOnFace(worn) ? worn : ItemType.None;

        /// <summary>The hair styles that hang below the brow, and so show under a helm.</summary>
        public static bool HangsBelowTheBrow(string hairStyle) => hairStyle is "long" or "ponytail" or "braids";

        private static void BakeFrame(in CrawlerLook look, in Colours colours, bool tabard, AgentVisualState state, int frame, Span<Rgba32> into)
        {
            Span<char> keys = stackalloc char[PixelsPerFrame];
            var pattern = ProceduralSprites.CrawlerKeys(state, frame);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    keys[y * Size + x] = pattern[y][x];

            var head = HeadAnchors.Of(state, frame);
            string style = LookCatalog.KeyOf(LookGene.HairStyle, look.HairStyle);
            if (colours.HasHelm && !HangsBelowTheBrow(style)) style = "crop"; // what rises above the crown is under the helm
            Hair(keys, head, style);
            FacialHair(keys, head, LookCatalog.KeyOf(LookGene.FacialHair, look.FacialHair));
            if (colours.HasHelm) HelmOn(keys, head);
            if (colours.HasMask) MaskOn(keys, head);

            int shift = 0;
            if (!head.Lying)
            {
                Build(keys, head.BodyTop, LookCatalog.KeyOf(LookGene.Build, look.Build));
                if (LookCatalog.KeyOf(LookGene.Lineage, look.Lineage) == "delver" && Shorten(keys, head.BodyTop)) shift = 1;
                if (tabard) TabardOn(keys, head.BodyTop + shift);
            }

            for (int i = 0; i < keys.Length; i++) into[i] = colours.Of(keys[i], head.Lying);
        }

        // --- Hair, at the head anchor: the hair's top row T, the eyes E, the chin C; the head's inside is columns 5 to 10 ---

        private static void Hair(Span<char> keys, in HeadAnchor head, string style)
        {
            int t = head.HairTop, l = HeadAnchor.Left, r = HeadAnchor.Right, mid = (l + r) / 2;
            // A lying head hangs nothing onto the blanket
            int hangTo = head.Lying ? head.Chin : head.Chin + 2;
            switch (style)
            {
                case "shaved":
                    Replace(keys, 'H', 'K', t, head.Eyes - 1);
                    break;
                case "side-part":
                    // The fringe swept across from the left
                    if (!head.Lying) for (int x = l + 1; x <= l + 2; x++) Set(keys, x, t + 1, 'H', "K");
                    else Set(keys, mid + 1, t, HairShade, "H");
                    break;
                case "long":
                    for (int y = t + 1; y <= hangTo; y++) Locks(keys, y, 'H', "#.");
                    break;
                case "ponytail":
                    // A tail behind the right ear, tied once
                    for (int y = t + 1; y <= Math.Min(hangTo, head.Chin + 1); y++)
                    {
                        Set(keys, r + 1, y, y == t + 1 ? HairShade : 'H', "#.");
                        Set(keys, r + 2, y, '#', ".");
                    }
                    break;
                case "braids":
                    // Two braids over the shoulders, banded hair and shade
                    for (int y = t + 1; y <= (head.Lying ? head.Chin : head.Chin + 3); y++) Locks(keys, y, ((y - t) & 1) == 0 ? HairShade : 'H', "#.C");
                    break;
                case "bun":
                    for (int x = mid; x <= mid + 1; x++)
                    {
                        Set(keys, x, t - 1, 'H', "#");
                        Set(keys, x, t - 2, 'H', ".#");
                        Set(keys, x, t - 3, '#', ".");
                    }
                    Set(keys, mid - 1, t - 2, '#', ".");
                    Set(keys, mid + 2, t - 2, '#', ".");
                    break;
                case "curls":
                    // A crown of curls: bumps above, fuller at the temples, shade between
                    for (int x = l; x <= r; x += 2)
                    {
                        Set(keys, x, t - 1, 'H', "#");
                        Set(keys, x, t - 2, '#', ".");
                    }
                    for (int x = l + 1; x <= r; x += 2) Set(keys, x, t, HairShade, "H");
                    for (int y = t; y <= t + 1 && !head.Lying; y++) Locks(keys, y, 'H', "#.");
                    break;
                case "coils":
                    // A rounded mass a pixel past the head all round
                    for (int x = l; x <= r; x++) Set(keys, x, t - 1, ((x + t) & 1) == 0 ? 'H' : HairShade, "#");
                    for (int x = l + 1; x <= r - 1; x++)
                    {
                        Set(keys, x, t - 2, 'H', ".#");
                        Set(keys, x, t - 3, '#', ".");
                    }
                    Set(keys, l, t - 2, '#', ".");
                    Set(keys, r, t - 2, '#', ".");
                    for (int y = t - 1; y <= (head.Lying ? t : t + 2); y++) Locks(keys, y, 'H', "#.");
                    break;
                case "mohawk":
                    Replace(keys, 'H', 'K', t, head.Eyes - 1);
                    for (int x = mid; x <= mid + 1; x++)
                    {
                        Set(keys, x, t, 'H', "K");
                        Set(keys, x, t - 1, 'H', "#");
                        Set(keys, x, t - 2, 'H', ".");
                        Set(keys, x, t - 3, '#', ".");
                    }
                    Set(keys, mid - 1, t - 2, '#', ".");
                    Set(keys, mid + 2, t - 2, '#', ".");
                    break;
            }
        }

        // Hair either side of the head on row y (over the head's outline), outlined outside
        private static void Locks(Span<char> keys, int y, char hair, string over)
        {
            Set(keys, HeadAnchor.Left - 1, y, hair, over);
            Set(keys, HeadAnchor.Right + 1, y, hair, over);
            Set(keys, HeadAnchor.Left - 2, y, '#', ".");
            Set(keys, HeadAnchor.Right + 2, y, '#', ".");
        }

        private static void FacialHair(Span<char> keys, in HeadAnchor head, string kind)
        {
            // The face is three rows: hair across a whole row reads as a mask, so a beard hangs below the chin (§9)
            int c = head.Chin, mid = (HeadAnchor.Left + HeadAnchor.Right) / 2;
            switch (kind)
            {
                case "stubble":
                    for (int x = mid - 1; x <= mid + 2; x++) Set(keys, x, c, HairShade, "K");
                    break;
                case "moustache":
                    for (int x = mid; x <= mid + 1; x++) Set(keys, x, c, 'H', "K");
                    break;
                case "beard":
                    // Asleep, it lies on the blanket's edge and no further
                    for (int x = mid; x <= mid + 1; x++) Set(keys, x, c, 'H', "K");
                    for (int x = mid - 1; x <= mid + 2; x++) Set(keys, x, c + 1, 'H', head.Lying ? "#" : "#C");
                    if (!head.Lying) for (int x = mid; x <= mid + 1; x++) Set(keys, x, c + 2, 'H', "C");
                    break;
            }
        }

        // The miner's helm (D4): the hair's top row becomes the helm with its lamp, the brim crosses the brow a pixel past
        // the head each side; hair below the brim still shows. A lying head wears the helm without a brim.
        private static void HelmOn(Span<char> keys, in HeadAnchor head)
        {
            int t = head.HairTop, l = HeadAnchor.Left, r = HeadAnchor.Right, mid = (l + r) / 2;
            for (int x = l; x <= r; x++) Set(keys, x, t, x == mid || x == mid + 1 ? Lamp : Helm, "HKh#");
            if (head.Lying) return;
            for (int x = l - 1; x <= r + 1; x++) Set(keys, x, t + 1, Brim, "HKh#.");
            Set(keys, l - 2, t + 1, '#', ".");
            Set(keys, r + 2, t + 1, '#', ".");
        }

        // The respirator's mask (D52): the mouth's row across the face, its filters at either side, over facial hair too
        private static void MaskOn(Span<char> keys, in HeadAnchor head)
        {
            int c = head.Chin, l = HeadAnchor.Left, r = HeadAnchor.Right;
            for (int x = l; x <= r; x++) Set(keys, x, c, x == l || x == r ? Filter : Mask, "KHh");
        }

        // --- The body, from its first row under the head down ---

        private static void Build(Span<char> keys, int fromRow, string build)
        {
            switch (build)
            {
                case "slight": // one column narrower: the left side moves in over the middle column
                    for (int y = fromRow; y < Size; y++)
                    {
                        var row = keys.Slice(y * Size, Size);
                        for (int x = Size / 2 - 1; x > 0; x--) row[x] = row[x - 1];
                        row[0] = '.';
                    }
                    break;
                case "broad": // one wider: the left side moves out, the middle column doubles
                    Widen(keys, fromRow, left: true);
                    break;
                case "heavy": // one wider each side
                    Widen(keys, fromRow, left: true);
                    Widen(keys, fromRow, left: false);
                    break;
            }
        }

        private static void Widen(Span<char> keys, int fromRow, bool left)
        {
            int middle = Size / 2 - 1;
            for (int y = fromRow; y < Size; y++)
            {
                var row = keys.Slice(y * Size, Size);
                if (left) for (int x = 0; x < middle; x++) row[x] = row[x + 1];
                else for (int x = Size - 1; x > middle + 1; x--) row[x] = row[x - 1];
            }
        }

        // A delver is a row shorter (§9): the first trouser row that holds nothing else (a tool, a mark) goes, and
        // everything above it moves down one, the head full size
        private static bool Shorten(Span<char> keys, int fromRow)
        {
            int legs = -1;
            for (int y = fromRow; y < Size && legs < 0; y++)
            {
                var row = keys.Slice(y * Size, Size);
                if (row.IndexOf('S') >= 0 && row.IndexOfAny(Carried) < 0) legs = y;
            }
            if (legs < 1) return false;
            for (int y = legs; y > 0; y--) keys.Slice((y - 1) * Size, Size).CopyTo(keys.Slice(y * Size, Size));
            keys.Slice(0, Size).Fill('.');
            return true;
        }

        // The guild's tabard (D8): two columns down the middle of the shirt, the device at the breast, a hem over the belt
        private static void TabardOn(Span<char> keys, int bodyTop)
        {
            var shoulders = keys.Slice(bodyTop * Size, Size);
            int left = -1, right = -1;
            for (int x = 0; x < Size; x++)
            {
                if (shoulders[x] == '.') continue;
                if (left < 0) left = x;
                right = x;
            }
            if (left < 0) return;
            int centre = (left + right) / 2;
            for (int y = bodyTop; y < Size; y++)
            {
                bool hem = keys.Slice(y * Size, Size).IndexOf('S') >= 0;
                for (int x = centre; x <= centre + 1; x++) Set(keys, x, y, y == bodyTop + 1 ? Device : Tabard, hem ? "S" : "C");
                if (hem) break;
            }
        }

        private static void Replace(Span<char> keys, char from, char to, int firstRow, int lastRow)
        {
            for (int y = Math.Max(0, firstRow); y <= Math.Min(Size - 1, lastRow); y++)
                for (int x = 0; x < Size; x++)
                    if (keys[y * Size + x] == from) keys[y * Size + x] = to;
        }

        // Writes key at (x, y) if the key there is one of over's
        private static void Set(Span<char> keys, int x, int y, char key, string over)
        {
            if ((uint)x >= Size || (uint)y >= Size) return;
            if (over.IndexOf(keys[y * Size + x]) >= 0) keys[y * Size + x] = key;
        }

        /// <summary>A look's colours by key, resolved once per bake.</summary>
        private readonly struct Colours
        {
            private readonly Rgba32 _skin, _hair, _hairShade, _shirt, _trousers, _helm, _brim, _lamp, _mask, _filter;

            public Colours(in CrawlerLook look, ItemType headGear)
            {
                _skin = LookPalettes.SkinOf(look);
                _hair = LookPalettes.HairOf(look);
                _hairShade = SpriteKit.RampOf(_hair).Shade;
                _shirt = LookPalettes.PrimaryOf(look);
                _trousers = SpriteKit.RampOf(LookPalettes.SecondaryOf(look)).Shade;
                HasHelm = headGear != ItemType.None && ItemArt.TryGet(ItemCatalog.Get(headGear)?.ArtKey, out var gear) && gear.Worn == Worn.Head;
                if (HasHelm)
                {
                    ItemArt.TryGet(ItemCatalog.Get(headGear).ArtKey, out var helm);
                    _helm = helm.Primary;
                    _brim = SpriteKit.RampOf(helm.Primary).Shade;
                    _lamp = helm.Secondary;
                }
                else _helm = _brim = _lamp = Rgba32.Clear;
                ItemLook face = default;
                HasMask = headGear != ItemType.None && ItemArt.TryGet(ItemCatalog.Get(headGear)?.ArtKey, out face) && face.Worn == Worn.Face;
                _mask = HasMask ? face.Primary : Rgba32.Clear;
                _filter = HasMask ? face.Secondary : Rgba32.Clear;
            }

            public bool HasHelm { get; }
            public bool HasMask { get; }

            public Rgba32 Of(char key, bool asleep) => key switch
            {
                '.' => Rgba32.Clear,
                'K' => _skin,
                'H' => _hair,
                HairShade => _hairShade,
                'C' => asleep ? ProceduralSprites.CrawlerKeyColour('C', asleep: true) : _shirt,
                'S' => asleep ? ProceduralSprites.CrawlerKeyColour('S', asleep: true) : _trousers,
                Helm => _helm,
                Brim => _brim,
                Lamp => _lamp,
                Mask => _mask,
                Filter => _filter,
                Tabard => TabardColour,
                Device => TabardDevice,
                _ => ProceduralSprites.CrawlerKeyColour(key),
            };
        }
    }
}
