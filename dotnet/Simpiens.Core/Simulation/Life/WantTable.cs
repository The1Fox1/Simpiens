using System;

namespace Simpiens.Simulation.Life
{
    /// <summary>
    /// A want, by id (Epic LIFE-1 plan 2, W.1, D20): a goal with a reason, held in the crawler's life record, pursued until it
    /// comes true or runs out. The id is what a want slot holds and the save keeps: never reuse one.
    /// </summary>
    public enum Want : ushort
    {
        None = 0,
        /// <summary>A stew supper at the tavern, wanted on waking with gold to spare and a tavern standing.</summary>
        TavernSupper = 1,
        /// <summary>A night of dice, wanted by the sociable on waking with little fun.</summary>
        NightOfDice = 2,
        /// <summary>A quiet walk, wanted by the quiet on waking with little fun.</summary>
        QuietWalk = 3,
        /// <summary>To see a friend not met for a day (names the friend).</summary>
        SeeAFriend = 4,
        /// <summary>Somewhere new: a venue not yet tried, wanted by the curious.</summary>
        SomewhereNew = 5,
    }

    /// <summary>What makes a want arise on waking (W.2).</summary>
    public enum WantTrigger : byte
    {
        None = 0,
        /// <summary>Half the comfortable purse or more, and a tavern standing.</summary>
        SpareGold = 1,
        /// <summary>Fun under 50, and a sociable nature.</summary>
        LowFunSociable = 2,
        /// <summary>Fun under 50, and a quiet nature.</summary>
        LowFunQuiet = 3,
        /// <summary>A warm friend not met for a game day.</summary>
        FriendUnseen = 4,
        /// <summary>A curious nature (over 0.3), and a venue not yet tried.</summary>
        Curious = 5,
    }

    /// <summary>What a want is for: an activity, a venue, or a friend.</summary>
    public enum WantTarget : byte
    {
        Activity = 1,
        Venue = 2,
        Friend = 3,
    }

    /// <summary>
    /// One want's row (W.1, D20): its words ("{0}" for the friend it names), what makes it arise, what it is for (its activity,
    /// for an activity's), how long it lasts in game hours, how strongly it pulls (its goal's base before the leisure score),
    /// and the thoughts it leaves when it comes true and when it runs out (none for a want that leaves nothing).
    /// </summary>
    public sealed class WantDefinition
    {
        public WantDefinition(Want id, string name, string words, WantTrigger trigger, WantTarget target, Activity activity, ushort hours, byte strength, Thought fulfilled, Thought expired)
        {
            Id = id;
            Name = name;
            Words = words;
            Trigger = trigger;
            Target = target;
            Activity = activity;
            Hours = hours;
            Strength = strength;
            Fulfilled = fulfilled;
            Expired = expired;
        }

        public Want Id { get; }
        public string Name { get; }

        /// <summary>The player's words: "A tavern supper", "To see {0}".</summary>
        public string Words { get; }

        public WantTrigger Trigger { get; }
        public WantTarget Target { get; }

        /// <summary>The activity it is for (an activity's want); none otherwise.</summary>
        public Activity Activity { get; }

        public ushort Hours { get; }
        public byte Strength { get; }
        public Thought Fulfilled { get; }
        public Thought Expired { get; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// The wants, as a table (Epic LIFE-1 plan 2, W.1, D20; the epic's A4). Static and immutable. A new want is a row, its
    /// thoughts' rows and, for a new trigger, its case where wants arise (AGENTS.md, "Adding a want").
    /// </summary>
    public static class WantTable
    {
        public const int Capacity = 32;

        /// <summary>The bounds every row keeps (<c>WantsValidator</c>).</summary>
        public const int MaxHours = 96, MaxStrength = 60;

        private static readonly WantDefinition[] ById = Build();

        private static WantDefinition[] Build()
        {
            var rows = new[]
            {
                new WantDefinition(Want.TavernSupper, "TavernSupper", "A tavern supper", WantTrigger.SpareGold, WantTarget.Activity, Activity.Supper, 24, 30, Thought.HadMyTavernSupper, Thought.MissedMySupper),
                new WantDefinition(Want.NightOfDice, "NightOfDice", "A night of dice", WantTrigger.LowFunSociable, WantTarget.Activity, Activity.Dice, 24, 25, Thought.ANightOfDice, Thought.NoDiceTonight),
                new WantDefinition(Want.QuietWalk, "QuietWalk", "A quiet walk", WantTrigger.LowFunQuiet, WantTarget.Activity, Activity.Walk, 24, 20, Thought.AQuietWalk, Thought.NoTimeToMyself),
                new WantDefinition(Want.SeeAFriend, "SeeAFriend", "To see {0}", WantTrigger.FriendUnseen, WantTarget.Friend, Activity.None, 48, 25, Thought.SawAFriend, Thought.MissedAFriend),
                new WantDefinition(Want.SomewhereNew, "SomewhereNew", "Somewhere new", WantTrigger.Curious, WantTarget.Venue, Activity.None, 72, 20, Thought.TriedSomewhereNew, Thought.None),
            };
            var byId = new WantDefinition[Capacity];
            foreach (var row in rows)
            {
                int id = (ushort)row.Id;
                if (id == 0 || id >= Capacity) throw new ArgumentException($"Want {row.Name} has id {id}; wants have ids from 1 to {Capacity - 1}.");
                if (byId[id] != null) throw new ArgumentException($"Two wants have id {id}.");
                byId[id] = row;
            }
            return byId;
        }

        public static WantDefinition Get(Want want) => (ushort)want < Capacity ? ById[(ushort)want] : null;

        public static System.Collections.Generic.IReadOnlyList<WantDefinition> All { get; } = Array.FindAll(ById, r => r != null);
    }
}
