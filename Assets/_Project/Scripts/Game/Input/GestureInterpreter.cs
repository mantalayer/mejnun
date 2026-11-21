using System.Collections.Generic;
using Mejnun.AI;
using Mejnun.Core;

namespace Mejnun.Game
{
    public enum GestureType
    {
        Tap,
        DoubleTap,
        Drag,
        LongPressDrag
    }

    public enum GestureHitKind
    {
        None,
        HandCard,
        BoardCard,
        MeldGrip,
        MeldGap,
        HandArea,
        EmptyBoard,
        HandSlot
    }

    public readonly struct GestureHit
    {
        private GestureHit(GestureHitKind kind, byte cardId, int meldId, int index, int targetIndex)
        {
            Kind = kind;
            CardId = cardId;
            MeldId = meldId;
            Index = index;
            TargetIndex = targetIndex;
        }

        public GestureHitKind Kind { get; }
        public byte CardId { get; }
        public int MeldId { get; }
        public int Index { get; }
        public int TargetIndex { get; }

        public static GestureHit None => default;

        public static GestureHit HandCard(byte cardId, int handIndex)
        {
            return new GestureHit(GestureHitKind.HandCard, cardId, 0, handIndex, handIndex);
        }

        public static GestureHit BoardCard(byte cardId, int meldId, int cardIndex, int targetIndex)
        {
            return new GestureHit(GestureHitKind.BoardCard, cardId, meldId, cardIndex, targetIndex);
        }

        public static GestureHit MeldGrip(int meldId, int boardIndex)
        {
            return new GestureHit(GestureHitKind.MeldGrip, 0, meldId, boardIndex, boardIndex);
        }

        public static GestureHit MeldGap(int meldId, int insertIndex)
        {
            return new GestureHit(GestureHitKind.MeldGap, 0, meldId, insertIndex, insertIndex);
        }

        public static GestureHit HandArea(int handIndex)
        {
            return new GestureHit(GestureHitKind.HandArea, 0, 0, handIndex, handIndex);
        }

        public static GestureHit HandSlot(int slotIndex)
        {
            return new GestureHit(GestureHitKind.HandSlot, 0, 0, slotIndex, slotIndex);
        }

        public static GestureHit EmptyBoard()
        {
            return new GestureHit(GestureHitKind.EmptyBoard, 0, 0, 0, 0);
        }
    }

    public readonly struct GestureInput
    {
        public GestureInput(GestureType type, GestureHit source, GestureHit target)
        {
            Type = type;
            Source = source;
            Target = target;
        }

        public GestureType Type { get; }
        public GestureHit Source { get; }
        public GestureHit Target { get; }
    }

    public sealed class GestureResult
    {
        internal GestureResult(CommandBatch batch)
        {
            Batch = batch;
        }

        public CommandBatch Batch { get; }
        public bool FitBoardRequested { get; internal set; }
        public bool SelectionChanged { get; internal set; }
        public bool ClearSelectionOnSuccess { get; internal set; }
        public bool HandOrderChanged { get; internal set; }
        public bool Denied { get; internal set; }
        public byte ReorderedCardId { get; internal set; }
        public IReadOnlyList<byte> ReorderedCardIds { get; internal set; }
        public int ReorderTargetIndex { get; internal set; }
        public bool ReorderTargetIsSlot { get; internal set; }
    }

    public readonly struct SelectionSnapshot
    {
        private readonly byte[] cardIds;

        internal SelectionSnapshot(IReadOnlyList<byte> selectedCardIds, int selectedMeldId)
        {
            cardIds = new byte[selectedCardIds.Count];
            for (var index = 0; index < selectedCardIds.Count; index++)
            {
                cardIds[index] = selectedCardIds[index];
            }

            BoardMeldId = selectedMeldId;
        }

        public IReadOnlyList<byte> CardIds => cardIds ?? new byte[0];
        public int BoardMeldId { get; }
    }

    public sealed class GestureInterpreter
    {
        private readonly List<byte> selectedCardIds = new List<byte>(Card.CardsPerDeck);
        private int selectedMeldId = -1;

        public IReadOnlyList<byte> SelectedCardIds => selectedCardIds;
        public int SelectedBoardMeldId => selectedMeldId;
        public bool IsBoardSelection => selectedMeldId >= 0;

        public GestureResult Interpret(GestureInput input, GameState state, TurnSession turn)
        {
            var result = new GestureResult(new CommandBatch());
            if (state == null || turn == null || state.CurrentSeat < 0)
            {
                return result;
            }

            switch (input.Type)
            {
                case GestureType.Tap:
                    InterpretTap(input, state, turn, result);
                    break;
                case GestureType.DoubleTap:
                    InterpretDoubleTap(input, state, turn, result);
                    break;
                case GestureType.Drag:
                case GestureType.LongPressDrag:
                    InterpretDrag(input, state, turn, result);
                    break;
            }

            return result;
        }

        public GestureResult InterpretHandGesture(GestureInput input, int handCount)
        {
            var result = new GestureResult(new CommandBatch());
            if (input.Type == GestureType.Tap && input.Source.Kind == GestureHitKind.HandCard)
            {
                ToggleCardSelection(input.Source.CardId);
                result.SelectionChanged = true;
                return result;
            }

            if ((input.Type != GestureType.Drag && input.Type != GestureType.LongPressDrag) ||
                input.Source.Kind != GestureHitKind.HandCard ||
                (input.Target.Kind != GestureHitKind.HandCard && input.Target.Kind != GestureHitKind.HandArea &&
                 input.Target.Kind != GestureHitKind.HandSlot))
            {
                return result;
            }

            result.HandOrderChanged = true;
            result.ReorderedCardId = input.Source.CardId;
            result.ReorderedCardIds = CopySelectedCardIds(input.Source.CardId);
            result.ReorderTargetIsSlot = input.Target.Kind == GestureHitKind.HandSlot;
            result.ReorderTargetIndex = result.ReorderTargetIsSlot
                ? input.Target.Index
                : input.Target.Kind == GestureHitKind.HandArea
                    ? handCount - 1
                    : input.Target.Index;
            return result;
        }

        public bool IsSelected(byte cardId)
        {
            return selectedCardIds.Contains(cardId);
        }

        public bool SelectHandRange(IReadOnlyList<byte> orderedHandIds, int anchorIndex, int currentIndex)
        {
            if (orderedHandIds == null || anchorIndex < 0 || anchorIndex >= orderedHandIds.Count ||
                currentIndex < 0 || currentIndex >= orderedHandIds.Count)
            {
                return false;
            }

            var switchedFromBoard = IsBoardSelection;
            if (switchedFromBoard)
            {
                selectedCardIds.Clear();
                selectedMeldId = -1;
            }

            var firstIndex = anchorIndex < currentIndex ? anchorIndex : currentIndex;
            var lastIndex = anchorIndex > currentIndex ? anchorIndex : currentIndex;
            var rangeCount = lastIndex - firstIndex + 1;
            var changed = switchedFromBoard || selectedCardIds.Count != rangeCount;
            for (var index = 0; !changed && index < rangeCount; index++)
            {
                changed = selectedCardIds[index] != orderedHandIds[firstIndex + index];
            }

            if (!changed)
            {
                return false;
            }

            selectedCardIds.Clear();
            for (var index = firstIndex; index <= lastIndex; index++)
            {
                selectedCardIds.Add(orderedHandIds[index]);
            }

            return true;
        }

        public byte[] GetSelectedBoardCardIds(GestureHit source, GameState state)
        {
            if (!IsBoardSelection || source.Kind != GestureHitKind.BoardCard || source.MeldId != selectedMeldId ||
                state == null)
            {
                return new byte[0];
            }

            var meld = FindMeld(state.Board, selectedMeldId);
            if (meld == null)
            {
                return new byte[0];
            }

            var ids = new List<byte>(selectedCardIds.Count);
            for (var index = 0; index < meld.Cards.Count; index++)
            {
                var cardId = meld.Cards[index].Id;
                if (selectedCardIds.Contains(cardId))
                {
                    ids.Add(cardId);
                }
            }

            return ids.ToArray();
        }

        public SelectionSnapshot CaptureSelection()
        {
            return new SelectionSnapshot(selectedCardIds, selectedMeldId);
        }

        public void RestoreSelection(SelectionSnapshot snapshot)
        {
            selectedCardIds.Clear();
            for (var index = 0; index < snapshot.CardIds.Count; index++)
            {
                selectedCardIds.Add(snapshot.CardIds[index]);
            }

            selectedMeldId = selectedCardIds.Count > 0 ? snapshot.BoardMeldId : -1;
        }

        public bool ToggleBoardCardSelection(GestureHit hit)
        {
            if (hit.Kind != GestureHitKind.BoardCard)
            {
                return false;
            }

            if (!IsBoardSelection || selectedMeldId != hit.MeldId)
            {
                selectedCardIds.Clear();
                selectedMeldId = hit.MeldId;
            }

            ToggleSelectedCard(hit.CardId);
            if (selectedCardIds.Count == 0)
            {
                selectedMeldId = -1;
            }

            return true;
        }

        public byte[] GetLongPressCardIds(GestureHit source, GameState state)
        {
            if (source.Kind == GestureHitKind.HandCard)
            {
                return CopySelectedCardIds(source.CardId);
            }

            if (source.Kind != GestureHitKind.BoardCard || state == null)
            {
                return new byte[0];
            }

            var meld = FindMeld(state.Board, source.MeldId);
            if (meld == null || source.Index < 0 || source.Index >= meld.Cards.Count)
            {
                return new byte[0];
            }

            var classification = MeldValidator.Classify(meld.Cards);
            return GetLongPressCardIds(meld, source.Index, classification.Kind);
        }

        public void Complete(GestureResult result, bool succeeded)
        {
            if (result != null && succeeded && result.ClearSelectionOnSuccess && selectedCardIds.Count > 0)
            {
                selectedCardIds.Clear();
                selectedMeldId = -1;
            }
        }

        public void ClearSelection()
        {
            selectedCardIds.Clear();
            selectedMeldId = -1;
        }

        private void InterpretTap(GestureInput input, GameState state, TurnSession turn, GestureResult result)
        {
            if (input.Source.Kind == GestureHitKind.HandCard)
            {
                ToggleCardSelection(input.Source.CardId);
                result.SelectionChanged = true;
                return;
            }

            var targetHit = input.Target.Kind == GestureHitKind.None ? input.Source : input.Target;
            if (targetHit.Kind == GestureHitKind.BoardCard &&
                (selectedCardIds.Count == 0 || IsBoardSelection))
            {
                ToggleBoardCardSelection(targetHit);
                result.SelectionChanged = true;
                return;
            }

            if (selectedCardIds.Count == 0 || !TryCreateTarget(targetHit, state, turn, out var target))
            {
                return;
            }

            var selectedIds = CopySelectedCardIds();
            if (IsBoardSelection && target.Kind == TargetKind.Hand && !AllFromStartHand(turn, selectedIds))
            {
                result.Denied = true;
                return;
            }

            AddMove(result.Batch, state.CurrentSeat, selectedIds, target);
            result.ClearSelectionOnSuccess = true;
        }

        private void InterpretDoubleTap(GestureInput input, GameState state, TurnSession turn,
            GestureResult result)
        {
            if (input.Source.Kind == GestureHitKind.EmptyBoard)
            {
                result.FitBoardRequested = true;
                return;
            }

            if (input.Source.Kind == GestureHitKind.HandCard)
            {
                var ids = CopySelectedCardIds(input.Source.CardId);
                if (ids.Length > 1)
                {
                    if (TryPlaceSelection(ids, state, turn, result.Batch))
                    {
                        result.ClearSelectionOnSuccess = true;
                    }

                    return;
                }

                if (state.Board.Count == 0)
                {
                    result.FitBoardRequested = true;
                    return;
                }

                var card = Card.FromId(input.Source.CardId);
                if (TryFindQuickPlaceTarget(card, state, turn, out var target))
                {
                    result.Batch.Add(GameCommand.MoveCards(state.CurrentSeat, new[] { card.Id }, target));
                    result.ClearSelectionOnSuccess = true;
                }

                return;
            }

            if (input.Source.Kind == GestureHitKind.BoardCard &&
                IsCardInStartHand(turn, input.Source.CardId))
            {
                result.Batch.Add(GameCommand.MoveCards(state.CurrentSeat, new[] { input.Source.CardId },
                    CardTarget.Hand(state.Hands[state.CurrentSeat].Count)));
                result.ClearSelectionOnSuccess = true;
            }
        }

        private static bool TryPlaceSelection(IReadOnlyList<byte> selectedIds, GameState state, TurnSession turn,
            CommandBatch batch)
        {
            var seat = state.CurrentSeat;
            var hand = state.Hands[seat];
            var selectedCards = new List<Card>(selectedIds.Count);
            for (var selectedIndex = 0; selectedIndex < selectedIds.Count; selectedIndex++)
            {
                var found = false;
                for (var handIndex = 0; handIndex < hand.Count; handIndex++)
                {
                    if (hand[handIndex].Id == selectedIds[selectedIndex])
                    {
                        selectedCards.Add(hand[handIndex]);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            if (MeldValidator.Classify(selectedCards).IsValid)
            {
                MeldValidator.CanonicalizeStraight(selectedCards);
                batch.Add(GameCommand.MoveCards(seat, CardIds(selectedCards), CardTarget.NewMeld(0)));
                return true;
            }

            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var meld = state.Board[meldIndex];
                if (turn.StartSnapshot.PlayerPhase == PlayerPhase.NotDown && WasOnStartBoard(turn, meld.Id))
                {
                    continue;
                }

                var combined = new List<Card>(meld.Cards.Count + selectedCards.Count);
                combined.AddRange(meld.Cards);
                combined.AddRange(selectedCards);
                if (MeldValidator.Classify(combined).IsValid)
                {
                    batch.Add(GameCommand.MoveCards(seat, CardIds(selectedCards),
                        CardTarget.Meld(meld.Id, meld.Cards.Count)));
                    return true;
                }

                var solution = MeldSolver.Solve(combined, new Card[0]);
                if (!solution.Feasible || solution.BudgetExceeded || solution.Melds.Count != 1)
                {
                    continue;
                }

                var ordered = new List<Card>(solution.Melds[0]);
                MeldValidator.CanonicalizeStraight(ordered);
                if (!TryGetContiguousInsertion(ordered, meld, selectedIds, out var insertionIndex,
                        out var insertionIds))
                {
                    continue;
                }

                batch.Add(GameCommand.MoveCards(seat, insertionIds, CardTarget.Meld(meld.Id, insertionIndex)));
                return true;
            }

            var partition = MeldSolver.Solve(selectedCards, new Card[0]);
            if (!partition.Feasible || partition.BudgetExceeded || partition.Melds.Count == 0)
            {
                return false;
            }

            for (var meldIndex = 0; meldIndex < partition.Melds.Count; meldIndex++)
            {
                var cards = new List<Card>(partition.Melds[meldIndex]);
                if (!MeldValidator.Classify(cards).IsValid)
                {
                    return false;
                }

                MeldValidator.CanonicalizeStraight(cards);
                batch.Add(GameCommand.MoveCards(seat, CardIds(cards), CardTarget.NewMeld(0)));
            }

            return true;
        }

        private static bool TryGetContiguousInsertion(IReadOnlyList<Card> ordered, Meld meld,
            IReadOnlyList<byte> selectedIds, out int insertionIndex, out byte[] insertionIds)
        {
            var added = new List<byte>(selectedIds.Count);
            insertionIndex = 0;
            var existingIndex = 0;
            var hasAdded = false;
            var passedAdded = false;
            for (var index = 0; index < ordered.Count; index++)
            {
                var card = ordered[index];
                if (Contains(selectedIds, card.Id))
                {
                    if (passedAdded)
                    {
                        insertionIds = null;
                        return false;
                    }

                    if (!hasAdded)
                    {
                        insertionIndex = existingIndex;
                        hasAdded = true;
                    }

                    added.Add(card.Id);
                    continue;
                }

                if (existingIndex >= meld.Cards.Count || meld.Cards[existingIndex].Id != card.Id)
                {
                    insertionIds = null;
                    return false;
                }

                existingIndex++;
                if (hasAdded)
                {
                    passedAdded = true;
                }
            }

            if (!hasAdded || existingIndex != meld.Cards.Count || added.Count != selectedIds.Count)
            {
                insertionIds = null;
                return false;
            }

            insertionIds = added.ToArray();
            return true;
        }

        private static bool Contains(IReadOnlyList<byte> ids, byte id)
        {
            for (var index = 0; index < ids.Count; index++)
            {
                if (ids[index] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static byte[] CardIds(IReadOnlyList<Card> cards)
        {
            var ids = new byte[cards.Count];
            for (var index = 0; index < cards.Count; index++)
            {
                ids[index] = cards[index].Id;
            }

            return ids;
        }

        private void InterpretDrag(GestureInput input, GameState state, TurnSession turn, GestureResult result)
        {
            if (input.Source.Kind == GestureHitKind.HandCard &&
                (input.Target.Kind == GestureHitKind.HandCard || input.Target.Kind == GestureHitKind.HandArea ||
                 input.Target.Kind == GestureHitKind.HandSlot))
            {
                result.HandOrderChanged = true;
                result.ReorderedCardId = input.Source.CardId;
                result.ReorderedCardIds = CopySelectedCardIds(input.Source.CardId);
                result.ReorderTargetIsSlot = input.Target.Kind == GestureHitKind.HandSlot;
                result.ReorderTargetIndex = result.ReorderTargetIsSlot
                    ? input.Target.Index
                    : input.Target.Kind == GestureHitKind.HandArea
                        ? state.Hands[state.CurrentSeat].Count - 1
                        : input.Target.Index;
                return;
            }

            if (!TryCreateTarget(input.Target, state, turn, out var target))
            {
                return;
            }

            var seat = state.CurrentSeat;
            if (input.Source.Kind == GestureHitKind.HandCard)
            {
                var ids = CopySelectedCardIds(input.Source.CardId);
                AddMove(result.Batch, seat, ids, target);
                result.ClearSelectionOnSuccess = true;
                return;
            }

            if (input.Source.Kind != GestureHitKind.BoardCard)
            {
                return;
            }

            var meld = FindMeld(state.Board, input.Source.MeldId);
            if (meld == null || input.Source.Index < 0 || input.Source.Index >= meld.Cards.Count)
            {
                return;
            }

            var classification = MeldValidator.Classify(meld.Cards);
            var selectedBoardIds = GetSelectedBoardCardIds(input.Source, state);
            if (selectedBoardIds.Length > 1 && IsSelected(input.Source.CardId))
            {
                if (target.Kind == TargetKind.Hand && !AllFromStartHand(turn, selectedBoardIds))
                {
                    result.Denied = true;
                    return;
                }

                AddMove(result.Batch, seat, selectedBoardIds,
                    ClampSameMeldTarget(target, meld, selectedBoardIds.Length));
                result.ClearSelectionOnSuccess = true;
                return;
            }

            if (input.Type == GestureType.LongPressDrag)
            {
                var ids = GetLongPressCardIds(meld, input.Source.Index, classification.Kind);
                if (target.Kind == TargetKind.Hand)
                {
                    if (!AllFromStartHand(turn, ids))
                    {
                        result.Denied = true;
                        return;
                    }
                }

                AddMove(result.Batch, seat, ids, ClampSameMeldTarget(target, meld, ids.Length));
                result.ClearSelectionOnSuccess = true;
                return;
            }

            if (target.Kind == TargetKind.Hand && !IsCardInStartHand(turn, input.Source.CardId))
            {
                result.Denied = true;
                return;
            }

            var targetIsSourceMeld = target.Kind == TargetKind.Meld && target.MeldId == meld.Id;
            if (!targetIsSourceMeld &&
                (classification.Kind == MeldKind.StraightLow || classification.Kind == MeldKind.StraightHigh) &&
                input.Source.Index > 0 && input.Source.Index < meld.Cards.Count - 1)
            {
                result.Batch.Add(GameCommand.SplitMeld(seat, meld.Id, input.Source.Index));
            }

            AddMove(result.Batch, seat, new[] { input.Source.CardId }, ClampSameMeldTarget(target, meld, 1));
            result.ClearSelectionOnSuccess = true;
        }

        private bool TryCreateTarget(GestureHit hit, GameState state, TurnSession turn, out CardTarget target)
        {
            switch (hit.Kind)
            {
                case GestureHitKind.BoardCard:
                    target = CardTarget.Meld(hit.MeldId, hit.TargetIndex);
                    return true;
                case GestureHitKind.MeldGap:
                    target = CardTarget.Meld(hit.MeldId, hit.Index);
                    return true;
                case GestureHitKind.EmptyBoard:
                    target = CardTarget.NewMeld(0);
                    return true;
                case GestureHitKind.HandCard:
                    target = CardTarget.Hand(hit.Index);
                    return true;
                case GestureHitKind.HandArea:
                    target = CardTarget.Hand(state.Hands[state.CurrentSeat].Count);
                    return true;
                default:
                    target = default;
                    return false;
            }
        }

        private bool TryFindQuickPlaceTarget(Card card, GameState state, TurnSession turn, out CardTarget target)
        {
            var isNotDown = state.PlayerPhases[state.CurrentSeat] == PlayerPhase.NotDown;
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var meld = state.Board[meldIndex];
                if (isNotDown && WasOnStartBoard(turn, meld.Id))
                {
                    continue;
                }

                var candidateCards = new List<Card>(meld.Cards.Count + 1);
                for (var insertIndex = 0; insertIndex <= meld.Cards.Count; insertIndex++)
                {
                    candidateCards.Clear();
                    for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                    {
                        if (cardIndex == insertIndex)
                        {
                            candidateCards.Add(card);
                        }

                        candidateCards.Add(meld.Cards[cardIndex]);
                    }

                    if (insertIndex == meld.Cards.Count)
                    {
                        candidateCards.Add(card);
                    }

                    if (MeldValidator.Classify(candidateCards).IsValid)
                    {
                        target = CardTarget.Meld(meld.Id, insertIndex);
                        return true;
                    }
                }
            }

            target = default;
            return false;
        }

        private static byte[] GetLongPressCardIds(Meld meld, int cardIndex, MeldKind kind)
        {
            var firstIndex = kind == MeldKind.StraightLow || kind == MeldKind.StraightHigh
                ? cardIndex > 0 ? cardIndex : 0
                : 0;
            var ids = new byte[meld.Cards.Count - firstIndex];
            for (var index = firstIndex; index < meld.Cards.Count; index++)
            {
                ids[index - firstIndex] = meld.Cards[index].Id;
            }

            return ids;
        }

        private static void AddMove(CommandBatch batch, int seat, byte[] cardIds, CardTarget target)
        {
            if (cardIds.Length > 0)
            {
                batch.Add(GameCommand.MoveCards(seat, cardIds, target));
            }
        }

        private static CardTarget ClampSameMeldTarget(CardTarget target, Meld sourceMeld, int movedCount)
        {
            if (target.Kind != TargetKind.Meld || target.MeldId != sourceMeld.Id)
            {
                return target;
            }

            var maxIndex = sourceMeld.Cards.Count - movedCount;
            return target.Index > maxIndex ? CardTarget.Meld(target.MeldId, maxIndex) : target;
        }

        private static Meld FindMeld(IReadOnlyList<Meld> melds, int meldId)
        {
            for (var index = 0; index < melds.Count; index++)
            {
                if (melds[index].Id == meldId)
                {
                    return melds[index];
                }
            }

            return null;
        }

        private static bool IsCardInStartHand(TurnSession turn, byte cardId)
        {
            var startHand = turn.StartSnapshot.Hand;
            for (var index = 0; index < startHand.Count; index++)
            {
                if (startHand[index].Id == cardId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AllFromStartHand(TurnSession turn, IReadOnlyList<byte> cardIds)
        {
            for (var index = 0; index < cardIds.Count; index++)
            {
                if (!IsCardInStartHand(turn, cardIds[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool WasOnStartBoard(TurnSession turn, int meldId)
        {
            var startBoard = turn.StartSnapshot.Board;
            for (var index = 0; index < startBoard.Count; index++)
            {
                if (startBoard[index].Id == meldId)
                {
                    return true;
                }
            }

            return false;
        }

        private byte[] CopySelectedCardIds()
        {
            return selectedCardIds.ToArray();
        }

        private byte[] CopySelectedCardIds(byte additionalCardId)
        {
            var ids = new List<byte>(selectedCardIds.Count + 1);
            for (var index = 0; index < selectedCardIds.Count; index++)
            {
                ids.Add(selectedCardIds[index]);
            }

            if (!ids.Contains(additionalCardId))
            {
                ids.Add(additionalCardId);
            }

            return ids.ToArray();
        }

        public void ToggleCardSelection(byte cardId)
        {
            if (IsBoardSelection)
            {
                selectedCardIds.Clear();
                selectedMeldId = -1;
            }

            ToggleSelectedCard(cardId);
        }

        private void ToggleSelectedCard(byte cardId)
        {
            var index = selectedCardIds.IndexOf(cardId);
            if (index >= 0)
            {
                selectedCardIds.RemoveAt(index);
            }
            else
            {
                selectedCardIds.Add(cardId);
            }
        }
    }
}