using System;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Evaluators
{
    /// <summary>
    /// Obsolete: Superseded by MentalBreakEvaluator in Epic Alpha Phase 3.
    /// Retained as a backward-compatible wrapper.
    /// </summary>
    [Obsolete("FrustrationEvaluator has been superseded by MentalBreakEvaluator. Use MentalBreakEvaluator instead.")]
    public class FrustrationEvaluator : MentalBreakEvaluator
    {
        public FrustrationEvaluator(IAsyncPathfinder pathfinder) : base(pathfinder)
        {
        }
    }
}
