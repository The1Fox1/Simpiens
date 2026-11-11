namespace Simpiens.Simulation.People
{
    /// <summary>
    /// Someone who has left the session (Epic UI-Beta B1, plan D4): who it was and what it looked like. The chronicle
    /// formats a line when it shows it, so a departed drifter still needs its name, and later its face. Since Epic 10
    /// Part 3, K5.4a, also whether it died, of what (a hazard's or a creature's kind in the detail) and when.
    /// </summary>
    public sealed class DepartedCrawler
    {
        public DepartedCrawler(EntityId id, string givenName, CrawlerLook look, DeathCause cause = DeathCause.None, byte causeDetail = 0, uint tick = 0)
        {
            Id = id;
            GivenName = givenName;
            Look = look;
            Cause = cause;
            CauseDetail = causeDetail;
            Tick = tick;
        }

        public EntityId Id { get; }
        public string GivenName { get; }
        public CrawlerLook Look { get; }

        /// <summary>What it died of; none for a crawler that walked out.</summary>
        public DeathCause Cause { get; }

        /// <summary>The cause's detail: the hazard's or the creature's kind.</summary>
        public byte CauseDetail { get; }

        /// <summary>The tick it left the world (0 for one saved before K5.4a).</summary>
        public uint Tick { get; }

        public bool Died => Cause != DeathCause.None;
    }
}
