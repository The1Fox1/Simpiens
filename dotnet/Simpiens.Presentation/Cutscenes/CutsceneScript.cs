using System;
using System.Collections.Generic;
using Simpiens.Entities;

namespace Simpiens.Presentation.Cutscenes
{
    /// <summary>The two kinds of cutscene (M3 design D9, producer P3).</summary>
    public enum CutsceneKind : byte
    {
        /// <summary>A traditional cutscene: it covers the world and the game waits (a still backdrop, characters, a text box).</summary>
        Hard = 0,
        /// <summary>Part of play: it runs over the live world, with real crawlers as its actors, and the game goes on.</summary>
        Dynamic = 1,
    }

    public enum CutsceneStepKind : byte
    {
        /// <summary>A line: <see cref="CutsceneStep.Who"/> says <see cref="CutsceneStep.Text"/>. Waits for the player (or, dynamic, a reading time).</summary>
        Say = 0,
        /// <summary>A pause of <see cref="CutsceneStep.Seconds"/>.</summary>
        Wait = 1,
        /// <summary>Hard: the still image behind the scene.</summary>
        Backdrop = 2,
        /// <summary>Hard: a character slides into a slot.</summary>
        Enter = 3,
        /// <summary>Hard: a character slides out.</summary>
        Leave = 4,
        /// <summary>Hard: a character faces left or right.</summary>
        Face = 5,
        /// <summary>Hard: a character's look changes (a crawler state, for the placeholders).</summary>
        Pose = 6,
        /// <summary>Dynamic: the camera goes to an actor (the player's own pan takes it back).</summary>
        Focus = 7,
    }

    /// <summary>Where a hard cutscene's character stands.</summary>
    public enum StageSlot : byte
    {
        Left = 0,
        Centre = 1,
        Right = 2,
    }

    /// <summary>One step of a script. Only the fields its <see cref="Kind"/> uses are set.</summary>
    public sealed class CutsceneStep
    {
        internal CutsceneStep(CutsceneStepKind kind) => Kind = kind;

        public CutsceneStepKind Kind { get; }
        /// <summary>A character's id (hard) or an actor's role (dynamic).</summary>
        public string Who { get; internal set; }
        public string Text { get; internal set; }
        public float Seconds { get; internal set; }
        public string Backdrop { get; internal set; }
        public StageSlot Slot { get; internal set; }
        public bool FacingLeft { get; internal set; }
        public AgentVisualState Pose { get; internal set; }

        /// <summary>True when the step holds the scene until it is done: a line or a pause. The rest happen at once.</summary>
        public bool Blocks => Kind == CutsceneStepKind.Say || Kind == CutsceneStepKind.Wait;
    }

    /// <summary>A hard cutscene's character: an id the steps use, the name its text box shows, and its look.</summary>
    public readonly struct CutsceneCharacter
    {
        public CutsceneCharacter(string id, string name, AgentVisualState look)
        {
            Id = id;
            Name = name;
            Look = look;
        }

        public string Id { get; }
        public string Name { get; }
        /// <summary>The crawler sprite that stands for it until real portraits exist (M7).</summary>
        public AgentVisualState Look { get; }
    }

    /// <summary>
    /// A cutscene written in code (M3 design D9), as sessions are: a kind, its cast (characters for a hard cutscene,
    /// actor roles for a dynamic one, bound to crawlers when it starts), and its steps in order. Immutable once built
    /// (<see cref="CutsceneBuilder"/>).
    /// </summary>
    public sealed class CutsceneScript
    {
        internal CutsceneScript(string id, CutsceneKind kind, List<CutsceneCharacter> characters, List<string> actors, List<CutsceneStep> steps)
        {
            Id = id;
            Kind = kind;
            Characters = characters;
            Actors = actors;
            Steps = steps;
        }

        public string Id { get; }
        public CutsceneKind Kind { get; }
        public IReadOnlyList<CutsceneCharacter> Characters { get; }
        /// <summary>Dynamic: the roles the trigger fills with crawlers, in order (the one who started it first).</summary>
        public IReadOnlyList<string> Actors { get; }
        public IReadOnlyList<CutsceneStep> Steps { get; }

        public bool TryGetCharacter(string id, out CutsceneCharacter character)
        {
            for (int i = 0; i < Characters.Count; i++)
            {
                if (Characters[i].Id == id) { character = Characters[i]; return true; }
            }
            character = default;
            return false;
        }
    }

    /// <summary>
    /// Writes a <see cref="CutsceneScript"/>. Hard cutscenes set a backdrop and move characters on a stage; dynamic ones
    /// name actor roles and point the camera at them. Both say lines and pause. <see cref="Build"/> checks that every step
    /// names someone the script knows and suits the kind.
    /// </summary>
    public sealed class CutsceneBuilder
    {
        private readonly string _id;
        private readonly CutsceneKind _kind;
        private readonly List<CutsceneCharacter> _characters = new List<CutsceneCharacter>();
        private readonly List<string> _actors = new List<string>();
        private readonly List<CutsceneStep> _steps = new List<CutsceneStep>();

        public CutsceneBuilder(string id, CutsceneKind kind)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A cutscene has an id.", nameof(id));
            _id = id;
            _kind = kind;
        }

        /// <summary>Hard: someone who can appear and speak.</summary>
        public CutsceneBuilder Character(string id, string name, AgentVisualState look)
        {
            _characters.Add(new CutsceneCharacter(id, name, look));
            return this;
        }

        /// <summary>Dynamic: the roles the trigger fills with crawlers, the one who started it first.</summary>
        public CutsceneBuilder Actors(params string[] roles)
        {
            _actors.AddRange(roles);
            return this;
        }

        public CutsceneBuilder Backdrop(string backdrop) => Add(new CutsceneStep(CutsceneStepKind.Backdrop) { Backdrop = backdrop });

        public CutsceneBuilder Enter(string who, StageSlot slot, bool facingLeft = false)
            => Add(new CutsceneStep(CutsceneStepKind.Enter) { Who = who, Slot = slot, FacingLeft = facingLeft });

        public CutsceneBuilder Leave(string who) => Add(new CutsceneStep(CutsceneStepKind.Leave) { Who = who });

        public CutsceneBuilder Face(string who, bool left) => Add(new CutsceneStep(CutsceneStepKind.Face) { Who = who, FacingLeft = left });

        public CutsceneBuilder Pose(string who, AgentVisualState pose) => Add(new CutsceneStep(CutsceneStepKind.Pose) { Who = who, Pose = pose });

        public CutsceneBuilder Focus(string actor) => Add(new CutsceneStep(CutsceneStepKind.Focus) { Who = actor });

        public CutsceneBuilder Say(string who, string text) => Add(new CutsceneStep(CutsceneStepKind.Say) { Who = who, Text = text ?? string.Empty });

        public CutsceneBuilder Wait(float seconds) => Add(new CutsceneStep(CutsceneStepKind.Wait) { Seconds = Math.Max(0f, seconds) });

        private CutsceneBuilder Add(CutsceneStep step)
        {
            _steps.Add(step);
            return this;
        }

        public CutsceneScript Build()
        {
            foreach (var step in _steps)
            {
                bool hardOnly = step.Kind == CutsceneStepKind.Backdrop || step.Kind == CutsceneStepKind.Enter || step.Kind == CutsceneStepKind.Leave
                    || step.Kind == CutsceneStepKind.Face || step.Kind == CutsceneStepKind.Pose;
                if (hardOnly && _kind != CutsceneKind.Hard) throw new InvalidOperationException($"{_id}: a {step.Kind} step belongs to a hard cutscene.");
                if (step.Kind == CutsceneStepKind.Focus && _kind != CutsceneKind.Dynamic) throw new InvalidOperationException($"{_id}: a Focus step belongs to a dynamic cutscene.");
                if (step.Who == null) continue;
                bool known = _kind == CutsceneKind.Hard ? _characters.Exists(c => c.Id == step.Who) : _actors.Contains(step.Who);
                if (!known) throw new InvalidOperationException($"{_id}: '{step.Who}' isn't in the cast.");
            }
            return new CutsceneScript(_id, _kind, new List<CutsceneCharacter>(_characters), new List<string>(_actors), new List<CutsceneStep>(_steps));
        }
    }
}
