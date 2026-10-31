namespace Simpiens.Simulation.People
{
    /// <summary>
    /// Someone who has left the session (Epic UI-Beta B1, plan D4): who it was and what it looked like. The chronicle
    /// formats a line when it shows it, so a departed drifter still needs its name, and later its face.
    /// </summary>
    public sealed class DepartedCrawler
    {
        public DepartedCrawler(EntityId id, string givenName, CrawlerLook look)
        {
            Id = id;
            GivenName = givenName;
            Look = look;
        }

        public EntityId Id { get; }
        public string GivenName { get; }
        public CrawlerLook Look { get; }
    }
}
