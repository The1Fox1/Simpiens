using System;

namespace Simpiens.Simulation.Life
{
    /// <summary>
    /// A thought, by id (Epic LIFE-1, L1.1, D7): a short memory of how life has been going, thought where its event happens,
    /// on the main thread, and held in the crawler's <see cref="CrawlerLife"/>. Mood is its needs plus its live thoughts. The
    /// id is what a thought slot holds and the save keeps: never reuse one. Thought, never rebuilt from the chronicle.
    /// </summary>
    public enum Thought : ushort
    {
        None = 0,
        /// <summary>A meal that took 30 or more off its hunger.</summary>
        AGoodMeal = 1,
        /// <summary>A meal at a Garden plot (its node rule's 50).</summary>
        AteAtTheGarden = 2,
        /// <summary>It woke from a bunk's sleep.</summary>
        SleptInABunk = 3,
        /// <summary>It collapsed from exhaustion where it stood.</summary>
        SleptOnTheFloor = 4,
        /// <summary>A gossip exchanged.</summary>
        AGoodTalk = 5,
        /// <summary>A crawler shared its food with it (names the giver).</summary>
        FedBy = 6,
        /// <summary>It shared its food.</summary>
        SharedMyFood = 7,
        /// <summary>Its wage was paid in full.</summary>
        PaidInFull = 8,
        /// <summary>Its wage went unpaid.</summary>
        WageUnpaid = 9,
        /// <summary>Paid below what it asks (Epic 11, D8).</summary>
        RaiseUnpaid = 10,
        /// <summary>A bounty paid, a hunt's share, an expedition's share.</summary>
        AGoodPayday = 11,
        /// <summary>A rival took the node it was working.</summary>
        BeatenToIt = 12,
        /// <summary>Struck by a creature.</summary>
        Hurt = 13,
        /// <summary>It collapsed in danger and lived.</summary>
        Downed = 14,
        /// <summary>A crawler carried it out or stood it up with a medicine (names the rescuer).</summary>
        RescuedBy = 15,
        /// <summary>It rescued a crawler (names the rescued).</summary>
        Saved = 16,
        /// <summary>A friend died (names the dead).</summary>
        FriendDied = 17,
        /// <summary>It struck the blow that slew a creature.</summary>
        AKill = 18,
        /// <summary>Its practice reached a band.</summary>
        GrewInMyCraft = 19,
        /// <summary>It earned an epithet.</summary>
        EarnedAName = 20,
        /// <summary>It made a masterpiece.</summary>
        MadeAMasterpiece = 21,
        /// <summary>A walk in the Garden ended (plan 2's activities, L2.2: thought at an activity's end).</summary>
        AWalkInTheGarden = 22,
        /// <summary>A browse of the stall's goods ended.</summary>
        BrowsedTheMarket = 23,
        /// <summary>A bout in the yard ended.</summary>
        AGoodBout = 24,
        /// <summary>An ale at the tavern ended.</summary>
        AnAleAtTheTavern = 25,
        /// <summary>A supper at the tavern ended.</summary>
        ASupperAtTheTavern = 26,
        /// <summary>A game of dice ended.</summary>
        AGameOfDice = 27,
    }

    /// <summary>What a thought is about: a need met or missed, an event in its life, or another crawler.</summary>
    public enum ThoughtKind : byte
    {
        Need = 0,
        Event = 1,
        Social = 2,
    }

    /// <summary>
    /// One thought's row (Epic LIFE-1, L1.1, D7): its words (with "{0}" for the crawler it names, if it names one), its value
    /// to mood, how long it lasts in game hours, how many times over it can be held, what each further stack adds (the value
    /// times the factor to the power of the stacks before it), whether it names a subject, and its kind.
    /// </summary>
    public sealed class ThoughtDefinition
    {
        public ThoughtDefinition(Thought id, string name, string words, sbyte value, ushort hours, byte stackLimit, float stackFactor, ThoughtKind kind)
        {
            Id = id;
            Name = name;
            Words = words;
            Value = value;
            Hours = hours;
            StackLimit = stackLimit;
            StackFactor = stackFactor;
            Kind = kind;
        }

        public Thought Id { get; }

        /// <summary>The code name, as the enum's.</summary>
        public string Name { get; }

        /// <summary>The player's words: "A good meal", "Rescued by {0}".</summary>
        public string Words { get; }

        /// <summary>What one stack adds to mood: positive for a good thought.</summary>
        public sbyte Value { get; }

        /// <summary>How long it lasts, in game hours, from the last time it was thought.</summary>
        public ushort Hours { get; }

        public byte StackLimit { get; }

        public float StackFactor { get; }

        public ThoughtKind Kind { get; }

        /// <summary>Whether it names a crawler: its words hold "{0}".</summary>
        public bool NamesSubject => Words.Contains("{0}");

        /// <summary>What <paramref name="stacks"/> stacks of it add to mood: the value, plus the value times the factor for each stack after the first, compounding.</summary>
        public float ValueAt(int stacks)
        {
            float total = 0f, step = Value;
            for (int i = 0; i < stacks; i++)
            {
                total += step;
                step *= StackFactor;
            }
            return total;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// The thoughts, as a table (Epic LIFE-1, L1.1, D7; the epic's A4). Static and immutable, as the deeds' table is. A new
    /// thought is a row and the place it is thought (AGENTS.md, "Adding a thought"). The values are L1.3's, set from the
    /// measurement of moods on the gate seeds (docs/life-model.md).
    /// </summary>
    public static class ThoughtTable
    {
        /// <summary>The highest id there can be, plus one: a ushort leaves room, and the table is sized to what it holds.</summary>
        public const int Capacity = 64;

        /// <summary>The bounds every row keeps (<c>ThoughtsValidator</c>).</summary>
        public const int MaxValue = 30, MaxHours = 240, MaxStacks = 5;

        private static readonly ThoughtDefinition[] ById = Build();

        private static ThoughtDefinition[] Build()
        {
            var rows = new[]
            {
                new ThoughtDefinition(Thought.AGoodMeal, "AGoodMeal", "A good meal", 3, 6, 2, 0.5f, ThoughtKind.Need),
                new ThoughtDefinition(Thought.AteAtTheGarden, "AteAtTheGarden", "Ate at the Garden", 4, 8, 1, 1f, ThoughtKind.Need),
                new ThoughtDefinition(Thought.SleptInABunk, "SleptInABunk", "Slept in a bunk", 3, 14, 1, 1f, ThoughtKind.Need),
                new ThoughtDefinition(Thought.SleptOnTheFloor, "SleptOnTheFloor", "Slept on the floor", -5, 14, 1, 1f, ThoughtKind.Need),
                new ThoughtDefinition(Thought.AGoodTalk, "AGoodTalk", "A good talk", 2, 6, 3, 0.5f, ThoughtKind.Social),
                new ThoughtDefinition(Thought.FedBy, "FedBy", "Fed by {0}", 4, 24, 1, 1f, ThoughtKind.Social),
                new ThoughtDefinition(Thought.SharedMyFood, "SharedMyFood", "Shared my food", 2, 24, 2, 0.5f, ThoughtKind.Social),
                new ThoughtDefinition(Thought.PaidInFull, "PaidInFull", "Paid in full", 3, 24, 1, 1f, ThoughtKind.Event),
                new ThoughtDefinition(Thought.WageUnpaid, "WageUnpaid", "Wage unpaid", -8, 48, 2, 0.5f, ThoughtKind.Event),
                new ThoughtDefinition(Thought.RaiseUnpaid, "RaiseUnpaid", "Raise unpaid", -4, 48, 1, 1f, ThoughtKind.Event),
                new ThoughtDefinition(Thought.AGoodPayday, "AGoodPayday", "A good payday", 4, 24, 3, 0.5f, ThoughtKind.Event),
                new ThoughtDefinition(Thought.BeatenToIt, "BeatenToIt", "Beaten to it", -3, 6, 3, 0.5f, ThoughtKind.Event),
                new ThoughtDefinition(Thought.Hurt, "Hurt", "Hurt", -4, 12, 3, 0.5f, ThoughtKind.Event),
                new ThoughtDefinition(Thought.Downed, "Downed", "Downed", -12, 48, 1, 1f, ThoughtKind.Event),
                new ThoughtDefinition(Thought.RescuedBy, "RescuedBy", "Rescued by {0}", 10, 72, 1, 1f, ThoughtKind.Social),
                new ThoughtDefinition(Thought.Saved, "Saved", "Saved {0}", 8, 48, 1, 1f, ThoughtKind.Social),
                new ThoughtDefinition(Thought.FriendDied, "FriendDied", "{0} died", -25, 120, 1, 1f, ThoughtKind.Social),
                new ThoughtDefinition(Thought.AKill, "AKill", "A kill", 4, 24, 3, 0.5f, ThoughtKind.Event),
                new ThoughtDefinition(Thought.GrewInMyCraft, "GrewInMyCraft", "Grew in my craft", 8, 48, 1, 1f, ThoughtKind.Event),
                new ThoughtDefinition(Thought.EarnedAName, "EarnedAName", "Earned a name", 12, 72, 1, 1f, ThoughtKind.Event),
                new ThoughtDefinition(Thought.MadeAMasterpiece, "MadeAMasterpiece", "Made a masterpiece", 15, 120, 1, 1f, ThoughtKind.Event),
                // An activity's end (plan 2, L2.2, D12): a little lift, gone by the next day
                new ThoughtDefinition(Thought.AWalkInTheGarden, "AWalkInTheGarden", "A walk in the Garden", 2, 6, 2, 0.5f, ThoughtKind.Event),
                new ThoughtDefinition(Thought.BrowsedTheMarket, "BrowsedTheMarket", "Browsed the market", 1, 6, 2, 0.5f, ThoughtKind.Event),
                new ThoughtDefinition(Thought.AGoodBout, "AGoodBout", "A good bout", 3, 8, 2, 0.5f, ThoughtKind.Event),
                new ThoughtDefinition(Thought.AnAleAtTheTavern, "AnAleAtTheTavern", "An ale at the tavern", 3, 8, 2, 0.5f, ThoughtKind.Event),
                new ThoughtDefinition(Thought.ASupperAtTheTavern, "ASupperAtTheTavern", "A supper at the tavern", 4, 10, 1, 1f, ThoughtKind.Need),
                new ThoughtDefinition(Thought.AGameOfDice, "AGameOfDice", "A game of dice", 4, 8, 2, 0.5f, ThoughtKind.Event),
            };
            var byId = new ThoughtDefinition[Capacity];
            foreach (var row in rows)
            {
                int id = (ushort)row.Id;
                if (id == 0 || id >= Capacity) throw new ArgumentException($"Thought {row.Name} has id {id}; thoughts have ids from 1 to {Capacity - 1}.");
                if (byId[id] != null) throw new ArgumentException($"Two thoughts have id {id}.");
                byId[id] = row;
            }
            return byId;
        }

        private static readonly ThoughtDefinition[] AllRows = Array.FindAll(ById, row => row != null);

        /// <summary>Every thought, in id order.</summary>
        public static System.Collections.Generic.IReadOnlyList<ThoughtDefinition> All => AllRows;

        /// <summary>The row of <paramref name="thought"/>, or null for none or an id with no row.</summary>
        public static ThoughtDefinition Get(Thought thought) => (ushort)thought < Capacity ? ById[(ushort)thought] : null;

        /// <summary>The code name of <paramref name="thought"/>, or "None".</summary>
        public static string NameOf(Thought thought) => Get(thought)?.Name ?? "None";
    }
}
