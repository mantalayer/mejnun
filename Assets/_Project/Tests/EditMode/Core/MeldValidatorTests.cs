using System.Collections.Generic;
using NUnit.Framework;

namespace Mejnun.Core.Tests
{
    public sealed class MeldValidatorTests
    {
        [Test]
        public void Classify_SameKindSetOfThreeIsValid()
        {
            var classification = MeldValidator.Classify(new[]
            {
                CardAt(Suit.Clubs, Rank.Eight),
                CardAt(Suit.Hearts, Rank.Eight),
                CardAt(Suit.Spades, Rank.Eight)
            });

            AssertClassification(classification, MeldKind.SameKind, InvalidReason.None);
        }

        [Test]
        public void Classify_SameKindSetOfFourIsValid()
        {
            var classification = MeldValidator.Classify(new[]
            {
                CardAt(Suit.Clubs, Rank.Eight),
                CardAt(Suit.Diamonds, Rank.Eight),
                CardAt(Suit.Hearts, Rank.Eight),
                CardAt(Suit.Spades, Rank.Eight)
            });

            AssertClassification(classification, MeldKind.SameKind, InvalidReason.None);
        }

        [Test]
        public void Classify_DoubleIsRejected()
        {
            var classification = MeldValidator.Classify(new[]
            {
                CardAt(Suit.Clubs, Rank.Eight),
                CardAt(Suit.Clubs, Rank.Eight, 1),
                CardAt(Suit.Hearts, Rank.Eight)
            });

            AssertClassification(classification, MeldKind.Invalid, InvalidReason.Double);
        }

        [Test]
        public void Classify_LowAceStraightIsValid()
        {
            var classification = MeldValidator.Classify(new[]
            {
                CardAt(Suit.Hearts, Rank.Ace),
                CardAt(Suit.Hearts, Rank.Two),
                CardAt(Suit.Hearts, Rank.Three)
            });

            AssertClassification(classification, MeldKind.StraightLow, InvalidReason.None);
        }

        [Test]
        public void Classify_HighAceStraightIsValid()
        {
            var classification = MeldValidator.Classify(new[]
            {
                CardAt(Suit.Hearts, Rank.Queen),
                CardAt(Suit.Hearts, Rank.King),
                CardAt(Suit.Hearts, Rank.Ace)
            });

            AssertClassification(classification, MeldKind.StraightHigh, InvalidReason.None);
        }

        [Test]
        public void Classify_ThirteenCardStraightIsValid()
        {
            var cards = new List<Card>();
            for (var rank = Rank.Ace; rank <= Rank.King; rank++)
            {
                cards.Add(CardAt(Suit.Diamonds, rank));
            }

            var classification = MeldValidator.Classify(cards);

            AssertClassification(classification, MeldKind.StraightLow, InvalidReason.None);
        }

        [Test]
        public void Classify_StraightDoesNotWrapAroundKingAceTwo()
        {
            var classification = MeldValidator.Classify(new[]
            {
                CardAt(Suit.Spades, Rank.King),
                CardAt(Suit.Spades, Rank.Ace),
                CardAt(Suit.Spades, Rank.Two)
            });

            AssertClassification(classification, MeldKind.Invalid, InvalidReason.WrapAround);
        }

        [Test]
        public void Classify_StraightWithGapReturnsNotConsecutive()
        {
            var classification = MeldValidator.Classify(new[]
            {
                CardAt(Suit.Spades, Rank.Two),
                CardAt(Suit.Spades, Rank.Four),
                CardAt(Suit.Spades, Rank.Five)
            });

            AssertClassification(classification, MeldKind.Invalid, InvalidReason.NotConsecutive);
        }

        [Test]
        public void Classify_TooFewCardsReturnsTooShort()
        {
            var classification = MeldValidator.Classify(new[]
            {
                CardAt(Suit.Clubs, Rank.Ace),
                CardAt(Suit.Clubs, Rank.Two)
            });

            AssertClassification(classification, MeldKind.Invalid, InvalidReason.TooShort);
        }

        [Test]
        public void Classify_FiveCardsOfAKindReturnsTooLong()
        {
            var classification = MeldValidator.Classify(new[]
            {
                CardAt(Suit.Clubs, Rank.Five),
                CardAt(Suit.Diamonds, Rank.Five),
                CardAt(Suit.Hearts, Rank.Five),
                CardAt(Suit.Spades, Rank.Five),
                CardAt(Suit.Clubs, Rank.Five, 1)
            });

            AssertClassification(classification, MeldKind.Invalid, InvalidReason.TooLong);
        }

        [Test]
        public void Classify_MoreThanThirteenCardsReturnsTooLong()
        {
            var cards = new List<Card>();
            for (var id = 0; id < 14; id++)
            {
                cards.Add(Card.FromId(id));
            }

            var classification = MeldValidator.Classify(cards);

            AssertClassification(classification, MeldKind.Invalid, InvalidReason.TooLong);
        }

        [Test]
        public void Classify_MixedSuitsReturnsMixedSuits()
        {
            var classification = MeldValidator.Classify(new[]
            {
                CardAt(Suit.Clubs, Rank.Two),
                CardAt(Suit.Hearts, Rank.Three),
                CardAt(Suit.Clubs, Rank.Four)
            });

            AssertClassification(classification, MeldKind.Invalid, InvalidReason.MixedSuits);
        }

        [Test]
        public void Classify_MixedRanksInDifferentSuitsReturnsMixedRanks()
        {
            var classification = MeldValidator.Classify(new[]
            {
                CardAt(Suit.Clubs, Rank.Two),
                CardAt(Suit.Diamonds, Rank.Four),
                CardAt(Suit.Hearts, Rank.Seven)
            });

            AssertClassification(classification, MeldKind.Invalid, InvalidReason.MixedRanks);
        }

        [Test]
        public void Classify_NullCardsIsRejected()
        {
            Assert.That(() => MeldValidator.Classify(null), Throws.ArgumentNullException);
        }

        [Test]
        public void IsBoardValid_RejectsNullBoard()
        {
            Assert.That(() => MeldValidator.IsBoardValid(null), Throws.ArgumentNullException);
        }

        [Test]
        public void CanonicalizeStraight_RejectsNullCardList()
        {
            Assert.That(() => MeldValidator.CanonicalizeStraight(null), Throws.ArgumentNullException);
        }

        [Test]
        public void IsBoardValid_RequiresEveryMeldToBeValid()
        {
            var board = new[]
            {
                new Meld(1, new[]
                {
                    CardAt(Suit.Clubs, Rank.Two),
                    CardAt(Suit.Clubs, Rank.Three),
                    CardAt(Suit.Clubs, Rank.Four)
                }),
                new Meld(2, new[]
                {
                    CardAt(Suit.Spades, Rank.Two),
                    CardAt(Suit.Spades, Rank.Four),
                    CardAt(Suit.Spades, Rank.Five)
                })
            };

            Assert.That(MeldValidator.IsBoardValid(board), Is.False);
        }

        [Test]
        public void CanonicalizeStraight_SortsLowAceAtStart()
        {
            var meld = new Meld(1, new[]
            {
                CardAt(Suit.Hearts, Rank.Three),
                CardAt(Suit.Hearts, Rank.Ace),
                CardAt(Suit.Hearts, Rank.Two)
            });

            CollectionAssert.AreEqual(new[] { Rank.Ace, Rank.Two, Rank.Three },
                meld.Cards.ConvertAll(card => card.Rank));
        }

        [Test]
        public void CanonicalizeStraight_SortsHighAceAtEnd()
        {
            var meld = new Meld(1, new[]
            {
                CardAt(Suit.Hearts, Rank.Ace),
                CardAt(Suit.Hearts, Rank.Queen),
                CardAt(Suit.Hearts, Rank.King)
            });

            CollectionAssert.AreEqual(new[] { Rank.Queen, Rank.King, Rank.Ace },
                meld.Cards.ConvertAll(card => card.Rank));
        }

        private static Card CardAt(Suit suit, Rank rank, int deck = 0)
        {
            return Card.FromId(deck * Card.CardsPerDeck + (int)suit * 13 + (int)rank);
        }

        private static void AssertClassification(MeldClassification actual, MeldKind kind, InvalidReason reason)
        {
            Assert.That(actual.Kind, Is.EqualTo(kind));
            Assert.That(actual.Reason, Is.EqualTo(reason));
            Assert.That(actual.IsValid, Is.EqualTo(kind != MeldKind.Invalid));
        }
    }
}