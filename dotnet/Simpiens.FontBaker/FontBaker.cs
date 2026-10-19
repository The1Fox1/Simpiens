using System;
using System.Collections.Generic;
using System.IO;
using KernSmith;
using KernSmith.Gum;
using RenderingLibrary.Graphics.Fonts;
using Simpiens.Presentation;

namespace Simpiens.FontBaker
{
    /// <summary>
    /// Bakes <see cref="FontSet"/>: each face rasterized by FreeType (hinted, grayscale anti-aliasing) at its exact size,
    /// written as a BMFont text file and its PNG page(s) under Gum's cache name. The same description Gum would make
    /// (<see cref="BmfcSave"/>), so the names and metrics are Gum's.
    /// </summary>
    public static class FontBaker
    {
        /// <summary>The TrueType sources, next to the baker: Regular and Bold.</summary>
        public static string SourceFolder => Path.Combine(AppContext.BaseDirectory, "Fonts", "Inter");

        private static bool _registered;

        /// <summary>Bakes every face into <paramref name="contentFolder"/> (its FontCache folder), returning the files written.</summary>
        public static IReadOnlyList<string> Bake(string contentFolder)
        {
            Register();
            var written = new List<string>();
            foreach (var face in FontSet.All)
            {
                var description = new BmfcSave
                {
                    FontName = face.Family,
                    FontSize = face.Size,
                    IsBold = face.Bold,
                    UseSmoothing = true,
                    Ranges = FontSet.Ranges,
                };
                string relative = description.FontCacheFileName.Replace('\\', '/');
                if (relative != face.CacheFileName) throw new InvalidOperationException($"Gum names {face} '{relative}', FontSet says '{face.CacheFileName}'.");

                var result = GumFontGenerator.Generate(description);
                if (result.FailedCodepoints != null && result.FailedCodepoints.Count > 0) throw new InvalidOperationException($"{face}: {result.FailedCodepoints.Count} characters failed to rasterize.");

                string path = Path.Combine(contentFolder, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string stem = Path.ChangeExtension(path, null);
                result.ToFile(stem); // writes stem.fnt, its pages (stem_0.png, ...) and stem.bmfc
                File.Delete(stem + ".bmfc"); // Gum's tool format: not needed to draw
                written.Add(path);
                for (int page = 0; page < result.Pages.Count; page++) written.Add($"{stem}_{page}.png");
            }
            return written;
        }

        private static void Register()
        {
            if (_registered) return;
            BmFont.RegisterFont(FontSet.Family, File.ReadAllBytes(Path.Combine(SourceFolder, "Inter-Regular.ttf")));
            BmFont.RegisterFont(FontSet.Family, File.ReadAllBytes(Path.Combine(SourceFolder, "Inter-Bold.ttf")), "Bold");
            _registered = true;
        }
    }
}
