using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Entities;
using Simpiens.Presentation.Cutscenes;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The cutscene script and its player, shared by both kinds (M3 design D9, plan step 3.1), without a GPU.</summary>
    public class CutsceneTests
    {
        private static CutsceneScript Hard() => new CutsceneBuilder("test-hard", CutsceneKind.Hard)
            .Character("ada", "Ada", AgentVisualState.Idle)
            .Character("bo", "Bo", AgentVisualState.Walking)
            .Backdrop("outpost")
            .Enter("ada", StageSlot.Left)
            .Say("ada", "Hello")
            .Wait(0.5f)
            .Enter("bo", StageSlot.Right, facingLeft: true)
            .Pose("ada", AgentVisualState.Gossiping)
            .Say("bo", "Hi")
            .Leave("ada")
            .Say("bo", "Bye")
            .Build();

        [Fact]
        public void StepsRunInOrderOnViewTimeAndLinesAppearLetterByLetter()
        {
            var player = new CutscenePlayer(Hard());
            Assert.Equal("outpost", player.Backdrop); // what doesn't block happens at once
            Assert.Equal("ada", player.Stage.Single().Character.Id);
            Assert.Equal("ada", player.Speaker);
            Assert.Equal(0, player.LettersShown);

            player.Update(0.1f); // 40 letters a second
            Assert.Equal(4, player.LettersShown);
            player.Update(1f);
            Assert.True(player.LineComplete);
            Assert.Equal("ada", player.Speaker); // a hard cutscene waits for the player

            player.Advance();
            Assert.Equal(CutsceneStepKind.Wait, player.Current.Kind);
            Assert.Null(player.Line);
            player.Update(0.49f);
            Assert.Equal(CutsceneStepKind.Wait, player.Current.Kind);
            player.Update(0.02f);
            Assert.Equal("bo", player.Speaker);
            Assert.Equal(new[] { "ada", "bo" }, player.Stage.Select(s => s.Character.Id));
            Assert.True(player.Stage[1].FacingLeft);
            Assert.Equal(AgentVisualState.Gossiping, player.Stage[0].Pose);
        }

        [Fact]
        public void AnAdvanceFinishesALineBeforeItMovesOn()
        {
            var player = new CutscenePlayer(Hard());
            player.Advance();
            Assert.True(player.LineComplete);
            Assert.Equal("Hello", player.Line);
            player.Advance();
            Assert.Equal(CutsceneStepKind.Wait, player.Current.Kind);
            player.Advance(); // a pause can be advanced past too
            Assert.Equal("Hi", player.Line);
        }

        [Fact]
        public void CharactersSlideInAndOutAndLeaveTheStage()
        {
            var player = new CutscenePlayer(Hard());
            var ada = player.Stage[0];
            Assert.Equal(0f, player.SlideIn(ada));
            player.Update(0.15f);
            Assert.Equal(0.5f, player.SlideIn(ada), 3);
            player.Update(0.3f);
            Assert.Equal(1f, player.SlideIn(ada));

            player.Advance(); // to the pause
            player.Advance(); // to Bo's line
            player.Advance(); // Bo's line shown whole
            player.Advance(); // Ada leaves; Bo's last line
            Assert.True(ada.Leaving);
            Assert.Contains(ada, player.Stage);
            player.Update(CutscenePlayer.SlideSeconds + 0.01f);
            Assert.DoesNotContain(ada, player.Stage);
            Assert.Equal("Bye", player.Line);

            player.Advance();
            player.Advance();
            Assert.True(player.Finished);
            Assert.Null(player.Current);
        }

        [Fact]
        public void EscSkipsTheScene()
        {
            var player = new CutscenePlayer(Hard());
            player.Skip();
            Assert.True(player.Finished);
            Assert.Empty(player.Stage);
            player.Advance();
            player.Update(1f);
            Assert.True(player.Finished);
        }

        [Fact]
        public void ADynamicSceneFocusesItsActorsAndItsLinesMoveOnByThemselves()
        {
            var script = new CutsceneBuilder("test-dynamic", CutsceneKind.Dynamic)
                .Actors("talker", "listener")
                .Focus("talker")
                .Say("talker", "Hi there")
                .Focus("listener")
                .Say("listener", "Hello")
                .Build();
            var player = new CutscenePlayer(script);
            var focused = new List<string>();
            player.Focused += focused.Add;
            Assert.Equal("talker", player.Focus);

            float first = "Hi there".Length / CutscenePlayer.LettersPerSecond + CutscenePlayer.ReadingSeconds("Hi there");
            player.Update(first - 0.05f);
            Assert.Equal("talker", player.Speaker);
            player.Update(0.1f);
            Assert.Equal("listener", player.Speaker);
            Assert.Equal(new[] { "listener" }, focused);

            player.Advance(); // the player can hurry it too
            player.Advance();
            Assert.True(player.Finished);
        }

        [Fact]
        public void TheSameTimeAndAdvancesReplayTheSameScene()
        {
            static List<(int, string, int)> Run()
            {
                var player = new CutscenePlayer(Hard());
                var trace = new List<(int, string, int)>();
                for (int i = 0; i < 60 && !player.Finished; i++)
                {
                    player.Update(0.07f);
                    if (i % 9 == 8) player.Advance();
                    trace.Add((player.Stage.Count, player.Speaker, player.LettersShown));
                }
                return trace;
            }
            Assert.Equal(Run(), Run());
        }

        [Fact]
        public void AScriptNamesOnlyItsCastAndStepsOfItsKind()
        {
            Assert.Throws<InvalidOperationException>(() => new CutsceneBuilder("x", CutsceneKind.Hard).Say("nobody", "?").Build());
            Assert.Throws<InvalidOperationException>(() => new CutsceneBuilder("x", CutsceneKind.Dynamic).Actors("a").Backdrop("outpost").Build());
            Assert.Throws<InvalidOperationException>(() => new CutsceneBuilder("x", CutsceneKind.Hard).Character("a", "A", AgentVisualState.Idle).Focus("a").Build());
            Assert.True(new CutsceneBuilder("x", CutsceneKind.Dynamic).Build() != null);
            Assert.True(new CutscenePlayer(new CutsceneBuilder("empty", CutsceneKind.Dynamic).Build()).Finished);
        }
    }
}
