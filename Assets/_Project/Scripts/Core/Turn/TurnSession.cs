using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mejnun.Core
{
    public sealed class TurnSession
    {
        private readonly GameState state;
        private readonly GameEvents events;
        private readonly List<Move> moves;
        private readonly ReadOnlyCollection<Move> readOnlyMoves;

        internal TurnSession(GameState state, GameEvents events)
        {
            this.state = state;
            this.events = events;
            Seat = state.CurrentSeat;
            StartSnapshot = new BoardSnapshot(state, Seat);
            moves = new List<Move>();
            readOnlyMoves = moves.AsReadOnly();
        }

        internal TurnSession(GameState state, GameEvents events, BoardSnapshot startSnapshot, List<Move> moves)
        {
            this.state = state;
            this.events = events;
            Seat = startSnapshot.Seat;
            StartSnapshot = startSnapshot;
            this.moves = new List<Move>(moves);
            readOnlyMoves = this.moves.AsReadOnly();
        }

        public int Seat { get; }
        public BoardSnapshot StartSnapshot { get; }
        public IReadOnlyList<Move> Moves => readOnlyMoves;

        public bool CanEndTurn()
        {
            return MeldValidator.IsBoardValid(state.Board) && HasHandCardOnBoard();
        }

        internal bool HasHandCardOnBoard()
        {
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var cards = state.Board[meldIndex].Cards;
                for (var cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    if (StartSnapshot.HasCardFromHand(cards[cardIndex].Id))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal CommandResult MoveCards(in GameCommand command, bool replay = false)
        {
            var cardIds = command.CardIdsUnsafe;
            if (cardIds.Length == 0)
            {
                return CommandResult.Failure(RuleError.EmptyMove);
            }

            var locations = new CardLocation[cardIds.Length];
            var cards = new Card[cardIds.Length];
            var handCardsMoved = 0;
            for (var index = 0; index < cardIds.Length; index++)
            {
                for (var previous = 0; previous < index; previous++)
                {
                    if (cardIds[previous] == cardIds[index])
                    {
                        return CommandResult.Failure(RuleError.DuplicateCard);
                    }
                }

                if (!TryFindCard(cardIds[index], out cards[index], out locations[index]))
                {
                    return CommandResult.Failure(RuleError.UnknownCard);
                }

                if (locations[index].IsHand)
                {
                    handCardsMoved++;
                }
            }

            var target = command.Target;
            Meld targetMeld = null;
            switch (target.Kind)
            {
                case TargetKind.Hand:
                    for (var index = 0; index < cardIds.Length; index++)
                    {
                        if (!StartSnapshot.HasCardFromHand(cardIds[index]))
                        {
                            return CommandResult.Failure(RuleError.BoardCardToHand);
                        }
                    }

                    if (target.Index < 0 || target.Index > state.Hands[Seat].Count - handCardsMoved)
                    {
                        return CommandResult.Failure(RuleError.InvalidTarget);
                    }

                    break;

                case TargetKind.Meld:
                    targetMeld = FindMeld(target.MeldId);
                    if (targetMeld == null)
                    {
                        return CommandResult.Failure(RuleError.UnknownMeld);
                    }

                    var removedFromTarget = CountFromMeld(locations, target.MeldId);
                    if (target.Index < 0 || target.Index > targetMeld.Cards.Count - removedFromTarget)
                    {
                        return CommandResult.Failure(RuleError.InvalidTarget);
                    }

                    break;

                case TargetKind.NewMeld:
                    if (target.Index != 0)
                    {
                        return CommandResult.Failure(RuleError.InvalidTarget);
                    }

                    break;

                default:
                    return CommandResult.Failure(RuleError.InvalidTarget);
            }

            if (state.PlayerPhases[Seat] == PlayerPhase.NotDown)
            {
                for (var index = 0; index < locations.Length; index++)
                {
                    if (!locations[index].IsHand && StartSnapshot.HasMeld(locations[index].MeldId))
                    {
                        return CommandResult.Failure(RuleError.NotDownBoardAccess);
                    }
                }

                if (target.Kind == TargetKind.Meld && StartSnapshot.HasMeld(target.MeldId))
                {
                    return CommandResult.Failure(RuleError.NotDownBoardAccess);
                }
            }

            var affected = CaptureAffectedMelds(locations, targetMeld);
            for (var index = 0; index < locations.Length; index++)
            {
                RemoveCard(cards[index], locations[index]);
            }

            if (target.Kind == TargetKind.Hand)
            {
                var hand = state.Hands[Seat];
                for (var index = 0; index < cards.Length; index++)
                {
                    hand.Insert(target.Index + index, cards[index]);
                }
            }
            else if (target.Kind == TargetKind.Meld)
            {
                for (var index = 0; index < cards.Length; index++)
                {
                    targetMeld.Cards.Insert(target.Index + index, cards[index]);
                }

                MeldValidator.CanonicalizeStraight(targetMeld.Cards);
            }
            else
            {
                var newMeld = new Meld(state.NextMeldId++, cards);
                state.Board.Add(newMeld);
                targetMeld = newMeld;
            }

            RemoveEmptyMelds(targetMeld);
            EmitAffectedMeldChanges(affected, target.Kind == TargetKind.NewMeld ? targetMeld : null, replay);

            if (!replay)
            {
                for (var index = 0; index < cards.Length; index++)
                {
                    var destination = target.Kind == TargetKind.NewMeld
                        ? CardTarget.Meld(targetMeld.Id, index)
                        : new CardTarget(target.Kind, target.MeldId, target.Index + index);
                    events.RaiseCardMoved(cards[index], locations[index].Target, destination);
                }

                moves.Add(new Move(command));
            }

            TryAutoGoDown(replay);
            return CommandResult.Success();
        }

        internal CommandResult SplitMeld(in GameCommand command, bool replay = false)
        {
            var meld = FindMeld(command.MeldId);
            if (meld == null)
            {
                return CommandResult.Failure(RuleError.UnknownMeld);
            }

            if (state.PlayerPhases[Seat] == PlayerPhase.NotDown && StartSnapshot.HasMeld(meld.Id))
            {
                return CommandResult.Failure(RuleError.NotDownBoardAccess);
            }

            var classification = MeldValidator.Classify(meld.Cards);
            if ((classification.Kind != MeldKind.StraightLow && classification.Kind != MeldKind.StraightHigh) ||
                command.Index <= 0 || command.Index >= meld.Cards.Count)
            {
                return CommandResult.Failure(RuleError.InvalidSplit);
            }

            var before = classification;
            var tail = meld.Cards.GetRange(command.Index, meld.Cards.Count - command.Index);
            meld.Cards.RemoveRange(command.Index, meld.Cards.Count - command.Index);
            MeldValidator.CanonicalizeStraight(meld.Cards);
            var newMeld = new Meld(state.NextMeldId++, tail);
            state.Board.Add(newMeld);

            if (!replay)
            {
                EmitTransition(meld.Id, before, MeldValidator.Classify(meld.Cards));
                EmitNewMeld(newMeld);
                for (var index = 0; index < tail.Count; index++)
                {
                    events.RaiseCardMoved(tail[index], CardTarget.Meld(meld.Id, command.Index + index),
                        CardTarget.Meld(newMeld.Id, index));
                }

                moves.Add(new Move(command));
            }

            TryAutoGoDown(replay);
            return CommandResult.Success();
        }

        internal CommandResult MergeMelds(in GameCommand command, bool replay = false)
        {
            if (command.MeldId == command.OtherMeldId)
            {
                return CommandResult.Failure(RuleError.InvalidMerge);
            }

            var meld = FindMeld(command.MeldId);
            var otherMeld = FindMeld(command.OtherMeldId);
            if (meld == null || otherMeld == null)
            {
                return CommandResult.Failure(RuleError.UnknownMeld);
            }

            if (state.PlayerPhases[Seat] == PlayerPhase.NotDown &&
                (StartSnapshot.HasMeld(meld.Id) || StartSnapshot.HasMeld(otherMeld.Id)))
            {
                return CommandResult.Failure(RuleError.NotDownBoardAccess);
            }

            var before = MeldValidator.Classify(meld.Cards);
            var originalCount = meld.Cards.Count;
            for (var index = 0; index < otherMeld.Cards.Count; index++)
            {
                meld.Cards.Add(otherMeld.Cards[index]);
            }

            state.Board.Remove(otherMeld);
            MeldValidator.CanonicalizeStraight(meld.Cards);

            if (!replay)
            {
                EmitTransition(meld.Id, before, MeldValidator.Classify(meld.Cards));
                for (var index = 0; index < otherMeld.Cards.Count; index++)
                {
                    events.RaiseCardMoved(otherMeld.Cards[index], CardTarget.Meld(otherMeld.Id, index),
                        CardTarget.Meld(meld.Id, originalCount + index));
                }

                moves.Add(new Move(command));
            }

            TryAutoGoDown(replay);
            return CommandResult.Success();
        }

        internal CommandResult GoDown(in GameCommand command, bool replay = false)
        {
            if (!CanGoDown())
            {
                return CommandResult.Failure(RuleError.GoDownInvalid);
            }

            state.PlayerPhases[Seat] = PlayerPhase.Down;
            if (!replay)
            {
                events.RaiseWentDown(Seat);
                moves.Add(new Move(command));
            }

            return CommandResult.Success();
        }

        internal CommandResult GoDownImplicit()
        {
            if (!CanGoDown())
            {
                return CommandResult.Failure(RuleError.GoDownInvalid);
            }

            state.PlayerPhases[Seat] = PlayerPhase.Down;
            events.RaiseWentDown(Seat);
            return CommandResult.Success();
        }

        internal CommandResult Undo()
        {
            if (moves.Count == 0)
            {
                return CommandResult.Failure(RuleError.NothingToUndo);
            }

            moves.RemoveAt(moves.Count - 1);
            Rebuild();
            events.RaiseUndone(Seat);
            return CommandResult.Success();
        }

        internal CommandResult Revert()
        {
            StartSnapshot.Restore(state);
            moves.Clear();
            events.RaiseBoardReverted(Seat);
            return CommandResult.Success();
        }

        internal void RevertForTurnEnd()
        {
            var hadMoves = moves.Count > 0;
            StartSnapshot.Restore(state);
            moves.Clear();
            if (hadMoves)
            {
                events.RaiseBoardReverted(Seat);
            }
        }

        internal bool CanGoDown()
        {
            if (state.PlayerPhases[Seat] != PlayerPhase.NotDown)
            {
                return false;
            }

            var hasNewMeld = false;
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var meld = state.Board[meldIndex];
                if (StartSnapshot.HasMeld(meld.Id))
                {
                    continue;
                }

                hasNewMeld = true;
                if (!MeldValidator.Classify(meld.Cards).IsValid)
                {
                    return false;
                }

                for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                {
                    if (!StartSnapshot.HasCardFromHand(meld.Cards[cardIndex].Id))
                    {
                        return false;
                    }
                }
            }

            return hasNewMeld;
        }

        internal IReadOnlyList<int> NewMeldIds()
        {
            var meldIds = new List<int>();
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var meld = state.Board[meldIndex];
                if (!StartSnapshot.HasMeld(meld.Id))
                {
                    meldIds.Add(meld.Id);
                }
            }

            return meldIds;
        }

        private void TryAutoGoDown(bool replay)
        {
            if (state.PlayerPhases[Seat] != PlayerPhase.NotDown || !CanGoDown())
            {
                return;
            }

            state.PlayerPhases[Seat] = PlayerPhase.Down;
            if (!replay)
            {
                events.RaiseWentDown(Seat);
            }
        }

        private void Rebuild()
        {
            StartSnapshot.Restore(state);
            for (var index = 0; index < moves.Count; index++)
            {
                var command = moves[index].Command;
                switch (command.Type)
                {
                    case CommandType.MoveCards:
                        MoveCards(command, true);
                        break;
                    case CommandType.SplitMeld:
                        SplitMeld(command, true);
                        break;
                    case CommandType.MergeMelds:
                        MergeMelds(command, true);
                        break;
                    case CommandType.GoDown:
                        GoDown(command, true);
                        break;
                }
            }
        }

        private bool TryFindCard(byte cardId, out Card card, out CardLocation location)
        {
            var hand = state.Hands[Seat];
            for (var index = 0; index < hand.Count; index++)
            {
                if (hand[index].Id == cardId)
                {
                    card = hand[index];
                    location = new CardLocation(true, 0, index);
                    return true;
                }
            }

            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var meld = state.Board[meldIndex];
                for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                {
                    if (meld.Cards[cardIndex].Id == cardId)
                    {
                        card = meld.Cards[cardIndex];
                        location = new CardLocation(false, meld.Id, cardIndex);
                        return true;
                    }
                }
            }

            card = default;
            location = default;
            return false;
        }

        private Meld FindMeld(int meldId)
        {
            for (var index = 0; index < state.Board.Count; index++)
            {
                if (state.Board[index].Id == meldId)
                {
                    return state.Board[index];
                }
            }

            return null;
        }

        private static int CountFromMeld(CardLocation[] locations, int meldId)
        {
            var count = 0;
            for (var index = 0; index < locations.Length; index++)
            {
                if (!locations[index].IsHand && locations[index].MeldId == meldId)
                {
                    count++;
                }
            }

            return count;
        }

        private void RemoveCard(Card card, CardLocation location)
        {
            if (location.IsHand)
            {
                state.Hands[Seat].Remove(card);
                return;
            }

            var meld = FindMeld(location.MeldId);
            meld.Cards.Remove(card);
        }

        private List<MeldChange> CaptureAffectedMelds(CardLocation[] locations, Meld targetMeld)
        {
            var affected = new List<MeldChange>();
            for (var index = 0; index < locations.Length; index++)
            {
                if (locations[index].IsHand)
                {
                    continue;
                }

                var meld = FindMeld(locations[index].MeldId);
                AddAffected(affected, meld);
            }

            AddAffected(affected, targetMeld);
            return affected;
        }

        private static void AddAffected(List<MeldChange> affected, Meld meld)
        {
            if (meld == null)
            {
                return;
            }

            for (var index = 0; index < affected.Count; index++)
            {
                if (affected[index].MeldId == meld.Id)
                {
                    return;
                }
            }

            affected.Add(new MeldChange(meld.Id, MeldValidator.Classify(meld.Cards)));
        }

        private void RemoveEmptyMelds(Meld except)
        {
            for (var index = state.Board.Count - 1; index >= 0; index--)
            {
                if (state.Board[index].Cards.Count == 0 && state.Board[index] != except)
                {
                    state.Board.RemoveAt(index);
                }
            }
        }

        private void EmitAffectedMeldChanges(List<MeldChange> affected, Meld newMeld, bool replay)
        {
            if (replay)
            {
                return;
            }

            for (var index = 0; index < affected.Count; index++)
            {
                var meld = FindMeld(affected[index].MeldId);
                if (meld != null)
                {
                    EmitTransition(meld.Id, affected[index].Classification, MeldValidator.Classify(meld.Cards));
                }
            }

            if (newMeld != null)
            {
                EmitNewMeld(newMeld);
            }
        }

        private void EmitNewMeld(Meld meld)
        {
            var classification = MeldValidator.Classify(meld.Cards);
            if (classification.IsValid)
            {
                events.RaiseMeldBecameValid(meld.Id, classification.Kind);
            }
            else
            {
                events.RaiseMeldBecameInvalid(meld.Id, classification.Reason);
            }
        }

        private void EmitTransition(int meldId, MeldClassification before, MeldClassification after)
        {
            if (before.IsValid == after.IsValid)
            {
                return;
            }

            if (after.IsValid)
            {
                events.RaiseMeldBecameValid(meldId, after.Kind);
            }
            else
            {
                events.RaiseMeldBecameInvalid(meldId, after.Reason);
            }
        }

        private readonly struct CardLocation
        {
            public CardLocation(bool isHand, int meldId, int index)
            {
                IsHand = isHand;
                MeldId = meldId;
                Index = index;
            }

            public bool IsHand { get; }
            public int MeldId { get; }
            public int Index { get; }
            public CardTarget Target => IsHand ? CardTarget.Hand(Index) : CardTarget.Meld(MeldId, Index);
        }

        private readonly struct MeldChange
        {
            public MeldChange(int meldId, MeldClassification classification)
            {
                MeldId = meldId;
                Classification = classification;
            }

            public int MeldId { get; }
            public MeldClassification Classification { get; }
        }
    }
}