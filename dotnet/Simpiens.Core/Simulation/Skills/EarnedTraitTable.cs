using System.Collections.Generic;
using Simpiens.Cognition.Contracts;

namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// An earned trait, by id (Epic 11, S6.3, A2, D29): what a crawler's deeds have made of it, "Battle-Hardened". The id is
    /// what a crawler's traits record holds in a trait slot and its save: never reuse one. What a trait is (its words, its
    /// kind, what earns it and its effects) is its row in <see cref="EarnedTraitTable"/>; production code names none outside
    /// the table.
    /// </summary>
    public enum EarnedTrait : byte
    {
        None = 0,
        BattleHardened = 1,
        Shaken = 2,
        MonsterHunter = 3,
        Lifesaver = 4,
        PatientTeacher = 5,
        Vengeful = 6
    }

    /// <summary>
    /// What a crawler's own act earns (Epic 11, S7.3, D29's "its own act"): a trait no deed counts toward, judged where the act
    /// happens by <see cref="EarnedTraitRules.JudgeAct"/>. Code names the act, never the trait.
    /// </summary>
    public enum TraitAct : byte
    {
        None = 0,
        /// <summary>A friend (warmth at the grudge line) downed or killed by a named creature: a grudge against it (D33).</summary>
        FriendFelled = 1
    }

    /// <summary>What a trait does for its holder (D29): helps, hurts, or acts on its own (a grudge, S7.3).</summary>
    public enum TraitKind : byte
    {
        Help = 0,
        Hurt = 1,
        Act = 2
    }

    /// <summary>One earned trait: a row of <see cref="EarnedTraitTable"/>.</summary>
    public sealed class EarnedTraitDefinition
    {
        public EarnedTraitDefinition(EarnedTrait id, string words, TraitKind kind, Deed deed, int count, SkillLean nature, SkillUse[] uses, params TechniqueEffect[] effects)
        {
            Id = id;
            Words = words;
            Kind = kind;
            Deed = deed;
            Count = count;
            Nature = nature;
            Uses = uses;
            Effects = effects ?? System.Array.Empty<TechniqueEffect>();
        }

        public EarnedTrait Id { get; }

        /// <summary>The code name, for logs.</summary>
        public string Name => Id.ToString();

        /// <summary>The player's words: "Battle-Hardened".</summary>
        public string Words { get; }

        public TraitKind Kind { get; }

        /// <summary>The deed whose count earns it, at the deed that brings the count there.</summary>
        public Deed Deed { get; }
        public int Count { get; }

        /// <summary>The nature it asks (its lean in the crawler's personality above 0: "if bold"); none for anyone.</summary>
        public SkillLean Nature { get; }

        /// <summary>The kinds of work its effects change; null for every kind.</summary>
        public IReadOnlyList<SkillUse> Uses { get; }

        /// <summary>Its effects, through the techniques' door (<see cref="CrawlerTechniques"/>), within the same bounds.</summary>
        public IReadOnlyList<TechniqueEffect> Effects { get; }

        /// <summary>The act that earns it (S7.3), for a trait no deed counts toward; none for a deed's trait.</summary>
        public TraitAct Act { get; init; }

        /// <summary>What an act's trait does, in the player's words, where it has no stat to describe (S7.3).</summary>
        public string ActWords { get; init; } = string.Empty;

        /// <summary>Whether its effects change <paramref name="use"/>.</summary>
        public bool AppliesTo(SkillUse use)
        {
            if (use == SkillUse.None) return false;
            if (Uses == null) return true;
            for (int i = 0; i < Uses.Count; i++) if (Uses[i] == use) return true;
            return false;
        }

        /// <summary>This row's value for <paramref name="stat"/>; 0 when it has none.</summary>
        public int ValueOf(TechniqueStat stat)
        {
            for (int i = 0; i < Effects.Count; i++) if (Effects[i].Stat == stat) return Effects[i].Value;
            return 0;
        }

        /// <summary>Its effects in the player's words: "+1 defense".</summary>
        public string EffectWords
        {
            get
            {
                var text = new System.Text.StringBuilder();
                for (int i = 0; i < Effects.Count; i++)
                {
                    if (i > 0) text.Append(", ");
                    text.Append(TechniqueRules.Describe(Effects[i]));
                }
                return text.ToString();
            }
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Earned traits, as a table (Epic 11, S6.3, A2, D29). Static and immutable. A new trait is an <see cref="EarnedTrait"/>
    /// value and a row; the rule, the door, the save, the chronicle and the client follow. The counts were set from the deeds
    /// measured on the gate seeds (<c>docs/skills-model.md</c> §5.9).
    /// </summary>
    public static class EarnedTraitTable
    {
        private static readonly SkillUse[] Fights = { SkillUse.MeleeFight, SkillUse.RangedFight };
        private static readonly SkillUse[] Tending = { SkillUse.Tend };

        private static readonly EarnedTraitDefinition[] ById = new EarnedTraitDefinition[256];
        private static readonly EarnedTraitDefinition[] AllRows = Build();

        private static TechniqueEffect E(TechniqueStat stat, int value) => new TechniqueEffect(stat, value);

        private static EarnedTraitDefinition[] Build()
        {
            var rows = new[]
            {
                new EarnedTraitDefinition(EarnedTrait.BattleHardened, "Battle-Hardened", TraitKind.Help, Deed.DowningSurvived, 2, SkillLean.Bold, Fights, E(TechniqueStat.Defense, 1)),
                new EarnedTraitDefinition(EarnedTrait.Shaken, "Shaken", TraitKind.Hurt, Deed.DowningSurvived, 1, SkillLean.Cautious, Fights, E(TechniqueStat.Attack, -10), E(TechniqueStat.Dread, 25)),
                new EarnedTraitDefinition(EarnedTrait.MonsterHunter, "Monster Hunter", TraitKind.Help, Deed.CreatureSlain, 10, SkillLean.None, Fights, E(TechniqueStat.Attack, 10)),
                new EarnedTraitDefinition(EarnedTrait.Lifesaver, "Lifesaver", TraitKind.Help, Deed.Rescue, 3, SkillLean.None, Tending, E(TechniqueStat.Pace, 15), E(TechniqueStat.Heal, 10)),
                new EarnedTraitDefinition(EarnedTrait.PatientTeacher, "Patient Teacher", TraitKind.Help, Deed.Taught, 10, SkillLean.None, null, E(TechniqueStat.Teach, 10)),
                // A grudge (S7.3, D33): earned by a friend's fall, its effect the lair judgement's, not a stat's
                new EarnedTraitDefinition(EarnedTrait.Vengeful, "Vengeful", TraitKind.Act, Deed.None, 0, SkillLean.None, Fights)
                    { Act = TraitAct.FriendFelled, ActWords = "braves its foe's lair with a quarter less to spare" },
            };
            foreach (var row in rows)
            {
                if (row.Id == EarnedTrait.None) throw new System.ArgumentException("An earned trait has id 0.");
                if (ById[(byte)row.Id] != null) throw new System.ArgumentException($"Two earned traits have id {(byte)row.Id}.");
                ById[(byte)row.Id] = row;
            }
            return rows;
        }

        /// <summary>Every earned trait, in id order.</summary>
        public static IReadOnlyList<EarnedTraitDefinition> All => AllRows;

        /// <summary>The trait's row, or null for none and undefined ids.</summary>
        public static EarnedTraitDefinition Get(EarnedTrait id) => ById[(byte)id];

        /// <summary>A code name for logs: the row's, or the raw id.</summary>
        public static string NameOf(EarnedTrait id) => Get(id)?.Name ?? $"EarnedTrait{(byte)id}";
    }

    /// <summary>Earned traits' rule (Epic 11, S6.3, D29): judged where a deed is counted, on the main thread; once earned, never lost.</summary>
    public static class EarnedTraitRules
    {
        /// <summary>
        /// The trait <paramref name="act"/> earns, held with news if it has room (Epic 11, S7.3); false for none earned (no row,
        /// held already, or every slot taken).
        /// </summary>
        public static bool JudgeAct(ref CrawlerTraits traits, TraitAct act)
        {
            if (act == TraitAct.None) return false;
            bool earned = false;
            var all = EarnedTraitTable.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Act == act && !traits.HasTrait(all[i].Id)) earned |= traits.AddTrait(all[i].Id);
            return earned;
        }

        /// <summary>True when <paramref name="traits"/> holds the trait <paramref name="act"/> earns.</summary>
        public static bool HoldsAct(in CrawlerTraits traits, TraitAct act)
        {
            if (act == TraitAct.None) return false;
            var all = EarnedTraitTable.All;
            for (int i = 0; i < all.Count; i++) if (all[i].Act == act && traits.HasTrait(all[i].Id)) return true;
            return false;
        }

        /// <summary>
        /// After <paramref name="deed"/> is counted in <paramref name="traits"/>: each trait whose count it has just reached and
        /// whose nature <paramref name="personality"/> has is held, while a slot is free, with news to tell. True when one was.
        /// </summary>
        public static bool Judge(ref CrawlerTraits traits, Deed deed, in AgentPersonality personality)
        {
            int count = traits.Count(deed);
            bool earned = false;
            var all = EarnedTraitTable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var row = all[i];
                if (row.Deed != deed || row.Count != count || traits.HasTrait(row.Id)) continue;
                if (row.Nature != SkillLean.None && SkillSet.Lean(row.Nature, personality) <= 0f) continue;
                earned |= traits.AddTrait(row.Id);
            }
            return earned;
        }
    }
}
