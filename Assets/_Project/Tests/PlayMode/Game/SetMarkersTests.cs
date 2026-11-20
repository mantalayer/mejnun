using System.Collections.Generic;
using Mejnun.Core;
using NUnit.Framework;

namespace Mejnun.Game.Tests
{
    public sealed class SetMarkersTests
    {
        [Test]
        public void Update_FollowsMovedMeldAndBreaksOntoItsLargestFragment()
        {
            var cards = new[] { Card.FromId(0), Card.FromId(1), Card.FromId(2) };
            var markers = new SetMarkers();
            var brokenCount = 0;
            markers.Broken += _ => brokenCount++;
            markers.Cycle(4, new[] { new Meld(4, cards) });

            markers.Update(new[] { new Meld(9, cards) });

            Assert.That(markers.Marks[0].MeldId, Is.EqualTo(9));
            Assert.That(brokenCount, Is.EqualTo(0));

            markers.Update(new List<Meld>
            {
                new Meld(10, new[] { cards[0], cards[1] }),
                new Meld(11, new[] { cards[2] })
            });

            Assert.That(markers.Marks.Count, Is.EqualTo(1));
            Assert.That(markers.Marks[0].MeldId, Is.EqualTo(10));
            Assert.That(markers.Marks[0].CardIds, Is.EqualTo(new byte[] { cards[0].Id, cards[1].Id }));
            Assert.That(brokenCount, Is.EqualTo(1));
        }

        [Test]
        public void CycleCard_AdvancesInOrderAndRemovesTheMarkAndClearRemovesAllMarks()
        {
            var cards = new[] { Card.FromId(0), Card.FromId(1), Card.FromId(2) };
            var markers = new SetMarkers();
            markers.CycleCard(cards[0].Id);
            Assert.That(markers.CardMarks.Count, Is.EqualTo(1));
            Assert.That(markers.CardMarks[0].Symbol, Is.EqualTo("●"));

            markers.CycleCard(cards[0].Id);
            Assert.That(markers.CardMarks[0].Symbol, Is.EqualTo("▲"));
            markers.CycleCard(cards[0].Id);
            Assert.That(markers.CardMarks[0].Symbol, Is.EqualTo("■"));
            markers.CycleCard(cards[0].Id);
            Assert.That(markers.CardMarks[0].Symbol, Is.EqualTo("★"));
            markers.CycleCard(cards[0].Id);
            Assert.That(markers.CardMarks, Is.Empty);

            markers.CycleCard(cards[1].Id);
            markers.Cycle(4, new[] { new Meld(4, cards) });
            markers.Clear();
            Assert.That(markers.CardMarks, Is.Empty);
            Assert.That(markers.Marks, Is.Empty);
        }

        [Test]
        public void RemoveCard_RemovesTheMarkAndReportsWhetherItExisted()
        {
            var cardId = Card.FromId(0).Id;
            var markers = new SetMarkers();
            markers.CycleCard(cardId);

            Assert.That(markers.RemoveCard(Card.FromId(1).Id), Is.False);
            Assert.That(markers.RemoveCard(cardId), Is.True);
            Assert.That(markers.CardMarks, Is.Empty);
            Assert.That(markers.RemoveCard(cardId), Is.False);
        }

        [Test]
        public void RestoreCardRestoresPreviousSymbolOrRemovesTheMark()
        {
            var cardId = Card.FromId(0).Id;
            var markers = new SetMarkers();
            markers.CycleCard(cardId);
            var previousSymbol = markers.GetCardSymbol(cardId);
            markers.CycleCard(cardId);

            markers.RestoreCard(cardId, previousSymbol);

            Assert.That(markers.GetCardSymbol(cardId), Is.EqualTo(previousSymbol));

            markers.RestoreCard(cardId, null);

            Assert.That(markers.GetCardSymbol(cardId), Is.Null);
            Assert.That(markers.CardMarks, Is.Empty);
        }

        [Test]
        public void RemoveSet_RemovesTheMarkAndReportsWhetherItExisted()
        {
            var meldId = 4;
            var markers = new SetMarkers();
            markers.Cycle(meldId, new[] { new Meld(meldId, new[] { Card.FromId(0), Card.FromId(1) }) });

            Assert.That(markers.Remove(meldId + 1), Is.False);
            Assert.That(markers.Remove(meldId), Is.True);
            Assert.That(markers.Marks, Is.Empty);
            Assert.That(markers.Remove(meldId), Is.False);
        }
    }
}