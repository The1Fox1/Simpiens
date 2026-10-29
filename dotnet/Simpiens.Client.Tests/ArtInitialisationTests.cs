using System.Reflection;
using System.Runtime.Loader;
using Simpiens.Presentation.Art;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The art's static initialisation has no cycle (graphics V5.2): whichever entry point is touched first (the client
    /// touches the sheets first, a table or a test may touch another), every class initialises. Each case loads the
    /// presentation assembly afresh in its own load context, so its statics start empty whatever the suite ran before.
    /// The rule that keeps it so: generators never read a table or a sheet.
    /// </summary>
    public class ArtInitialisationTests
    {
        [Theory]
        [InlineData("Simpiens.Presentation.Art.SpriteSheets", "All")]
        [InlineData("Simpiens.Presentation.Art.TopDownArtTable", "Catalog")]
        [InlineData("Simpiens.Presentation.Art.IsometricArtTable", "Catalog")]
        [InlineData("Simpiens.Presentation.Art.IconArtTable", "Catalog")]
        [InlineData("Simpiens.Presentation.Art.WornSprites", "All")]
        [InlineData("Simpiens.Presentation.Art.ItemSprites", "Icons")]
        [InlineData("Simpiens.Presentation.ViewMode", "Isometric")]
        public void EachEntryPointInitialisesFirst(string typeName, string property)
        {
            var context = new AssemblyLoadContext("fresh " + typeName, isCollectible: true);
            try
            {
                var presentation = context.LoadFromAssemblyPath(typeof(SpriteSheets).Assembly.Location);
                object Read(string type, string name) => presentation.GetType(type, throwOnError: true).GetProperty(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);
                Assert.NotNull(Read(typeName, property));
                // Then everything else, from that start
                Assert.NotNull(Read("Simpiens.Presentation.Art.SpriteSheets", "World"));
                Assert.NotNull(Read("Simpiens.Presentation.Art.TopDownArtTable", "Catalog"));
                Assert.NotNull(Read("Simpiens.Presentation.Art.IsometricArtTable", "Catalog"));
                Assert.NotNull(Read("Simpiens.Presentation.Art.IconArtTable", "Catalog"));
            }
            finally
            {
                context.Unload();
            }
        }
    }
}
