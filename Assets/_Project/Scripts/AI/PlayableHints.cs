using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.AI
{
    /// <summary>
    /// "Highlight playable" (§1.11): hand cards that the Easy-depth search can place right now, either in a new
    /// set made from the hand or (once down) as a simple extension of a set on the board.
    /// </summary>
    public static class PlayableHints
    {
        public static HashSet<byte> Find(GameState state)
        {
            var playable = new HashSet<byte>();
            if (state == null || state.Phase != GamePhase.Playing || state.CurrentSeat < 0 ||
                state.CurrentSeat >= state.Hands.Count)
            {
                return playable;
            }

            var hand = state.Hands[state.CurrentSeat];
            var handSets = MeldSolver.SolveHandOnly(hand);
            if (handSets.Feasible)
            {
                for (var index = 0; index < handSets.UsedOptional.Count; index++)
                {
                    playable.Add(handSets.UsedOptional[index].Id);
                }
            }

            if (state.PlayerPhases[state.CurrentSeat] != PlayerPhase.Down)
            {
                return playable;
            }

            var candidate = new List<Card>(14);
            for (var handIndex = 0; handIndex < hand.Count; handIndex++)
            {
                var card = hand[handIndex];
                if (playable.Contains(card.Id))
                {
                    continue;
                }

                for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
                {
                    var meld = state.Board[meldIndex].Cards;
                    if (!MeldValidator.Classify(meld).IsValid)
                    {
                        continue;
                    }

                    candidate.Clear();
                    candidate.AddRange(meld);
                    candidate.Add(card);
                    if (MeldValidator.Classify(candidate).IsValid)
                    {
                        playable.Add(card.Id);
                        break;
                    }
                }
            }

            return playable;
        }

        public static HashSet<byte> FindBoardMoves(GameState state)
        {
            var movable = new HashSet<byte>();
            if (state == null || state.Phase != GamePhase.Playing || state.CurrentSeat < 0 ||
                state.CurrentSeat >= state.Hands.Count || state.PlayerPhases[state.CurrentSeat] != PlayerPhase.Down)
            {
                return movable;
            }

            var candidate = new List<Card>(14);
            var hand = state.Hands[state.CurrentSeat];
            var resolutionCache = new Dictionary<string, bool>();
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var meld = state.Board[meldIndex];
                var cards = meld.Cards;
                if (!MeldValidator.Classify(cards).IsValid)
                {
                    continue;
                }

                for (var cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    candidate.Clear();
                    for (var remainingIndex = 0; remainingIndex < cards.Count; remainingIndex++)
                    {
                        if (remainingIndex != cardIndex)
                        {
                            candidate.Add(cards[remainingIndex]);
                        }
                    }

                    if (!MeldValidator.Classify(candidate).IsValid)
                    {
                        continue;
                    }

                    for (var handIndex = 0; handIndex < hand.Count; handIndex++)
                    {
                        var handCard = hand[handIndex];
                        candidate.Add(handCard);
                        var canReplace = MeldValidator.Classify(candidate).IsValid;
                        candidate.RemoveAt(candidate.Count - 1);
                        if (canReplace && CanResolveBoardAfterMove(state, meldIndex, meld.Id,
                                cards[cardIndex], handCard, resolutionCache))
                        {
                            movable.Add(cards[cardIndex].Id);
                            break;
                        }
                    }
                }
            }

            return movable;
        }

        private static bool CanResolveBoardAfterMove(GameState state, int movedMeldIndex, int movedMeldId,
            Card displacedCard, Card handCard, Dictionary<string, bool> cache)
        {
            var key = movedMeldId + ":" + (byte)displacedCard.Suit + ":" + (byte)displacedCard.Rank + ":" +
                      (byte)handCard.Suit + ":" + (byte)handCard.Rank;
            if (cache.TryGetValue(key, out var canResolve))
            {
                return canResolve;
            }

            var required = new List<Card>(Card.CardCount);
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                if (meldIndex == movedMeldIndex)
                {
                    continue;
                }

                required.AddRange(state.Board[meldIndex].Cards);
            }

            required.Add(displacedCard);
            var hand = state.Hands[state.CurrentSeat];
            var optional = new List<Card>(hand.Count - 1);
            for (var handIndex = 0; handIndex < hand.Count; handIndex++)
            {
                if (hand[handIndex].Id != handCard.Id)
                {
                    optional.Add(hand[handIndex]);
                }
            }

            canResolve = MeldSolver.Solve(required, optional).Feasible;
            cache.Add(key, canResolve);
            return canResolve;
        }
    }
}
