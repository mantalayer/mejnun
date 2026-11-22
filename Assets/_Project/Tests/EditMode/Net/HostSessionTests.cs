using Mejnun.AI;
using Mejnun.Core;
using NUnit.Framework;

namespace Mejnun.Net.Tests
{
    public sealed class HostSessionTests
    {
        [Test]
        public void Host_RejectsCommandsFromAnotherSeat()
        {
            var host = NewHost();
            host.Join("player-a", "A");
            var rejected = false;
            host.MessageSent += (id, message) => rejected |= id == "player-a" && message is RejectedMessage;

            var accepted = host.SubmitCommands("player-a", new[] { GameCommand.Undo(1) });

            Assert.That(accepted, Is.False);
            Assert.That(rejected, Is.True);
            Assert.That(host.Game.State.TurnNumber, Is.EqualTo(1));
        }

        [Test]
        public void NetCodec_RoundTripsHurryUpMessage()
        {
            var decoded = (HurryUpMessage)NetCodec.Decode(NetCodec.Encode(new HurryUpMessage(2)));

            Assert.That(decoded.FromSeat, Is.EqualTo(2));
        }

        [Test]
        public void Host_RelaysHurryUpToCurrentSeatWithSenderSeat()
        {
            var host = NewHost();
            using (var network = new LoopbackNetwork(host))
            {
                var clientA = network.Connect("player-a", "A");
                var clientB = network.Connect("player-b", "B");
                var targetSeat = host.Game.State.CurrentSeat;
                var targetClient = targetSeat == 0 ? clientA : clientB;
                var senderClient = targetSeat == 0 ? clientB : clientA;
                var senderSeat = targetSeat == 0 ? 1 : 0;
                var receivedFromSeat = -1;
                targetClient.HurryUpReceived += seat => receivedFromSeat = seat;

                host.Tick(HostSession.HurryUpDelaySeconds);
                senderClient.SubmitHurryUp();

                Assert.That(receivedFromSeat, Is.EqualTo(senderSeat));
            }
        }

        [Test]
        public void Host_RejectsHurryUpFromCurrentSeatAndEnforcesCooldown()
        {
            var host = NewHost();
            host.Join("player-a", "A");
            host.Join("player-b", "B");
            var targetSeat = host.Game.State.CurrentSeat;
            var currentPlayerId = targetSeat == 0 ? "player-a" : "player-b";
            var waitingPlayerId = targetSeat == 0 ? "player-b" : "player-a";

            Assert.That(host.RequestHurryUp(currentPlayerId), Is.False);
            host.Tick(HostSession.HurryUpDelaySeconds);
            Assert.That(host.RequestHurryUp(waitingPlayerId), Is.True);
            Assert.That(host.RequestHurryUp(waitingPlayerId), Is.False);

            host.Tick(HostSession.HurryUpCooldownSeconds - 1);
            Assert.That(host.RequestHurryUp(waitingPlayerId), Is.False);

            host.Tick(1);
            Assert.That(host.RequestHurryUp(waitingPlayerId), Is.True);
        }

        [Test]
        public void Host_RejectsHurryUpUntilCurrentTurnHasLastedTwentySeconds()
        {
            var host = NewHost();
            host.Join("player-a", "A");
            host.Join("player-b", "B");
            var targetSeat = host.Game.State.CurrentSeat;
            var waitingPlayerId = targetSeat == 0 ? "player-b" : "player-a";

            host.Tick(HostSession.HurryUpDelaySeconds - 1);
            Assert.That(host.RequestHurryUp(waitingPlayerId), Is.False);

            host.Tick(1);
            Assert.That(host.RequestHurryUp(waitingPlayerId), Is.True);
        }

        [Test]
        public void Host_RaisesHurryUpSentWhenCurrentSeatIsHostLocalSeat()
        {
            var host = NewHost();
            var targetSeat = host.Game.State.CurrentSeat;
            const string hostAuthenticationId = "host-player";
            const string waitingPlayerId = "waiting-player";
            if (targetSeat == 0)
            {
                host.Join(hostAuthenticationId, "Host");
                host.Join(waitingPlayerId, "Player");
            }
            else
            {
                host.Join(waitingPlayerId, "Player");
                host.Join(hostAuthenticationId, "Host");
            }

            var eventTargetSeat = -1;
            var eventFromSeat = -1;
            host.HurryUpSent += (seat, fromSeat) =>
            {
                eventTargetSeat = seat;
                eventFromSeat = fromSeat;
            };

            host.Tick(HostSession.HurryUpDelaySeconds);
            Assert.That(host.RequestHurryUp(waitingPlayerId), Is.True);
            Assert.That(eventTargetSeat, Is.EqualTo(targetSeat));
            Assert.That(eventFromSeat, Is.EqualTo(1 - targetSeat));
        }

        [Test]
        public void Host_AppliesCommandGroupsAtomicallyAndMirrorsState()
        {
            var host = NewHost();
            using (var network = new LoopbackNetwork(host))
            {
                var client = network.Connect("player-a", "A");
                var initialBytes = SnapshotSerializer.Write(host.Game);
                var accepted = host.SubmitCommands("player-a", new[]
                {
                    GameCommand.DrawOrPass(0),
                    GameCommand.Undo(0)
                });

                Assert.That(accepted, Is.False);
                CollectionAssert.AreEqual(initialBytes, SnapshotSerializer.Write(host.Game));

                client.Submit(new[] { GameCommand.DrawOrPass(0) });

                Assert.That(client.Game.State.TurnNumber, Is.EqualTo(host.Game.State.TurnNumber));
                Assert.That(client.Game.State.CurrentSeat, Is.EqualTo(host.Game.State.CurrentSeat));
                CollectionAssert.AreEqual(SnapshotSerializer.WriteForSeat(host.Game, 0),
                    SnapshotSerializer.Write(client.Game));
            }
        }

        [Test]
        public void Host_MirrorsMoveUndoRedoRevertAndFixBoardBeforeTurnEnd()
        {
            var host = NewHost();
            using (var network = new LoopbackNetwork(host))
            {
                var client = network.Connect("player-a", "A");
                var meldCardIds = FindValidMeld(host.Game, 0);
                var move = GameCommand.MoveCards(0, meldCardIds, CardTarget.NewMeld(0));

                client.Submit(new[] { move });
                AssertMirrored(host, client, 0);

                client.Submit(new[] { GameCommand.Undo(0) });
                AssertMirrored(host, client, 0);

                client.Submit(new[] { move });
                AssertMirrored(host, client, 0);

                client.Submit(new[] { GameCommand.Revert(0) });
                AssertMirrored(host, client, 0);

                client.Submit(new[] { move });
                client.Submit(new[]
                {
                    GameCommand.MoveCards(0, new[] { meldCardIds[0] }, CardTarget.NewMeld(0))
                });
                Assert.That(MeldValidator.IsBoardValid(host.Game.State.Board), Is.False);

                var fixCommands = BoardFixer.Plan(host.Game);
                Assert.That(fixCommands.Count, Is.GreaterThan(0));
                client.Submit(fixCommands);

                Assert.That(MeldValidator.IsBoardValid(host.Game.State.Board), Is.True);
                AssertMirrored(host, client, 0);
                Assert.That(host.Game.State.CurrentSeat, Is.Zero);
            }
        }

        [Test]
        public void Host_RematchWaitsForEveryPlayerThenStartsOneSharedGame()
        {
            var host = NewFinishedHost();
            var previousSeed = host.Game.Setup.Seed;
            using (var network = new LoopbackNetwork(host))
            {
                var clientA = network.Connect("player-a", "A");
                var clientB = network.Connect("player-b", "B");

                clientA.SubmitRematch();

                Assert.That(host.IsRematchPending, Is.True);
                Assert.That(host.RematchReadyCount, Is.EqualTo(1));
                Assert.That(host.Game.State.Phase, Is.EqualTo(GamePhase.ChoosingDealer));
                Assert.That(host.Game.Setup.Seed, Is.Not.EqualTo(previousSeed));
                AssertMirrored(host, clientA, 0);
                AssertMirrored(host, clientB, 1);
                Assert.That(clientA.IsRematchPending, Is.True);
                Assert.That(clientA.RematchStatus.ReadySeats, Is.EqualTo(1));
                Assert.That(host.SubmitCommands("player-a", new[] { GameCommand.ChooseRandomDealer() }), Is.False);
                Assert.That(host.Game.State.Phase, Is.EqualTo(GamePhase.ChoosingDealer));

                clientB.SubmitRematch();

                Assert.That(host.IsRematchPending, Is.False);
                Assert.That(host.Game.State.Phase, Is.EqualTo(GamePhase.Playing));
                Assert.That(host.Game.State.Dealer, Is.EqualTo(1));
                Assert.That(clientA.Game.State.Dealer, Is.EqualTo(host.Game.State.Dealer));
                Assert.That(clientB.Game.State.Dealer, Is.EqualTo(host.Game.State.Dealer));
                AssertMirrored(host, clientA, 0);
                AssertMirrored(host, clientB, 1);
            }
        }

        [Test]
        public void Host_FirstPlayerVoteResolvesAfterEveryConnectedCandidateVotes()
        {
            var host = NewChoosingHost(3);
            using (var network = new LoopbackNetwork(host))
            {
                var clientA = network.Connect("player-a", "A");
                var clientB = network.Connect("player-b", "B");
                var clientC = network.Connect("player-c", "C");

                Assert.That(host.BeginFirstPlayerVote("dealer.question.example"), Is.True);
                Assert.That(clientA.FirstPlayerVoteStatus.IsOpen, Is.True);
                CollectionAssert.AreEqual(new[] { true, true, true }, clientA.FirstPlayerVoteStatus.Candidates);
                Assert.That(host.CastFirstPlayerVote("player-a", 1), Is.True);
                Assert.That(host.Game.State.Phase, Is.EqualTo(GamePhase.ChoosingDealer));

                clientB.SubmitFirstPlayerVote(1);
                clientC.SubmitFirstPlayerVote(2);

                Assert.That(host.IsFirstPlayerVoteOpen, Is.False);
                Assert.That(host.Game.State.Phase, Is.EqualTo(GamePhase.Playing));
                Assert.That(host.Game.State.CurrentSeat, Is.EqualTo(1));
                Assert.That(clientA.FirstPlayerVoteStatus == null || !clientA.FirstPlayerVoteStatus.IsOpen, Is.True);
                Assert.That(clientA.Game.State.CurrentSeat, Is.EqualTo(1));
            }
        }

        [Test]
        public void Host_FirstPlayerVoteRejectsNonCandidateVoter()
        {
            var host = NewChoosingHost(3);
            using (var network = new LoopbackNetwork(host))
            {
                var clientA = network.Connect("player-a", "A");
                var clientB = network.Connect("player-b", "B");
                host.BeginFirstPlayerVote("dealer.question.example");
                var clientC = network.Connect("player-c", "C");

                Assert.That(clientC.FirstPlayerVoteStatus.IsOpen, Is.True);
                Assert.That(clientC.FirstPlayerVoteStatus.Candidates[2], Is.False);
                Assert.That(host.CastFirstPlayerVote("player-c", 0), Is.False);
                Assert.That(clientA.FirstPlayerVoteStatus.Candidates[0], Is.True);
                Assert.That(clientB.FirstPlayerVoteStatus.Candidates[1], Is.True);
            }
        }

        [Test]
        public void Host_AloneStartsWithHostWithoutOpeningVote()
        {
            var host = NewChoosingHost(2);
            host.Join("player-a", "A");

            Assert.That(host.BeginFirstPlayerVote("dealer.question.example"), Is.True);

            Assert.That(host.IsFirstPlayerVoteOpen, Is.False);
            Assert.That(host.Game.State.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(host.Game.State.CurrentSeat, Is.Zero);
        }

        [Test]
        public void Host_RematchWithoutNextFirstPlayerOpensVote()
        {
            var host = NewStalemateHost();
            Assert.That(host.Game.State.Result.NextFirstPlayer.HasValue, Is.False);
            using (var network = new LoopbackNetwork(host))
            {
                var clientA = network.Connect("player-a", "A");
                var clientB = network.Connect("player-b", "B");
                var clientC = network.Connect("player-c", "C");

                clientA.SubmitRematch();
                clientB.SubmitRematch();
                clientC.SubmitRematch();

                Assert.That(host.IsRematchPending, Is.False);
                Assert.That(host.Game.State.Phase, Is.EqualTo(GamePhase.ChoosingDealer));
                Assert.That(host.IsFirstPlayerVoteOpen, Is.True);
                Assert.That(clientA.FirstPlayerVoteStatus.IsOpen, Is.True);
                Assert.That(clientA.IsRematchPending, Is.False);
            }
        }

        [Test]
        public void Host_RematchIsIgnoredWhileGameIsNotFinished()
        {
            var host = NewHost();
            using (var network = new LoopbackNetwork(host))
            {
                var client = network.Connect("player-a", "A");
                var previousSeed = host.Game.Setup.Seed;

                client.SubmitRematch();

                Assert.That(host.IsRematchPending, Is.False);
                Assert.That(host.Game.State.Phase, Is.EqualTo(GamePhase.Playing));
                Assert.That(host.Game.Setup.Seed, Is.EqualTo(previousSeed));
            }
        }

        [Test]
        public void Host_RematchCountsBotSeatsAsReady()
        {
            var host = NewFinishedHost(3);
            host.AddBot(1, "Bot");
            using (var network = new LoopbackNetwork(host))
            {
                var clientA = network.Connect("player-a", "A");
                var clientB = network.Connect("player-b", "B");

                clientA.SubmitRematch();

                Assert.That(host.IsRematchPending, Is.True);
                Assert.That(host.RematchReadyCount, Is.EqualTo(2));
                Assert.That(clientA.RematchStatus.ReadySeats, Is.EqualTo(2));

                clientB.SubmitRematch();

                Assert.That(host.IsRematchPending, Is.False);
                Assert.That(host.Game.State.Phase, Is.EqualTo(GamePhase.Playing));
                Assert.That(host.Game.State.Dealer, Is.EqualTo(2));
                AssertMirrored(host, clientA, 0);
                AssertMirrored(host, clientB, 2);
            }
        }

        [Test]
        public void Host_TickAppliesTurnTimeout()
        {
            var game = NewGame(1);
            var host = new HostSession(game);
            host.Join("player-a", "A");

            host.Tick(1.1);

            Assert.That(host.Game.State.TurnNumber, Is.EqualTo(2));
        }

        [Test]
        public void Host_DisconnectedSeatCanBeTakenOverByBot()
        {
            var host = NewHost();
            host.Join("player-a", "A");
            Assert.That(host.Disconnect("player-a", DisconnectChoice.Wait), Is.True);
            host.Tick(SeatRoster.DefaultGraceSeconds);
            Assert.That(host.ResolveDisconnect("player-a", DisconnectChoice.TakeOverWithBot), Is.True);

            host.Tick(0.5);

            Assert.That(host.Game.State.CurrentSeat, Is.EqualTo(1));
        }

        [Test]
        public void Host_ReconnectRestoresSeatAndSnapshot()
        {
            var host = NewHost();
            using (var network = new LoopbackNetwork(host))
            {
                var client = network.Connect("player-a", "A");
                Assert.That(network.Disconnect("player-a", DisconnectChoice.Wait), Is.True);

                var reconnected = network.Connect("player-a", "A again");

                Assert.That(reconnected, Is.SameAs(client));
                Assert.That(reconnected.LocalSeat, Is.Zero);
                Assert.That(host.GetSeat(0).Connected, Is.True);
            }
        }

        [Test]
        public void Host_RejoinAfterGraceSendsCurrentPauseState()
        {
            var host = NewHost();
            using (var network = new LoopbackNetwork(host))
            {
                network.Connect("player-a", "A");
                network.Connect("player-b", "B");
                Assert.That(network.Disconnect("player-a", DisconnectChoice.Wait), Is.True);
                host.Tick(SeatRoster.DefaultGraceSeconds);

                var sentToReturningPlayer = new System.Collections.Generic.List<NetMessage>();
                host.MessageSent += (id, message) =>
                {
                    if (id == "player-a")
                    {
                        sentToReturningPlayer.Add(message);
                    }
                };

                var reconnected = network.Rejoin("player-a", "A again");

                Assert.That(reconnected.LocalSeat, Is.Zero);
                Assert.That(reconnected.IsPaused, Is.False);
                Assert.That(host.GetSeat(0).Presence, Is.EqualTo(SeatPresence.Connected));
                Assert.That(sentToReturningPlayer.Exists(message => message is PausedMessage paused &&
                    paused.Reason == PauseReason.None), Is.True);
                Assert.That(sentToReturningPlayer.Exists(message => message is PausedMessage paused &&
                    paused.Reason == PauseReason.WaitingForPlayers), Is.False);
            }
        }

        [Test]
        public void Host_SeatStatusChangesTrackConnectionsAndReadiness()
        {
            var host = NewHost();
            var statuses = new System.Collections.Generic.List<SeatStatusMessage>();
            host.SeatStatusChanged += statuses.Add;

            host.Join("player-a", "A");

            Assert.That(host.AreAllSeatsConnected, Is.False);
            Assert.That(statuses.Exists(status => status.Seat == 0 && status.Connected &&
                                                   status.DisplayName == "A"), Is.True);

            host.Join("player-b", "B");

            Assert.That(host.AreAllSeatsConnected, Is.True);
            Assert.That(statuses.Exists(status => status.Seat == 1 && status.Connected &&
                                                   status.DisplayName == "B"), Is.True);
            Assert.That(host.Disconnect("player-b", DisconnectChoice.Wait), Is.True);
            Assert.That(host.AreAllSeatsConnected, Is.False);
        }

        [Test]
        public void Host_ReconnectingSeatFreezesTurnAndExpiresIntoWaitingState()
        {
            var host = NewHost();
            host.Join("player-a", "A");
            host.Join("player-b", "B");
            var initialTurn = host.Game.State.TurnNumber;
            var expiredSeat = -1;
            host.SeatExpired += seat => expiredSeat = seat;

            Assert.That(host.Disconnect("player-a", DisconnectChoice.Wait), Is.True);
            host.Tick(10);

            Assert.That(host.GetSeat(0).Presence, Is.EqualTo(SeatPresence.Reconnecting));
            Assert.That(host.Roster.Get(0).GraceRemaining, Is.EqualTo(SeatRoster.DefaultGraceSeconds - 10));
            Assert.That(host.Game.State.TurnNumber, Is.EqualTo(initialTurn));
            Assert.That(host.IsPaused, Is.True);

            host.Tick(10);

            Assert.That(host.GetSeat(0).Presence, Is.EqualTo(SeatPresence.Disconnected));
            Assert.That(expiredSeat, Is.Zero);
            Assert.That(host.PauseReason, Is.EqualTo(PauseReason.WaitingForPlayers));
            Assert.That(host.SubmitCommands("player-b", new[] { GameCommand.Undo(1) }), Is.False);
            Assert.That(host.Game.State.TurnNumber, Is.EqualTo(initialTurn));
        }

        [Test]
        public void Host_NewJoinerCannotClaimSeatDuringGraceButCanClaimAfterExpiry()
        {
            var host = NewHost();
            host.Join("player-a", "A");
            host.Join("player-b", "B");
            RejectedMessage rejection = null;
            host.MessageSent += (id, message) =>
            {
                if (id == "player-c")
                {
                    rejection = message as RejectedMessage;
                }
            };

            host.Disconnect("player-a", DisconnectChoice.Wait);
            Assert.That(host.Join("player-c", "C"), Is.EqualTo(-1));
            Assert.That(rejection.Code, Is.EqualTo(JoinRejectReason.NoFreeSeat));

            host.Tick(SeatRoster.DefaultGraceSeconds);

            Assert.That(host.Join("player-c", "C"), Is.Zero);
            Assert.That(host.GetSeat(0).AuthenticationId, Is.EqualTo("player-c"));
        }

        [Test]
        public void Host_ReclaimingAiControlledCurrentSeatWaitsUntilTurnChanges()
        {
            var host = NewHost();
            host.Join("player-a", "A");
            host.Join("player-b", "B");
            var aiSeat = host.Game.State.CurrentSeat;
            var authenticationId = aiSeat == 0 ? "player-a" : "player-b";

            host.Disconnect(authenticationId, DisconnectChoice.Wait);
            host.Tick(SeatRoster.DefaultGraceSeconds);
            Assert.That(host.ResolveDisconnect(authenticationId, DisconnectChoice.TakeOverWithBot), Is.True);

            var joinerSeat = host.Join("player-c", "C");
            Assert.That(joinerSeat, Is.EqualTo(aiSeat));
            Assert.That(host.GetSeat(aiSeat).PendingHandBack, Is.True);
            Assert.That(host.GetSeat(aiSeat).Presence, Is.EqualTo(SeatPresence.BotControlled));

            host.Tick(0.5);

            Assert.That(host.Game.State.CurrentSeat, Is.Not.EqualTo(aiSeat));
            Assert.That(host.GetSeat(aiSeat).Presence, Is.EqualTo(SeatPresence.Connected));
            Assert.That(host.GetSeat(aiSeat).PendingHandBack, Is.False);
        }

        [Test]
        public void Host_SavePauseAndMigrationRestoreTheSession()
        {
            var store = new InMemorySnapshotStore();
            var host = new HostSession(NewHostGame(), store);
            host.Join("player-a", "A");
            host.Disconnect("player-a", DisconnectChoice.SaveAndPause);
            Assert.That(host.IsPaused, Is.True);

            var migrated = HostSession.FromRecord(store.Load(), store);

            Assert.That(migrated.IsPaused, Is.True);
            CollectionAssert.AreEqual(SnapshotSerializer.Write(host.Game), SnapshotSerializer.Write(migrated.Game));
            migrated.Join("player-a", "A");
            migrated.Resume();
            Assert.That(migrated.IsPaused, Is.False);
            Assert.That(store.Load().Paused, Is.False);
        }

        [Test]
        public void NetCodec_RoundTripsCommandsAndStateMessages()
        {
            var command = GameCommand.MoveCards(0, new byte[] { 1, 2 }, CardTarget.Meld(4, 1));
            var commands = (CommandsMessage)NetCodec.Decode(NetCodec.Encode(new CommandsMessage(new[] { command })));
            var state = (SeatStatusMessage)NetCodec.Decode(NetCodec.Encode(
                new SeatStatusMessage(1, "user", "Player", SeatPresence.Reconnecting, false, 8.5f)));

            Assert.That(commands.Commands[0].Type, Is.EqualTo(command.Type));
            CollectionAssert.AreEqual(command.CardIds, commands.Commands[0].CardIds);
            Assert.That(state.DisplayName, Is.EqualTo("Player"));
            Assert.That(state.Presence, Is.EqualTo(SeatPresence.Reconnecting));
            Assert.That(state.GraceRemaining, Is.EqualTo(8.5f));
        }

        [Test]
        public void NetCodec_RoundTripsPauseRejectAwayAndRematchMessages()
        {
            var paused = (PausedMessage)NetCodec.Decode(NetCodec.Encode(
                new PausedMessage(PauseReason.WaitingForPlayers)));
            var rejected = (RejectedMessage)NetCodec.Decode(NetCodec.Encode(
                new RejectedMessage(JoinRejectReason.NoFreeSeat, "full")));
            var away = NetCodec.Decode(NetCodec.Encode(new AwayMessage()));
            var rematch = NetCodec.Decode(NetCodec.Encode(new RematchMessage()));
            var rematchStatus = (RematchStatusMessage)NetCodec.Decode(NetCodec.Encode(
                new RematchStatusMessage(1, 2)));

            Assert.That(paused.Reason, Is.EqualTo(PauseReason.WaitingForPlayers));
            Assert.That(rejected.Code, Is.EqualTo(JoinRejectReason.NoFreeSeat));
            Assert.That(rejected.DebugReason, Is.EqualTo("full"));
            Assert.That(away, Is.TypeOf<AwayMessage>());
            Assert.That(rematch, Is.TypeOf<RematchMessage>());
            Assert.That(rematchStatus.ReadySeats, Is.EqualTo(1));
            Assert.That(rematchStatus.SeatCount, Is.EqualTo(2));
        }

        [Test]
        public void SessionRecordCodec_RoundTripsHostMigrationSnapshotAndSeats()
        {
            var host = NewHost();
            host.Join("player-a", "A");
            host.AddBot(1, "Bot", BotDifficulty.Hard);
            var original = host.CreateRecord();

            var restored = SessionRecordCodec.Read(SessionRecordCodec.Write(original));

            CollectionAssert.AreEqual(original.Snapshot, restored.Snapshot);
            Assert.That(restored.Seats[0].AuthenticationId, Is.EqualTo("player-a"));
            Assert.That(restored.Seats[1].IsBot, Is.True);
            Assert.That(restored.Seats[1].Difficulty, Is.EqualTo(BotDifficulty.Hard));
        }

        [Test]
        public void SessionRecordCodec_ReadsVersionOneRecordsAsReconnectingSeats()
        {
            var snapshot = SnapshotSerializer.Write(NewHostGame());
            var bytes = CreateVersionOneRecord(snapshot);

            var record = SessionRecordCodec.Read(bytes);

            Assert.That(record.Seats[0].Presence, Is.EqualTo(SeatPresence.Reconnecting));
            Assert.That(record.Seats[1].Presence, Is.EqualTo(SeatPresence.Empty));
            Assert.That(record.PauseReason, Is.EqualTo(PauseReason.None));
        }

        [Test]
        public void Host_PrepareForPromotionRestoresLocalSeatAndKeepsOtherPlayersReconnecting()
        {
            var host = NewHost();
            host.Join("player-a", "A");
            host.Join("player-b", "B");
            var promotedHost = HostSession.FromRecord(host.CreateRecord());
            promotedHost.SetPauseReason(PauseReason.HostAway);

            var localSeat = promotedHost.PrepareForPromotion("player-a");

            Assert.That(localSeat, Is.Zero);
            Assert.That(promotedHost.Roster.Get(localSeat).Presence, Is.EqualTo(SeatPresence.Connected));
            Assert.That(promotedHost.Roster.Get(1).Presence, Is.EqualTo(SeatPresence.Reconnecting));
            Assert.That(promotedHost.Roster.Get(1).GraceRemaining,
                Is.EqualTo(HostSession.MigrationReconnectGraceSeconds));
            Assert.That(promotedHost.PauseReason, Is.EqualTo(PauseReason.WaitingForPlayers));
        }

        [Test]
        public void Host_PrepareForPromotionReturnsMinusOneWhenLocalSeatIsMissing()
        {
            var host = NewHost();

            var localSeat = host.PrepareForPromotion("unknown-player");

            Assert.That(localSeat, Is.EqualTo(-1));
            Assert.That(host.Roster.Get(0).Presence, Is.EqualTo(SeatPresence.Empty));
        }

        [Test]
        public void Host_PrepareForPromotionGivesBotControlledHumanAReconnectGrace()
        {
            var host = NewHost();
            host.Join("player-a", "A");
            host.Join("player-b", "B");
            host.Roster.MarkDropped("player-b");
            host.Roster.Tick(SeatRoster.DefaultGraceSeconds);
            host.Roster.SetBotControl(1, BotDifficulty.Medium);
            var promotedHost = HostSession.FromRecord(host.CreateRecord());

            promotedHost.PrepareForPromotion("player-a");

            Assert.That(promotedHost.Roster.Get(1).Presence, Is.EqualTo(SeatPresence.Reconnecting));
            Assert.That(promotedHost.Roster.Get(1).GraceRemaining,
                Is.EqualTo(HostSession.MigrationReconnectGraceSeconds));
        }

        [Test]
        public void SessionRecordCodec_TryReadRejectsMissingOrMalformedRecords()
        {
            Assert.That(SessionRecordCodec.TryRead(null, out var nullRecord), Is.False);
            Assert.That(nullRecord, Is.Null);
            Assert.That(SessionRecordCodec.TryRead(new byte[0], out var emptyRecord), Is.False);
            Assert.That(emptyRecord, Is.Null);
            Assert.That(SessionRecordCodec.TryRead(new byte[] { 1, 2, 3 }, out var malformedRecord), Is.False);
            Assert.That(malformedRecord, Is.Null);
        }

        [Test]
        public void SessionRecordCodec_TryReadAcceptsAValidRecord()
        {
            var bytes = SessionRecordCodec.Write(NewHost().CreateRecord());

            Assert.That(SessionRecordCodec.TryRead(bytes, out var record), Is.True);
            Assert.That(record, Is.Not.Null);
        }

        private static byte[] CreateVersionOneRecord(byte[] snapshot)
        {
            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(0x4D4A5352u);
                writer.Write(1);
                writer.Write(false);
                writer.Write(snapshot.Length);
                writer.Write(snapshot);
                writer.Write(2);
                writer.Write("player-a");
                writer.Write("A");
                writer.Write(true);
                writer.Write(false);
                writer.Write((byte)BotDifficulty.Medium);
                writer.Write(string.Empty);
                writer.Write(string.Empty);
                writer.Write(false);
                writer.Write(false);
                writer.Write((byte)BotDifficulty.Medium);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static HostSession NewHost()
        {
            return new HostSession(NewHostGame());
        }

        private static HostSession NewChoosingHost(int seatCount)
        {
            return new HostSession(new MejnunGame(new GameSetup(seatCount, 73UL)));
        }

        private static HostSession NewFinishedHost(int seatCount = 2)
        {
            var game = new MejnunGame(new GameSetup(seatCount, 71UL, dealer: seatCount - 1));
            for (var seat = 1; seat < seatCount; seat++)
            {
                var topIndex = game.State.DrawPile.Count - 1;
                game.State.Hands[seat].Add(game.State.DrawPile[topIndex]);
                game.State.DrawPile.RemoveAt(topIndex);
            }

            while (game.State.DrawPile.Count > 0)
            {
                var topIndex = game.State.DrawPile.Count - 1;
                game.State.Hands[1].Add(game.State.DrawPile[topIndex]);
                game.State.DrawPile.RemoveAt(topIndex);
            }

            for (var pass = 0; pass < seatCount; pass++)
            {
                var seat = game.State.CurrentSeat;
                Assert.That(game.Apply(GameCommand.DrawOrPass(seat)).Ok, Is.True);
            }

            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Finished));
            return new HostSession(game);
        }

        private static HostSession NewStalemateHost()
        {
            const int seatCount = 3;
            var game = new MejnunGame(new GameSetup(seatCount, 71UL, dealer: seatCount - 1));
            while (game.State.DrawPile.Count > 0)
            {
                var topIndex = game.State.DrawPile.Count - 1;
                game.State.Hands[2].Add(game.State.DrawPile[topIndex]);
                game.State.DrawPile.RemoveAt(topIndex);
            }

            Assert.That(game.State.Hands[0].Count, Is.EqualTo(game.State.Hands[1].Count));
            Assert.That(game.State.Hands[2].Count, Is.GreaterThan(game.State.Hands[0].Count));
            for (var pass = 0; pass < seatCount; pass++)
            {
                Assert.That(game.Apply(GameCommand.DrawOrPass(game.State.CurrentSeat)).Ok, Is.True);
            }

            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Finished));
            return new HostSession(game);
        }

        private static MejnunGame NewHostGame()
        {
            return new MejnunGame(new GameSetup(2, 71UL, dealer: 1));
        }

        private static MejnunGame NewGame(int turnSeconds)
        {
            return new MejnunGame(new GameSetup(2, 71UL, dealer: 1, turnSeconds: turnSeconds));
        }

        private static byte[] FindValidMeld(MejnunGame game, int seat)
        {
            var hand = game.State.Hands[seat];
            for (var first = 0; first < hand.Count; first++)
            {
                for (var second = first + 1; second < hand.Count; second++)
                {
                    for (var third = second + 1; third < hand.Count; third++)
                    {
                        var cards = new[] { hand[first], hand[second], hand[third] };
                        if (MeldValidator.Classify(cards).IsValid)
                        {
                            return new[] { hand[first].Id, hand[second].Id, hand[third].Id };
                        }
                    }
                }
            }

            Assert.Fail("The deterministic starting hand must contain a valid meld.");
            return null;
        }

        private static void AssertMirrored(HostSession host, ClientSession client, int seat)
        {
            CollectionAssert.AreEqual(SnapshotSerializer.WriteForSeat(host.Game, seat),
                SnapshotSerializer.Write(client.Game));
        }
    }
}