namespace Simpiens.Presentation.Art.People
{
    /// <summary>
    /// How a crawler feels, as its portrait shows it (Epic UI-Beta B3, §3.3): one of seven faces, chosen from its state
    /// by <see cref="MoodRule"/>. The order is the faces', not the rule's priority.
    /// </summary>
    public enum Mood : byte
    {
        Content = 0,
        Tired = 1,
        Hungry = 2,
        Frustrated = 3,
        Scared = 4,
        Hurt = 5,
        Asleep = 6,
    }
}
