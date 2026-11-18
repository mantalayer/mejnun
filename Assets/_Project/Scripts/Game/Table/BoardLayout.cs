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

            var cardGap = cardSize.x * 0.14f;
            var meldGap = cardSize.x * 0.38f;
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
            for (var meldIndex = 0; meldIndex < melds.Length; meldIndex++)
            {
                var meld = melds[meldIndex];
                var hitBounds = meld.Bounds;
                hitBounds.xMin -= cardSize.x * 0.18f;
                hitBounds.xMax += cardSize.x * 0.18f;
                hitBounds.yMin -= cardSize.y * 0.13f;
                hitBounds.yMax += cardSize.y * 0.13f;
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
                    meldId = meld.MeldId;
                    return true;
                }
            }

            meldId = -1;
            return false;
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