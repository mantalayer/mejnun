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
    }
}
