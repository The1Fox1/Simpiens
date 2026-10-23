using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Simpiens.Presentation
{
    /// <summary>One glyph of a baked font: where it is in its page and how it sits on the line (BMFont's char line).</summary>
    public readonly struct GlyphMetrics
    {
        public readonly int Id, X, Y, Width, Height, XOffset, YOffset, XAdvance, Page;

        public GlyphMetrics(int id, int x, int y, int width, int height, int xOffset, int yOffset, int xAdvance, int page)
        {
            Id = id;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            XOffset = xOffset;
            YOffset = yOffset;
            XAdvance = xAdvance;
            Page = page;
        }
    }

    /// <summary>
    /// A baked font's metrics, read from its BMFont text file (Epic CLIENT text cleanup T3): the line, every glyph and
    /// the kerning pairs. Whole pixels throughout, so text laid out from them lands on pixels.
    /// </summary>
    public sealed class BitmapFontMetrics
    {
        private readonly Dictionary<int, GlyphMetrics> _glyphs = new Dictionary<int, GlyphMetrics>(256);
        private readonly Dictionary<long, int> _kerning = new Dictionary<long, int>(2048);
        private readonly List<string> _pages = new List<string>(1);

        private BitmapFontMetrics(FontFace face) => Face = face;

        public FontFace Face { get; }
        /// <summary>From one line's top to the next's.</summary>
        public int LineHeight { get; private set; }
        /// <summary>From the line's top to its baseline.</summary>
        public int Base { get; private set; }
        /// <summary>The atlas pages' file names, by page id, next to the .fnt.</summary>
        public IReadOnlyList<string> Pages => _pages;
        /// <summary>
        /// Where this face's pages start in its <see cref="FontLibrary"/>'s numbering (<see cref="FontLibrary.PageCount"/>),
        /// so a glyph names its texture by one integer (review R3).
        /// </summary>
        public int FirstPage { get; internal set; }
        public int GlyphCount => _glyphs.Count;
        public int KerningCount => _kerning.Count;

        /// <summary>The glyph for a character, or '?' when the font doesn't have it.</summary>
        public GlyphMetrics Glyph(char c) => _glyphs.TryGetValue(c, out var g) ? g : _glyphs['?'];

        public bool HasGlyph(char c) => _glyphs.ContainsKey(c);

        public int Kerning(char first, char second) => _kerning.TryGetValue(Key(first, second), out int amount) ? amount : 0;

        /// <summary>The pen's travel over the text: the advances and kerning, in pixels.</summary>
        public int Measure(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int width = 0;
            for (int i = 0; i < text.Length; i++)
            {
                width += Glyph(text[i]).XAdvance;
                if (i + 1 < text.Length) width += Kerning(text[i], text[i + 1]);
            }
            return width;
        }

        /// <summary>Reads a face's .fnt through <paramref name="open"/> (a file, or the platform's content: assets on Android).</summary>
        public static BitmapFontMetrics Load(FontFace face, Func<string, Stream> open)
        {
            using var stream = open(FontSet.ContentFolder + "/" + face.CacheFileName);
            using var reader = new StreamReader(stream);
            return Parse(face, reader.ReadToEnd());
        }

        public static BitmapFontMetrics Parse(FontFace face, string fnt)
        {
            var font = new BitmapFontMetrics(face);
            using var lines = new StringReader(fnt);
            for (string line = lines.ReadLine(); line != null; line = lines.ReadLine())
            {
                int space = line.IndexOf(' ');
                string tag = space < 0 ? line : line.Substring(0, space);
                switch (tag)
                {
                    case "common":
                        font.LineHeight = Int(line, "lineHeight");
                        font.Base = Int(line, "base");
                        break;
                    case "page":
                        int id = Int(line, "id");
                        while (font._pages.Count <= id) font._pages.Add(null);
                        font._pages[id] = Text(line, "file");
                        break;
                    case "char":
                        int c = Int(line, "id");
                        font._glyphs[c] = new GlyphMetrics(c, Int(line, "x"), Int(line, "y"), Int(line, "width"), Int(line, "height"),
                            Int(line, "xoffset"), Int(line, "yoffset"), Int(line, "xadvance"), Int(line, "page"));
                        break;
                    case "kerning":
                        font._kerning[Key(Int(line, "first"), Int(line, "second"))] = Int(line, "amount");
                        break;
                }
            }
            if (!font._glyphs.ContainsKey('?')) throw new InvalidDataException($"{face}: the font has no '?' to fall back on.");
            return font;
        }

        private static long Key(int first, int second) => ((long)first << 32) | (uint)second;

        private static int Int(string line, string key) => int.Parse(Value(line, key), NumberStyles.Integer, CultureInfo.InvariantCulture);

        private static string Text(string line, string key) => Value(line, key).Trim('"');

        private static string Value(string line, string key)
        {
            string marker = " " + key + "=";
            int start = line.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) throw new InvalidDataException($"'{key}' is missing from: {line}");
            start += marker.Length;
            int end = line[start] == '"' ? line.IndexOf('"', start + 1) + 1 : line.IndexOf(' ', start);
            return end < 0 ? line.Substring(start) : line.Substring(start, end - start);
        }
    }

    /// <summary>Every <see cref="FontSet"/> face's metrics, read once.</summary>
    public sealed class FontLibrary
    {
        private readonly Dictionary<FontFace, BitmapFontMetrics> _fonts = new Dictionary<FontFace, BitmapFontMetrics>();

        public static FontLibrary Load(Func<string, Stream> open)
        {
            var library = new FontLibrary();
            foreach (var face in FontSet.All)
            {
                var font = BitmapFontMetrics.Load(face, open);
                font.FirstPage = library.PageCount;
                library.PageCount += font.Pages.Count;
                library._fonts[face] = font;
            }
            return library;
        }

        /// <summary>Every face's pages, numbered from 0 in <see cref="FontSet.All"/>'s order (<see cref="BitmapFontMetrics.FirstPage"/>).</summary>
        public int PageCount { get; private set; }

        /// <summary>From files: the content folder's parent (the game's folder, or a test's).</summary>
        public static FontLibrary LoadFrom(string folder) => Load(path => File.OpenRead(Path.Combine(folder, path)));

        public BitmapFontMetrics this[FontFace face] => _fonts[face];
        public IEnumerable<BitmapFontMetrics> All => _fonts.Values;
    }
}
