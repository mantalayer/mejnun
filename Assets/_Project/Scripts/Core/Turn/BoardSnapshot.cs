using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mejnun.Core
{
    public sealed class BoardSnapshot
    {
        private readonly Card[] hand;
        private readonly Meld[] board;
        private readonly ReadOnlyCollection<Card> readOnlyHand;

        internal BoardSnapshot(GameState state, int seat)
        {
            Seat = seat;
            PlayerPhase = state.PlayerPhases[seat];
            NextMeldId = state.NextMeldId;
            hand = state.Hands[seat].ToArray();
            readOnlyHand = Array.AsReadOnly(hand);
            board = new Meld[state.Board.Count];
            for (var index = 0; index < board.Length; index++)
            {
                board[index] = CopyMeld(state.Board[index]);
            }
        }

        internal BoardSnapshot(int seat, Card[] hand, Meld[] board, PlayerPhase playerPhase, int nextMeldId)
        {
            Seat = seat;
            PlayerPhase = playerPhase;
            NextMeldId = nextMeldId;
            this.hand = (Card[])hand.Clone();
            readOnlyHand = Array.AsReadOnly(this.hand);
            this.board = new Meld[board.Length];
            for (var index = 0; index < board.Length; index++)
            {
                this.board[index] = CopyMeld(board[index]);
            }
        }

        public int Seat { get; }
        public PlayerPhase PlayerPhase { get; }
        public int NextMeldId { get; }
        public IReadOnlyList<Card> Hand => readOnlyHand;
        public IReadOnlyList<Meld> Board
        {
            get
            {
                var copy = new Meld[board.Length];
                for (var index = 0; index < board.Length; index++)
                {
                    copy[index] = CopyMeld(board[index]);
                }

                return Array.AsReadOnly(copy);
            }
        }

        internal Card[] HandCards => hand;
        internal Meld[] BoardMelds => board;

        internal bool HasCardFromHand(byte cardId)
        {
            for (var index = 0; index < hand.Length; index++)
            {
                if (hand[index].Id == cardId)
                {
                    return true;
                }
            }

            return false;
        }

        internal bool HasMeld(int meldId)
        {
            for (var index = 0; index < board.Length; index++)
            {
                if (board[index].Id == meldId)
                {
                    return true;
                }
            }

            return false;
        }

        internal void Restore(GameState state)
        {
            var handAtSeat = state.Hands[Seat];
            handAtSeat.Clear();
            handAtSeat.AddRange(hand);

            state.Board.Clear();
            for (var index = 0; index < board.Length; index++)
            {
                state.Board.Add(CopyMeld(board[index]));
            }

            state.PlayerPhases[Seat] = PlayerPhase;
            state.NextMeldId = NextMeldId;
        }

        internal static Meld CopyMeld(Meld meld)
        {
            return new Meld(meld.Id, meld.Cards);
        }
    }
}