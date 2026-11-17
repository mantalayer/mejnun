using System.Collections.Generic;
using Mejnun.Core;
using NUnit.Framework;
using UnityEngine;

namespace Mejnun.Game.Tests
{
    public sealed class BoardLayoutTests
    {
        [Test]
        public void Compute_SeparatesSlotsKeepsThemInsideRowsAndPutsNewGroupLast()
        {
            var melds = CreateMelds(8, 4);
            var layout = BoardLayout.Compute(melds, 1.6f, new Vector2(0.64f, 0.97f));

            for (var slotIndex = 0; slotIndex < layout.Slots.Count; slotIndex++)
            {
                var slot = layout.Slots[slotIndex];
                Assert.That(slot.Rect.xMin, Is.GreaterThanOrEqualTo(-layout.RowWidth * 0.5f - 0.001f));
                Assert.That(slot.Rect.xMax, Is.LessThanOrEqualTo(layout.RowWidth * 0.5f + 0.001f));
                for (var otherIndex = slotIndex + 1; otherIndex < layout.Slots.Count; otherIndex++)
                {
                    Assert.That(slot.Rect.Overlaps(layout.Slots[otherIndex].Rect), Is.False,
                        "Slots " + slotIndex + " and " + otherIndex + " overlap.");
                }
            }

            Assert.That(layout.Slots[layout.Slots.Count - 1].IsNewGroup, Is.True);
            Assert.That(layout.NewGroupSlot, Is.EqualTo(layout.Slots[layout.Slots.Count - 1].Rect));
        }

        [Test]
        public void Compute_ChangesRowCountForDifferentViewportAspects()
        {
            var melds = CreateMelds(8, 4);
            var narrow = BoardLayout.Compute(melds, 0.7f, new Vector2(0.64f, 0.97f));
            var wide = BoardLayout.Compute(melds, 2.2f, new Vector2(0.64f, 0.97f));

            Assert.That(narrow.RowCount, Is.GreaterThan(wide.RowCount));
        }

        [Test]
        public void TryHit_ReturnsInsertionIndexForGapAndNewGroupTarget()
        {
            var layout = BoardLayout.Compute(CreateMelds(1, 3), 1.7f, new Vector2(0.64f, 0.97f));
            var first = layout.Melds[0].CardRects[0];
            var second = layout.Melds[0].CardRects[1];
            var gapPoint = new Vector2((first.center.x + second.center.x) * 0.5f, first.center.y);

            Assert.That(layout.TryHit(gapPoint, out var gapHit), Is.True);
            Assert.That(gapHit.IsNewGroup, Is.False);
            Assert.That(gapHit.MeldId, Is.EqualTo(layout.Melds[0].MeldId));
            Assert.That(gapHit.InsertIndex, Is.EqualTo(1));
            Assert.That(layout.TryHit(layout.NewGroupSlot.center, out var newGroupHit), Is.True);
            Assert.That(newGroupHit.IsNewGroup, Is.True);
        }

        private static List<Meld> CreateMelds(int meldCount, int cardsPerMeld)
        {
            var melds = new List<Meld>(meldCount);
            var cardId = 0;
            for (var meldIndex = 0; meldIndex < meldCount; meldIndex++)
            {
                var cards = new List<Card>(cardsPerMeld);
                for (var cardIndex = 0; cardIndex < cardsPerMeld; cardIndex++)
                {
                    cards.Add(Card.FromId(cardId++));
                }

                melds.Add(new Meld(meldIndex + 1, cards));
            }

            return melds;
        }
    }
}