using Simpiens.Cognition.Contracts;
using Simpiens.Simulation;

namespace Simpiens.Testing.Environment
{
    /// <summary>
    /// Runs an intent the way the tick does, call after call, until it ends (Epic 10 Part 2, T5.1). On the 24-hour day a
    /// trade, a claim or a harvest takes game minutes (<c>ActionTuning</c>), so a validator checks what an action does
    /// when it ends, whatever the session's pace.
    /// </summary>
    public static class HeadlessIntents
    {
        /// <summary>The longest action a validator waits for: a game hour (the longest action is half that).</summary>
        public const int MaxTicks = DayClock.TicksPerHour;

        /// <summary>Executes <paramref name="state"/>'s intent until it returns anything but in progress, at most <paramref name="maxTicks"/> times.</summary>
        public static IntentResult RunToCompletion(this SimulationManager sim, WorldEntity node, ActiveIntentState state, int maxTicks = MaxTicks)
        {
            var result = sim.ExecuteIntent(node, state);
            for (int i = 1; result == IntentResult.InProgress && i < maxTicks; i++) result = sim.ExecuteIntent(node, state);
            return result;
        }
    }
}
