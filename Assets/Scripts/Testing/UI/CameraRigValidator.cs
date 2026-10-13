using System;
using Simpiens.Core.Session.Layouts;
using Simpiens.Entities;
using UnityEngine;

namespace Simpiens.Testing.UI
{
    /// <summary>
    /// Epic MAP Phase 4: the camera maths behind the player's view (headless). Reframe fits the whole map; the view
    /// never leaves the map's frame plus the margin; zoom keeps the point under the cursor fixed and stays within its
    /// limits; follow converges; a new screen shape keeps the view inside the limits.
    /// </summary>
    public class CameraRigValidator
    {
        private const float Eps = 1e-3f;
        private const float Aspect = 16f / 9f;

        public void RunAllValidationTests()
        {
            Debug.Log("[CameraRigValidator] Beginning camera rig validation...");

            ValidateReframe();
            ValidateClamp();
            ValidateZoom();
            ValidateFollow();
            ValidateAspectChange();
            ValidatePicker();

            Debug.Log("<color=green>[CameraRigValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static CameraRig Floor1Rig()
        {
            var rig = new CameraRig();
            rig.Configure(Floor1Layout.Definition.CameraFrame, Aspect);
            return rig;
        }

        private void ValidateReframe()
        {
            var rig = Floor1Rig();
            var frame = rig.Frame;
            Assert(Near(rig.Position, frame.center), $"Reframe centres on the map ({rig.Position} vs {frame.center}).");
            Assert(rig.Size * 2f >= frame.height - Eps && rig.Size * 2f * Aspect >= frame.width - Eps, "Reframe shows the whole frame.");
            Assert(Mathf.Approximately(rig.Size, rig.MaxSize), "Reframe is the furthest zoom.");

            rig.ZoomAt(new Vector2(5f, 3f), 0.3f);
            rig.Pan(new Vector2(4f, 4f));
            rig.Reframe();
            Assert(Near(rig.Position, frame.center) && Mathf.Approximately(rig.Size, rig.MaxSize), "Reframe restores the whole view after zoom and pan.");
            Debug.Log("[PASS] Test 1: Reframe fits the whole map.");
        }

        private void ValidateClamp()
        {
            var rig = Floor1Rig();
            var frame = rig.Frame;
            rig.ZoomAt(frame.center, 0.01f);
            Assert(Mathf.Approximately(rig.Size, CameraRig.MinSize), $"Zoom stops at the closest size ({rig.Size}).");

            // Push the view off every edge: it stops at the frame plus the margin
            var pushes = new[] { new Vector2(1000f, 0f), new Vector2(-1000f, 0f), new Vector2(0f, 1000f), new Vector2(0f, -1000f) };
            for (int i = 0; i < pushes.Length; i++)
            {
                rig.Pan(pushes[i]);
                Vector2 half = new Vector2(rig.Size * Aspect, rig.Size);
                Rect view = new Rect(rig.Position - half, half * 2f);
                Assert(view.xMin >= frame.xMin - CameraRig.Margin - Eps && view.xMax <= frame.xMax + CameraRig.Margin + Eps
                    && view.yMin >= frame.yMin - CameraRig.Margin - Eps && view.yMax <= frame.yMax + CameraRig.Margin + Eps,
                    $"Pan {pushes[i]}: view {view} leaves the frame {frame} plus the margin.");
            }
            rig.Pan(new Vector2(1000f, 1000f));
            Assert(Mathf.Approximately(rig.Position.x, frame.xMax + CameraRig.Margin - rig.Size * Aspect)
                && Mathf.Approximately(rig.Position.y, frame.yMax + CameraRig.Margin - rig.Size), $"Pushed into the corner, the view sits on it ({rig.Position}).");
            Debug.Log("[PASS] Test 2: The view never leaves the map's frame plus the margin.");
        }

        private void ValidateZoom()
        {
            var rig = Floor1Rig();
            var cursor = new Vector2(5f, 3f);
            Vector2 before = (cursor - rig.Position) / rig.Size; // the cursor's place on screen, in view heights
            rig.ZoomAt(cursor, 0.5f);
            Vector2 after = (cursor - rig.Position) / rig.Size;
            Assert(Near(before, after), $"Zooming in keeps the cursor's world point under it ({before} vs {after}).");
            Assert(Mathf.Approximately(rig.Size, rig.MaxSize * 0.5f), "Zoom scales the size by the factor.");

            rig.ZoomAt(cursor, 100f);
            Assert(Mathf.Approximately(rig.Size, rig.MaxSize), "Zoom out stops at the whole-map size.");
            rig.ZoomAt(cursor, 0f);
            rig.ZoomAt(cursor, -1f);
            Assert(Mathf.Approximately(rig.Size, rig.MaxSize), "Non-positive factors are ignored.");

            // Zooming twice in and out comes back to the same view
            rig.ZoomAt(cursor, 0.5f);
            var p = rig.Position;
            rig.ZoomAt(cursor, 0.5f);
            rig.ZoomAt(cursor, 2f);
            Assert(Near(rig.Position, p), "Zoom in then out about the same point returns to the same view.");
            Debug.Log("[PASS] Test 3: Zoom keeps the cursor point fixed and stays within its limits.");
        }

        private void ValidateFollow()
        {
            var rig = Floor1Rig();
            rig.ZoomAt(rig.Frame.center, 0.2f);
            var target = new Vector2(4f, -8f); // the Bunkhouse
            float previous = Vector2.Distance(rig.Position, target);
            for (int i = 0; i < 120; i++)
            {
                rig.Follow(target, 1f / 60f);
                float d = Vector2.Distance(rig.Position, target);
                Assert(d <= previous + Eps, $"Follow never moves away from the target (step {i}: {d} > {previous}).");
                previous = d;
            }
            Assert(previous < 0.05f, $"Follow reaches the target within 2 s ({previous} away).");

            // Frame-rate independence: one 0.5 s step lands where thirty 1/60 s steps do
            var a = Floor1Rig(); a.ZoomAt(a.Frame.center, 0.2f);
            var b = Floor1Rig(); b.ZoomAt(b.Frame.center, 0.2f);
            a.Follow(target, 0.5f);
            for (int i = 0; i < 30; i++) b.Follow(target, 1f / 60f);
            Assert(Vector2.Distance(a.Position, b.Position) < 0.01f, $"Follow is frame-rate independent ({a.Position} vs {b.Position}).");

            // A crawler walking at 10x (20 units/s) never pulls ahead: the gap still closes
            var m = Floor1Rig();
            m.ZoomAt(m.Frame.center, 0.2f);
            m.ResetFollow();
            var walker = new Vector2(-10f, 0f);
            for (int i = 0; i < 60; i++)
            {
                walker += new Vector2(20f / 60f, 0f);
                m.Follow(walker, 1f / 60f);
            }
            Assert(Vector2.Distance(m.Position, walker) < 0.05f, $"Follow keeps up with a target moving 20 units/s ({Vector2.Distance(m.Position, walker)} behind).");
            Debug.Log("[PASS] Test 4: Follow converges on its target, keeps up with a fast one, independent of frame rate.");
        }

        private void ValidateAspectChange()
        {
            var rig = Floor1Rig();
            rig.SetAspect(1f); // a square window: the width now limits the whole-map size
            Assert(rig.MaxSize * 2f >= rig.Frame.width - Eps, "A narrower screen raises the whole-map size to fit the width.");
            rig.Reframe();
            Assert(rig.Size * 2f * rig.Aspect >= rig.Frame.width - Eps, "Reframe on the new shape shows the whole width.");
            rig.SetAspect(3f);
            Assert(rig.Size <= rig.MaxSize + Eps && rig.Size >= CameraRig.MinSize - Eps, "A wider screen keeps the size within the new limits.");
            Debug.Log("[PASS] Test 5: A new screen shape keeps the view within the limits.");
        }

        private void ValidatePicker()
        {
            var a = new GUID(1u, 0u, 0u, 1u);
            var b = new GUID(1u, 0u, 0u, 2u);
            var c = new GUID(1u, 0u, 0u, 3u);
            var crowd = new[]
            {
                new PickCandidate(c, new Vector2(0.4f, 0f)),
                new PickCandidate(b, new Vector2(0f, 0.3f)),
                new PickCandidate(a, new Vector2(0f, -0.3f)),
            };
            float r = WorldSelection.PickRadius;
            Assert(WorldPicker.TryPickNearest(new Vector2(0f, 0.25f), crowd, r, out var id) && id == b, "The nearest crawler is picked.");
            Assert(WorldPicker.TryPickNearest(Vector2.zero, crowd, r, out id) && id == a, "Equal distances go to the lower id.");
            Assert(!WorldPicker.TryPickNearest(new Vector2(5f, 5f), crowd, r, out _), "Nothing beyond the radius is picked.");
            Assert(!WorldPicker.TryPickNearest(Vector2.zero, new PickCandidate[0], r, out _) && !WorldPicker.TryPickNearest(Vector2.zero, null, r, out _), "An empty map picks nothing.");
            Assert(WorldPicker.TryPickNearest(new Vector2(0.4f + r - 0.01f, 0f), crowd, r, out id) && id == c, "A click just inside the radius picks.");

            // Selection state: a crawler and its following, a sector, clearing
            var selection = new WorldSelection();
            int changes = 0;
            selection.Changed += () => changes++;
            selection.SelectCrawler(b, follow: true);
            Assert(selection.SelectedCrawler == b && selection.Following && changes == 1, "Selecting a crawler follows it.");
            selection.StopFollowing();
            Assert(selection.SelectedCrawler == b && !selection.Following && changes == 2, "Stopping keeps the crawler selected.");
            selection.SelectSector(3);
            selection.SelectSector(3);
            Assert(selection.SelectedSector == 3 && changes == 3, "A sector selection changes once.");
            selection.ClickAt(Vector2.zero); // no registry: no crawlers, no sectors
            Assert(selection.SelectedCrawler == default && !selection.Following, "A click on empty floor drops the crawler.");
            selection.Clear();
            Assert(selection.SelectedSector == -1 && selection.SelectedCrawler == default, "Clear drops everything.");
            Debug.Log("[PASS] Test 6: Clicks pick the nearest crawler within reach (ties by id); selection state follows the rules.");
        }

        private static bool Near(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < 1e-3f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[CameraRigValidator] Assertion failed: {message}");
        }
    }
}
