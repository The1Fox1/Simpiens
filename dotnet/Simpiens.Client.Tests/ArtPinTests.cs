using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Every sprite of every sheet pinned by name (graphics V0): its size and a hash of its pixels, one line each in
    /// <c>Baselines/art-pins.txt</c>. A changed, new or missing sprite fails by name. The pixels are generated on the
    /// CPU, so the pins are exact on any machine. A deliberate art change re-records them (<c>SIMPIENS_RECORD_ART=1</c>)
    /// in its own commit, after a look at the art review sheet, with the renders re-recorded in the pinned image.
    /// </summary>
    public class ArtPinTests
    {
        private static string PinsPath => Path.Combine(Repo.Root, "dotnet", "Simpiens.Client.Tests", "Baselines", "art-pins.txt");

        [Fact]
        public void EverySpriteIsAsPinned()
        {
            var actual = CurrentPins();
            if (Environment.GetEnvironmentVariable("SIMPIENS_RECORD_ART") == "1")
            {
                File.WriteAllLines(PinsPath, new[] { "# name width height fnv64-of-rgba (graphics V0; re-record with SIMPIENS_RECORD_ART=1 after a look at art-sheet.png)" }
                    .Concat(actual.Select(p => $"{p.Key} {p.Value}")));
                return;
            }
            Assert.True(File.Exists(PinsPath), $"No art pins at {PinsPath}; record them with SIMPIENS_RECORD_ART=1 (and look at the art first).");
            var pinned = File.ReadAllLines(PinsPath).Where(l => l.Length > 0 && l[0] != '#')
                .Select(l => l.Split(' ', 2)).ToDictionary(p => p[0], p => p[1], StringComparer.Ordinal);

            var changes = new List<string>();
            foreach (var pin in actual)
            {
                if (!pinned.TryGetValue(pin.Key, out var was)) changes.Add($"new: {pin.Key} ({pin.Value})");
                else if (was != pin.Value) changes.Add($"changed: {pin.Key} ({was} -> {pin.Value})");
            }
            foreach (var name in pinned.Keys.Where(n => !actual.ContainsKey(n))) changes.Add($"gone: {name}");
            Assert.True(changes.Count == 0, "The art changed:\n" + string.Join("\n", changes));
        }

        /// <summary>Every sprite once (a sprite in two sheets is one sprite), by name in ordinal order: "width height hash".</summary>
        internal static SortedDictionary<string, string> CurrentPins()
        {
            var pins = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var sheet in SpriteSheets.All)
            {
                foreach (var sprite in SpriteSheets.Contents(sheet)) pins[sprite.Name] = $"{sprite.Width} {sprite.Height} {HashOf(sprite)}";
            }
            return pins;
        }

        private static ulong HashOf(PixelSprite sprite)
        {
            ulong hash = 14695981039346656037UL;
            foreach (var p in sprite.Pixels)
            {
                hash = (hash ^ p.R) * 1099511628211UL;
                hash = (hash ^ p.G) * 1099511628211UL;
                hash = (hash ^ p.B) * 1099511628211UL;
                hash = (hash ^ p.A) * 1099511628211UL;
            }
            return hash;
        }
    }
}
