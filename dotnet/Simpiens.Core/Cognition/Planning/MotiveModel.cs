using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// The motive model (Epic LIFE-1, L0.5, D2; the epic's A1): one rule for every goal's priority, the goal's own number (its
    /// base, <see cref="GoapGoal.BasePriority"/>) times the weights every goal shares. The goal selector reaches a goal's
    /// priority only through here.
    /// <list type="bullet">
    /// <item><b>The weight</b> multiplies the shared factors, each exactly 1 until its milestone sets it: the session's weight
    /// by motive (<see cref="Simpiens.Simulation.Life.LifeTuning.MotiveWeight"/>); then mood by motive (L1.4), the want
    /// (plan 2), the routine by family and hour, satiation on work and the day's intention (plan 3).</item>
    /// <item><b>Exact while neutral:</b> the factors are multiplied together first, then the base by their product. A float
    /// times exactly 1 is itself, so a weight of 1 leaves every choice, and the golden hashes, as they were.</item>
    /// </list>
    /// Pure and allocation-free; reads only the context, so it runs off the main thread.
    /// </summary>
    public static class MotiveModel
    {
        /// <summary>The priority of <paramref name="goal"/> for <paramref name="context"/>: its base times its weight.</summary>
        public static float Priority(GoapGoal goal, AgentContext context) => Score(goal, context, out _, out _);

        /// <summary>The priority, with its parts: <paramref name="basePriority"/>, the goal's own number, and <paramref name="weight"/>, the shared factors' product.</summary>
        public static float Score(GoapGoal goal, AgentContext context, out float basePriority, out float weight)
        {
            basePriority = goal.BasePriority(context);
            weight = Weight(goal, context);
            return basePriority * weight;
        }

        /// <summary>The product of the factors every goal of <paramref name="goal"/>'s motive and family shares, for <paramref name="context"/>.</summary>
        public static float Weight(GoapGoal goal, AgentContext context)
        {
            var life = Considerations.Economy(context).Life;
            float weight = life.MotiveWeight((int)goal.Motive);
            return weight;
        }
    }

    /// <summary>
    /// A motive's shared urgency, 0 to 100 (Epic LIFE-1, L0.5, D2): what a need asks for now. A new goal's base is its
    /// motive's urgency times its own opportunity, with no constants of its own (AGENTS.md, "Adding a goal"). A motive with
    /// no need behind it (income, gear, duty, ...) has none here: its goals' bases carry their own number.
    /// </summary>
    public static class Urgency
    {
        public static float Of(Motive motive, AgentContext context) => motive switch
        {
            Motive.Hunger => context.Hunger,
            Motive.Rest => 100f - context.Energy,
            Motive.Company => 100f - context.Social,
            _ => 0f,
        };
    }
}
