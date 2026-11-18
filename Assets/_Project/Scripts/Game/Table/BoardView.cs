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
        private readonly Dictionary<int, Vector2> organicOrigins = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, HashSet<byte>> previousMeldCardIds =
            new Dictionary<int, HashSet<byte>>();
        private readonly List<int> staleOriginIds = new List<int>();
        private readonly List<Rect> occupiedBounds = new List<Rect>();
        private readonly List<SpriteRenderer> previewCards = new List<SpriteRenderer>();
        private readonly List<Vector3> previewTargets = new List<Vector3>();
        private readonly List<byte> previewShiftedCardIds = new List<byte>();
        private readonly HashSet<byte> liftedCardIds = new HashSet<byte>();
        private MeldOutline previewOutline;
        private IReadOnlyList<Meld> lastBoard;
        private IReadOnlyDictionary<byte, CardView> lastCardViews;
        private float lastViewportAspect;
        private Vector2 lastCardSize;
        private bool lastOrganicMode;
        private bool hasLastOrganicMode;
        private bool hasPendingNewMeldPoint;
        private Vector2 pendingNewMeldPoint;
        private Rect? cpuPlacementArea;
        private float previewOpacity;
        private float previewOpacityTarget;

        public BoardLayoutResult CurrentLayout { get; private set; }
        public Bounds CurrentBounds { get; private set; }

        public void SetCpuPlacementArea(Rect? area)
        {
            cpuPlacementArea = area;
        }

        public void ShowDropPreview(GestureHit target, bool isValid, IReadOnlyList<byte> draggedCardIds,
            IReadOnlyCollection<byte> liftedCardIds, Vector2 boardPoint, bool overlapSets, bool organicBoard)
        {
            if (CurrentLayout == null)
            {
                return;
            }

            if (overlapSets)
            {
                if (previewOutline != null)
                {
                    previewOutline.Hide();
                }

                ShowCardPreview(target, isValid, draggedCardIds, liftedCardIds, boardPoint, organicBoard);
            }
            else
            {
                ShowBorderPreview(target, isValid);
                HideCardPreview();
            }
        }

        public void SetPendingNewMeldPoint(Vector2 point)
        {
            pendingNewMeldPoint = point;
            hasPendingNewMeldPoint = true;
        }

        public void ClearPendingNewMeldPoint()
        {
            hasPendingNewMeldPoint = false;
        }

        private void ShowBorderPreview(GestureHit target, bool isValid)
        {
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
                    HideBorderPreview();
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
                HideBorderPreview();
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
            HideBorderPreview();
            HideCardPreview();
            if (lastBoard != null && lastCardViews != null)
            {
                Layout(lastBoard, lastCardViews, lastViewportAspect, lastCardSize, liftedCardIds);
            }
        }

        public void SetLiftedCardIds(ICollection<byte> cardIds)
        {
            if (ReferenceEquals(cardIds, liftedCardIds))
            {
                return;
            }

            liftedCardIds.Clear();
            if (cardIds == null)
            {
                return;
            }

            foreach (var cardId in cardIds)
            {
                liftedCardIds.Add(cardId);
            }
        }

        private void HideBorderPreview()
        {
            previewOutline?.Hide();
        }

        private void ShowCardPreview(GestureHit target, bool isValid, IReadOnlyList<byte> draggedCardIds,
            IReadOnlyCollection<byte> liftedCardIds, Vector2 boardPoint, bool organicBoard)
        {
            ResetShiftedCards();
            if (draggedCardIds == null || draggedCardIds.Count == 0 || lastCardViews == null)
            {
                HideCardPreview();
                return;
            }

            var count = draggedCardIds.Count;
            var cardSize = CurrentLayout.CardSize;
            var step = cardSize.x * 0.45f;
            var center = Vector2.zero;
            BoardMeldLayout meldLayout = null;
            var insertIndex = 0;
            if (target.Kind == GestureHitKind.EmptyBoard)
            {
                if (organicBoard)
                {
                    occupiedBounds.Clear();
                    for (var index = 0; index < CurrentLayout.Melds.Count; index++)
                    {
                        occupiedBounds.Add(CurrentLayout.Melds[index].Bounds);
                    }

                    var width = cardSize.x + (count - 1) * step;
                    var desired = new Rect(boardPoint.x - width * 0.5f, boardPoint.y - cardSize.y * 0.5f,
                        width, cardSize.y);
                    var origin = BoardLayout.FindFreeOrigin(desired,
                        new Vector2(desired.xMin, desired.yMin), occupiedBounds, cardSize.x * 0.38f,
                        cardSize.y * 0.28f, new Vector2(cardSize.x * 0.5f, cardSize.y * 0.5f));
                    center = new Vector2(origin.x + cardSize.x * 0.5f, origin.y + cardSize.y * 0.5f);
                }
                else
                {
                    center = CurrentLayout.NewGroupSlot.center;
                }
            }
            else if (target.Kind == GestureHitKind.MeldGap || target.Kind == GestureHitKind.BoardCard)
            {
                meldLayout = FindMeldLayout(CurrentLayout, target.MeldId);
                if (meldLayout == null || meldLayout.CardRects.Count == 0)
                {
                    HideCardPreview();
                    return;
                }

                insertIndex = Mathf.Clamp(target.Kind == GestureHitKind.BoardCard ? target.TargetIndex : target.Index,
                    0, meldLayout.CardRects.Count);
                center = insertIndex >= meldLayout.CardRects.Count
                    ? meldLayout.CardRects[meldLayout.CardRects.Count - 1].center + new Vector2(step, 0f)
                    : meldLayout.CardRects[insertIndex].center;
            }
            else
            {
                HideCardPreview();
                return;
            }

            EnsurePreviewCards(count);
            previewTargets.Clear();
            for (var index = 0; index < count; index++)
            {
                var cardId = draggedCardIds[index];
                if (!lastCardViews.TryGetValue(cardId, out var source) || source.Renderer == null)
                {
                    previewCards[index].gameObject.SetActive(false);
                    previewTargets.Add(Vector3.zero);
                    continue;
                }

                var preview = previewCards[index];
                if (source.Renderer.sprite == null)
                {
                    preview.gameObject.SetActive(false);
                    previewTargets.Add(Vector3.zero);
                    continue;
                }

                preview.gameObject.SetActive(true);
                preview.sprite = source.Renderer.sprite;
                preview.sortingLayerID = source.Renderer.sortingLayerID;
                preview.sortingOrder = 1000 + index;
                var spriteHeight = preview.sprite.bounds.size.y;
                if (spriteHeight > 0f)
                {
                    preview.transform.localScale = Vector3.one * (cardSize.y / spriteHeight);
                }

                preview.color = new Color(0.08f, 0.1f, 0.08f, 0.34f);
                previewTargets.Add(new Vector3(center.x + index * step, center.y, 0f));
            }

            for (var index = count; index < previewCards.Count; index++)
            {
                previewCards[index].gameObject.SetActive(false);
            }

            if (meldLayout != null)
            {
                for (var cardIndex = insertIndex; cardIndex < meldLayout.CardRects.Count; cardIndex++)
                {
                    var cardId = FindCardId(meldLayout.MeldId, cardIndex);
                    if (cardId < 0 || Contains(liftedCardIds, (byte)cardId) ||
                        !lastCardViews.TryGetValue((byte)cardId, out var cardView))
                    {
                        continue;
                    }

                    cardView.SetSortingOrder(300 + cardIndex);
                    previewShiftedCardIds.Add((byte)cardId);
                    BoardLayout.CardJitter(cardId, meldLayout.MeldId, GameSettings.CardMessiness, cardSize,
                        out var jitter, out _);
                    var destination = meldLayout.CardRects[cardIndex].center + jitter + new Vector2(count * step, 0f);
                    var world = transform.TransformPoint(new Vector3(destination.x, destination.y, 0f));
                    var parent = cardView.transform.parent;
                    cardView.MoveTo(parent == null ? world : parent.InverseTransformPoint(world));
                }
            }

            previewOpacityTarget = isValid ? 1f : 0.55f;
        }

        private int FindCardId(int meldId, int cardIndex)
        {
            if (lastBoard == null)
            {
                return -1;
            }

            for (var meldIndex = 0; meldIndex < lastBoard.Count; meldIndex++)
            {
                var meld = lastBoard[meldIndex];
                if (meld.Id == meldId && cardIndex >= 0 && cardIndex < meld.Cards.Count)
                {
                    return meld.Cards[cardIndex].Id;
                }
            }

            return -1;
        }

        private void EnsurePreviewCards(int count)
        {
            while (previewCards.Count < count)
            {
                var previewObject = new GameObject("Card Drop Shadow");
                previewObject.transform.SetParent(transform, false);
                var renderer = previewObject.AddComponent<SpriteRenderer>();
                renderer.color = new Color(0.08f, 0.1f, 0.08f, 0f);
                previewCards.Add(renderer);
            }
        }

        private void HideCardPreview()
        {
            ResetShiftedCards();
            previewOpacityTarget = 0f;
            if (previewCards.Count == 0)
            {
                return;
            }
        }

        private void ResetShiftedCards()
        {
            if (lastCardViews == null || lastBoard == null || CurrentLayout == null)
            {
                previewShiftedCardIds.Clear();
                return;
            }

            for (var index = 0; index < previewShiftedCardIds.Count; index++)
            {
                var cardId = previewShiftedCardIds[index];
                if (!lastCardViews.TryGetValue(cardId, out var view))
                {
                    continue;
                }

                for (var meldIndex = 0; meldIndex < lastBoard.Count; meldIndex++)
                {
                    var meld = lastBoard[meldIndex];
                    var meldLayout = FindMeldLayout(CurrentLayout, meld.Id);
                    if (meldLayout == null)
                    {
                        continue;
                    }

                    for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                    {
                        if (meld.Cards[cardIndex].Id != cardId)
                        {
                            continue;
                        }

                        view.SetSortingOrder(10 + cardIndex);
                        BoardLayout.CardJitter(cardId, meld.Id, GameSettings.CardMessiness, CurrentLayout.CardSize,
                            out var jitter, out _);
                        var center = meldLayout.CardRects[cardIndex].center + jitter;
                        var world = transform.TransformPoint(new Vector3(center.x, center.y, 0f));
                        var parent = view.transform.parent;
                        view.MoveTo(parent == null ? world : parent.InverseTransformPoint(world));
                        break;
                    }
                }
            }

            previewShiftedCardIds.Clear();
        }

        private void Update()
        {
            previewOpacity = Mathf.MoveTowards(previewOpacity, previewOpacityTarget, Time.unscaledDeltaTime * 5f);
            for (var index = 0; index < previewCards.Count; index++)
            {
                var renderer = previewCards[index];
                if (!renderer.gameObject.activeSelf)
                {
                    continue;
                }

                if (index < previewTargets.Count)
                {
                    var target = previewTargets[index];
                    renderer.transform.localPosition = Vector3.Lerp(renderer.transform.localPosition, target,
                        1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
                }

                var tint = renderer.color;
                tint.a = previewOpacity * 0.34f;
                renderer.color = tint;
                if (previewOpacity <= 0f && previewOpacityTarget <= 0f)
                {
                    renderer.gameObject.SetActive(false);
                }
            }

        }

        public void SetMeldOutlineOffset(int meldId, Vector3 offset)
        {
            if (outlines.TryGetValue(meldId, out var outline))
            {
                outline.transform.localPosition = offset;
            }
        }

        public bool MoveMeldByDelta(int meldId, Vector2 delta)
        {
            if (!GameSettings.OrganicBoard || !organicOrigins.TryGetValue(meldId, out var origin))
            {
                return false;
            }

            organicOrigins[meldId] = origin + delta;
            if (lastBoard != null && lastCardViews != null)
            {
                Layout(lastBoard, lastCardViews, lastViewportAspect, lastCardSize, liftedCardIds);
            }

            return true;
        }

        public BoardLayoutResult Layout(IReadOnlyList<Meld> board, IReadOnlyDictionary<byte, CardView> cardViews,
            float viewportAspect, Vector2 cardSize, ICollection<byte> liftedCardIds = null)
        {
            SetLiftedCardIds(liftedCardIds);
            lastBoard = board;
            lastCardViews = cardViews;
            lastViewportAspect = viewportAspect;
            lastCardSize = cardSize;
            var overlapSets = GameSettings.OverlapSets;
            var organicBoard = GameSettings.OrganicBoard;
            var constrainedMeldIds = FindMeldsWithNewCards(board);
            if (organicBoard && (!hasLastOrganicMode || !lastOrganicMode) && CurrentLayout != null)
            {
                SeedOrganicOrigins(CurrentLayout);
            }

            hasLastOrganicMode = true;
            lastOrganicMode = organicBoard;
            if (board.Count == 0)
            {
                organicOrigins.Clear();
            }

            if (organicBoard)
            {
                staleOriginIds.Clear();
                foreach (var pair in organicOrigins)
                {
                    staleOriginIds.Add(pair.Key);
                }

                for (var index = 0; index < board.Count; index++)
                {
                    staleOriginIds.Remove(board[index].Id);
                }

                for (var index = 0; index < staleOriginIds.Count; index++)
                {
                    organicOrigins.Remove(staleOriginIds[index]);
                }

                var hints = new Dictionary<int, Vector2>();
                if (hasPendingNewMeldPoint)
                {
                    for (var index = 0; index < board.Count; index++)
                    {
                        if (!organicOrigins.ContainsKey(board[index].Id) && board[index].Cards.Count > 0)
                        {
                            hints.Add(board[index].Id, pendingNewMeldPoint);
                            hasPendingNewMeldPoint = false;
                            break;
                        }
                    }
                }

                CurrentLayout = BoardLayout.ComputeOrganic(board, cardSize, overlapSets, organicOrigins, hints,
                    cpuPlacementArea, constrainedMeldIds, out var nextOrigins, GameSettings.BoardCardSpread);
                organicOrigins.Clear();
                foreach (var pair in nextOrigins)
                {
                    organicOrigins.Add(pair.Key, pair.Value);
                }
            }
            else
            {
                CurrentLayout = BoardLayout.Compute(board, viewportAspect, cardSize, overlapSets,
                    GameSettings.BoardCardSpread);
            }

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
                var isValid = MeldValidator.Classify(meld.Cards).IsValid;
                if (overlapSets && isValid)
                {
                    outline.Hide();
                }
                else
                {
                    outline.SetBounds(meldLayout.Bounds, isValid, cardSize);
                }
                for (var cardIndex = 0; cardIndex < meld.Cards.Count && cardIndex < meldLayout.CardRects.Count;
                     cardIndex++)
                {
                    var card = meld.Cards[cardIndex];
                    if (!this.liftedCardIds.Contains(card.Id) && cardViews.TryGetValue(card.Id, out var view))
                    {
                        var center = meldLayout.CardRects[cardIndex].center;
                        BoardLayout.CardJitter(card.Id, meld.Id, GameSettings.CardMessiness, cardSize,
                            out var jitter, out var angle);
                        view.SetLayoutPose(Quaternion.Euler(0f, 0f, angle), 1f);
                        view.SetSortingOrder(10 + cardIndex);
                        var world = transform.TransformPoint(new Vector3(center.x + jitter.x, center.y + jitter.y, 0f));
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
            UpdatePreviousMeldCardIds(board);
            return CurrentLayout;
        }

        private HashSet<int> FindMeldsWithNewCards(IReadOnlyList<Meld> board)
        {
            var constrainedMeldIds = new HashSet<int>();
            for (var meldIndex = 0; meldIndex < board.Count; meldIndex++)
            {
                var meld = board[meldIndex];
                if (meld.Cards.Count == 0)
                {
                    continue;
                }

                if (!previousMeldCardIds.TryGetValue(meld.Id, out var previousCardIds))
                {
                    constrainedMeldIds.Add(meld.Id);
                    continue;
                }

                for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                {
                    if (!previousCardIds.Contains(meld.Cards[cardIndex].Id))
                    {
                        constrainedMeldIds.Add(meld.Id);
                        break;
                    }
                }
            }

            return constrainedMeldIds;
        }

        private void UpdatePreviousMeldCardIds(IReadOnlyList<Meld> board)
        {
            previousMeldCardIds.Clear();
            for (var meldIndex = 0; meldIndex < board.Count; meldIndex++)
            {
                var meld = board[meldIndex];
                var cardIds = new HashSet<byte>();
                for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                {
                    cardIds.Add(meld.Cards[cardIndex].Id);
                }

                previousMeldCardIds.Add(meld.Id, cardIds);
            }
        }

        private void SeedOrganicOrigins(BoardLayoutResult layout)
        {
            organicOrigins.Clear();
            for (var index = 0; index < layout.Melds.Count; index++)
            {
                var meld = layout.Melds[index];
                organicOrigins[meld.MeldId] = meld.Bounds.position;
            }
        }

        private static bool Contains(IReadOnlyCollection<byte> collection, byte cardId)
        {
            if (collection == null)
            {
                return false;
            }

            foreach (var value in collection)
            {
                if (value == cardId)
                {
                    return true;
                }
            }

            return false;
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