using System;
using Simpiens.Narrative;
using Simpiens.Simulation.Skills;

namespace Simpiens.Presentation.Cutscenes
{
    /// <summary>
    /// The skills' moments as dynamic scenes (Epic 11, S4.8, D17): a band reached and a technique picked is a scene with the
    /// crawler as its one actor, written from the chronicle's event: its vow, then the road not taken. Only the patron's people
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

        /// <summary>The trigger for every technique picked by one of the patron's people, a game hour apart at the most often.</summary>
        public static SceneTrigger Choices() =>
            SceneTrigger.OnMoment(ChoiceFor, whose: crawler => crawler.Ours, once: false, cooldownTicks: ChoiceCooldownTicks, ChronicleEventType.TechniqueChosen);

        /// <summary>Arms the skills' scenes on <paramref name="director"/>.</summary>
        public static void AddTo(SceneDirector director)
        {
            if (director == null) throw new ArgumentNullException(nameof(director));
            director.Add(Choices());
        }
    }
}
