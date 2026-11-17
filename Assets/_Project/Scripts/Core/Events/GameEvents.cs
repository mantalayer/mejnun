using System;

namespace Mejnun.Core
{
    public sealed class GameEvents
    {
        public event Action<int> DealerChosen;
        public event Action<string, string> DeckFacesAssigned;
        public event Action<int, Card> CardDealt;
        public event Action<int, int> TurnStarted;
        public event Action<Card, CardTarget, CardTarget> CardMoved;
        public event Action<int, MeldKind> MeldBecameValid;
        public event Action<int, InvalidReason> MeldBecameInvalid;
        public event Action<int> WentDown;
        public event Action<int> Undone;
        public event Action<int> BoardReverted;
        public event Action<int, Card> Drew;
        public event Action<int> Passed;
        public event Action<int, int> TurnEnded;
        public event Action<GameResult> Won;
        public event Action<GameResult> Stalemate;

        internal void RaiseDealerChosen(int seat)
        {
            DealerChosen?.Invoke(seat);
        }

        internal void RaiseDeckFacesAssigned(string deckZero, string deckOne)
        {
            DeckFacesAssigned?.Invoke(deckZero, deckOne);
        }

        internal void RaiseCardDealt(int seat, Card card)
        {
            CardDealt?.Invoke(seat, card);
        }

        internal void RaiseTurnStarted(int seat, int turnNumber)
        {
            TurnStarted?.Invoke(seat, turnNumber);
        }

        internal void RaiseCardMoved(Card card, CardTarget from, CardTarget to)
        {
            CardMoved?.Invoke(card, from, to);
        }

        internal void RaiseMeldBecameValid(int meldId, MeldKind kind)
        {
            MeldBecameValid?.Invoke(meldId, kind);
        }

        internal void RaiseMeldBecameInvalid(int meldId, InvalidReason reason)
        {
            MeldBecameInvalid?.Invoke(meldId, reason);
        }

        internal void RaiseWentDown(int seat)
        {
            WentDown?.Invoke(seat);
        }

        internal void RaiseUndone(int seat)
        {
            Undone?.Invoke(seat);
        }

        internal void RaiseBoardReverted(int seat)
        {
            BoardReverted?.Invoke(seat);
        }

        internal void RaiseDrew(int seat, Card card)
        {
            Drew?.Invoke(seat, card);
        }

        internal void RaisePassed(int seat)
        {
            Passed?.Invoke(seat);
        }

        internal void RaiseTurnEnded(int seat, int nextSeat)
        {
            TurnEnded?.Invoke(seat, nextSeat);
        }

        internal void RaiseWon(GameResult result)
        {
            Won?.Invoke(result);
        }

        internal void RaiseStalemate(GameResult result)
        {
            Stalemate?.Invoke(result);
        }
    }
}