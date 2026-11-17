using System.Collections.Generic;
using Mejnun.Core;
using NUnit.Framework;

namespace Mejnun.Game.Tests
{
    public sealed class GestureInterpreterTests
    {
        [Test]
        public void DragFromMiddleOfStraightSplitsThenMovesOnlyThePickedCard()
        {
            var game = GestureTestGameFactory.CreateGameWithStraightInHand(4, out var straight);
            var seat = game.State.CurrentSeat;
            Assert.That(game.Apply(GameCommand.MoveCards(seat, straight, CardTarget.NewMeld(0))).Ok, Is.True);
            var meld = game.State.Board[0];
            var pickedCard = meld.Cards[1];
            var interpreter = new GestureInterpreter();

            var result = interpreter.Interpret(new GestureInput(GestureType.Drag,
                GestureHit.BoardCard(pickedCard.Id, meld.Id, 1, 0), GestureHit.EmptyBoard()), game.State, game.Turn);

            Assert.That(result.Batch.Count, Is.EqualTo(2));
            Assert.That(result.Batch.Commands[0].Type, Is.EqualTo(CommandType.SplitMeld));
            Assert.That(result.Batch.Commands[0].MeldId, Is.EqualTo(meld.Id));
            Assert.That(result.Batch.Commands[0].Index, Is.EqualTo(1));
            Assert.That(result.Batch.Commands[1].Type, Is.EqualTo(CommandType.MoveCards));
            Assert.That(result.Batch.Commands[1].CardIds, Is.EqualTo(new[] { pickedCard.Id }));
        }

        [Test]
        public void LongPressOnStraightPicksUpTheTailAsOneMove()
        {
            var game = GestureTestGameFactory.CreateGameWithStraightInHand(5, out var straight);
            var seat = game.State.CurrentSeat;
            Assert.That(game.Apply(GameCommand.MoveCards(seat, straight, CardTarget.NewMeld(0))).Ok, Is.True);
            var meld = game.State.Board[0];
            var interpreter = new GestureInterpreter();
            var sourceIndex = 2;

            var result = interpreter.Interpret(new GestureInput(GestureType.LongPressDrag,
                GestureHit.BoardCard(meld.Cards[sourceIndex].Id, meld.Id, sourceIndex, sourceIndex),
                GestureHit.EmptyBoard()), game.State, game.Turn);

            Assert.That(result.Batch.Count, Is.EqualTo(1));
            Assert.That(result.Batch.Commands[0].Type, Is.EqualTo(CommandType.MoveCards));
            Assert.That(result.Batch.Commands[0].CardIds,
                Is.EqualTo(new[] { straight[2], straight[3], straight[4] }));
        }

        [Test]
        public void DoubleTapQuickPlacesValidExtensionAndDoesNothingWhenNoExtensionExists()
        {
            var game = GestureTestGameFactory.CreateGameWithStraightInHand(4, out var straight);
            var seat = game.State.CurrentSeat;
            var firstThree = new[] { straight[0], straight[1], straight[2] };
            Assert.That(game.Apply(GameCommand.MoveCards(seat, firstThree, CardTarget.NewMeld(0))).Ok, Is.True);
            var hand = game.State.Hands[seat];
            var extendingCard = Card.FromId(straight[3]);
            var handIndex = hand.IndexOf(extendingCard);
            var interpreter = new GestureInterpreter();

            var quickPlace = interpreter.Interpret(new GestureInput(GestureType.DoubleTap,
                GestureHit.HandCard(extendingCard.Id, handIndex), GestureHit.None), game.State, game.Turn);

            Assert.That(quickPlace.Batch.Count, Is.EqualTo(1));
            Assert.That(quickPlace.Batch.Commands[0].Type, Is.EqualTo(CommandType.MoveCards));
            Assert.That(quickPlace.Batch.Commands[0].Target.MeldId, Is.EqualTo(game.State.Board[0].Id));

            var noExtension = FindNonExtendingCard(hand, game.State.Board[0]);
            Assert.That(noExtension, Is.Not.EqualTo(byte.MaxValue));
            var noPlace = interpreter.Interpret(new GestureInput(GestureType.DoubleTap,
                GestureHit.HandCard(noExtension, hand.IndexOf(Card.FromId(noExtension))), GestureHit.None),
                game.State, game.Turn);
            Assert.That(noPlace.Batch.Count, Is.EqualTo(0));
        }

        [Test]
        public void QuickPlaceIgnoresMeldsThatWereAlreadyOnTheBoardAtTurnStart()
        {
            var game = GestureTestGameFactory.CreateGameWithAccessibleStraightExtension(out _,
                out var extensionCardId);
            var seat = game.State.CurrentSeat;
            var hand = game.State.Hands[seat];
            var interpreter = new GestureInterpreter();

            var result = interpreter.Interpret(new GestureInput(GestureType.DoubleTap,
                GestureHit.HandCard(extensionCardId, hand.IndexOf(Card.FromId(extensionCardId))), GestureHit.None),
                game.State, game.Turn);

            Assert.That(game.State.PlayerPhases[seat], Is.EqualTo(PlayerPhase.NotDown));
            Assert.That(result.Batch.Count, Is.EqualTo(0));
        }

        [Test]
        public void DoubleTapOnBoardOriginCardDoesNotReturnItToHand()
        {
            var game = GestureTestGameFactory.CreateGameWithStraightInHand(3, out var straight);
            var firstSeat = game.State.CurrentSeat;
            Assert.That(game.Apply(GameCommand.MoveCards(firstSeat, straight, CardTarget.NewMeld(0))).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.EndTurn(firstSeat)).Ok, Is.True);
            var seat = game.State.CurrentSeat;
            var meld = game.State.Board[0];
            var boardCard = meld.Cards[0];
            var interpreter = new GestureInterpreter();

            var result = interpreter.Interpret(new GestureInput(GestureType.DoubleTap,
                GestureHit.BoardCard(boardCard.Id, meld.Id, 0, 0), GestureHit.None), game.State, game.Turn);

            Assert.That(IsInStartHand(game.Turn, boardCard.Id), Is.False);
            Assert.That(result.Batch.Count, Is.EqualTo(0));
        }

        private static byte FindNonExtendingCard(IReadOnlyList<Card> hand, Meld meld)
        {
            for (var handIndex = 0; handIndex < hand.Count; handIndex++)
            {
                var candidate = hand[handIndex];
                var canExtend = false;
                for (var insertIndex = 0; insertIndex <= meld.Cards.Count; insertIndex++)
                {
                    var cards = new List<Card>(meld.Cards.Count + 1);
                    for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                    {
                        if (cardIndex == insertIndex)
                        {
                            cards.Add(candidate);
                        }

                        cards.Add(meld.Cards[cardIndex]);
                    }

                    if (insertIndex == meld.Cards.Count)
                    {
                        cards.Add(candidate);
                    }

                    if (MeldValidator.Classify(cards).IsValid)
                    {
                        canExtend = true;
                        break;
                    }
                }

                if (!canExtend)
                {
                    return candidate.Id;
                }
            }

            return byte.MaxValue;
        }

        private static bool IsInStartHand(TurnSession turn, byte cardId)
        {
            for (var index = 0; index < turn.StartSnapshot.Hand.Count; index++)
            {
                if (turn.StartSnapshot.Hand[index].Id == cardId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}