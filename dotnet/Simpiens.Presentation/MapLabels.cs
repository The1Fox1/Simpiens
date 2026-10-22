using System;
using System.Collections.Generic;

namespace Simpiens.Presentation
{
    public enum LabelKind : byte
    {
        /// <summary>A room's name (the map's labels): grows and shrinks with the zoom, one size per row.</summary>
        Room = 0,
        /// <summary>"UNCHARTED" under an uncharted room's name, one size smaller, fading with the fog.</summary>
        Uncharted = 1,
        /// <summary>The selected crawler's name above it, at one size.</summary>
        Name = 2,
    }

    /// <summary>Text the map asks for: a world point to hang it on, in a colour (its alpha fades it).</summary>
    public readonly struct WorldLabel
    {
        public readonly string Text;
        public readonly Vec2 At;
        public readonly LabelKind Kind;
        public readonly Rgba32 Color;

        public WorldLabel(string text, Vec2 at, LabelKind kind, Rgba32 color)
        {
            Text = text;
            At = at;
            Kind = kind;
            Color = color;
        }
    }

    /// <summary>One glyph to draw on screen: a rectangle of a font page, at whole pixels.</summary>
    public readonly struct ScreenGlyph
    {
        /// <summary>The page in the <see cref="FontLibrary"/>'s numbering (<see cref="BitmapFontMetrics.FirstPage"/>): one integer, no lookup by face (review R3).</summary>
        public readonly int Page;
        public readonly int SourceX, SourceY, Width, Height, X, Y;
        public readonly Rgba32 Color;

        public ScreenGlyph(int page, int sourceX, int sourceY, int width, int height, int x, int y, Rgba32 color)
        {
            Page = page;
            SourceX = sourceX;
            SourceY = sourceY;
            Width = width;
            Height = height;
            X = x;
            Y = y;
            Color = color;
        }
    }

    /// <summary>
    /// The map's text on screen (Epic CLIENT text cleanup T3): labels hang on world points but are drawn in screen
    /// space, 1:1 from <see cref="FontSet"/>'s baked faces, every glyph on a whole pixel, so they stay crisp at any zoom.
    /// <list type="bullet">
    /// <item><description>Room names are <see cref="RoomWorldSize"/> world units tall, rounded down to a baked size, so they grow with the map as Unity's labels did, in steps. A row of names shares one size, the largest at which none overlaps its neighbours; a row that fits at no size from <see cref="MinSize"/> is hidden.</description></item>
    /// <item><description>"UNCHARTED" sits under its room's name, one size smaller, and fades with the fog.</description></item>
    /// <item><description>The selected crawler's name sits above its point at <see cref="NameSize"/>.</description></item>
    /// </list>
    /// Every label has a one-pixel dark shadow so it reads on any floor.
    /// </summary>
    public sealed class MapLabelLayout
    {
        public const float RoomWorldSize = 1f;
        public const int MinSize = 10;
        public const int NameSize = 13;
        /// <summary>The least space between two names on a row, in pixels.</summary>
        public const int Gap = 8;
        /// <summary>Names whose anchors are closer than this on screen, in height (world units at the view's scale), share a row.</summary>
        public const float RowTolerance = 0.5f;

        private static readonly Rgba32 Shadow = new Rgba32(0, 0, 0, 150);

        private readonly List<int> _rooms = new List<int>(16);
        private readonly List<int> _row = new List<int>(16);
        private readonly Dictionary<int, int> _roomSize = new Dictionary<int, int>(16);
        private readonly Dictionary<Vec2Key, int> _roomAt = new Dictionary<Vec2Key, int>(16);
        private readonly List<Vec2> _screenAt = new List<Vec2>(16);
        private readonly Comparison<int> _byScreenX;
        private readonly HashSet<int> _done = new HashSet<int>(); // room labels already in a row, this layout

        public MapLabelLayout()
        {
            _byScreenX = (a, b) => _screenAt[a].X.CompareTo(_screenAt[b].X);
        }

        /// <summary>What was placed last: each label's face and rectangle, or no face when hidden.</summary>
        public List<PlacedLabel> Placed { get; } = new List<PlacedLabel>(16);

        public readonly struct PlacedLabel
        {
            public readonly int Index;
            public readonly bool Shown;
            public readonly FontFace Face;
            public readonly int X, Y, Width, Height;

            public PlacedLabel(int index, bool shown, FontFace face, int x, int y, int width, int height)
            {
                Index = index;
                Shown = shown;
                Face = face;
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }
        }

        private readonly struct Vec2Key : IEquatable<Vec2Key>
        {
            private readonly float _x, _y;
            public Vec2Key(Vec2 v) { _x = v.X; _y = v.Y; }
            public bool Equals(Vec2Key other) => _x.Equals(other._x) && _y.Equals(other._y);
            public override bool Equals(object obj) => obj is Vec2Key other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(_x, _y);
        }

        /// <summary>Places <paramref name="labels"/> for <paramref name="view"/> and writes their glyphs (shadows first) to <paramref name="glyphs"/>.</summary>
        public void Layout(IReadOnlyList<WorldLabel> labels, in View view, FontLibrary fonts, List<ScreenGlyph> glyphs)
        {
            glyphs.Clear();
            Placed.Clear();
            _roomSize.Clear();
            _roomAt.Clear();
            if (labels == null || fonts == null) return;

            // Room names: a size per row, the largest the zoom allows at which the row's names don't overlap
            // Rows are rows on screen, so the rule holds in any projection
            _rooms.Clear();
            _screenAt.Clear();
            for (int i = 0; i < labels.Count; i++)
            {
                _screenAt.Add(view.WorldToScreen(labels[i].At));
                if (labels[i].Kind == LabelKind.Room) _rooms.Add(i);
            }
            int wanted = (int)MathF.Floor(RoomWorldSize * view.PixelsPerUnit);
            float rowTolerance = RowTolerance * view.PixelsPerUnit;
            var done = _done;
            done.Clear();
            foreach (int first in _rooms)
            {
                if (done.Contains(first)) continue;
                _row.Clear();
                foreach (int other in _rooms)
                {
                    if (!done.Contains(other) && MathF.Abs(_screenAt[other].Y - _screenAt[first].Y) < rowTolerance) _row.Add(other);
                }
                _row.Sort(_byScreenX);
                int size = RowSize(labels, _row, wanted, view, fonts);
                foreach (int i in _row)
                {
                    done.Add(i);
                    _roomSize[i] = size;
                    _roomAt[new Vec2Key(labels[i].At)] = i;
                }
            }

            for (int i = 0; i < labels.Count; i++)
            {
                var label = labels[i];
                switch (label.Kind)
                {
                    case LabelKind.Room:
                    {
                        int size = _roomSize[i];
                        if (size == 0) { Placed.Add(new PlacedLabel(i, false, default, 0, 0, 0, 0)); break; }
                        var font = fonts[FontSet.Bold(size)];
                        var at = view.WorldToScreen(label.At);
                        Emit(i, label, font, Round(at.X - font.Measure(label.Text) * 0.5f), Round(at.Y - font.Base * 0.5f), glyphs);
                        break;
                    }
                    case LabelKind.Uncharted:
                    {
                        // Under its room's name when the room has one (and it shows), else on its own point
                        int size = MinSize;
                        float top;
                        var at = view.WorldToScreen(label.At);
                        if (_roomAt.TryGetValue(new Vec2Key(label.At), out int room))
                        {
                            int roomSize = _roomSize[room];
                            if (roomSize == 0) { Placed.Add(new PlacedLabel(i, false, default, 0, 0, 0, 0)); break; }
                            size = Smaller(roomSize);
                            var roomFont = fonts[FontSet.Bold(roomSize)];
                            top = Round(at.Y - roomFont.Base * 0.5f) + roomFont.LineHeight;
                        }
                        else
                        {
                            top = at.Y - fonts[FontSet.Bold(size)].Base * 0.5f;
                        }
                        var font = fonts[FontSet.Bold(size)];
                        Emit(i, label, font, Round(at.X - font.Measure(label.Text) * 0.5f), Round(top), glyphs);
                        break;
                    }
                    default:
                    {
                        var font = fonts[FontSet.Bold(NameSize)];
                        var at = view.WorldToScreen(label.At);
                        Emit(i, label, font, Round(at.X - font.Measure(label.Text) * 0.5f), Round(at.Y) - font.LineHeight, glyphs);
                        break;
                    }
                }
            }
        }

        /// <summary>The largest baked size up to <paramref name="wanted"/> at which the row's names keep <see cref="Gap"/> apart; 0 when none from <see cref="MinSize"/> does.</summary>
        private static int RowSize(IReadOnlyList<WorldLabel> labels, List<int> row, int wanted, in View view, FontLibrary fonts)
        {
            var sizes = FontSet.BoldSizes;
            for (int s = sizes.Length - 1; s >= 0; s--)
            {
                int size = sizes[s];
                if (size > wanted || size < MinSize) continue;
                var font = fonts[FontSet.Bold(size)];
                bool fits = true;
                for (int k = 1; k < row.Count && fits; k++)
                {
                    float left = view.WorldToScreen(labels[row[k - 1]].At).X + font.Measure(labels[row[k - 1]].Text) * 0.5f;
                    float right = view.WorldToScreen(labels[row[k]].At).X - font.Measure(labels[row[k]].Text) * 0.5f;
                    fits = right - left >= Gap;
                }
                if (fits) return size;
            }
            return 0;
        }

        /// <summary>The next baked size down (for "UNCHARTED"), not below <see cref="MinSize"/>.</summary>
        private static int Smaller(int size)
        {
            var sizes = FontSet.BoldSizes;
            for (int s = sizes.Length - 1; s >= 0; s--)
            {
                if (sizes[s] < size && sizes[s] >= MinSize) return sizes[s];
            }
            return size;
        }

        private void Emit(int index, in WorldLabel label, BitmapFontMetrics font, int left, int top, List<ScreenGlyph> glyphs)
        {
            Placed.Add(new PlacedLabel(index, true, font.Face, left, top, font.Measure(label.Text), font.LineHeight));
            var shadow = new Rgba32(Shadow.R, Shadow.G, Shadow.B, (byte)(Shadow.A * label.Color.A / 255));
            AddGlyphs(font, label.Text, left + 1, top + 1, shadow, glyphs);
            AddGlyphs(font, label.Text, left, top, label.Color, glyphs);
        }

        /// <summary>Lays a line of text from its top-left corner, every glyph on a whole pixel.</summary>
        public static void AddGlyphs(BitmapFontMetrics font, string text, int left, int top, Rgba32 color, List<ScreenGlyph> glyphs)
        {
            int pen = left;
            for (int i = 0; i < text.Length; i++)
            {
                var g = font.Glyph(text[i]);
                if (g.Width > 0 && g.Height > 0) glyphs.Add(new ScreenGlyph(font.FirstPage + g.Page, g.X, g.Y, g.Width, g.Height, pen + g.XOffset, top + g.YOffset, color));
                pen += g.XAdvance;
                if (i + 1 < text.Length) pen += font.Kerning(text[i], text[i + 1]);
            }
        }

        private static int Round(float value) => (int)MathF.Floor(value + 0.5f);
    }
}
