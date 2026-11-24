using System;
using Simpiens.Entities;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Life
{
    /// <summary>
    /// A way to spend free time, by id (Epic LIFE-1 plan 2, L2.2, D12; the epic's A4, A6): what a venue offers and Recreate
    /// scores. The id is what venues' data and a want's target name: never reuse one.
    /// </summary>
    public enum Activity : byte
    {
        None = 0,
        /// <summary>Sitting by the hearth's fire: <c>GatherAtAnchor</c>'s, not Recreate's; the hearth's relax fills fun at its rate.</summary>
        Fireside = 1,
        /// <summary>A walk in the Garden.</summary>
        Walk = 2,
        /// <summary>Browsing the stall's goods.</summary>
        Browse = 3,
        /// <summary>A bout in the guildhall's yard, for guild members, two at a time.</summary>
        Spar = 4,
        /// <summary>An ale at the tavern.</summary>
        Ale = 5,
        /// <summary>A stew supper at the tavern.</summary>
        Supper = 6,
        /// <summary>Dice at the tavern, two to four at a table.</summary>
        Dice = 7,
    }

    /// <summary>
    /// The group an activity's variety is reckoned in (D15): doing one activity of a group often makes every activity of it
    /// pall. At most 8, one byte each in the life record.
    /// </summary>
    public enum VarietyGroup : byte
    {
        Fireside = 0,
        Outdoors = 1,
        Market = 2,
        Games = 3,
        Drink = 4,
        Meal = 5,
    }

    /// <summary>The side of a crawler's nature that leans it toward an activity (D12): read from its personality as a factor.</summary>
    public enum ActivityLean : byte
    {
        None = 0,
        /// <summary>Sociability.</summary>
        Sociable = 1,
        /// <summary>Sociability's lack: the quiet ones.</summary>
        Solitary = 2,
        /// <summary>Greed.</summary>
        Greedy = 3,
        /// <summary>Risk tolerance.</summary>
        Bold = 4,
    }

    /// <summary>
    /// One activity's row (D12): its words; what it fills a game hour (fun, and company), what it does once at its start (a
    /// meal's hunger), how long a spell lasts in game minutes, what it costs (an item served, bought as it begins; none for a
    /// free one), its variety group, the company it needs at the venue (none, or a range), the side of nature that leans
    /// toward it, the pose it is drawn in, and the thought it leaves at its end (none for the fireside, whose talk is gossip's).
    /// </summary>
    public sealed class ActivityDefinition
    {
        public ActivityDefinition(Activity id, string name, string words, byte funPerHour, byte socialPerHour, sbyte hunger, ushort minutes,
            ItemType cost, VarietyGroup group, byte companyMin, byte companyMax, ActivityLean lean, AgentVisualState pose, Thought thought)
        {
            Id = id;
            Name = name;
            Words = words;
            FunPerHour = funPerHour;
            SocialPerHour = socialPerHour;
            Hunger = hunger;
            Minutes = minutes;
            Cost = cost;
            Group = group;
            CompanyMin = companyMin;
            CompanyMax = companyMax;
            Lean = lean;
            Pose = pose;
            Thought = thought;
        }

        public Activity Id { get; }

        /// <summary>The code name, as the enum's.</summary>
        public string Name { get; }

        /// <summary>The player's words: "A walk in the Garden".</summary>
        public string Words { get; }

        /// <summary>Fun filled a game hour.</summary>
        public byte FunPerHour { get; }

        /// <summary>Company filled a game hour (the social need).</summary>
        public byte SocialPerHour { get; }

        /// <summary>What it does to hunger once, as it begins (a supper's -40); 0 for none.</summary>
        public sbyte Hunger { get; }

        /// <summary>How long a spell lasts, in game minutes; 0 for the fireside, which <c>GatherAtAnchor</c>'s relax times.</summary>
        public ushort Minutes { get; }

        /// <summary>The item served for it, bought from the stall's stock as it begins (plan 2's T.3, T.5); none for a free one.</summary>
        public ItemType Cost { get; }

        public VarietyGroup Group { get; }

        /// <summary>How many others it needs at the activity: 0 for none; dice two to four, so one to three others.</summary>
        public byte CompanyMin { get; }

        /// <summary>The most at one activity together; 0 for no limit.</summary>
        public byte CompanyMax { get; }

        public ActivityLean Lean { get; }

        /// <summary>How the client draws a crawler at it.</summary>
        public AgentVisualState Pose { get; }

        /// <summary>The thought it leaves when a spell ends; none for the fireside.</summary>
        public Thought Thought { get; }

        /// <summary>Whether Recreate offers it (every activity but the fireside, which is company's).</summary>
        public bool IsRecreation => Minutes > 0;

        public override string ToString() => Name;
    }

    /// <summary>
    /// The activities, as a table (Epic LIFE-1 plan 2, L2.2, D12; the epic's A4). Static and immutable, as the thoughts' table
    /// is. A new activity is a row, its thought's row and the venues that offer it (AGENTS.md, "Adding an activity"). The
    /// numbers are starting points, tuned at L2.6 against the day's measurement (docs/life-model.md §5).
    /// </summary>
    public static class ActivityTable
    {
        /// <summary>The highest id there can be, plus one.</summary>
        public const int Capacity = 32;

        /// <summary>The bounds every row keeps (<c>ActivityValidator</c>).</summary>
        public const int MaxFunPerHour = 60, MaxSocialPerHour = 30, MaxMinutes = 240, MaxCompany = 4, VarietyGroups = 8;

        private static readonly ActivityDefinition[] ById = Build();

        private static ActivityDefinition[] Build()
        {
            var rows = new[]
            {
                new ActivityDefinition(Activity.Fireside, "Fireside", "By the fire", 6, 0, 0, 0, ItemType.None, VarietyGroup.Fireside, 0, 0, ActivityLean.Sociable, AgentVisualState.Gossiping, Thought.None),
                new ActivityDefinition(Activity.Walk, "Walk", "A walk in the Garden", 15, 0, 0, 45, ItemType.None, VarietyGroup.Outdoors, 0, 0, ActivityLean.Solitary, AgentVisualState.Walking, Thought.AWalkInTheGarden),
                new ActivityDefinition(Activity.Browse, "Browse", "Browsing the stall", 10, 0, 0, 30, ItemType.None, VarietyGroup.Market, 0, 0, ActivityLean.Greedy, AgentVisualState.Idle, Thought.BrowsedTheMarket),
                new ActivityDefinition(Activity.Spar, "Spar", "Sparring in the yard", 20, 5, 0, 45, ItemType.None, VarietyGroup.Games, 1, 2, ActivityLean.Bold, AgentVisualState.Rallying, Thought.AGoodBout),
                new ActivityDefinition(Activity.Ale, "Ale", "An ale", 20, 5, 0, 45, ItemType.None, VarietyGroup.Drink, 0, 0, ActivityLean.Sociable, AgentVisualState.Idle, Thought.AnAleAtTheTavern),
                new ActivityDefinition(Activity.Supper, "Supper", "A stew supper", 10, 0, -40, 45, ItemType.None, VarietyGroup.Meal, 0, 0, ActivityLean.None, AgentVisualState.Idle, Thought.ASupperAtTheTavern),
                new ActivityDefinition(Activity.Dice, "Dice", "Dice", 30, 10, 0, 60, ItemType.None, VarietyGroup.Games, 1, 4, ActivityLean.Bold, AgentVisualState.Idle, Thought.AGameOfDice),
            };
            var byId = new ActivityDefinition[Capacity];
            foreach (var row in rows)
            {
                int id = (byte)row.Id;
                if (id == 0 || id >= Capacity) throw new ArgumentException($"Activity {row.Name} has id {id}; activities have ids from 1 to {Capacity - 1}.");
                if (byId[id] != null) throw new ArgumentException($"Two activities have id {id}.");
                byId[id] = row;
            }
            return byId;
        }

        /// <summary>The row for <paramref name="activity"/>; null for none or an id with no row.</summary>
        public static ActivityDefinition Get(Activity activity) => (byte)activity < Capacity ? ById[(byte)activity] : null;

        /// <summary>Every row, in id order.</summary>
        public static System.Collections.Generic.IReadOnlyList<ActivityDefinition> All { get; } = Array.FindAll(ById, r => r != null);
    }
}
