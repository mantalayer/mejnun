using System;
using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Game
{
    public static class OpeningSeedFinder
    {
        public const int DefaultCandidateLimit = 2048;

        public static ulong Find(ulong baseSeed, int seatCount, int candidateLimit = DefaultCandidateLimit)
        {
            if (seatCount < 2 || seatCount > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(seatCount));
            }

            var count = Math.Max(0, candidateLimit);
            for (var candidate = 0; candidate < count; candidate++)
            {
                var seed = unchecked(baseSeed + (ulong)candidate);
                if (EveryHandCanGoDown(seed, seatCount, false) && EveryHandCanGoDown(seed, seatCount, true))
                {
                    return seed;
                }
            }

            return baseSeed;
        }

        public static bool EveryHandCanGoDown(ulong seed, int seatCount, bool randomDealer)
        {
            var game = new MejnunGame(new GameSetup(seatCount, seed));
            var dealerResult = randomDealer
                ? game.Apply(GameCommand.ChooseRandomDealer())
                : game.Apply(GameCommand.ChooseDealer(0));
            if (!dealerResult.Ok)
            {
                return false;
            }

            for (var seat = 0; seat < game.State.Hands.Count; seat++)
            {
                if (!HasOpeningMeld(game.State.Hands[seat]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasOpeningMeld(IReadOnlyList<Card> hand)
        {
            for (var first = 0; first < hand.Count - 2; first++)
            {
                for (var second = first + 1; second < hand.Count - 1; second++)
                {
                    for (var third = second + 1; third < hand.Count; third++)
                    {
                        if (MeldValidator.Classify(new[] { hand[first], hand[second], hand[third] }).IsValid)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}