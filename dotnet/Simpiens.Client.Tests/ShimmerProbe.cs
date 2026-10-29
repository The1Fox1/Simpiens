using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// A measurement, not a gate (graphics V2.5 and V3.1, epic F19): how many shapes a walking crawler takes as it moves by
    /// sixteenths of a pixel, at whole and in-between zooms, with and without snapping standing pieces to whole pixels.
    /// Point sampling is modelled on the CPU as the renderer draws (<see cref="SamplingTests.Shapes"/>).
    /// Run: <c>-method "*ShimmerProbe*" -explicit only -showLiveOutput</c>.
    /// </summary>
    public class ShimmerProbe
    {
        private readonly ITestOutputHelper _output;

        public ShimmerProbe(ITestOutputHelper output) => _output = output;

        [Theory(Explicit = true)]
        [InlineData("top-down")]
        [InlineData("isometric")]
        public void MeasureShimmer(string mode)
        {
            var viewMode = mode == "top-down" ? ViewMode.TopDown : ViewMode.Isometric;
            foreach (float zoom in new[] { 1f, 1.25f, 1.37f, 1.5f, 2f, 2.5f, 3f })
            {
                _output.WriteLine($"{mode}, zoom {zoom}: {SamplingTests.Shapes(viewMode, zoom, snap: false)} shape(s) as it moves; " +
                    $"snapped to whole pixels, {SamplingTests.Shapes(viewMode, zoom, snap: true)}");
            }
        }
    }
}
