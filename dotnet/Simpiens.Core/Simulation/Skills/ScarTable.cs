using System.Collections.Generic;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.World;

namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// A scar, by id (Epic 11, S6.2, A2, D28): the mark a downing survived leaves, by what downed the crawler. The id is what a
    /// crawler's traits record holds in a scar slot and its save: never reuse one. What a scar is (its words, its cause and its
    /// shape) is its row in <see cref="ScarTable"/>; production code names none outside the table.
    /// </summary>
    public enum Scar : byte
    {
        None = 0,
        RatBite = 1,
        BeetleClaw = 2,
        SpinnerAcid = 3,
        BroodBite = 4,
        GateBlow = 5,
        Rockfall = 6,
        FoulAirBurn = 7
    }

    /// <summary>How a scar is drawn (the client's bodies and portraits).</summary>
    public enum ScarShape : byte
    {
        /// <summary>One line: a blow's cut.</summary>
        Cut = 0,
        /// <summary>Three short parallel lines: a claw.</summary>
        Claw = 1,
        /// <summary>Two rows of points: teeth.</summary>
        Bite = 2,
        /// <summary>A pale patch: acid or a burn.</summary>
        Burn = 3
    }

    /// <summary>Where on the face a scar sits: by the slot it fills, so the first is on a cheek and no draw is needed (D28).</summary>
    public enum ScarPlace : byte
    {
        LeftCheek = 0,
        Brow = 1,
        RightCheek = 2,
        Chin = 3
    }

    /// <summary>One scar: a row of <see cref="ScarTable"/>.</summary>
    public sealed class ScarDefinition
    {
        public ScarDefinition(Scar id, string words, DeathCause cause, byte detail, ScarShape shape)
        {
            Id = id;
            Words = words;
            Cause = cause;
            Detail = detail;
            Shape = shape;
        }

        public Scar Id { get; }

        /// <summary>The code name, for logs.</summary>
        public string Name => Id.ToString();

        /// <summary>The player's words: "a beetle's claw".</summary>
        public string Words { get; }

        /// <summary>What leaves it: a creature (its kind the detail) or a hazard (its kind the detail).</summary>
        public DeathCause Cause { get; }
        public byte Detail { get; }

        public ScarShape Shape { get; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Scars, as a table (Epic 11, S6.2, A2, D28): a row for each creature kind and each hazard that leaves one. Static and
    /// immutable. A new creature kind takes a row here (the validator asks one of every kind); a hazard may leave none (the
    /// flood). Scars change no number: they are told, drawn and listed.
    /// </summary>
    public static class ScarTable
    {
        private static readonly ScarDefinition[] ById = new ScarDefinition[256];
        private static readonly ScarDefinition[] AllRows = Build();

        private static ScarDefinition[] Build()
        {
            var rows = new[]
            {
                new ScarDefinition(Scar.RatBite, "a rat's bite", DeathCause.Creature, (byte)CreatureKind.BurrowRat, ScarShape.Bite),
                new ScarDefinition(Scar.BeetleClaw, "a beetle's claw", DeathCause.Creature, (byte)CreatureKind.StoneBeetle, ScarShape.Claw),
                new ScarDefinition(Scar.SpinnerAcid, "a spinner's acid", DeathCause.Creature, (byte)CreatureKind.CaveSpinner, ScarShape.Burn),
                new ScarDefinition(Scar.BroodBite, "the Matriarch's bite", DeathCause.Creature, (byte)CreatureKind.Matriarch, ScarShape.Bite),
                new ScarDefinition(Scar.GateBlow, "the Gatekeeper's blow", DeathCause.Creature, (byte)CreatureKind.Gatekeeper, ScarShape.Cut),
                new ScarDefinition(Scar.Rockfall, "a rockfall's mark", DeathCause.Hazard, (byte)HazardKind.FallingRock, ScarShape.Cut),
                new ScarDefinition(Scar.FoulAirBurn, "the foul air's burn", DeathCause.Hazard, (byte)HazardKind.FoulAir, ScarShape.Burn),
            };
            foreach (var row in rows)
            {
                if (row.Id == Scar.None) throw new System.ArgumentException("A scar has id 0.");
                if (ById[(byte)row.Id] != null) throw new System.ArgumentException($"Two scars have id {(byte)row.Id}.");
                ById[(byte)row.Id] = row;
            }
            return rows;
        }

        /// <summary>Every scar, in id order.</summary>
        public static IReadOnlyList<ScarDefinition> All => AllRows;

        /// <summary>The scar's row, or null for none and undefined ids.</summary>
        public static ScarDefinition Get(Scar id) => ById[(byte)id];

        /// <summary>The scar <paramref name="cause"/> with <paramref name="detail"/> leaves (a creature kind, a hazard kind); none for one that leaves none.</summary>
        public static Scar For(DeathCause cause, byte detail)
        {
            for (int i = 0; i < AllRows.Length; i++) if (AllRows[i].Cause == cause && AllRows[i].Detail == detail) return AllRows[i].Id;
            return Scar.None;
        }

        /// <summary>A place in the player's words: "the left cheek".</summary>
        public static string PlaceWords(ScarPlace place) => place switch
        {
            ScarPlace.LeftCheek => "the left cheek",
            ScarPlace.Brow => "the brow",
            ScarPlace.RightCheek => "the right cheek",
            _ => "the chin",
        };

        /// <summary>A scar where it sits: "a beetle's claw across the left cheek"; empty for none.</summary>
        public static string Describe(Scar scar, int slot)
        {
            var row = Get(scar);
            return row == null ? string.Empty : $"{row.Words} across {PlaceWords((ScarPlace)slot)}";
        }
    }
}
