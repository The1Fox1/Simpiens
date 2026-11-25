using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// The motive model (Epic LIFE-1, L0.5, D2; the epic's A1): one rule for every goal's priority, the goal's own number (its
    /// base, <see cref="GoapGoal.BasePriority"/>) times the weights every goal shares, plus commitment to the goal just
    /// pursued (L0.7). The goal selector reaches a goal's
    /// priority only through here.
    /// <list type="bullet">
    /// <item><b>The weight</b> multiplies the shared factors, each exactly 1 until its milestone sets it: the session's weight
    /// by motive (<see cref="Simpiens.Simulation.Life.LifeTuning.MotiveWeight"/>); mood by motive and band
    /// (<see cref="Simpiens.Simulation.Life.LifeTuning.MoodWeight"/>, L1.4); then the want
    /// (plan 2), the routine by family and hour (plan 3's R.3-R.4), satiation and weariness on work (<see cref="Rhythm"/>, R.6) and the day's intention (R.7).</item>
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
            => Score(goal, context, null, 0f, out basePriority, out weight);

        /// <summary>
        /// The priority with commitment (Epic LIFE-1, L0.7, D5): <paramref name="commitment"/> is added when
        /// <paramref name="goal"/> is <paramref name="committed"/>, the goal whose plan just ended, and only when it isn't 0,
        /// so a commitment of 0 leaves every priority to the bit. Only a valid, unsatisfied goal is scored, so it holds a crawler
        /// to a goal it can still pursue.
        /// </summary>
        public static float Score(GoapGoal goal, AgentContext context, GoapGoal committed, float commitment, out float basePriority, out float weight)
        {
            basePriority = goal.BasePriority(context);
            weight = Weight(goal, context);
            float priority = basePriority * weight;
            if (commitment != 0f && goal == committed) priority += commitment;
            return priority;
        }

        /// <summary>The product of the factors every goal of <paramref name="goal"/>'s motive and family shares, for <paramref name="context"/>.</summary>
        public static float Weight(GoapGoal goal, AgentContext context)
        {
            var life = Considerations.Economy(context).Life;
            int motive = (int)goal.Motive;
            // Mood's weight (L1.4, D9) reaches every crawler, under orders too: orders are duty, a motive weighed like the rest,
            // and a member's mood weighs its duty as it weighs its earning (the producer, 2026-11-24: "Crawlers can absolutely
            // disobey orders"; plan 2's D25, amending the epic's A8). The routine's weight by block and family (plan 3's D30) never
            // scales the unweighted family, so orders, hunger, rescue and leaving keep their pull at any hour (D36)
            var family = goal.FamilyFor(context);
            float weight = life.MotiveWeight(motive) * life.MoodWeight(context.Mood, motive) * life.RoutineWeight(context.Block, (int)family);
            // Satiation and weariness on work (plan 3's R.6, D32, D33): exactly 1 under the purse target and unwearied
            if (family == ActivityFamily.Work) weight *= Rhythm.OnWork(context);
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
            Motive.Fun => 100f - context.Life.Fun, // its boredom (plan 2's L2.1, D11)
            _ => 0f,
        };
    }
}
