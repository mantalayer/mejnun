using System;
using System.Collections.Generic;
using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    public static class BoardLayout
    {
        public static BoardLayoutResult Compute(IReadOnlyList<Meld> melds, float viewportAspect, Vector2 cardSize)
        {
            return Compute(melds, viewportAspect, cardSize, false);
        }

        public static BoardLayoutResult Compute(IReadOnlyList<Meld> melds, float viewportAspect, Vector2 cardSize,
            bool overlapSets)
        {
            if (melds == null)
            {
                throw new ArgumentNullException(nameof(melds));
            }

            if (viewportAspect <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(viewportAspect));
            }

            if (cardSize.x <= 0f || cardSize.y <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cardSize));
            }

            var cardGap = cardSize.x * (overlapSets ? -0.55f : 0.14f);
            var meldGap = cardSize.x * (overlapSets ? 0.6f : 0.38f);
            var rowGap = cardSize.y * 0.28f;
            var totalWidth = 0f;
            var longestMeldWidth = 0f;
            var totalCardCount = 0;
            for (var meldIndex = 0; meldIndex < melds.Count; meldIndex++)
            {
                var cardCount = melds[meldIndex].Cards.Count;
                if (cardCount == 0)
                {
                    continue;
                }

                var meldWidth = GetMeldWidth(cardCount, cardSize.x, cardGap);
                longestMeldWidth = Mathf.Max(longestMeldWidth, meldWidth);
                totalWidth += meldWidth;
                totalCardCount += cardCount;
            }

            var nonEmptyMeldCount = CountNonEmptyMelds(melds);
            if (nonEmptyMeldCount > 1)
            {
                totalWidth += meldGap * (nonEmptyMeldCount - 1);
            }

            var minimumRowWidth = Mathf.Max(cardSize.x, longestMeldWidth);
            var rowWidth = minimumRowWidth;
            var slotWidth = nonEmptyMeldCount == 0 ? 0f : meldGap;
            var contentWidth = totalWidth + slotWidth + cardSize.x;
            var bestFitScale = -1f;
            var bestRowCount = int.MaxValue;
            var candidateCount = Mathf.Max(1, nonEmptyMeldCount + 1);
            for (var candidateRows = 1; candidateRows <= candidateCount; candidateRows++)
            {
                var candidateWidth = Mathf.Max(minimumRowWidth, contentWidth / candidateRows) + 0.0001f;
                GetLayoutMetrics(melds, candidateWidth, cardSize, cardGap, meldGap, out var candidateRowCount,
                    out var candidateActualWidth);
                var candidateHeight = candidateRowCount * cardSize.y + (candidateRowCount - 1) * rowGap;
                var fitScale = Mathf.Min(viewportAspect / Mathf.Max(candidateActualWidth, 0.001f),
                    1f / Mathf.Max(candidateHeight, 0.001f));
                if (fitScale > bestFitScale + 0.00001f ||
                    (Mathf.Abs(fitScale - bestFitScale) <= 0.00001f && candidateRowCount < bestRowCount))
                {
                    bestFitScale = fitScale;
                    bestRowCount = candidateRowCount;
                    rowWidth = candidateWidth;
                }
            }

            var layouts = new List<BoardMeldLayout>(nonEmptyMeldCount);
            var rowWidths = new List<float> { 0f };
            var row = 0;
            var cursorX = 0f;
            for (var meldIndex = 0; meldIndex < melds.Count; meldIndex++)
            {
                var meld = melds[meldIndex];
                var cardCount = meld.Cards.Count;
                if (cardCount == 0)
                {
                    continue;
                }

                var meldWidth = GetMeldWidth(cardCount, cardSize.x, cardGap);
                if (cursorX > 0f && cursorX + meldGap + meldWidth > rowWidth)
                {
                    row++;
                    rowWidths.Add(0f);
                    cursorX = 0f;
                }

                if (cursorX > 0f)
                {
                    cursorX += meldGap;
                }

                var cardRects = new Rect[cardCount];
                for (var cardIndex = 0; cardIndex < cardCount; cardIndex++)
                {
                    var x = cursorX + cardSize.x * 0.5f + cardIndex * (cardSize.x + cardGap);
                    cardRects[cardIndex] = new Rect(x - cardSize.x * 0.5f,
                        -row * (cardSize.y + rowGap) - cardSize.y * 0.5f, cardSize.x, cardSize.y);
                }

                layouts.Add(new BoardMeldLayout(meld.Id, row, cardRects));
                cursorX += meldWidth;
                rowWidths[row] = cursorX;
            }

            if (cursorX > 0f && cursorX + meldGap + cardSize.x > rowWidth)
            {
                row++;
                rowWidths.Add(0f);
                cursorX = 0f;
            }
            else if (cursorX > 0f)
            {
                cursorX += meldGap;
            }

            var newGroupRect = new Rect(cursorX, -row * (cardSize.y + rowGap) - cardSize.y * 0.5f,
                cardSize.x, cardSize.y);
            rowWidths[row] = cursorX + cardSize.x;
            var rowCount = row + 1;
            var verticalOffset = (rowCount - 1) * (cardSize.y + rowGap) * 0.5f;
            var slots = new List<BoardLayoutSlot>(totalCardCount + 1);
            var bounds = default(Rect);
            var hasBounds = false;
            for (var meldIndex = 0; meldIndex < layouts.Count; meldIndex++)
            {
                var meldLayout = layouts[meldIndex];
                var horizontalOffset = -rowWidths[meldLayout.RowIndex] * 0.5f;
                var meldBounds = default(Rect);
                for (var cardIndex = 0; cardIndex < meldLayout.CardRectCount; cardIndex++)
                {
                    var rect = meldLayout.GetCardRect(cardIndex);
                    rect.x += horizontalOffset;
                    rect.y += verticalOffset;
                    meldLayout.SetCardRect(cardIndex, rect);
                    meldBounds = AddToMeldBounds(meldBounds, cardIndex, rect);
                    slots.Add(new BoardLayoutSlot(rect, meldLayout.MeldId, cardIndex, false));
                    bounds = AddToBounds(bounds, rect, ref hasBounds);
                }

                meldLayout.Bounds = meldBounds;
            }

            var newGroupRowOffset = -rowWidths[row] * 0.5f;
            newGroupRect.x += newGroupRowOffset;
            newGroupRect.y += verticalOffset;
            slots.Add(new BoardLayoutSlot(newGroupRect, -1, 0, true));
            bounds = AddToBounds(bounds, newGroupRect, ref hasBounds);

            return new BoardLayoutResult(layouts.ToArray(), slots.ToArray(), newGroupRect, bounds, rowWidth, rowCount,
                cardSize);
        }

        public static BoardLayoutResult ComputeOrganic(IReadOnlyList<Meld> melds, Vector2 cardSize, bool overlapSets,
            IReadOnlyDictionary<int, Vector2> preferredOrigins, IReadOnlyDictionary<int, Vector2> hints,
            out Dictionary<int, Vector2> origins)
        {
            return ComputeOrganic(melds, cardSize, overlapSets, preferredOrigins, hints, null, null, out origins);
        }

        public static BoardLayoutResult ComputeOrganic(IReadOnlyList<Meld> melds, Vector2 cardSize, bool overlapSets,
            IReadOnlyDictionary<int, Vector2> preferredOrigins, IReadOnlyDictionary<int, Vector2> hints,
            Rect? placementArea, ICollection<int> constrainedMeldIds, out Dictionary<int, Vector2> origins)
        {
            if (melds == null)
            {
                throw new ArgumentNullException(nameof(melds));
            }

            if (cardSize.x <= 0f || cardSize.y <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cardSize));
            }

            var cardGap = cardSize.x * (overlapSets ? -0.55f : 0.14f);
            var meldGap = cardSize.x * (overlapSets ? 0.6f : 0.38f);
            var rowGap = cardSize.y * 0.28f;
            var occupied = new List<Rect>(melds.Count);
            var layouts = new List<BoardMeldLayout>(melds.Count);
            var slots = new List<BoardLayoutSlot>();
            origins = new Dictionary<int, Vector2>(melds.Count);
            var bounds = default(Rect);
            var hasBounds = false;
            var rightmost = 0f;

            for (var pass = 0; pass < 2; pass++)
            {
                for (var meldIndex = 0; meldIndex < melds.Count; meldIndex++)
                {
                    var meld = melds[meldIndex];
                    var hasPreferredOrigin = preferredOrigins != null && preferredOrigins.ContainsKey(meld.Id);
                    if (hasPreferredOrigin != (pass == 0))
                    {
                        continue;
                    }

                    var cardCount = meld.Cards.Count;
                    if (cardCount == 0)
                    {
                        continue;
                    }

                    var meldWidth = GetMeldWidth(cardCount, cardSize.x, cardGap);
                    var meldBounds = new Rect(0f, 0f, meldWidth, cardSize.y);
                    Vector2 desiredOrigin;
                    if (preferredOrigins != null && preferredOrigins.TryGetValue(meld.Id, out desiredOrigin))
                    {
                        // Keep the existing set where it was unless the new board makes that impossible.
                    }
                    else if (hints != null && hints.TryGetValue(meld.Id, out desiredOrigin))
                    {
                        desiredOrigin -= new Vector2(meldWidth * 0.5f, cardSize.y * 0.5f);
                    }
                    else
                    {
                        desiredOrigin = new Vector2(-meldWidth * 0.5f, -cardSize.y * 0.5f);
                    }

                    var constrained = placementArea.HasValue && constrainedMeldIds != null &&
                                      constrainedMeldIds.Contains(meld.Id);
                    if (constrained && CanFitInside(meldBounds, placementArea.Value))
                    {
                        desiredOrigin = ClampOriginToArea(meldBounds, desiredOrigin, placementArea.Value);
                    }

                    var origin = FindFreeOrigin(meldBounds, desiredOrigin, occupied, meldGap, rowGap,
                        new Vector2(cardSize.x * 0.5f, cardSize.y * 0.5f), constrained ? placementArea : null);
                    origins[meld.Id] = origin;
                    var cardRects = new Rect[cardCount];
                    var meldLayout = new BoardMeldLayout(meld.Id, 0, cardRects);
                    for (var cardIndex = 0; cardIndex < cardCount; cardIndex++)
                    {
                        var rect = new Rect(origin.x + cardIndex * (cardSize.x + cardGap), origin.y, cardSize.x,
                            cardSize.y);
                        cardRects[cardIndex] = rect;
                        slots.Add(new BoardLayoutSlot(rect, meld.Id, cardIndex, false));
                    }

                    meldLayout.Bounds = new Rect(origin.x, origin.y, meldWidth, cardSize.y);
                    layouts.Add(meldLayout);
                    occupied.Add(meldLayout.Bounds);
                    bounds = AddToBounds(bounds, meldLayout.Bounds, ref hasBounds);
                    rightmost = Mathf.Max(rightmost, meldLayout.Bounds.xMax);
                }
            }

            var newGroupBounds = new Rect(rightmost + meldGap, -cardSize.y * 0.5f, cardSize.x, cardSize.y);
            var newGroupOrigin = FindFreeOrigin(newGroupBounds, new Vector2(newGroupBounds.xMin, newGroupBounds.yMin),
                occupied, meldGap, rowGap, new Vector2(cardSize.x * 0.5f, cardSize.y * 0.5f));
            var newGroupSlot = new Rect(newGroupOrigin.x, newGroupOrigin.y, cardSize.x, cardSize.y);
            slots.Add(new BoardLayoutSlot(newGroupSlot, -1, 0, true));
            bounds = AddToBounds(bounds, newGroupSlot, ref hasBounds);

            return new BoardLayoutResult(layouts.ToArray(), slots.ToArray(), newGroupSlot, bounds, bounds.width, 1,
                cardSize);
        }

        public static Vector2 FindFreeOrigin(Rect bounds, Vector2 desiredOrigin, IReadOnlyList<Rect> occupied,
            float horizontalGap, float verticalGap, Vector2 searchStep)
        {
            var stepX = Mathf.Max(searchStep.x, bounds.width * 0.25f, 0.01f);
            var stepY = Mathf.Max(searchStep.y, bounds.height * 0.25f, 0.01f);
            var candidate = new Rect(desiredOrigin.x, desiredOrigin.y, bounds.width, bounds.height);
            if (IsFree(candidate, occupied, horizontalGap, verticalGap))
            {
                return desiredOrigin;
            }

            for (var ring = 1; ring <= 64; ring++)
            {
                for (var x = -ring; x <= ring; x++)
                {
                    for (var y = -ring; y <= ring; y++)
                    {
                        if (Mathf.Abs(x) != ring && Mathf.Abs(y) != ring)
                        {
                            continue;
                        }

                        var origin = desiredOrigin + new Vector2(x * stepX, y * stepY);
                        candidate.position = origin;
                        if (IsFree(candidate, occupied, horizontalGap, verticalGap))
                        {
                            return origin;
                        }
                    }
                }
            }

            var fallbackX = desiredOrigin.x;
            if (occupied != null)
            {
                for (var index = 0; index < occupied.Count; index++)
                {
                    fallbackX = Mathf.Max(fallbackX, occupied[index].xMax + horizontalGap);
                }
            }

            return new Vector2(fallbackX + horizontalGap, desiredOrigin.y);
        }

        public static Vector2 FindFreeOrigin(Rect bounds, Vector2 desiredOrigin, IReadOnlyList<Rect> occupied,
            float horizontalGap, float verticalGap, Vector2 searchStep, Rect? allowedArea)
        {
            if (!allowedArea.HasValue || !CanFitInside(bounds, allowedArea.Value))
            {
                return FindFreeOrigin(bounds, desiredOrigin, occupied, horizontalGap, verticalGap, searchStep);
            }

            var area = allowedArea.Value;
            var stepX = Mathf.Max(searchStep.x, bounds.width * 0.25f, 0.01f);
            var stepY = Mathf.Max(searchStep.y, bounds.height * 0.25f, 0.01f);
            desiredOrigin = ClampOriginToArea(bounds, desiredOrigin, area);
            var candidate = new Rect(desiredOrigin.x, desiredOrigin.y, bounds.width, bounds.height);
            if (IsInsideArea(candidate, area) && IsFree(candidate, occupied, horizontalGap, verticalGap))
            {
                return desiredOrigin;
            }

            var maxX = area.xMax - bounds.width;
            var maxY = area.yMax - bounds.height;
            var maxDistanceX = Mathf.Max(Mathf.Abs(area.xMin - desiredOrigin.x),
                Mathf.Abs(maxX - desiredOrigin.x)) / stepX;
            var maxDistanceY = Mathf.Max(Mathf.Abs(area.yMin - desiredOrigin.y),
                Mathf.Abs(maxY - desiredOrigin.y)) / stepY;
            var maxRing = Mathf.CeilToInt(Mathf.Max(maxDistanceX, maxDistanceY)) + 1;
            for (var ring = 1; ring <= maxRing; ring++)
            {
                for (var x = -ring; x <= ring; x++)
                {
                    for (var y = -ring; y <= ring; y++)
                    {
                        if (Mathf.Abs(x) != ring && Mathf.Abs(y) != ring)
                        {
                            continue;
                        }

                        var origin = desiredOrigin + new Vector2(x * stepX, y * stepY);
                        candidate.position = origin;
                        if (IsInsideArea(candidate, area) &&
                            IsFree(candidate, occupied, horizontalGap, verticalGap))
                        {
                            return origin;
                        }
                    }
                }
            }

            var candidateXs = new List<float> { desiredOrigin.x, area.xMin, maxX };
            var candidateYs = new List<float> { desiredOrigin.y, area.yMin, maxY };
            if (occupied != null)
            {
                for (var index = 0; index < occupied.Count; index++)
                {
                    var other = occupied[index];
                    candidateXs.Add(Mathf.Clamp(other.xMin - bounds.width - horizontalGap, area.xMin, maxX));
                    candidateXs.Add(Mathf.Clamp(other.xMax + horizontalGap, area.xMin, maxX));
                    candidateYs.Add(Mathf.Clamp(other.yMin - bounds.height - verticalGap, area.yMin, maxY));
                    candidateYs.Add(Mathf.Clamp(other.yMax + verticalGap, area.yMin, maxY));
                }
            }

            for (var xIndex = 0; xIndex < candidateXs.Count; xIndex++)
            {
                for (var yIndex = 0; yIndex < candidateYs.Count; yIndex++)
                {
                    var origin = new Vector2(candidateXs[xIndex], candidateYs[yIndex]);
                    candidate.position = origin;
                    if (IsInsideArea(candidate, area) && IsFree(candidate, occupied, horizontalGap, verticalGap))
                    {
                        return origin;
                    }
                }
            }

            return FindFreeOrigin(bounds, desiredOrigin, occupied, horizontalGap, verticalGap, searchStep);
        }

        public static void CardJitter(int cardId, int meldId, float messiness, Vector2 cardSize,
            out Vector2 offset, out float angle)
        {
            var amount = Mathf.Clamp01(messiness);
            var hash = unchecked(cardId * 374761393 + meldId * 668265263 + 1442695041);
            hash = unchecked((hash ^ (hash >> 13)) * 1274126177);
            var x = (hash & 0x7fffffff) / (float)int.MaxValue;
            hash = unchecked((hash ^ (hash >> 16)) * 1597334677);
            var y = (hash & 0x7fffffff) / (float)int.MaxValue;
            hash = unchecked((hash ^ (hash >> 15)) * 1013904223);
            var rotation = (hash & 0x7fffffff) / (float)int.MaxValue;
            offset = new Vector2((x * 2f - 1f) * cardSize.x * 0.1f * amount,
                (y * 2f - 1f) * cardSize.y * 0.06f * amount);
            angle = (rotation * 2f - 1f) * 7f * amount;
        }

        private static bool IsFree(Rect bounds, IReadOnlyList<Rect> occupied, float horizontalGap, float verticalGap)
        {
            if (occupied == null)
            {
                return true;
            }

            var padded = bounds;
            padded.xMin -= horizontalGap * 0.5f;
            padded.xMax += horizontalGap * 0.5f;
            padded.yMin -= verticalGap * 0.5f;
            padded.yMax += verticalGap * 0.5f;
            for (var index = 0; index < occupied.Count; index++)
            {
                var other = occupied[index];
                other.xMin -= horizontalGap * 0.5f;
                other.xMax += horizontalGap * 0.5f;
                other.yMin -= verticalGap * 0.5f;
                other.yMax += verticalGap * 0.5f;
                if (padded.Overlaps(other))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CanFitInside(Rect bounds, Rect area)
        {
            return bounds.width <= area.width && bounds.height <= area.height;
        }

        private static bool IsInsideArea(Rect bounds, Rect area)
        {
            const float tolerance = 0.0001f;
            return bounds.xMin >= area.xMin - tolerance && bounds.xMax <= area.xMax + tolerance &&
                   bounds.yMin >= area.yMin - tolerance && bounds.yMax <= area.yMax + tolerance;
        }

        private static Vector2 ClampOriginToArea(Rect bounds, Vector2 origin, Rect area)
        {
            if (!CanFitInside(bounds, area))
            {
                return origin;
            }

            return new Vector2(Mathf.Clamp(origin.x, area.xMin, area.xMax - bounds.width),
                Mathf.Clamp(origin.y, area.yMin, area.yMax - bounds.height));
        }

        private static int CountNonEmptyMelds(IReadOnlyList<Meld> melds)
        {
            var count = 0;
            for (var index = 0; index < melds.Count; index++)
            {
                if (melds[index].Cards.Count > 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static void GetLayoutMetrics(IReadOnlyList<Meld> melds, float rowWidth, Vector2 cardSize,
            float cardGap, float meldGap, out int rowCount, out float actualWidth)
        {
            var row = 0;
            var cursorX = 0f;
            actualWidth = 0f;
            for (var meldIndex = 0; meldIndex < melds.Count; meldIndex++)
            {
                var cardCount = melds[meldIndex].Cards.Count;
                if (cardCount == 0)
                {
                    continue;
                }

                var meldWidth = GetMeldWidth(cardCount, cardSize.x, cardGap);
                if (cursorX > 0f && cursorX + meldGap + meldWidth > rowWidth)
                {
                    actualWidth = Mathf.Max(actualWidth, cursorX);
                    row++;
                    cursorX = 0f;
                }

                if (cursorX > 0f)
                {
                    cursorX += meldGap;
                }

                cursorX += meldWidth;
            }

            if (cursorX > 0f && cursorX + meldGap + cardSize.x > rowWidth)
            {
                actualWidth = Mathf.Max(actualWidth, cursorX);
                row++;
                cursorX = cardSize.x;
            }
            else
            {
                if (cursorX > 0f)
                {
                    cursorX += meldGap;
                }

                cursorX += cardSize.x;
            }

            actualWidth = Mathf.Max(actualWidth, cursorX);
            rowCount = row + 1;
        }

        private static float GetMeldWidth(int cardCount, float cardWidth, float cardGap)
        {
            return cardCount * cardWidth + (cardCount - 1) * cardGap;
        }

        private static Rect AddToMeldBounds(Rect bounds, int cardIndex, Rect rect)
        {
            if (cardIndex == 0)
            {
                return rect;
            }

            return Union(bounds, rect);
        }

        private static Rect AddToBounds(Rect bounds, Rect rect, ref bool hasBounds)
        {
            if (!hasBounds)
            {
                hasBounds = true;
                return rect;
            }

            return Union(bounds, rect);
        }

        private static Rect Union(Rect left, Rect right)
        {
            return Rect.MinMaxRect(Mathf.Min(left.xMin, right.xMin), Mathf.Min(left.yMin, right.yMin),
                Mathf.Max(left.xMax, right.xMax), Mathf.Max(left.yMax, right.yMax));
        }
    }

    public sealed class BoardLayoutResult
    {
        public const float MeldFrameHorizontalPadding = 0.6f;
        public const float MeldFrameVerticalPadding = 0.46f;

        private readonly BoardMeldLayout[] melds;
        private readonly BoardLayoutSlot[] slots;
        private readonly Vector2 cardSize;

        internal BoardLayoutResult(BoardMeldLayout[] meldLayouts, BoardLayoutSlot[] layoutSlots, Rect newGroupSlot,
            Rect bounds, float rowWidth, int rowCount, Vector2 layoutCardSize)
        {
            melds = meldLayouts;
            slots = layoutSlots;
            NewGroupSlot = newGroupSlot;
            Bounds = bounds;
            RowWidth = rowWidth;
            RowCount = rowCount;
            cardSize = layoutCardSize;
        }

        public IReadOnlyList<BoardMeldLayout> Melds => melds;
        public IReadOnlyList<BoardLayoutSlot> Slots => slots;
        public Rect NewGroupSlot { get; }
        public Rect Bounds { get; }
        public float RowWidth { get; }
        public int RowCount { get; }
        public Vector2 CardSize => cardSize;

        public bool TryHitMeldFrame(Vector2 point, out int meldId)
        {
            if (NewGroupSlot.Contains(point))
            {
                meldId = -1;
                return false;
            }

            var closestDistance = float.MaxValue;
            var closestMeldId = -1;
            for (var meldIndex = 0; meldIndex < melds.Length; meldIndex++)
            {
                var meld = melds[meldIndex];
                var hitBounds = meld.Bounds;
                hitBounds.xMin -= cardSize.x * MeldFrameHorizontalPadding;
                hitBounds.xMax += cardSize.x * MeldFrameHorizontalPadding;
                hitBounds.yMin -= cardSize.y * MeldFrameVerticalPadding;
                hitBounds.yMax += cardSize.y * MeldFrameVerticalPadding;
                if (!hitBounds.Contains(point))
                {
                    continue;
                }

                var overCard = false;
                for (var cardIndex = 0; cardIndex < meld.CardRectCount; cardIndex++)
                {
                    if (meld.GetCardRect(cardIndex).Contains(point))
                    {
                        overCard = true;
                        break;
                    }
                }

                if (!overCard)
                {
                    var delta = new Vector2(
                        Mathf.Max(meld.Bounds.xMin - point.x, 0f, point.x - meld.Bounds.xMax),
                        Mathf.Max(meld.Bounds.yMin - point.y, 0f, point.y - meld.Bounds.yMax));
                    var distance = delta.sqrMagnitude;
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestMeldId = meld.MeldId;
                    }
                }
            }

            meldId = closestMeldId;
            return closestMeldId >= 0;
        }

        public bool TryHit(Vector2 point, out BoardLayoutHit hit)
        {
            for (var meldIndex = 0; meldIndex < melds.Length; meldIndex++)
            {
                var meld = melds[meldIndex];
                var hitBounds = meld.Bounds;
                hitBounds.xMin -= cardSize.x * 0.1f;
                hitBounds.xMax += cardSize.x * 0.1f;
                hitBounds.yMin -= cardSize.y * 0.12f;
                hitBounds.yMax += cardSize.y * 0.12f;
                if (!hitBounds.Contains(point))
                {
                    continue;
                }

                var insertIndex = 0;
                while (insertIndex < meld.CardRectCount &&
                       point.x >= meld.GetCardRect(insertIndex).center.x)
                {
                    insertIndex++;
                }

                hit = new BoardLayoutHit(false, meld.MeldId, insertIndex);
                return true;
            }

            if (NewGroupSlot.Contains(point) || Bounds.Contains(point))
            {
                hit = new BoardLayoutHit(true, -1, 0);
                return true;
            }

            hit = default;
            return false;
        }
    }

    public sealed class BoardMeldLayout
    {
        private readonly Rect[] cardRects;

        internal BoardMeldLayout(int meldId, int rowIndex, Rect[] cardRects)
        {
            MeldId = meldId;
            RowIndex = rowIndex;
            this.cardRects = cardRects;
        }

        public int MeldId { get; }
        public IReadOnlyList<Rect> CardRects => cardRects;
        public Rect Bounds { get; internal set; }

        internal int RowIndex { get; }
        internal int CardRectCount => cardRects.Length;

        internal Rect GetCardRect(int index)
        {
            return cardRects[index];
        }

        internal void SetCardRect(int index, Rect rect)
        {
            cardRects[index] = rect;
        }
    }

    public readonly struct BoardLayoutSlot
    {
        public BoardLayoutSlot(Rect rect, int meldId, int cardIndex, bool isNewGroup)
        {
            Rect = rect;
            MeldId = meldId;
            CardIndex = cardIndex;
            IsNewGroup = isNewGroup;
        }

        public Rect Rect { get; }
        public int MeldId { get; }
        public int CardIndex { get; }
        public bool IsNewGroup { get; }
    }

    public readonly struct BoardLayoutHit
    {
        public BoardLayoutHit(bool isNewGroup, int meldId, int insertIndex)
        {
            IsNewGroup = isNewGroup;
            MeldId = meldId;
            InsertIndex = insertIndex;
        }

        public bool IsNewGroup { get; }
        public int MeldId { get; }
        public int InsertIndex { get; }
    }
}