using System.Collections.Generic;
using Mejnun.AI;
using Mejnun.Core;

namespace Mejnun.Game
{
    public sealed class CardTracker
    {
        private readonly List<HashSet<byte>> knownHeldCards = new List<HashSet<byte>>();
        private readonly List<byte> previousBoardCards = new List<byte>();
        private readonly List<int> previousHandCounts = new List<int>();
        private readonly List<bool> drewWhileDown = new List<bool>();
        private int previousCurrentSeat = -1;
        private bool initialized;

        public void Reset()
        {
            knownHeldCards.Clear();
            previousBoardCards.Clear();
            previousHandCounts.Clear();
            drewWhileDown.Clear();
            previousCurrentSeat = -1;
            initialized = false;
        }

        public void Observe(GameState state)
        {
            if (state == null)
            {
                return;
            }

            if (!initialized || knownHeldCards.Count != state.Hands.Count)
            {
                Initialize(state);
                return;
            }

            var currentBoardCards = GetBoardCardIds(state);
            if (previousCurrentSeat >= 0 && previousCurrentSeat < knownHeldCards.Count)
            {
                for (var index = 0; index < previousBoardCards.Count; index++)
                {
                    var cardId = previousBoardCards[index];
                    if (!currentBoardCards.Contains(cardId))
                    {
                        knownHeldCards[previousCurrentSeat].Add(cardId);
                    }
                }
            }

            for (var seat = 0; seat < state.Hands.Count; seat++)
            {
                if (state.Hands[seat].Count > previousHandCounts[seat] && state.PlayerPhases[seat] == PlayerPhase.Down)
                {
                    drewWhileDown[seat] = true;
                }
            }

            for (var seat = 0; seat < knownHeldCards.Count; seat++)
            {
                foreach (var cardId in currentBoardCards)
                {
                    knownHeldCards[seat].Remove(cardId);
                }
            }

            previousBoardCards.Clear();
            previousBoardCards.AddRange(currentBoardCards);
            for (var seat = 0; seat < state.Hands.Count; seat++)
            {
                previousHandCounts[seat] = state.Hands[seat].Count;
            }

            previousCurrentSeat = state.CurrentSeat;
        }

        public BotKnowledge CreateKnowledge(GameState state)
        {
            Observe(state);
            var cards = new byte[knownHeldCards.Count][];
            var drew = new bool[drewWhileDown.Count];
            var counts = new int[state.Hands.Count];
            for (var seat = 0; seat < knownHeldCards.Count; seat++)
            {
                cards[seat] = new byte[knownHeldCards[seat].Count];
                knownHeldCards[seat].CopyTo(cards[seat]);
                drew[seat] = drewWhileDown[seat];
                counts[seat] = state.Hands[seat].Count;
            }

            return new BotKnowledge(cards, drew, counts);
        }

        private void Initialize(GameState state)
        {
            Reset();
            for (var seat = 0; seat < state.Hands.Count; seat++)
            {
                knownHeldCards.Add(new HashSet<byte>());
                previousHandCounts.Add(state.Hands[seat].Count);
                drewWhileDown.Add(false);
            }

            previousBoardCards.AddRange(GetBoardCardIds(state));
            previousCurrentSeat = state.CurrentSeat;
            initialized = true;
        }

        private static HashSet<byte> GetBoardCardIds(GameState state)
        {
            var cards = new HashSet<byte>();
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var meld = state.Board[meldIndex].Cards;
                for (var cardIndex = 0; cardIndex < meld.Count; cardIndex++)
                {
                    cards.Add(meld[cardIndex].Id);
                }
            }

            return cards;
        }
    }
}