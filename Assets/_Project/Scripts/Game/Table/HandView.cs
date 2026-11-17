using System.Collections.Generic;
using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    public sealed class HandView : MonoBehaviour
    {
        [SerializeField, Range(0.25f, 0.9f)] private float cardStepRatio = 0.52f;
        [SerializeField] private float bottomMargin = 0.12f;

        private readonly List<Card> orderedCards = new List<Card>(12);
        private float scrollOffset;

        public float ScrollOffset => scrollOffset;

        public void Layout(IReadOnlyList<Card> hand, HandOrder order, int seat,
            IReadOnlyDictionary<byte, CardView> views, Camera camera, TableCamera tableCamera, Vector2 cardSize,
            float pixelsPerUnit, float moveDuration = -1f)
        {
            if (hand == null || order == null || views == null || camera == null)
            {
                return;
            }

            order.Synchronize(seat, hand);
            order.CopyCards(seat, orderedCards);
            var cardCount = orderedCards.Count;
            if (cardCount == 0)
            {
                scrollOffset = 0f;
                return;
            }

            var halfWidth = camera.orthographicSize * camera.aspect;
            var viewportWidth = Mathf.Max(cardSize.x, halfWidth * 2f - 0.2f);
            var step = cardSize.x * cardStepRatio;
            var rowWidth = cardSize.x + (cardCount - 1) * step;
            var maxScroll = Mathf.Max(0f, rowWidth - viewportWidth);
            scrollOffset = Mathf.Clamp(scrollOffset, 0f, maxScroll);
            var cameraPosition = camera.transform.position;
            var firstX = cameraPosition.x - rowWidth * 0.5f + cardSize.x * 0.5f - scrollOffset;
            var y = cameraPosition.y - camera.orthographicSize + cardSize.y * 0.5f + bottomMargin;
            for (var index = 0; index < cardCount; index++)
            {
                var card = orderedCards[index];
                if (!views.TryGetValue(card.Id, out var view))
                {
                    continue;
                }

                view.SetSortingOrder(100 + index);
                var position = new Vector3(firstX + index * step, y, 0f);
                if (tableCamera != null)
                {
                    position = tableCamera.SnapWorldPosition(position, pixelsPerUnit);
                }

                view.MoveTo(position, moveDuration);
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