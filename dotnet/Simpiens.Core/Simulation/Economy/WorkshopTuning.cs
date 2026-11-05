namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// The workshops' numbers (Epic 10 Part 2): what a station costs to build. Part of the session's
    /// <see cref="EconomyTuning"/>, never literals.
    /// </summary>
    public readonly struct WorkshopTuning
    {
        /// <summary>What building a station takes from the stall's treasury, once (Q2): the <c>Construction</c> sink.</summary>
        public readonly ushort ConstructionCost;

        public WorkshopTuning(ushort constructionCost)
        {
            ConstructionCost = constructionCost;
        }

        public static WorkshopTuning Default => new WorkshopTuning(constructionCost: 300);
    }
}
