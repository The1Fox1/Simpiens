using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Entities;

namespace Simpiens.Presentation
{
    /// <summary>How a crawler looks this frame: its sprite, and the tilt and squash of its micro-animation.</summary>
    public readonly struct CrawlerPose
    {
        public CrawlerPose(AgentVisualState state, PixelSprite sprite, float tiltDegrees, float scaleX, float scaleY)
        {
            State = state;
            Sprite = sprite;
            TiltDegrees = tiltDegrees;
            ScaleX = scaleX;
            ScaleY = scaleY;
        }

        public AgentVisualState State { get; }
        public PixelSprite Sprite { get; }
        /// <summary>Counter-clockwise, as Unity's z rotation.</summary>
        public float TiltDegrees { get; }
        /// <summary>Multipliers of the crawler's size (<see cref="WorldScene.CrawlerSize"/>).</summary>
        public float ScaleX { get; }
        public float ScaleY { get; }
    }

    /// <summary>
    /// A crawler's look (Epic CLIENT M1): a port of Unity's <c>AgentView</c>, rule for rule. The state shown is the
    /// simulation's visual state with the crises and social moments on top; a state is held for
    /// <see cref="MinDisplayTicks"/> ticks before it may change, so sub-tick flashes stay readable; each state has its
    /// two-frame animation and its micro-animation, on the view's clock (seconds). View state only: it reads what a
    /// frame captured of each crawler (<see cref="EntityView"/>), never the crawler itself.
    /// </summary>
    public sealed class CrawlerAppearance
    {
        /// <summary>
        /// Minimum logical ticks a displayed state is held before switching (Unity's <c>AgentView.MinDisplayTicks</c>):
        /// at most 3 switches per 0.9 simulated seconds.
        /// </summary>
        public const int MinDisplayTicks = 3;

        private struct Shown
        {
            public AgentVisualState State;
            public long SinceTick;
        }

        private readonly Dictionary<EntityId, Shown> _shown = new Dictionary<EntityId, Shown>(64);
        private long _lastTick = long.MinValue;

        /// <summary>Forgets every crawler's displayed state (a new session: entity ids repeat per seed).</summary>
        public void Reset()
        {
            _shown.Clear();
            _lastTick = long.MinValue;
        }

        /// <summary>
        /// The crawler's look at <paramref name="tick"/>, from its simulated <paramref name="state"/> and passing
        /// <paramref name="flags"/>, with <paramref name="viewSeconds"/> driving the animation.
        /// </summary>
        public CrawlerPose Pose(EntityId id, AgentVisualState state, CrawlerFlags flags, long tick, float viewSeconds)
        {
            if (tick < _lastTick) Reset(); // the clock went back: a respawn or a load
            _lastTick = tick;

            state = Displayed(id, Desired(state, flags), tick);
            var sprite = ProceduralSprites.GetAgentSprite(state, AnimationFrame(state, viewSeconds));
            MicroAnimation(state, viewSeconds, out float tilt, out float scaleX, out float scaleY);
            return new CrawlerPose(state, sprite, tilt, scaleX, scaleY);
        }

        /// <summary>
        /// The state to show before the dwell: a tantrum or starvation shows as panic, sleep stays sleep (brief social
        /// looks don't sit the sleeper up), then yielding, sharing and gossiping persist over the simulation's state.
        /// </summary>
        public static AgentVisualState Desired(AgentVisualState state, CrawlerFlags flags)
        {
            if ((flags & (CrawlerFlags.Tantrum | CrawlerFlags.StarvationPanic)) != 0) return AgentVisualState.Panicking;
            if (state == AgentVisualState.Sleeping) return state;
            if ((flags & CrawlerFlags.Yielding) != 0) return AgentVisualState.Yielding;
            if ((flags & CrawlerFlags.Sharing) != 0) return AgentVisualState.Sharing;
            if ((flags & CrawlerFlags.Gossiping) != 0) return AgentVisualState.Gossiping;
            return state;
        }

        private AgentVisualState Displayed(EntityId id, AgentVisualState desired, long tick)
        {
            if (!_shown.TryGetValue(id, out var shown))
            {
                _shown[id] = new Shown { State = desired, SinceTick = tick };
                return desired;
            }
            if (desired == shown.State) return desired;
            if (tick - shown.SinceTick < MinDisplayTicks) return shown.State; // hold until the dwell elapses
            _shown[id] = new Shown { State = desired, SinceTick = tick };
            return desired;
        }

        /// <summary>The sprite frame (0 or 1): 8 frames a second panicking, 1.5 asleep, 4 otherwise (Unity's speeds).</summary>
        public static int AnimationFrame(AgentVisualState state, float viewSeconds)
        {
            float framesPerSecond = state == AgentVisualState.Panicking ? 8f : state == AgentVisualState.Sleeping ? 1.5f : 4f;
            return (int)(viewSeconds * framesPerSecond) & 1;
        }

        /// <summary>Unity's per-state micro-animation: tilt (degrees, counter-clockwise) and scale multipliers.</summary>
        public static void MicroAnimation(AgentVisualState state, float t, out float tiltDegrees, out float scaleX, out float scaleY)
        {
            tiltDegrees = 0f;
            scaleX = scaleY = 1f;
            switch (state)
            {
                case AgentVisualState.Walking:
                    tiltDegrees = MathF.Sin(t * 10f) * 3f; // walking bob and slight body tilt
                    break;
                case AgentVisualState.Gossiping:
                    scaleX = scaleY = 1f + MathF.Abs(MathF.Sin(t * 5f)) * 0.08f; // gentle interaction bounce
                    break;
                case AgentVisualState.Harvesting:
                    tiltDegrees = MathF.Sin(t * 7f) * 6f; // rhythmic gathering lean
                    break;
                case AgentVisualState.Panicking:
                    tiltDegrees = MathF.Sin(t * 22f) * 8f; // frantic tremor
                    scaleX = scaleY = 1f + MathF.Sin(t * 18f) * 0.1f;
                    break;
                case AgentVisualState.Thinking:
                    tiltDegrees = MathF.Sin(t * 3f) * 2f; // gentle thought float
                    break;
                case AgentVisualState.Resting:
                {
                    float breath = 1f + MathF.Sin(t * 1.5f) * 0.04f; // snoozing breath and relaxed slump
                    scaleX = breath;
                    scaleY = 2f - breath;
                    tiltDegrees = 2f;
                    break;
                }
                case AgentVisualState.Sleeping:
                    scaleY = 1f + MathF.Sin(t * 1.2f) * 0.03f; // slow breathing; width held inside the bunk
                    break;
                case AgentVisualState.Yielding:
                    tiltDegrees = MathF.Sin(t * 8f) * -5f; // cowering flinch
                    scaleX = scaleY = 1f - MathF.Abs(MathF.Sin(t * 6f)) * 0.08f;
                    break;
                case AgentVisualState.Sharing:
                    tiltDegrees = -3f; // offering: forward tilt and soft pulse
                    scaleX = scaleY = 1f + MathF.Abs(MathF.Sin(t * 6f)) * 0.08f;
                    break;
                case AgentVisualState.Rallying:
                    tiltDegrees = MathF.Sin(t * 6f) * 2f; // marching bob, upright
                    scaleX = scaleY = 1f + MathF.Abs(MathF.Sin(t * 8f)) * 0.08f;
                    break;
                case AgentVisualState.Defiant:
                    tiltDegrees = MathF.Sin(t * 14f) * 4f; // rigid stance, defiant jitter
                    scaleX = scaleY = 1f + MathF.Sin(t * 12f) * 0.05f;
                    break;
                default:
                {
                    float breath = 1f + MathF.Sin(t * 2f) * 0.03f; // idle: calm breathing
                    scaleX = breath;
                    scaleY = 2f - breath;
                    break;
                }
            }
        }
    }
}
