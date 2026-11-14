using Simpiens.Cognition;

namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// Where practice is recorded (Epic 10 Part 3, K9.2): the fight, the harvest, the stations and the rescue each tell the
    /// simulation what was practised, and it raises the skill, logs a level gained and tells a band reached in the chronicle.
    /// Main thread only.
    /// </summary>
    public interface ISkillPractice
    {
        /// <summary>Adds <paramref name="experience"/> to <paramref name="agent"/>'s <paramref name="skill"/>; nothing for no agent or no skill.</summary>
        void Practise(AgentState agent, Skill skill, int experience);
    }
}
