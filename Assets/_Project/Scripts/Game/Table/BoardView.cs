using System.Collections.Generic;
using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    public sealed class BoardView : MonoBehaviour
    {
        private readonly Dictionary<int, MeldOutline> outlines = new Dictionary<int, MeldOutline>();
        private readonly List<int> staleMeldIds = new List<int>();
        private readonly Stack<MeldOutline> outlinePool = new Stack<MeldOutline>();
        private MeldOutline previewOutline;

        public BoardLayoutResult CurrentLayout { get; private set; }
        public Bounds CurrentBounds { get; private set; }

        public void ShowDropPreview(GestureHit target, bool isValid)
        {
            if (CurrentLayout == null)
            {
                return;
            }

            Rect previewBounds;
            if (target.Kind == GestureHitKind.EmptyBoard)
            {
                previewBounds = CurrentLayout.NewGroupSlot;
            }
            else if (target.Kind == GestureHitKind.MeldGap || target.Kind == GestureHitKind.BoardCard)
            {
                var meldLayout = FindMeldLayout(CurrentLayout, target.MeldId);
                if (meldLayout == null || meldLayout.CardRects.Count == 0)
                {
                    ClearDropPreview();
                    return;
                }

                var insertIndex = target.Kind == GestureHitKind.BoardCard ? target.TargetIndex : target.Index;
                var cardRects = meldLayout.CardRects;
                var gapCenter = insertIndex <= 0
                    ? cardRects[0].xMin - CurrentLayout.CardSize.x * 0.07f
                    : insertIndex >= cardRects.Count
                        ? cardRects[cardRects.Count - 1].xMax + CurrentLayout.CardSize.x * 0.07f
                        : (cardRects[insertIndex - 1].xMax + cardRects[insertIndex].xMin) * 0.5f;
                var gapWidth = CurrentLayout.CardSize.x * 0.16f;
                previewBounds = new Rect(gapCenter - gapWidth * 0.5f, meldLayout.Bounds.yMin, gapWidth,
                    meldLayout.Bounds.height);
            }
            else
            {
                ClearDropPreview();
                return;
            }

            if (previewOutline == null)
            {
                var previewObject = new GameObject("Drop Preview");
                previewObject.transform.SetParent(transform, false);
                previewObject.AddComponent<LineRenderer>();
                previewOutline = previewObject.AddComponent<MeldOutline>();
            }

            if (!previewOutline.gameObject.activeSelf)
            {
                previewOutline.gameObject.SetActive(true);
            }

            previewOutline.SetBounds(previewBounds, isValid, CurrentLayout.CardSize);
        }

        public void ClearDropPreview()
        {
            if (previewOutline != null)
            {
                previewOutline.Hide();
            }
        }

        public void SetMeldOutlineOffset(int meldId, Vector3 offset)
        {
            if (outlines.TryGetValue(meldId, out var outline))
            {
                outline.transform.localPosition = offset;
            }
        }

        public BoardLayoutResult Layout(IReadOnlyList<Meld> board, IReadOnlyDictionary<byte, CardView> cardViews,
            float viewportAspect, Vector2 cardSize)
        {
            CurrentLayout = BoardLayout.Compute(board, viewportAspect, cardSize);
            staleMeldIds.Clear();
            foreach (var pair in outlines)
            {
                staleMeldIds.Add(pair.Key);
            }

            for (var meldIndex = 0; meldIndex < board.Count; meldIndex++)
            {
                var meld = board[meldIndex];
                if (meld.Cards.Count == 0)
                {
                    continue;
                }

                var meldLayout = FindMeldLayout(CurrentLayout, meld.Id);
                if (meldLayout == null)
                {
                    continue;
                }

                var outline = GetOutline(meld.Id);
                staleMeldIds.Remove(meld.Id);
                outline.transform.localPosition = Vector3.zero;
                outline.SetBounds(meldLayout.Bounds, MeldValidator.Classify(meld.Cards).IsValid, cardSize);
                for (var cardIndex = 0; cardIndex < meld.Cards.Count && cardIndex < meldLayout.CardRects.Count;
                     cardIndex++)
                {
                    var card = meld.Cards[cardIndex];
                    if (cardViews.TryGetValue(card.Id, out var view))
                    {
                        var center = meldLayout.CardRects[cardIndex].center;
                        view.SetSortingOrder(10);
                        var world = transform.TransformPoint(new Vector3(center.x, center.y, 0f));
                        var parent = view.transform.parent;
                        view.MoveTo(parent == null ? world : parent.InverseTransformPoint(world));
                    }
                }
            }

            for (var index = 0; index < staleMeldIds.Count; index++)
            {
                var meldId = staleMeldIds[index];
                if (!outlines.TryGetValue(meldId, out var outline))
                {
                    continue;
                }

                outlines.Remove(meldId);
                outline.Hide();
                outline.gameObject.SetActive(false);
                outlinePool.Push(outline);
            }

            var rect = CurrentLayout.Bounds;
            var boundsCenter = transform.TransformPoint(new Vector3(rect.center.x, rect.center.y, 0f));
            var boundsSize = transform.TransformVector(new Vector3(rect.width, rect.height, 0f));
            CurrentBounds = new Bounds(boundsCenter, boundsSize);
            return CurrentLayout;
        }

        private MeldOutline GetOutline(int meldId)
        {
            if (outlines.TryGetValue(meldId, out var outline))
            {
                return outline;
            }

            if (outlinePool.Count > 0)
            {
                outline = outlinePool.Pop();
                outline.gameObject.SetActive(true);
            }
            else
            {
                var outlineObject = new GameObject("Meld Outline " + meldId);
                outlineObject.transform.SetParent(transform, false);
                outlineObject.AddComponent<LineRenderer>();
                outline = outlineObject.AddComponent<MeldOutline>();
            }

            outlines.Add(meldId, outline);
            return outline;
        }

        private static BoardMeldLayout FindMeldLayout(BoardLayoutResult layout, int meldId)
        {
            for (var index = 0; index < layout.Melds.Count; index++)
            {
                if (layout.Melds[index].MeldId == meldId)
                {
                    return layout.Melds[index];
                }
            }

            return null;
        }
    }
}