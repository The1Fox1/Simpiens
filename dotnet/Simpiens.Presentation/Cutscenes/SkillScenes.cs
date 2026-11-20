using System;
using Simpiens.Narrative;
using Simpiens.Simulation.Skills;

namespace Simpiens.Presentation.Cutscenes
{
    /// <summary>
    /// The skills' moments as dynamic scenes (Epic 11, S4.8, D17): a band reached and a technique picked is a scene with the
    /// crawler as its one actor, written from the chronicle's event: its vow, then the road not taken. A first kill is one too
    /// (S6.4, D30), in the voice of its best skill. Only the patron's people
    /// (members and artisans) get one, and at most one a game hour, so the outpost's many Apprentice bands don't drown the
    /// screen; the chronicle tells everyone's. Placeholder words until the narrative work writes them (Epic N).
    /// </summary>
    public static class SkillScenes
    {
        /// <summary>The role the crawler plays.</summary>
        public const string Crawler = "crawler";

        /// <summary>A choice's scene waits at least a game hour after the last (420 ticks).</summary>
        public const long ChoiceCooldownTicks = 420;

        /// <summary>The scene for a technique picked (<see cref="ChronicleEventType.TechniqueChosen"/>); null for an event naming no technique.</summary>
        public static CutsceneScript ChoiceFor(ChronicleEvent e)
        {
            var technique = TechniqueTable.Get((Technique)(byte)e.ExtraData);
            if (technique == null) return null;
            var other = TechniqueTable.Get(TechniqueTable.OtherThan(technique.Id));
            var scene = new CutsceneBuilder("skill-choice-" + technique.Name, CutsceneKind.Dynamic)
                .Actors(Crawler)
                .Focus(Crawler)
                .Say(Crawler, technique.Vow);
            if (other != null) scene.Say(Crawler, $"{other.DisplayName}? Not for me. {technique.DisplayName}.");
            return scene.Build();
        }

        /// <summary>
        /// The scene for a crawler's first kill (<see cref="ChronicleEventType.FirstBlood"/>, Epic 11, S6.4, D30): what it says,
        /// in the voice of its best skill, then the creature it slew.
        /// </summary>
        public static CutsceneScript FirstBloodFor(ChronicleEvent e)
        {
            int extra = (int)e.ExtraData;
            var kind = Simpiens.Simulation.Creatures.CreatureCatalog.Get((Simpiens.Simulation.Creatures.CreatureKind)(extra % 256));
            var voice = SkillTable.Get((Skill)(extra / 256))?.Voice;
            var scene = new CutsceneBuilder("first-blood", CutsceneKind.Dynamic)
                .Actors(Crawler)
                .Focus(Crawler)
                .Say(Crawler, voice?.FirstKill ?? "First blood.");
            if (kind != null) scene.Say(Crawler, $"A {kind.DisplayName.ToLowerInvariant()}. My first.");
            return scene.Build();
        }

        /// <summary>The trigger for every first blood among the patron's people, a game hour apart at the most often.</summary>
        public static SceneTrigger FirstBloods() =>
            SceneTrigger.OnMoment(FirstBloodFor, whose: crawler => crawler.Ours, once: false, cooldownTicks: ChoiceCooldownTicks, ChronicleEventType.FirstBlood);

        /// <summary>The trigger for every technique picked by one of the patron's people, a game hour apart at the most often.</summary>
        public static SceneTrigger Choices() =>
            SceneTrigger.OnMoment(ChoiceFor, whose: crawler => crawler.Ours, once: false, cooldownTicks: ChoiceCooldownTicks, ChronicleEventType.TechniqueChosen);

        /// <summary>Arms the skills' scenes on <paramref name="director"/>.</summary>
        public static void AddTo(SceneDirector director)
        {
            if (director == null) throw new ArgumentNullException(nameof(director));
            director.Add(Choices());
            director.Add(FirstBloods());
        }
    }
}
