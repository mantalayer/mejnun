using System;
using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.AI.Tests
{
    internal static class AiTestUtil
    {
        /// <summary>Parses "10H", "AS", "KD"… A trailing "'" picks the second deck's copy, e.g. "5H'".</summary>
        public static Card Parse(string notation)
        {
            var deck = 0;
            if (notation.EndsWith("'", StringComparison.Ordinal))
            {
                deck = 1;
                notation = notation.Substring(0, notation.Length - 1);
            }

            Suit suit;
            switch (char.ToUpperInvariant(notation[notation.Length - 1]))
            {
                case 'C':
                    suit = Suit.Clubs;
                    break;
                case 'D':
                    suit = Suit.Diamonds;
                    break;
                case 'H':
                    suit = Suit.Hearts;
                    break;
                case 'S':
                    suit = Suit.Spades;
                    break;
                default:
                    throw new FormatException("Unknown suit in " + notation);
            }

            var rankText = notation.Substring(0, notation.Length - 1).ToUpperInvariant();
            int rank;
            switch (rankText)
            {
                case "A":
                    rank = 0;
                    break;
                case "J":
                    rank = 10;
                    break;
                case "Q":
                    rank = 11;
                    break;
                case "K":
                    rank = 12;
                    break;
                default:
                    rank = int.Parse(rankText) - 1;
                    break;
            }

            return Card.FromId(deck * Card.CardsPerDeck + (int)suit * 13 + rank);
        }

        public static List<Card> Cards(params string[] notations)
        {
            var cards = new List<Card>(notations.Length);
            for (var index = 0; index < notations.Length; index++)
            {
                cards.Add(Parse(notations[index]));
            }

            return cards;
        }

        /// <summary>A 2-seat game on seat 0's turn with the given hand, board and phase.</summary>
        public static MejnunGame Game(string[] hand, string[][] board, bool isDown, params string[] drawPile)
        {
            var game = new MejnunGame(new GameSetup(2, 1UL, 1));
            var state = game.State;
            state.Hands[0].Clear();
            state.Hands[0].AddRange(Cards(hand));
            state.Hands[1].Clear();
            state.Hands[1].AddRange(Cards("2C'", "6D'", "KS'"));
            state.Board.Clear();
            for (var index = 0; index < board.Length; index++)
            {
                state.Board.Add(new Meld(index + 1, Cards(board[index])));
            }

            state.NextMeldId = board.Length + 1;
            state.DrawPile.Clear();
            state.DrawPile.AddRange(Cards(drawPile.Length == 0 ? new[] { "QH" } : drawPile));
            state.PlayerPhases[0] = isDown ? PlayerPhase.Down : PlayerPhase.NotDown;
            state.CurrentSeat = 0;
            game.RefreshTurnSession();
            return game;
        }

        public static int BoardCardCount(GameState state)
        {
            var count = 0;
            for (var index = 0; index < state.Board.Count; index++)
            {
                count += state.Board[index].Cards.Count;
            }

            return count;
        }

        public static void AssertValidPartition(MeldSolution solution, IReadOnlyList<Card> required,
            IReadOnlyList<Card> optional)
        {
            var seen = new HashSet<byte>();
            for (var meldIndex = 0; meldIndex < solution.Melds.Count; meldIndex++)
            {
                var meld = solution.Melds[meldIndex];
                if (!MeldValidator.Classify(meld).IsValid)
                {
                    throw new InvalidOperationException("The solver returned an invalid set.");
                }

                for (var cardIndex = 0; cardIndex < meld.Count; cardIndex++)
                {
                    if (!seen.Add(meld[cardIndex].Id))
                    {
                        throw new InvalidOperationException("The solver used a card twice.");
                    }
                }
            }

            for (var index = 0; index < required.Count; index++)
            {
                if (!seen.Contains(required[index].Id))
                {
                    throw new InvalidOperationException("The solver left out a required card.");
                }
            }

            var optionalIds = new HashSet<byte>();
            for (var index = 0; index < optional.Count; index++)
            {
                optionalIds.Add(optional[index].Id);
            }

            for (var index = 0; index < solution.UsedOptional.Count; index++)
            {
                if (!optionalIds.Contains(solution.UsedOptional[index].Id))
                {
                    throw new InvalidOperationException("The solver used a card it was not given.");
                }
            }

            if (seen.Count != required.Count + solution.OptionalUsed)
            {
                throw new InvalidOperationException("The solver's card count does not add up.");
            }
        }
    }
}
