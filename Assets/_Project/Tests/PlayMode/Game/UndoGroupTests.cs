using System.Collections.Generic;
using Mejnun.Core;
using NUnit.Framework;
using UnityEngine;

namespace Mejnun.Game.Tests
{
    public sealed class UndoGroupTests
    {
        [Test]
        public void CycleMarkedCardRaisesMarkFeedbackWithoutMarkingItsSet()
        {
            var game = GestureTestGameFactory.CreateGameWithStraightInHand(4, out var straight);
            var seat = game.State.CurrentSeat;
            Assert.That(game.Apply(GameCommand.MoveCards(seat, straight, CardTarget.NewMeld(0))).Ok, Is.True);
            var cardId = game.State.Board[0].Cards[1].Id;
            var controllerObject = new GameObject("Table Controller Test");
            FeedbackCue? raisedCue = null;
            System.Action<FeedbackCue> onCueRaised = cue => raisedCue = cue;
            FeedbackCues.Raised += onCueRaised;
            try
            {
                var controller = controllerObject.AddComponent<TableController>();
                controller.Initialize(game);

                controller.CycleMarkedCard(cardId);

                Assert.That(raisedCue, Is.EqualTo(FeedbackCue.Mark));
                Assert.That(controller.MarkedCards.Count, Is.EqualTo(1));
                Assert.That(controller.MarkedSets, Is.Empty);
            }
            finally
            {
                FeedbackCues.Raised -= onCueRaised;
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void OneUndoRestoresAWholeTwoCommandGesture()
        {
            var game = GestureTestGameFactory.CreateGameWithStraightInHand(4, out var straight);
            var seat = game.State.CurrentSeat;
            Assert.That(game.Apply(GameCommand.MoveCards(seat, straight, CardTarget.NewMeld(0))).Ok, Is.True);
            var handBefore = CopyCardIds(game.State.Hands[seat]);
            var boardBefore = BoardSignature(game.State.Board);
            var meld = game.State.Board[0];
            var pickedCard = meld.Cards[1];
            var controllerObject = new GameObject("Table Controller Test");
            try
            {
                var controller = controllerObject.AddComponent<TableController>();
                controller.Initialize(game);

                var drop = controller.ExecuteGesture(new GestureInput(GestureType.Drag,
                    GestureHit.BoardCard(pickedCard.Id, meld.Id, 1, 0), GestureHit.EmptyBoard()));

                Assert.That(drop.Ok, Is.True);
                Assert.That(controller.UndoGroupCount, Is.EqualTo(1));
                Assert.That(controller.UndoGesture().Ok, Is.True);
                Assert.That(controller.UndoGroupCount, Is.EqualTo(0));
                Assert.That(CopyCardIds(game.State.Hands[seat]), Is.EqualTo(handBefore));
                Assert.That(BoardSignature(game.State.Board), Is.EqualTo(boardBefore));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void MarkModeTurnsOffAfterDragUndoAndRevert()
        {
            var game = GestureTestGameFactory.CreateGameWithStraightInHand(4, out var straight);
            var seat = game.State.CurrentSeat;
            Assert.That(game.Apply(GameCommand.MoveCards(seat, straight, CardTarget.NewMeld(0))).Ok, Is.True);
            var meld = game.State.Board[0];
            var controllerObject = new GameObject("Table Controller Test");
            try
            {
                var controller = controllerObject.AddComponent<TableController>();
                controller.Initialize(game);
                controller.SetMarkMode(true);

                var drop = controller.ExecuteGesture(new GestureInput(GestureType.Drag,
                    GestureHit.BoardCard(meld.Cards[1].Id, meld.Id, 1, 0), GestureHit.EmptyBoard()));

                Assert.That(drop.Ok, Is.True);
                Assert.That(controller.MarkModeActive, Is.False);
                controller.SetMarkMode(true);
                Assert.That(controller.UndoGesture().Ok, Is.True);
                Assert.That(controller.MarkModeActive, Is.False);
                controller.SetMarkMode(true);
                Assert.That(controller.RevertTurn().Ok, Is.True);
                Assert.That(controller.MarkModeActive, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void FailedSecondCommandRollsBackTheGestureAndDoesNotPushUndo()
        {
            var game = GestureTestGameFactory.CreateGameWithStraightInHand(3, out var straight);
            var seat = game.State.CurrentSeat;
            var handBefore = CopyCardIds(game.State.Hands[seat]);
            var boardBefore = BoardSignature(game.State.Board);
            var remainingCard = FindCardNotIn(game.State.Hands[seat], straight);
            var controllerObject = new GameObject("Table Controller Test");
            try
            {
                var controller = controllerObject.AddComponent<TableController>();
                controller.Initialize(game);
                var batch = new CommandBatch();
                batch.Add(GameCommand.MoveCards(seat, straight, CardTarget.NewMeld(0)));
                batch.Add(GameCommand.MoveCards(seat, new[] { remainingCard }, CardTarget.Meld(9999, 0)));

                var result = controller.ExecuteBatch(batch);

                Assert.That(result.Ok, Is.False);
                Assert.That(controller.UndoGroupCount, Is.EqualTo(0));
                Assert.That(CopyCardIds(game.State.Hands[seat]), Is.EqualTo(handBefore));
                Assert.That(BoardSignature(game.State.Board), Is.EqualTo(boardBefore));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void RevertClearsAllGestureUndoGroups()
        {
            var game = GestureTestGameFactory.CreateGameWithStraightInHand(4, out var straight);
            var seat = game.State.CurrentSeat;
            var controllerObject = new GameObject("Table Controller Test");
            try
            {
                var controller = controllerObject.AddComponent<TableController>();
                controller.Initialize(game);
                Assert.That(controller.ApplyCommand(GameCommand.MoveCards(seat,
                    new[] { straight[0], straight[1], straight[2] }, CardTarget.NewMeld(0))).Ok, Is.True);
                Assert.That(controller.ApplyCommand(GameCommand.MoveCards(seat, new[] { straight[3] },
                    CardTarget.Meld(game.State.Board[0].Id, 3))).Ok, Is.True);
                Assert.That(controller.UndoGroupCount, Is.EqualTo(2));

                Assert.That(controller.RevertTurn().Ok, Is.True);

                Assert.That(controller.UndoGroupCount, Is.EqualTo(0));
                Assert.That(game.State.Board.Count, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void RedoReappliesTheWholeUndoneGroup()
        {
            var game = GestureTestGameFactory.CreateGameWithStraightInHand(4, out var straight);
            var seat = game.State.CurrentSeat;
            var controllerObject = new GameObject("Table Controller Test");
            try
            {
                var controller = controllerObject.AddComponent<TableController>();
                controller.Initialize(game);
                Assert.That(controller.ApplyCommand(GameCommand.MoveCards(seat, straight,
                    CardTarget.NewMeld(0))).Ok, Is.True);
                var boardCount = game.State.Board[0].Cards.Count;

                Assert.That(controller.UndoGesture().Ok, Is.True);
                Assert.That(controller.RedoGroupCount, Is.EqualTo(1));
                Assert.That(game.State.Board.Count, Is.EqualTo(0));
                Assert.That(controller.RedoGesture().Ok, Is.True);

                Assert.That(game.State.Board[0].Cards.Count, Is.EqualTo(boardCount));
                Assert.That(MeldValidator.IsBoardValid(game.State.Board), Is.True);
                Assert.That(controller.UndoGroupCount, Is.EqualTo(1));
                Assert.That(controller.RedoGroupCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
            }
        }

        private static byte[] CopyCardIds(IReadOnlyList<Card> cards)
        {
            var ids = new byte[cards.Count];
            for (var index = 0; index < cards.Count; index++)
            {
                ids[index] = cards[index].Id;
            }

            return ids;
        }

        private static string BoardSignature(IReadOnlyList<Meld> melds)
        {
            var signature = new System.Text.StringBuilder();
            for (var meldIndex = 0; meldIndex < melds.Count; meldIndex++)
            {
                signature.Append(melds[meldIndex].Id).Append(':');
                for (var cardIndex = 0; cardIndex < melds[meldIndex].Cards.Count; cardIndex++)
                {
                    signature.Append(melds[meldIndex].Cards[cardIndex].Id).Append(',');
                }

                signature.Append(';');
            }

            return signature.ToString();
        }

        private static byte FindCardNotIn(IReadOnlyList<Card> hand, IReadOnlyList<byte> excludedIds)
        {
            for (var cardIndex = 0; cardIndex < hand.Count; cardIndex++)
            {
                var excluded = false;
                for (var excludedIndex = 0; excludedIndex < excludedIds.Count; excludedIndex++)
                {
                    if (hand[cardIndex].Id == excludedIds[excludedIndex])
                    {
                        excluded = true;
                        break;
                    }
                }

                if (!excluded)
                {
                    return hand[cardIndex].Id;
                }
            }

            return byte.MaxValue;
        }
    }
}