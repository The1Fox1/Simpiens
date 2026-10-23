using System;
using System.Collections.Generic;
using Simpiens.Narrative;

namespace Simpiens.Presentation.Cutscenes
{
    public enum SceneTriggerKind : byte
    {
        /// <summary>A crawler enters a zone (a sector): it is the scene's first actor.</summary>
        EntersZone = 0,
        /// <summary>A crawler starts an interaction with another (a chronicle event naming both): they are its two actors.</summary>
        Interaction = 1,
    }

    /// <summary>
    /// What starts a dynamic scene (M3 design D9, producer P7): a crawler entering a zone, or a crawler starting an
    /// interaction with another. A scene never moves anyone: the crawlers act as they decided, the scene presents them.
    /// A trigger plays once per world, or again after a cooldown in ticks.
    /// </summary>
    public sealed class SceneTrigger
    {
        private SceneTrigger(SceneTriggerKind kind, CutsceneScript script, bool once, long cooldownTicks)
        {
            if (script == null) throw new ArgumentNullException(nameof(script));
            if (script.Kind != CutsceneKind.Dynamic) throw new ArgumentException($"{script.Id} is a hard cutscene: a trigger starts dynamic ones.", nameof(script));
            Kind = kind;
            Script = script;
            Once = once;
            CooldownTicks = Math.Max(0L, cooldownTicks);
        }

        public static SceneTrigger OnEntering(int sectorId, CutsceneScript script, bool once = true, long cooldownTicks = 0)
            => new SceneTrigger(SceneTriggerKind.EntersZone, script, once, cooldownTicks) { SectorId = sectorId };

        public static SceneTrigger OnInteraction(CutsceneScript script, bool once, long cooldownTicks, params ChronicleEventType[] types)
            => new SceneTrigger(SceneTriggerKind.Interaction, script, once, cooldownTicks) { Types = types ?? Array.Empty<ChronicleEventType>() };

        public SceneTriggerKind Kind { get; }
        public CutsceneScript Script { get; }
        public bool Once { get; }
        public long CooldownTicks { get; }
        /// <summary>The zone entered (<see cref="SceneTriggerKind.EntersZone"/>).</summary>
        public int SectorId { get; private set; } = -1;
        /// <summary>The interactions that count (<see cref="SceneTriggerKind.Interaction"/>).</summary>
        public IReadOnlyList<ChronicleEventType> Types { get; private set; } = Array.Empty<ChronicleEventType>();

        internal bool Fired;
        internal long LastTick = long.MinValue;

        internal bool Ready(long tick) => !(Once && Fired) && (LastTick == long.MinValue || tick - LastTick >= CooldownTicks);

        internal bool Counts(ChronicleEventType type)
        {
            for (int i = 0; i < Types.Count; i++) if (Types[i] == type) return true;
            return false;
        }
    }

    /// <summary>A dynamic scene playing: its player, and the crawlers in its roles (in the script's role order).</summary>
    public sealed class DynamicScene
    {
        private readonly EntityId[] _actors;

        internal DynamicScene(SceneTrigger trigger, EntityId[] actors, long tick)
        {
            Trigger = trigger;
            _actors = actors;
            StartedTick = tick;
            Player = new CutscenePlayer(trigger.Script);
        }

        public SceneTrigger Trigger { get; }
        public CutscenePlayer Player { get; }
        public long StartedTick { get; }
        public IReadOnlyList<EntityId> Actors => _actors;

        /// <summary>The crawler playing <paramref name="role"/>, or default.</summary>
        public EntityId ActorOf(string role)
        {
            var roles = Trigger.Script.Actors;
            for (int i = 0; i < roles.Count && i < _actors.Length; i++) if (roles[i] == role) return _actors[i];
            return default;
        }
    }

    /// <summary>
    /// Starts dynamic scenes from what the frames saw (M3 design D9): its triggers are checked once per captured frame,
    /// on the simulation's thread, so a scene starts on the same tick in every run, whatever the frame rate. One scene
    /// plays at a time: a trigger that fires while one plays is dropped. A rebuilt world (a respawn, a switch or a load)
    /// ends the scene playing and re-arms every trigger. It reads frames and writes nothing to the simulation.
    /// </summary>
    public sealed class SceneDirector
    {
        private readonly List<SceneTrigger> _triggers = new List<SceneTrigger>(4);
        private uint _epoch;

        public SceneDirector(WorldViewFrames frames)
        {
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            _epoch = frames.Epoch;
            frames.Captured += OnCaptured;
        }

        public IReadOnlyList<SceneTrigger> Triggers => _triggers;

        /// <summary>The scene playing, or null.</summary>
        public DynamicScene Current { get; private set; }

        /// <summary>Raised when a trigger starts a scene.</summary>
        public event Action<DynamicScene> Started;

        public void Add(SceneTrigger trigger) => _triggers.Add(trigger ?? throw new ArgumentNullException(nameof(trigger)));

        /// <summary>The scene playing is over (its player finished): the next trigger may start another.</summary>
        public void End() => Current = null;

        private void OnCaptured(WorldViewFrames frames)
        {
            var now = frames.Current;
            if (now.Epoch != _epoch)
            {
                _epoch = now.Epoch;
                Current = null; // its crawlers are gone: ids are reissued
                foreach (var trigger in _triggers)
                {
                    trigger.Fired = false;
                    trigger.LastTick = long.MinValue;
                }
                return;
            }
            if (Current != null || _triggers.Count == 0) return;

            for (int t = 0; t < _triggers.Count; t++)
            {
                var trigger = _triggers[t];
                if (!trigger.Ready(now.Tick)) continue;
                var actors = trigger.Kind == SceneTriggerKind.Interaction ? Interaction(trigger, now) : Entry(trigger, frames);
                if (actors == null || actors.Length < trigger.Script.Actors.Count) continue;
                trigger.Fired = true;
                trigger.LastTick = now.Tick;
                Current = new DynamicScene(trigger, actors, now.Tick);
                Started?.Invoke(Current);
                return;
            }
        }

        /// <summary>The first interaction of a counted kind this tick between two crawlers in the world: who started it, then the other.</summary>
        private static EntityId[] Interaction(SceneTrigger trigger, WorldViewFrame now)
        {
            var events = now.Events;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (!trigger.Counts(e.EventType) || e.ActorAId == default || e.ActorBId == default || e.ActorAId == e.ActorBId) continue;
                if (!now.TryGetIndex(e.ActorAId, out int a) || !now[a].IsCrawler) continue;
                if (!now.TryGetIndex(e.ActorBId, out int b) || !now[b].IsCrawler) continue;
                return new[] { e.ActorAId, e.ActorBId };
            }
            return null;
        }

        /// <summary>The first crawler (registration order) in the zone now that was elsewhere a tick ago.</summary>
        private static EntityId[] Entry(SceneTrigger trigger, WorldViewFrames frames)
        {
            var now = frames.Current;
            var before = frames.Previous;
            if (before.Epoch != now.Epoch) return null;
            for (int i = 0; i < now.Count; i++)
            {
                ref readonly var entity = ref now[i];
                if (!entity.IsCrawler || entity.SectorId != trigger.SectorId) continue;
                if (before.TryGetIndex(entity.Id, out int j) && before[j].SectorId != trigger.SectorId) return new[] { entity.Id };
            }
            return null;
        }
    }
}
