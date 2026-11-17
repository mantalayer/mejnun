using System;
using System.Linq;
using NUnit.Framework;

namespace Mejnun.Core.Tests
{
    public sealed class CardTests
    {
        [Test]
        public void Card_FromIdRoundTripsAll104Cards()
        {
            for (var id = 0; id < Card.CardCount; id++)
            {
                var card = Card.FromId(id);

                Assert.That(card.Id, Is.EqualTo(id));
                Assert.That((int)card.Deck, Is.EqualTo(id / 52));
                Assert.That((int)card.Suit, Is.EqualTo((id % 52) / 13));
                Assert.That((int)card.Rank, Is.EqualTo(id % 13));
            }
        }

        [TestCase(-1)]
        [TestCase(104)]
        public void Card_FromIdRejectsOutOfRangeIds(int id)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Card.FromId(id));
        }

        [Test]
        public void Card_IsDoubleOfRequiresSameSuitAndRankAcrossDecks()
        {
            var first = Card.FromId(0);
            var secondDeckCopy = Card.FromId(Card.CardsPerDeck);
            var differentSuit = Card.FromId(13 + Card.CardsPerDeck);

            Assert.That(first.IsDoubleOf(secondDeckCopy), Is.True);
            Assert.That(secondDeckCopy.IsDoubleOf(first), Is.True);
            Assert.That(first.IsDoubleOf(first), Is.False);
            Assert.That(first.IsDoubleOf(differentSuit), Is.False);
        }

        [Test]
        public void Card_ValueEqualitySupportsBoxingHashingAndOperators()
        {
            var card = Card.FromId(17);
            object boxedCard = card;
            var otherCard = Card.FromId(18);

            Assert.That(card.Equals(boxedCard), Is.True);
            Assert.That(card.Equals("not a card"), Is.False);
            Assert.That(card.GetHashCode(), Is.EqualTo(card.Id));
            Assert.That(card == Card.FromId(17), Is.True);
            Assert.That(card != otherCard, Is.True);
        }

        [Test]
        public void Shoe_Contains104UniqueCardIds()
        {
            var shoe = new Shoe();

            Assert.That(shoe.Count, Is.EqualTo(104));
            Assert.That(shoe.Cards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(104));
        }

        [Test]
        public void Shoe_ShuffleIsDeterministicForTheSameSeed()
        {
            var first = new Shoe();
            var second = new Shoe();

            first.Shuffle(new Pcg32(123456UL));
            second.Shuffle(new Pcg32(123456UL));

            CollectionAssert.AreEqual(first.Cards, second.Cards);
        }

        [Test]
        public void Shoe_ShuffleChangesOrderForDifferentSeeds()
        {
            var first = new Shoe();
            var second = new Shoe();

            first.Shuffle(new Pcg32(1UL));
            second.Shuffle(new Pcg32(2UL));

            CollectionAssert.AreNotEqual(first.Cards, second.Cards);
        }

        [Test]
        public void Shoe_ShufflePreservesEveryCard()
        {
            var shoe = new Shoe();
            shoe.Shuffle(new Pcg32(99UL));

            Assert.That(shoe.Cards.Select(card => card.Id).OrderBy(id => id),
                Is.EqualTo(Enumerable.Range(0, Card.CardCount)));
        }

        [Test]
        public void Shoe_ShuffleRejectsNullGenerator()
        {
            Assert.Throws<ArgumentNullException>(() => new Shoe().Shuffle(null));
        }
    }
}