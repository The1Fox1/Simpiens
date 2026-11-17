namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// A deed, by id (Epic 11, A7, D2): something a crawler did that the outpost remembers, counted where it happens, on the
    /// main thread, in its <see cref="CrawlerTraits"/>. The id is the deed's counter in the record and in the save: never reuse
    /// one, and keep it under <see cref="DeedTable.Capacity"/>.
    /// </summary>
    public enum Deed : byte
    {
        None = 0
    }

    /// <summary>The deeds, as a table (Epic 11, A2, D2).</summary>
    public static class DeedTable
    {
        /// <summary>The most deeds there can be: the counters a crawler's traits record keeps. A 17th changes the save format.</summary>
        public const int Capacity = 16;
    }
}
