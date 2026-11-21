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
        public void NetCodec_RoundTripsPauseRejectAndAwayMessages()
        {
            var paused = (PausedMessage)NetCodec.Decode(NetCodec.Encode(
                new PausedMessage(PauseReason.WaitingForPlayers)));
            var rejected = (RejectedMessage)NetCodec.Decode(NetCodec.Encode(
                new RejectedMessage(JoinRejectReason.NoFreeSeat, "full")));
            var away = NetCodec.Decode(NetCodec.Encode(new AwayMessage()));

            Assert.That(paused.Reason, Is.EqualTo(PauseReason.WaitingForPlayers));
            Assert.That(rejected.Code, Is.EqualTo(JoinRejectReason.NoFreeSeat));
            Assert.That(rejected.DebugReason, Is.EqualTo("full"));
            Assert.That(away, Is.TypeOf<AwayMessage>());
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