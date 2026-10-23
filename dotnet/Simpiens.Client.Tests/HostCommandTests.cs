using System;
using System.IO;
using Simpiens.Core.Session;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The host's commands (review D3): speed, pause, save, load and the session switch are queued by the views and
    /// applied by the session before its next ticks, never inside a click. No GPU: a <see cref="ClientSession"/> alone.
    /// </summary>
    public class HostCommandTests
    {
        private static ClientSession Session(int ticksPerUpdate = 0)
            => new ClientSession(new ClientOptions
            {
                TicksPerUpdate = ticksPerUpdate,
                SaveDirectory = Path.Combine(Path.GetTempPath(), "simpiens-client-tests", Guid.NewGuid().ToString("N")),
            });

        private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

        [Fact]
        public void TheQueueKeepsTheOrderAndACommandQueuedWhileApplyingWaitsForTheNextDrain()
        {
            var queue = new HostCommandQueue();
            queue.Enqueue(HostCommand.Pause);
            queue.Enqueue(HostCommand.SetSpeed(5f));
            queue.Enqueue(HostCommand.SwitchSession("devmap"));

            var drained = queue.Drain();
            Assert.Equal(new[] { HostCommand.Pause, HostCommand.SetSpeed(5f), HostCommand.SwitchSession("devmap") }, drained);
            Assert.Equal(0, queue.Count);
            queue.Enqueue(HostCommand.Resume); // as if applying a command queued another
            Assert.Equal(3, drained.Count);
            Assert.Equal(new[] { HostCommand.Resume }, queue.Drain());
            Assert.Empty(queue.Drain());

            Assert.Throws<ArgumentOutOfRangeException>(() => HostCommand.SetSpeed(0f));
            Assert.Throws<ArgumentException>(() => HostCommand.SwitchSession(""));
        }

        [Fact]
        public void PauseHoldsTheWorldAndKeepsTheSpeedForTheResume()
        {
            using var session = Session();
            var clock = session.Simulation.Clock;
            var frame = TimeSpan.FromSeconds(0.33); // whole ticks with room to spare, whatever float rounding does
            session.HostCommands.Enqueue(HostCommand.SetSpeed(5f));
            Assert.Equal(1f, clock.TimeMultiplier); // queued: nothing changes until the session's update
            session.Update(frame);
            Assert.Equal(5f, clock.TimeMultiplier);
            Assert.Equal(16L, clock.CurrentTick); // the command applied before the ticks: 1.65 s of world, 16 ticks

            session.HostCommands.Enqueue(HostCommand.Pause);
            session.Update(frame);
            Assert.True(session.Paused);
            Assert.Equal(0f, clock.TimeMultiplier);
            Assert.Equal(16L, clock.CurrentTick);

            session.HostCommands.Enqueue(HostCommand.SetSpeed(2f)); // chosen while paused: still paused
            session.Update(frame);
            Assert.Equal(16L, clock.CurrentTick);
            session.HostCommands.Enqueue(HostCommand.Resume);
            session.Update(frame);
            Assert.False(session.Paused);
            Assert.Equal(2f, session.Speed);
            Assert.Equal(23L, clock.CurrentTick); // 0.66 s and the 0.05 s left over: 7 more
        }

        [Fact]
        public void APauseHoldsWholeTickUpdatesTooSoAPausedClockCountsNoTicks()
        {
            using var session = Session(ticksPerUpdate: 1);
            session.Update(Second);
            Assert.Equal(1L, session.Simulation.Clock.CurrentTick);
            session.HostCommands.Enqueue(HostCommand.Pause);
            session.Update(Second);
            session.Update(Second);
            Assert.Equal(1L, session.Simulation.Clock.CurrentTick); // epic F6 doesn't apply: no tick is counted unsimulated
            session.HostCommands.Enqueue(HostCommand.Resume);
            session.Update(Second);
            Assert.Equal(2L, session.Simulation.Clock.CurrentTick);
        }

        [Fact]
        public void SaveAndLoadRunInTheSessionsUpdateBetweenTicks()
        {
            using var session = Session(ticksPerUpdate: 10);
            var saves = session.DashboardModel.Saves;
            Assert.True(saves.Available);
            session.Update(Second);

            saves.Save();
            Assert.Equal("Saving...", saves.Status);
            Assert.False(saves.IsSaving); // asked, not started
            session.Update(Second); // captured at tick 10, before this update's ticks
            for (int i = 0; i < 200 && saves.IsSaving; i++)
            {
                System.Threading.Thread.Sleep(5);
                saves.Poll();
            }
            Assert.Equal("Saved at tick 10.", saves.Status);
            Assert.Equal(20L, session.Simulation.Clock.CurrentTick);

            saves.Load();
            Assert.Equal(20L, session.Simulation.Clock.CurrentTick);
            session.Update(Second);
            Assert.Equal("Loaded the save from tick 10.", saves.Status);
            Assert.Equal(20L, session.Simulation.Clock.CurrentTick); // loaded at 10, then this update's 10 ticks
        }

        [Fact]
        public void ASwitchToASessionNobodyRegisteredChangesNothing()
        {
            using var session = Session();
            var before = session.Simulation.Host.Current;
            session.HostCommands.Enqueue(HostCommand.SwitchSession("no-such-session"));
            session.Update(Second);
            Assert.Same(before, session.Simulation.Host.Current);
            Assert.Equal(OutpostSession.SessionId, session.Simulation.Host.Current.Id);
        }
    }
}
