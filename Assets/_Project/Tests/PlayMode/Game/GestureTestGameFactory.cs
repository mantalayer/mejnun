using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Game.Tests
{
    internal static class GestureTestGameFactory
    {
        public static MejnunGame CreateGameWithStraightInHand(int length, out byte[] straight)
        {
            for (ulong seed = 1; seed < 2048; seed++)
            {
                var game = new MejnunGame(new GameSetup(2, seed, 0));
                if (TryFindStraight(game.State.Hands[game.State.CurrentSeat], length, out straight))
                {
                    return game;
                }
            }

            throw new System.InvalidOperationException("Could not find a deterministic hand with a straight.");
        }

        public static MejnunGame CreateGameWithAccessibleStraightExtension(out byte[] straight,
            out byte extensionCardId)
        {
            for (ulong seed = 1; seed < 4096; seed++)
            {
                var game = new MejnunGame(new GameSetup(2, seed, 0));
                var firstSeat = game.State.CurrentSeat;
                if (!TryFindStraight(game.State.Hands[firstSeat], 3, out straight))
                {
                    continue;
                }

                var moveResult = game.Apply(GameCommand.MoveCards(firstSeat, straight, CardTarget.NewMeld(0)));
                if (!moveResult.Ok || !game.Apply(GameCommand.EndTurn(firstSeat)).Ok)
                {
                    continue;
                }

                if (TryFindExtensionCard(game.State.Hands[game.State.CurrentSeat], game.State.Board[0],
                        out extensionCardId))
                {
                    return game;
                }
            }

            throw new System.InvalidOperationException("Could not find a hand with an extension to a start-of-turn meld.");
        }

        public static bool TryFindExtensionCard(IReadOnlyList<Card> hand, Meld meld, out byte cardId)
        {
            var candidateCards = new List<Card>(meld.Cards.Count + 1);
            for (var handIndex = 0; handIndex < hand.Count; handIndex++)
            {
                var candidate = hand[handIndex];
                for (var insertIndex = 0; insertIndex <= meld.Cards.Count; insertIndex++)
                {
                    candidateCards.Clear();
                    for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                    {
                        if (cardIndex == insertIndex)
                        {
                            candidateCards.Add(candidate);
                        }

                        candidateCards.Add(meld.Cards[cardIndex]);
                    }

                    if (insertIndex == meld.Cards.Count)
                    {
                        candidateCards.Add(candidate);
                    }

                    if (MeldValidator.Classify(candidateCards).IsValid)
                    {
                        cardId = candidate.Id;
                        return true;
                    }
                }
            }

            cardId = 0;
            return false;
        }

        public static bool TryFindStraight(IReadOnlyList<Card> hand, int length, out byte[] straight)
        {
            for (var suitIndex = 0; suitIndex < 4; suitIndex++)
            {
                for (var firstRank = 1; firstRank + length <= 13; firstRank++)
                {
                    var ids = new byte[length];
                    var foundAll = true;
                    for (var offset = 0; offset < length; offset++)
                    {
                        var found = false;
                        for (var handIndex = 0; handIndex < hand.Count; handIndex++)
                        {
                            var card = hand[handIndex];
                            if ((int)card.Suit == suitIndex && (int)card.Rank == firstRank + offset)
                            {
                                ids[offset] = card.Id;
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            foundAll = false;
                            break;
                        }
                    }

                    if (foundAll)
                    {
                        straight = ids;
                        return true;
                    }
                }
            }

            straight = null;
            return false;
        }
    }
}