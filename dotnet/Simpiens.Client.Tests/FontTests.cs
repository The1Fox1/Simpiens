using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gum.GueDeriving;
using RenderingLibrary.Graphics.Fonts;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The baked fonts (Epic CLIENT text cleanup T1): FontSet's faces under Gum's own cache names, shipped next to the
    /// game, identical to a fresh bake (the font golden), and drawn by Gum from their atlases.
    /// </summary>
    public class FontTests
    {
        private static string Committed => Path.Combine(Repo.Root, "dotnet", "Simpiens.Client", "Content");
        private static string Shipped => Path.Combine(AppContext.BaseDirectory, FontSet.ContentFolder);

        [Fact]
        public void FontSetNamesEveryFaceAsGumDoes()
        {
            foreach (var face in FontSet.All)
            {
                string gum = BmfcSave.GetFontCacheFileNameFor(face.Size, face.Family, outline: 0, useFontSmoothing: true, isItalic: false, isBold: face.Bold).Replace('\\', '/');
                Assert.Equal(gum, face.CacheFileName);
            }
        }

        [Fact]
        public void EveryFaceIsBakedAndShipsNextToTheGame()
        {
            foreach (var face in FontSet.All)
            {
                string fnt = Path.Combine(Shipped, face.CacheFileName);
                Assert.True(File.Exists(fnt), $"{face}: {fnt} is missing.");
                var pages = Regex.Matches(File.ReadAllText(fnt), "^page id=\\d+ file=\"([^\"]+)\"", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();
                Assert.NotEmpty(pages);
                foreach (var page in pages) Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(fnt), page)), $"{face}: page {page} is missing.");
            }
        }

        [Fact]
        public void TheCommittedFontsAreWhatTheBakerMakes()
        {
            string fresh = Path.Combine(Path.GetTempPath(), "simpiens-font-bake", Guid.NewGuid().ToString("N"));
            try
            {
                Simpiens.FontBaker.FontBaker.Bake(fresh);
                var baked = Files(Path.Combine(fresh, "FontCache"));
                var committed = Files(Path.Combine(Committed, "FontCache"));
                Assert.Equal(baked.Keys.OrderBy(k => k), committed.Keys.OrderBy(k => k));
                foreach (var name in baked.Keys)
                {
                    Assert.True(baked[name].AsSpan().SequenceEqual(committed[name]), $"{name} differs from a fresh bake: re-bake (dotnet run --project dotnet/Simpiens.FontBaker) and commit it on purpose.");
                }
            }
            finally
            {
                if (Directory.Exists(fresh)) Directory.Delete(fresh, recursive: true);
            }
        }

        private static Dictionary<string, byte[]> Files(string folder)
            => Directory.GetFiles(folder).ToDictionary(Path.GetFileName, File.ReadAllBytes);

        [Fact]
        public void GumDrawsEveryFaceFromItsAtlas()
        {
            Offscreen.Start(frames: 1);
            var errors = new List<string>();
            void Collect(string message) => errors.Add(message);
            Gum.Wireframe.CustomSetPropertyOnRenderable.PropertyAssignmentError += Collect;
            try
            {
                foreach (var face in FontSet.All)
                {
                    var text = new TextRuntime();
                    text.SuspendLayout(); // one lookup for the whole face, not one per property
                    text.Font = face.Family;
                    text.FontSize = face.Size;
                    text.IsBold = face.Bold;
                    text.ResumeLayout();
                    int lineHeight = int.Parse(Regex.Match(File.ReadAllText(Path.Combine(Shipped, face.CacheFileName)), "lineHeight=(\\d+)").Groups[1].Value);
                    Assert.NotNull(text.Typeface);
                    Assert.Equal(lineHeight, text.Typeface.LineHeightInPixels);
                }
            }
            finally
            {
                Gum.Wireframe.CustomSetPropertyOnRenderable.PropertyAssignmentError -= Collect;
            }
            Assert.Empty(errors);
        }
    }
}
