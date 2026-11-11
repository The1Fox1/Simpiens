using System.Collections.Generic;

namespace Simpiens.Presentation
{
    /// <summary>
    /// How each creature is shown across frames (Epic 10 Part 3, K2.5): whether it moved over the last tick (it scuttles
    /// through its frames) or stood (it holds its first), and which way it faces on screen. Its sprites face right; one that
    /// went left on screen is mirrored, and keeps facing the way it last went when it stops. Which way is left is the
    /// projection's, so the facing is the view's own: the frames carry only where a creature is. A creature that swung
    /// lunges at its quarry for <see cref="StrikeSeconds"/> of view time from when the view first saw the swing (its strike
    /// pose), facing it.
    /// </summary>
    public sealed class CreatureAppearance
    {
        /// <summary>Less than this many world units over a tick is standing still: a creature at its place, or swinging in reach.</summary>
        public const float StillDistance = 0.01f;

        /// <summary>How long a strike's lunge lasts, in view time: out and back.</summary>
        public const float StrikeSeconds = 0.25f;

        private struct Shown
        {
            public bool FacesLeft;
            public bool Moving;
            /// <summary>The tick of the last swing seen, and the view time it was first seen at.</summary>
            public long StrikeTick;
            public float StrikeSeen;
            /// <summary>The last frame's tick that had this creature.</summary>
            public long SeenTick;
        }

        private readonly Dictionary<EntityId, Shown> _shown = new Dictionary<EntityId, Shown>(32);
        private readonly List<EntityId> _gone = new List<EntityId>(4);

        /// <summary>Creatures whose look is kept.</summary>
        public int Remembered => _shown.Count;

        /// <summary>Forgets every creature: a new world. The scene calls it on a new epoch.</summary>
        public void Reset() => _shown.Clear();

        /// <summary>
        /// Moves every creature of <paramref name="frames"/>' current frame on: moving or still from where the two frames have
        /// it, and facing the way it went on screen through <paramref name="projection"/>. Once per update (review A6); the same
        /// frames twice change nothing. Forgets creatures the frame no longer has (slain, or gone with a load). No allocation
        /// once warm.
        /// </summary>
        public void Advance(WorldViewFrames frames, IViewProjection projection, float viewSeconds = 0f)
        {
            var now = frames?.Current;
            if (now == null) return;
            var before = frames.Previous;
            bool sameWorld = before != null && before.Epoch == now.Epoch;
            int present = 0;
            for (int i = 0; i < now.Count; i++)
            {
                ref readonly var entity = ref now[i];
                if (!entity.IsCreature) continue;
                present++;
                if (!_shown.TryGetValue(entity.Id, out var shown)) shown.StrikeTick = -1;
                shown.SeenTick = now.Tick;
                shown.Moving = false;
                if (entity.Striking && shown.StrikeTick != now.Tick)
                {
                    shown.StrikeTick = now.Tick;
                    shown.StrikeSeen = viewSeconds;
                }
                if (sameWorld && before.TryGetIndex(entity.Id, out int j))
                {
                    var from = before[j].Position;
                    shown.Moving = (entity.Position - from).SqrMagnitude > StillDistance * StillDistance;
                    if (shown.Moving && projection != null)
                    {
                        float across = projection.ToView(entity.Position).X - projection.ToView(from).X;
                        if (across < 0f) shown.FacesLeft = true;
                        else if (across > 0f) shown.FacesLeft = false; // straight up or down the screen: as it was
                    }
                }
                _shown[entity.Id] = shown;
            }
            ForgetAbsent(now.Tick, present);
        }

        /// <summary>Whether the creature went left on screen last it moved (its sprite mirrored); false for one never seen moving.</summary>
        public bool FacesLeft(EntityId id) => _shown.TryGetValue(id, out var shown) && shown.FacesLeft;

        /// <summary>Whether the creature moved over the last tick.</summary>
        public bool Moving(EntityId id) => _shown.TryGetValue(id, out var shown) && shown.Moving;

        /// <summary>
        /// How far into its lunge the creature is at <paramref name="viewSeconds"/>: 0 at rest, rising to 1 halfway through
        /// <see cref="StrikeSeconds"/> after the view saw its swing and back to 0 at its end.
        /// </summary>
        public float Lunge(EntityId id, float viewSeconds)
        {
            if (!_shown.TryGetValue(id, out var shown) || shown.StrikeTick < 0) return 0f;
            float t = (viewSeconds - shown.StrikeSeen) / StrikeSeconds;
            return t <= 0f || t >= 1f ? 0f : System.MathF.Sin(t * System.MathF.PI);
        }

        private void ForgetAbsent(long tick, int present)
        {
            if (_shown.Count <= present) return;
            _gone.Clear();
            foreach (var pair in _shown)
            {
                if (pair.Value.SeenTick != tick) _gone.Add(pair.Key);
            }
            for (int i = 0; i < _gone.Count; i++) _shown.Remove(_gone[i]);
        }
    }
}
