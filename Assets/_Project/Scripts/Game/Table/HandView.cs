using System.Collections.Generic;
using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    public sealed class HandView : MonoBehaviour
    {
        [SerializeField] private float bottomMargin = 0.12f;

        private readonly List<Card> orderedCards = new List<Card>(12);
        private float scrollOffset;

        public float ScrollOffset => scrollOffset;
        public int RowCount { get; private set; } = 1;
        public float HandTopY { get; private set; }

        public static int CalculateRowCount(int screenWidth, int screenHeight, int cardCount)
        {
            return screenHeight > screenWidth && cardCount > 8 ? 2 : 1;
        }

        public static void CalculateArcPose(float normalizedPosition, float arcAmount, out float offset, out float angle)
        {
            offset = arcAmount * (1f - normalizedPosition * normalizedPosition);
            angle = -normalizedPosition * 24f * arcAmount;
        }

        public void Layout(IReadOnlyList<Card> hand, HandOrder order, int seat,
            IReadOnlyDictionary<byte, CardView> views, Transform handRoot, Vector2 cardSize, float referenceSize,
            float aspect, float bottomInset, float moveDuration = -1f, ICollection<byte> liftedCardIds = null,
            int gapIndex = -1)
        {
            if (hand == null || order == null || views == null || handRoot == null)
            {
                return;
            }

            order.Synchronize(seat, hand);
            order.CopyCards(seat, orderedCards);
            var cardCount = orderedCards.Count;
            if (cardCount == 0)
            {
                scrollOffset = 0f;
                HandTopY = -referenceSize;
                return;
            }

            var halfWidth = referenceSize * aspect;
            var cardScale = Mathf.Clamp(GameSettings.HandCardScale, 0.5f, 1.5f);
            var scaledCardSize = cardSize * cardScale;
            var viewportWidth = Mathf.Max(scaledCardSize.x, halfWidth * 2f - 0.2f);
            var step = scaledCardSize.x * Mathf.Clamp(GameSettings.HandSpread, 0.25f, 0.9f);
            RowCount = CalculateRowCount(Screen.width, Screen.height, cardCount);
            var cardsPerRow = RowCount == 1 ? cardCount : Mathf.CeilToInt(cardCount / 2f);
            var liftedCount = liftedCardIds == null ? 0 : liftedCardIds.Count;
            var rowWidth = scaledCardSize.x + (cardsPerRow + liftedCount - 1) * step;
            var maxScroll = Mathf.Max(0f, rowWidth - viewportWidth);
            scrollOffset = Mathf.Clamp(scrollOffset, 0f, maxScroll);
            var firstX = -rowWidth * 0.5f + scaledCardSize.x * 0.5f - scrollOffset;
            var bottomY = -referenceSize + scaledCardSize.y * 0.5f + bottomMargin + bottomInset +
                          Mathf.Clamp(GameSettings.HandLift, 0f, 1.5f);
            var rowStep = scaledCardSize.y * 0.68f;
            var displayIndex = 0;
            var gapInserted = false;
            HandTopY = float.NegativeInfinity;
            for (var index = 0; index < cardCount; index++)
            {
                var card = orderedCards[index];
                if (liftedCardIds != null && liftedCardIds.Contains(card.Id))
                {
                    continue;
                }

                if (!gapInserted && gapIndex >= 0 && displayIndex >= gapIndex)
                {
                    displayIndex += liftedCount;
                    gapInserted = true;
                }

                if (!views.TryGetValue(card.Id, out var view))
                {
                    continue;
                }

                view.SetSortingOrder(100 + index);
                var row = RowCount == 1 ? 0 : displayIndex / cardsPerRow;
                var column = RowCount == 1 ? displayIndex : displayIndex % cardsPerRow;
                var normalizedPosition = cardsPerRow <= 1
                    ? 0f
                    : (column - (cardsPerRow - 1) * 0.5f) / ((cardsPerRow - 1) * 0.5f);
                var arcAmount = GameSettings.HandArc ? Mathf.Clamp(GameSettings.HandArcAmount, 0f, 1.5f) : 0f;
                CalculateArcPose(normalizedPosition, arcAmount, out var arcOffset, out var angle);
                view.SetLayoutPose(Quaternion.Euler(0f, 0f, angle), cardScale);
                var centerY = bottomY + row * rowStep + arcOffset;
                view.MoveTo(new Vector3(firstX + column * step, centerY, 0f), moveDuration);
                var radians = angle * Mathf.Deg2Rad;
                var verticalExtent = Mathf.Abs(Mathf.Cos(radians)) * scaledCardSize.y * 0.5f +
                                     Mathf.Abs(Mathf.Sin(radians)) * scaledCardSize.x * 0.5f;
                HandTopY = Mathf.Max(HandTopY, centerY + verticalExtent);
                displayIndex++;
            }

            if (float.IsNegativeInfinity(HandTopY))
            {
                HandTopY = bottomY + scaledCardSize.y;
            }
        }

        public void Scroll(float distance)
        {
            scrollOffset = Mathf.Max(0f, scrollOffset + distance);
        }

        public void ResetScroll()
        {
            scrollOffset = 0f;
        }

        public void Reorder(HandOrder order, int seat, byte cardId, int targetIndex)
        {
            if (order != null)
            {
                order.Reorder(seat, cardId, targetIndex);
            }
        }
    }
}