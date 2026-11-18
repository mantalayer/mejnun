using System.Collections.Generic;
using System.Reflection;
using Mejnun.Audio;
using Mejnun.Core;
using NUnit.Framework;

namespace Mejnun.Game.Tests
{
    public sealed class AudioMapperTests
    {
        [Test]
        public void BindSubscribesToEveryGameEvent()
        {
            var game = new MejnunGame(new GameSetup(2, 1UL));
            var mapper = new GameAudioMapper(new RecordingAudioService());

            mapper.Bind(game);

            var eventFields = typeof(GameEvents).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var eventField in eventFields)
            {
                Assert.That(eventField.GetValue(game.Events), Is.Not.Null, eventField.Name);
            }
        }

        [Test]
        public void DealerChoicePlaysShuffleAndQueuesCappedDealSounds()
        {
            var game = new MejnunGame(new GameSetup(2, 1UL));
            var audio = new RecordingAudioService();
            var mapper = new GameAudioMapper(audio);
            mapper.Bind(game);

            Assert.That(game.Apply(GameCommand.ChooseDealer(0)).Ok, Is.True);
            for (var frame = 0; frame < 20; frame++)
            {
                mapper.AdvanceFrame(0.1f);
            }

            Assert.That(audio.Posts, Does.Contain(AudioIds.PlayCardShuffle));
            Assert.That(audio.Posts.FindAll(eventName => eventName == AudioIds.PlayCardDeal).Count, Is.EqualTo(12));
        }

        [Test]
        public void DrawPlaysDrawAndStartsNextTurn()
        {
            var game = new MejnunGame(new GameSetup(2, 3UL));
            var audio = new RecordingAudioService();
            var mapper = new GameAudioMapper(audio);
            mapper.Bind(game);
            Assert.That(game.Apply(GameCommand.ChooseDealer(0)).Ok, Is.True);
            audio.Clear();

            Assert.That(game.Apply(GameCommand.DrawOrPass(game.State.CurrentSeat)).Ok, Is.True);

            Assert.That(audio.Posts, Does.Contain(AudioIds.PlayCardDraw));
            Assert.That(audio.Posts, Does.Contain(AudioIds.PlayTurnStartPlayer));
        }

        [Test]
        public void TurnStartedUsesDifferentSoundsForHumanAndBotSeats()
        {
            var game = new MejnunGame(new GameSetup(2, 4UL));
            var audio = new RecordingAudioService();
            var mapper = new GameAudioMapper(audio);
            mapper.Bind(game, seat => seat == 0, 1);

            RaiseGameEvent(game.Events, "RaiseTurnStarted", 0, 1);
            RaiseGameEvent(game.Events, "RaiseTurnStarted", 1, 2);

            Assert.That(audio.Posts, Is.EqualTo(new[]
            {
                AudioIds.PlayTurnStartPlayer,
                AudioIds.PlayTurnStartBot,
            }));
        }

        [Test]
        public void MultipleCardMovesInOneFrameProduceOneDropSound()
        {
            var game = GestureTestGameFactory.CreateGameWithStraightInHand(4, out var straight);
            var audio = new RecordingAudioService();
            var mapper = new GameAudioMapper(audio);
            mapper.Bind(game);

            Assert.That(game.Apply(GameCommand.MoveCards(game.State.CurrentSeat, straight,
                CardTarget.NewMeld(0))).Ok, Is.True);
            mapper.ReportDropCue();
            mapper.AdvanceFrame(1f / 60f);

            Assert.That(audio.Posts.FindAll(eventName => eventName == AudioIds.PlayCardDrop).Count, Is.EqualTo(1));
        }

        [Test]
        public void WentDownAndRevealInOneFrameProduceOneSound()
        {
            var game = new MejnunGame(new GameSetup(2, 41UL));
            var audio = new RecordingAudioService();
            var mapper = new GameAudioMapper(audio);
            mapper.Bind(game);

            RaiseGameEvent(game.Events, "RaiseWentDown", 0);
            RaiseGameEvent(game.Events, "RaiseDownRevealed", 0, new List<int>());
            mapper.AdvanceFrame(1f / 60f);

            Assert.That(audio.Posts.FindAll(eventName => eventName == AudioIds.PlayGoDown).Count, Is.EqualTo(1));
        }

        [Test]
        public void UndoAndRevertPlayTheirFeedbackEvents()
        {
            var game = GestureTestGameFactory.CreateGameWithStraightInHand(4, out var straight);
            var audio = new RecordingAudioService();
            var mapper = new GameAudioMapper(audio);
            mapper.Bind(game);
            var seat = game.State.CurrentSeat;

            Assert.That(game.Apply(GameCommand.MoveCards(seat, straight, CardTarget.NewMeld(0))).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.Undo(seat)).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.MoveCards(seat, straight, CardTarget.NewMeld(0))).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.Revert(seat)).Ok, Is.True);

            Assert.That(audio.Posts, Does.Contain(AudioIds.PlayUndo));
            Assert.That(audio.Posts, Does.Contain(AudioIds.PlayBoardRevert));
        }

        [Test]
        public void EmptyHandWinPlaysShoutAndWinStinger()
        {
            var game = new MejnunGame(new GameSetup(2, 5UL));
            var audio = new RecordingAudioService();
            var mapper = new GameAudioMapper(audio);
            mapper.Bind(game);

            RaiseGameEvent(game.Events, "RaiseWon", new GameResult(GameEndKind.EmptyHand, 0, 0));

            Assert.That(audio.Switches, Does.Contain((AudioIds.ShoutType, AudioIds.ShoutDefault)));
            Assert.That(audio.Posts, Does.Contain(AudioIds.PlayMejnunShout));
            Assert.That(audio.Posts, Does.Contain(AudioIds.PlayWinStinger));
        }

        [Test]
        public void SingleHumanLosingToBotPlaysLoseStinger()
        {
            var game = new MejnunGame(new GameSetup(2, 6UL));
            var audio = new RecordingAudioService();
            var mapper = new GameAudioMapper(audio);
            mapper.Bind(game, seat => seat == 0, 1);

            RaiseGameEvent(game.Events, "RaiseWon", new GameResult(GameEndKind.EmptyHand, 1, 1));

            Assert.That(audio.Posts, Does.Contain(AudioIds.PlayLoseStinger));
            Assert.That(audio.Posts, Does.Not.Contain(AudioIds.PlayWinStinger));
        }

        [Test]
        public void UnbindStopsPostingEvents()
        {
            var game = new MejnunGame(new GameSetup(2, 7UL));
            var audio = new RecordingAudioService();
            var mapper = new GameAudioMapper(audio);
            mapper.Bind(game);
            Assert.That(game.Apply(GameCommand.ChooseDealer(0)).Ok, Is.True);
            audio.Clear();
            mapper.Unbind();

            Assert.That(game.Apply(GameCommand.DrawOrPass(game.State.CurrentSeat)).Ok, Is.True);
            mapper.AdvanceFrame(1f);

            Assert.That(audio.Posts, Is.Empty);
        }

        [Test]
        public void TimerTensionWarningTickAndTimeoutUseThresholdsOnce()
        {
            var game = new MejnunGame(new GameSetup(2, 8UL));
            var audio = new RecordingAudioService();
            var mapper = new GameAudioMapper(audio);
            mapper.Bind(game);

            mapper.ReportTurnTimer(60f, 60f);
            mapper.ReportTurnTimer(10f, 60f);
            mapper.ReportTurnTimer(5f, 60f);
            mapper.ReportTurnTimer(5f, 60f);
            mapper.ReportTimeout();
            mapper.ReportTimeout();

            Assert.That(audio.Rtpcs[0], Is.EqualTo((AudioIds.TimerTension, 0f)));
            Assert.That(audio.Rtpcs[1].Item2, Is.EqualTo(1f - 10f / 60f).Within(0.0001f));
            Assert.That(audio.Posts.FindAll(eventName => eventName == AudioIds.PlayTimerWarning).Count, Is.EqualTo(1));
            Assert.That(audio.Posts.FindAll(eventName => eventName == AudioIds.PlayTimerTick).Count, Is.EqualTo(1));
            Assert.That(audio.Posts.FindAll(eventName => eventName == AudioIds.PlayTimerHalas).Count, Is.EqualTo(1));
        }

        private static void RaiseGameEvent(GameEvents events, string methodName, params object[] arguments)
        {
            var method = typeof(GameEvents).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(events, arguments);
        }

        private sealed class RecordingAudioService : IAudioService
        {
            public readonly List<string> Posts = new List<string>();
            public readonly List<(string, float)> Rtpcs = new List<(string, float)>();
            public readonly List<(string, string)> Switches = new List<(string, string)>();

            public void Post(string eventName)
            {
                Posts.Add(eventName);
            }

            public void SetState(string stateGroup, string state)
            {
            }

            public void SetSwitch(string switchGroup, string switchValue)
            {
                Switches.Add((switchGroup, switchValue));
            }

            public void SetRtpc(string rtpcName, float value)
            {
                Rtpcs.Add((rtpcName, value));
            }

            public bool LoadBank(string bankName)
            {
                return true;
            }

            public void Clear()
            {
                Posts.Clear();
                Rtpcs.Clear();
                Switches.Clear();
            }
        }
    }
}