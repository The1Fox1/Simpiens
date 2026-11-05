using Simpiens.Presentation.Art.People;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>Every colour option of the look catalogue has a colour in the presentation (Epic UI-Beta B2).</summary>
    public class LookPaletteTests
    {
        [Fact]
        public void EveryColourOptionHasAColour() => Assert.True(LookPalettes.CoversCatalogue(), "A colour option of LookCatalog has no colour in LookPalettes.");
    }
}
