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
        public void Compute_UsesOneRowForAFewMeldsOnAWideScreen()
        {
            var layout = BoardLayout.Compute(CreateMelds(3, 3), 2.28f, new Vector2(0.64f, 0.97f));

            Assert.That(layout.RowCount, Is.EqualTo(1));
        }

        [Test]
        public void Compute_OverlapModeOverlapsCardsWithinEachMeld()
        {
            var layout = BoardLayout.Compute(CreateMelds(2, 3), 1.6f, new Vector2(0.64f, 0.97f), true);

            Assert.That(layout.Melds[0].CardRects[0].Overlaps(layout.Melds[0].CardRects[1]), Is.True);
            Assert.That(layout.Melds[0].Bounds.Overlaps(layout.Melds[1].Bounds), Is.False);
        }

        [Test]
        public void CardSpread_IncreasesDistanceBetweenCardsWithinMeld()
        {
            var melds = CreateMelds(1, 3);
            var cardSize = new Vector2(0.64f, 0.97f);
            var compact = BoardLayout.Compute(melds, 1.6f, cardSize, false, 1f);
            var expanded = BoardLayout.Compute(melds, 1.6f, cardSize, false, 1.5f);
            var compactDistance = compact.Melds[0].CardRects[1].center.x - compact.Melds[0].CardRects[0].center.x;
            var expandedDistance = expanded.Melds[0].CardRects[1].center.x - expanded.Melds[0].CardRects[0].center.x;

            Assert.That(expandedDistance, Is.GreaterThan(compactDistance));

            var preferred = new Dictionary<int, Vector2> { { 1, Vector2.zero } };
            var compactOrganic = BoardLayout.ComputeOrganic(melds, cardSize, false, preferred, null, null, null,
                out _, 1f);
            var expandedOrganic = BoardLayout.ComputeOrganic(melds, cardSize, false, preferred, null, null, null,
                out _, 1.5f);
            compactDistance = compactOrganic.Melds[0].CardRects[1].center.x -
                              compactOrganic.Melds[0].CardRects[0].center.x;
            expandedDistance = expandedOrganic.Melds[0].CardRects[1].center.x -
                               expandedOrganic.Melds[0].CardRects[0].center.x;

            Assert.That(expandedDistance, Is.GreaterThan(compactDistance));
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

        [Test]
        public void TryHitMeldFrame_HitsOutlineButNotCardsOrNewGroupSlot()
        {
            var layout = BoardLayout.Compute(CreateMelds(1, 3), 1.7f, new Vector2(0.64f, 0.97f));
            var meld = layout.Melds[0];
            var framePoint = new Vector2(meld.Bounds.center.x, meld.Bounds.yMax + layout.CardSize.y * 0.1f);

            Assert.That(layout.TryHitMeldFrame(framePoint, out var frameMeldId), Is.True);
            Assert.That(frameMeldId, Is.EqualTo(meld.MeldId));
            Assert.That(layout.TryHitMeldFrame(meld.CardRects[0].center, out _), Is.False);
            Assert.That(layout.TryHitMeldFrame(layout.NewGroupSlot.center, out _), Is.False);
            Assert.That(layout.TryHitMeldFrame(new Vector2(100f, 100f), out _), Is.False);
        }

        [Test]
        public void TryHitMeldFrame_HitsNearSetPerimeter()
        {
            var layout = BoardLayout.Compute(CreateMelds(1, 3), 1.7f, new Vector2(0.64f, 0.97f));
            var meld = layout.Melds[0];
            var perimeterPoint = new Vector2(meld.Bounds.xMin - layout.CardSize.x * 0.4f, meld.Bounds.center.y);

            Assert.That(layout.TryHitMeldFrame(perimeterPoint, out var meldId), Is.True);
            Assert.That(meldId, Is.EqualTo(meld.MeldId));
        }

        [Test]
        public void TryHitMeldFrame_ChoosesNearestMeldWhenGripAreasOverlap()
        {
            var cardSize = new Vector2(0.64f, 0.97f);
            var layout = BoardLayout.Compute(CreateMelds(2, 1), 10f, cardSize);
            var firstMeld = layout.Melds[0];
            var secondMeld = layout.Melds[1];
            var point = new Vector2(
                firstMeld.Bounds.xMax + (secondMeld.Bounds.xMin - firstMeld.Bounds.xMax) * 0.75f,
                firstMeld.Bounds.center.y);
            var firstGripBounds = firstMeld.Bounds;
            firstGripBounds.xMin -= cardSize.x * BoardLayoutResult.MeldFrameHorizontalPadding;
            firstGripBounds.xMax += cardSize.x * BoardLayoutResult.MeldFrameHorizontalPadding;
            firstGripBounds.yMin -= cardSize.y * BoardLayoutResult.MeldFrameVerticalPadding;
            firstGripBounds.yMax += cardSize.y * BoardLayoutResult.MeldFrameVerticalPadding;
            var secondGripBounds = secondMeld.Bounds;
            secondGripBounds.xMin -= cardSize.x * BoardLayoutResult.MeldFrameHorizontalPadding;
            secondGripBounds.xMax += cardSize.x * BoardLayoutResult.MeldFrameHorizontalPadding;
            secondGripBounds.yMin -= cardSize.y * BoardLayoutResult.MeldFrameVerticalPadding;
            secondGripBounds.yMax += cardSize.y * BoardLayoutResult.MeldFrameVerticalPadding;

            Assert.That(firstGripBounds.Contains(point), Is.True);
            Assert.That(secondGripBounds.Contains(point), Is.True);
            Assert.That(layout.TryHitMeldFrame(point, out var meldId), Is.True);
            Assert.That(meldId, Is.EqualTo(secondMeld.MeldId));
        }

        [Test]
        public void ComputeOrganic_KeepsAvailablePreviousOrigins()
        {
            var melds = CreateMelds(2, 3);
            var preferred = new Dictionary<int, Vector2>
            {
                { 1, new Vector2(-2f, 1f) },
                { 2, new Vector2(3f, -1f) }
            };

            BoardLayout.ComputeOrganic(melds, new Vector2(0.64f, 0.97f), false, preferred, null,
                out var origins);

            Assert.That(origins[1], Is.EqualTo(preferred[1]));
            Assert.That(origins[2], Is.EqualTo(preferred[2]));
        }

        [Test]
        public void ComputeOrganic_MovesOnlyCollidingMeldAndKeepsBoundsSeparate()
        {
            var melds = CreateMelds(3, 3);
            var preferred = new Dictionary<int, Vector2>
            {
                { 1, Vector2.zero },
                { 2, Vector2.zero },
                { 3, new Vector2(5f, 0f) }
            };

            var layout = BoardLayout.ComputeOrganic(melds, new Vector2(0.64f, 0.97f), false, preferred, null,
                out var origins);

            Assert.That(origins[1], Is.EqualTo(preferred[1]));
            Assert.That(origins[2], Is.Not.EqualTo(preferred[2]));
            Assert.That(origins[3], Is.EqualTo(preferred[3]));
            for (var index = 0; index < layout.Melds.Count; index++)
            {
                for (var otherIndex = index + 1; otherIndex < layout.Melds.Count; otherIndex++)
                {
                    Assert.That(layout.Melds[index].Bounds.Overlaps(layout.Melds[otherIndex].Bounds), Is.False);
                }
            }
        }

        [Test]
        public void ComputeOrganic_PlacesNewMeldAtAvailableHint()
        {
            var melds = CreateMelds(1, 3);
            var hint = new Vector2(4f, -2f);
            var hints = new Dictionary<int, Vector2> { { 1, hint } };

            var layout = BoardLayout.ComputeOrganic(melds, new Vector2(0.64f, 0.97f), false, null, hints,
                out var origins);

            Assert.That(layout.Melds[0].Bounds.center, Is.EqualTo(hint));
            Assert.That(origins[1], Is.EqualTo(layout.Melds[0].Bounds.position));
        }

        [Test]
        public void ComputeOrganic_PlacesConstrainedMeldInsidePlacementAreaWhenRoomIsAvailable()
        {
            var melds = CreateMelds(1, 3);
            var area = new Rect(4f, 3f, 3f, 2f);
            var constrainedMeldIds = new HashSet<int> { 1 };

            var layout = BoardLayout.ComputeOrganic(melds, new Vector2(0.64f, 0.97f), false, null, null, area,
                constrainedMeldIds, out _);

            Assert.That(layout.Melds[0].Bounds.xMin, Is.GreaterThanOrEqualTo(area.xMin));
            Assert.That(layout.Melds[0].Bounds.xMax, Is.LessThanOrEqualTo(area.xMax));
            Assert.That(layout.Melds[0].Bounds.yMin, Is.GreaterThanOrEqualTo(area.yMin));
            Assert.That(layout.Melds[0].Bounds.yMax, Is.LessThanOrEqualTo(area.yMax));
        }

        [Test]
        public void ComputeOrganic_FindsNarrowFreeSpaceInsidePlacementArea()
        {
            var melds = CreateMelds(2, 3);
            melds[0] = new Meld(1, new List<Card> { Card.FromId(0) });
            var area = new Rect(0f, 0f, 3f, 0.971f);
            var preferred = new Dictionary<int, Vector2> { { 1, Vector2.zero } };

            var layout = BoardLayout.ComputeOrganic(melds, new Vector2(0.64f, 0.97f), false, preferred, null, area,
                new HashSet<int> { 2 }, out _);

            Assert.That(layout.Melds[1].Bounds.xMin, Is.GreaterThanOrEqualTo(area.xMin));
            Assert.That(layout.Melds[1].Bounds.xMax, Is.LessThanOrEqualTo(area.xMax));
            Assert.That(layout.Melds[1].Bounds.yMin, Is.GreaterThanOrEqualTo(area.yMin));
            Assert.That(layout.Melds[1].Bounds.yMax, Is.LessThanOrEqualTo(area.yMax));
        }

        [Test]
        public void ComputeOrganic_FallsBackOutsidePlacementAreaWhenItIsFullOrTooSmall()
        {
            var fullAreaMelds = CreateMelds(2, 3);
            var fullArea = new Rect(0f, 0f, 3f, 2f);
            var preferred = new Dictionary<int, Vector2> { { 1, Vector2.zero } };
            var constrainedMeldIds = new HashSet<int> { 2 };

            var fullAreaLayout = BoardLayout.ComputeOrganic(fullAreaMelds, new Vector2(0.64f, 0.97f), false,
                preferred, null, fullArea, constrainedMeldIds, out _);

            Assert.That(fullAreaLayout.Melds[1].Bounds.xMin >= fullArea.xMin &&
                        fullAreaLayout.Melds[1].Bounds.xMax <= fullArea.xMax &&
                        fullAreaLayout.Melds[1].Bounds.yMin >= fullArea.yMin &&
                        fullAreaLayout.Melds[1].Bounds.yMax <= fullArea.yMax, Is.False);

            var smallArea = new Rect(10f, 10f, 0.5f, 0.5f);
            var smallAreaLayout = BoardLayout.ComputeOrganic(CreateMelds(1, 3), new Vector2(0.64f, 0.97f), false,
                null, null, smallArea, new HashSet<int> { 1 }, out _);

            Assert.That(smallAreaLayout.Melds[0].Bounds.xMin >= smallArea.xMin &&
                        smallAreaLayout.Melds[0].Bounds.xMax <= smallArea.xMax &&
                        smallAreaLayout.Melds[0].Bounds.yMin >= smallArea.yMin &&
                        smallAreaLayout.Melds[0].Bounds.yMax <= smallArea.yMax, Is.False);
        }

        [Test]
        public void ComputeOrganic_DoesNotConstrainPreferredMeldsThatDidNotGainCards()
        {
            var preferredOrigin = new Vector2(5f, 6f);
            var preferred = new Dictionary<int, Vector2> { { 1, preferredOrigin } };
            var area = new Rect(-2f, -2f, 4f, 4f);

            BoardLayout.ComputeOrganic(CreateMelds(1, 3), new Vector2(0.64f, 0.97f), false, preferred, null,
                area, new HashSet<int>(), out var origins);

            Assert.That(origins[1], Is.EqualTo(preferredOrigin));
        }

        [Test]
        public void CardJitter_IsZeroAtZeroMessinessDeterministicAndBounded()
        {
            var cardSize = new Vector2(0.64f, 0.97f);
            BoardLayout.CardJitter(12, 4, 0f, cardSize, out var zeroOffset, out var zeroAngle);
            BoardLayout.CardJitter(12, 4, 0.75f, cardSize, out var firstOffset, out var firstAngle);
            BoardLayout.CardJitter(12, 4, 0.75f, cardSize, out var secondOffset, out var secondAngle);

            Assert.That(zeroOffset, Is.EqualTo(Vector2.zero));
            Assert.That(zeroAngle, Is.EqualTo(0f));
            Assert.That(firstOffset, Is.EqualTo(secondOffset));
            Assert.That(firstAngle, Is.EqualTo(secondAngle));
            Assert.That(Mathf.Abs(firstOffset.x), Is.LessThanOrEqualTo(cardSize.x * 0.1f));
            Assert.That(Mathf.Abs(firstOffset.y), Is.LessThanOrEqualTo(cardSize.y * 0.06f));
            Assert.That(Mathf.Abs(firstAngle), Is.LessThanOrEqualTo(7f));
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