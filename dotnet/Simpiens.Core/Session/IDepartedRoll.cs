using System.Collections.Generic;
using Simpiens.Simulation.People;

namespace Simpiens.Core.Session
{
    /// <summary>
    /// Everyone who has left the session, the dead with their causes (Epic UI-Beta B1; Epic 10 Part 3, K5.4a, K5.8c): read by
    /// the dashboard's dossier of the dead. The session host keeps it.
    /// </summary>
    public interface IDepartedRoll
    {
        IReadOnlyList<DepartedCrawler> Departed { get; }
    }
}
