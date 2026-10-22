using System;
using System.Collections.Generic;
using Simpiens.Entities;

namespace Simpiens.Presentation.Cutscenes
{
    /// <summary>A hard cutscene's character on the stage: where it stands, which way it faces, how it looks, and its slide.</summary>
    public sealed class StagedCharacter
    {
        internal StagedCharacter(CutsceneCharacter character) => Character = character;

        public CutsceneCharacter Character { get; }
        public StageSlot Slot { get; internal set; }
        public bool FacingLeft { get; internal set; }
        public AgentVisualState Pose { get; internal set; }
        /// <summary>The player's time when it began to enter, or to leave.</summary>
        public float SlideStart { get; internal set; }
        public bool Leaving { get; internal set; }
    }

    /// <summary>
    /// Plays a <see cref="CutsceneScript"/> (M3 design D9), engine-free: the view drives it with time and the player's
    /// advance, and reads what to show. Steps that don't block (backdrop, entering, facing, focus) happen at once; a line
    /// appears letter by letter at <see cref="LettersPerSecond"/>, and an advance first finishes it, then moves on. A
    /// dynamic cutscene's lines also move on by themselves after a reading time, because the game doesn't wait.
    /// Deterministic: the same time steps and advances give the same scene.
    /// </summary>
    public sealed class CutscenePlayer
    {
        public const float LettersPerSecond = 40f;
        /// <summary>How long a character takes to slide in or out.</summary>
        public const float SlideSeconds = 0.3f;

        private readonly List<StagedCharacter> _stage = new List<StagedCharacter>(4);
        private int _next; // the next step to run
        private float _stepTime; // time in the current blocking step
        private float _revealed; // letters of the line shown

        public CutscenePlayer(CutsceneScript script)
        {
            Script = script ?? throw new ArgumentNullException(nameof(script));
            RunToBlockingStep();
        }

        public CutsceneScript Script { get; }

        /// <summary>The player's own clock: the time it has been updated with.</summary>
        public float Time { get; private set; }

        public bool Finished { get; private set; }

        /// <summary>The blocking step being played (a line or a pause), or null when finished.</summary>
        public CutsceneStep Current { get; private set; }

        /// <summary>The line being shown: its speaker (a character id or an actor role) and text; null between lines.</summary>
        public string Speaker => Current != null && Current.Kind == CutsceneStepKind.Say ? Current.Who : null;

        public string Line => Current != null && Current.Kind == CutsceneStepKind.Say ? Current.Text : null;

        /// <summary>How many letters of <see cref="Line"/> show.</summary>
        public int LettersShown => Line == null ? 0 : Math.Min(Line.Length, (int)_revealed);

        public bool LineComplete => Line != null && LettersShown >= Line.Length;

        /// <summary>Hard: the backdrop last set, or null.</summary>
        public string Backdrop { get; private set; }

        /// <summary>Hard: who is on the stage, in the order they entered (those leaving included until their slide ends).</summary>
        public IReadOnlyList<StagedCharacter> Stage => _stage;

        /// <summary>Dynamic: the actor the camera was last sent to, or null.</summary>
        public string Focus { get; private set; }

        /// <summary>Raised when the camera is sent to an actor (dynamic).</summary>
        public event Action<string> Focused;

        /// <summary>A dynamic line waits this long once it is all shown: a second, and a little more for a longer line.</summary>
        public static float ReadingSeconds(string line) => Math.Min(6f, 1f + 0.05f * (line?.Length ?? 0));

        /// <summary>Moves the scene on by <paramref name="seconds"/> of view time.</summary>
        public void Update(float seconds)
        {
            if (Finished || seconds <= 0f) return;
            Time += seconds;
            _stepTime += seconds;
            if (Current.Kind == CutsceneStepKind.Say)
            {
                _revealed += seconds * LettersPerSecond;
                if (Script.Kind == CutsceneKind.Dynamic && _stepTime >= Current.Text.Length / LettersPerSecond + ReadingSeconds(Current.Text)) Next();
            }
            else if (_stepTime >= Current.Seconds) Next();
            PruneLeavers();
        }

        /// <summary>The player's advance (a click, Space, Enter): a line still appearing is shown whole; otherwise the scene moves on.</summary>
        public void Advance()
        {
            if (Finished) return;
            if (Current.Kind == CutsceneStepKind.Say && !LineComplete)
            {
                _revealed = Current.Text.Length;
                return;
            }
            Next();
        }

        /// <summary>Ends the scene at once (Esc in a hard cutscene).</summary>
        public void Skip()
        {
            Finished = true;
            Current = null;
            _stage.Clear();
        }

        /// <summary>How far a staged character has slid in (0 to 1), or out when leaving (1 to 0).</summary>
        public float SlideIn(StagedCharacter character)
        {
            float t = SimMath.Clamp01((Time - character.SlideStart) / SlideSeconds);
            return character.Leaving ? 1f - t : t;
        }

        private void Next()
        {
            RunToBlockingStep();
            PruneLeavers();
        }

        /// <summary>Runs the steps that happen at once, up to the next line or pause; past the last step, the scene is finished.</summary>
        private void RunToBlockingStep()
        {
            Current = null;
            _stepTime = 0f;
            _revealed = 0f;
            while (_next < Script.Steps.Count)
            {
                var step = Script.Steps[_next++];
                if (step.Blocks)
                {
                    Current = step;
                    return;
                }
                Apply(step);
            }
            Finished = true;
        }

        private void Apply(CutsceneStep step)
        {
            switch (step.Kind)
            {
                case CutsceneStepKind.Backdrop:
                    Backdrop = step.Backdrop;
                    break;
                case CutsceneStepKind.Enter:
                {
                    Script.TryGetCharacter(step.Who, out var character);
                    var staged = Find(step.Who);
                    if (staged == null)
                    {
                        staged = new StagedCharacter(character) { Pose = character.Look };
                        _stage.Add(staged);
                    }
                    staged.Slot = step.Slot;
                    staged.FacingLeft = step.FacingLeft;
                    staged.Leaving = false;
                    staged.SlideStart = Time;
                    break;
                }
                case CutsceneStepKind.Leave:
                {
                    var staged = Find(step.Who);
                    if (staged != null && !staged.Leaving)
                    {
                        staged.Leaving = true;
                        staged.SlideStart = Time;
                    }
                    break;
                }
                case CutsceneStepKind.Face:
                {
                    var staged = Find(step.Who);
                    if (staged != null) staged.FacingLeft = step.FacingLeft;
                    break;
                }
                case CutsceneStepKind.Pose:
                {
                    var staged = Find(step.Who);
                    if (staged != null) staged.Pose = step.Pose;
                    break;
                }
                case CutsceneStepKind.Focus:
                    Focus = step.Who;
                    Focused?.Invoke(step.Who);
                    break;
            }
        }

        private StagedCharacter Find(string id)
        {
            for (int i = 0; i < _stage.Count; i++)
            {
                if (_stage[i].Character.Id == id) return _stage[i];
            }
            return null;
        }

        /// <summary>Characters whose slide out has ended leave the stage.</summary>
        private void PruneLeavers()
        {
            for (int i = _stage.Count - 1; i >= 0; i--)
            {
                if (_stage[i].Leaving && Time - _stage[i].SlideStart >= SlideSeconds) _stage.RemoveAt(i);
            }
        }
    }
}
