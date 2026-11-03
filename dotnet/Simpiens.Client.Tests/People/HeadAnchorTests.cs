using System;
using System.Linq;
using Simpiens.Entities;
using Simpiens.Presentation;
using Simpiens.Presentation.Art.People;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// The head anchors (Epic UI-Beta B2) against the pose patterns they describe: a row that drifts from its frame
    /// would put hair on a shoulder. Each frame's hair starts on its anchor's row and is only on the head; the face's
    /// rows are skin; the head's outline closes under the chin.
    /// </summary>
    public class HeadAnchorTests
    {
        [Fact]
        public void EveryFrameHasAHead()
        {
            var states = (AgentVisualState[])Enum.GetValues(typeof(AgentVisualState));
            Assert.Equal(states.Length * 2, HeadAnchors.Count);
            foreach (var state in states)
            {
                for (int frame = 0; frame < 2; frame++)
                {
                    var keys = ProceduralSprites.CrawlerKeys(state, frame);
                    var head = HeadAnchors.Of(state, frame);
                    string at = $"{state} {frame}";

                    Assert.True(Count(keys[head.HairTop], 'H', HeadAnchor.Left - 1, HeadAnchor.Right + 1) >= 5, $"{at}: no hair across row {head.HairTop}.");
                    for (int y = 0; y < keys.Count; y++)
                    {
                        if (y >= head.HairTop && y < head.Eyes) continue;
                        Assert.True(keys[y].IndexOf('H') < 0, $"{at}: hair on row {y}, outside the head's hair rows.");
                    }
                    if (!head.Lying)
                    {
                        var below = keys[head.HairTop + 1];
                        Assert.True(below[HeadAnchor.Left] == 'H' && below[HeadAnchor.Right] == 'H', $"{at}: no hair at the temples on row {head.HairTop + 1}.");
                    }
                    Assert.True(Count(keys[head.Eyes], 'K', HeadAnchor.Left, HeadAnchor.Right) >= 2, $"{at}: row {head.Eyes} isn't the face.");
                    Assert.True(Count(keys[head.Chin], 'K', HeadAnchor.Left, HeadAnchor.Right) >= 2, $"{at}: row {head.Chin} isn't the face.");
                    Assert.True(head.HairTop < head.Eyes && head.Eyes < head.Chin, $"{at}: the rows are out of order.");
                    Assert.True(Count(keys[head.Chin + 1], '#', HeadAnchor.Left, HeadAnchor.Right) == HeadAnchor.Right - HeadAnchor.Left + 1,
                        $"{at}: the head's outline doesn't close under the chin on row {head.Chin + 1}.");
                }
            }
        }

        [Fact]
        public void OnlyTheSleeperLies()
        {
            Assert.Equal(new[] { AgentVisualState.Sleeping }, HeadAnchors.All.Select((h, i) => (h, i)).Where(p => p.h.Lying).Select(p => (AgentVisualState)(p.i / 2)).Distinct());
        }

        private static int Count(string row, char key, int from, int to)
        {
            int n = 0;
            for (int x = Math.Max(0, from); x <= Math.Min(row.Length - 1, to); x++) if (row[x] == key) n++;
            return n;
        }
    }
}
