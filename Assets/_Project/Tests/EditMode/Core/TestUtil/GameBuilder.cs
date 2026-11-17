using System;
using System.Collections.Generic;

namespace Mejnun.Core.Tests.TestUtil
{
    internal static class GameBuilder
    {
        public static MejnunGame Create(int seatCount = 2, ulong seed = 1UL, int? dealer = 0,
            IReadOnlyList<string> facePool = null)
        {
            return new MejnunGame(new GameSetup(seatCount, seed, dealer, facePool: facePool));
        }

        public static void SetHand(MejnunGame game, int seat, params string[] cards)
        {
            var hand = game.State.Hands[seat];
            hand.Clear();
            for (var index = 0; index < cards.Length; index++)
            {
                hand.Add(ParseCard(cards[index]));
            }

            game.RefreshTurnSession();
        }

        public static void SetBoard(MejnunGame game, params string[][] melds)
        {
            game.State.Board.Clear();
            for (var meldIndex = 0; meldIndex < melds.Length; meldIndex++)
            {
                var cards = new List<Card>(melds[meldIndex].Length);
                for (var cardIndex = 0; cardIndex < melds[meldIndex].Length; cardIndex++)
                {
                    cards.Add(ParseCard(melds[meldIndex][cardIndex]));
                }

                game.State.Board.Add(new Meld(meldIndex + 1, cards));
            }

            game.State.NextMeldId = melds.Length + 1;
            game.RefreshTurnSession();
        }

        public static void SetDrawPile(MejnunGame game, params string[] cards)
        {
            game.State.DrawPile.Clear();
            for (var index = 0; index < cards.Length; index++)
            {
                game.State.DrawPile.Add(ParseCard(cards[index]));
            }
        }

        public static void SetPlayerPhase(MejnunGame game, int seat, PlayerPhase phase)
        {
            game.State.PlayerPhases[seat] = phase;
            game.RefreshTurnSession();
        }

        public static Card ParseCard(string notation)
        {
            if (string.IsNullOrEmpty(notation) || notation.Length < 2)
            {
                throw new FormatException("Card notation must contain a rank followed by a suit.");
            }

            var suit = char.ToUpperInvariant(notation[notation.Length - 1]);
            Suit parsedSuit;
            switch (suit)
            {
                case 'C':
                    parsedSuit = Suit.Clubs;
                    break;
                case 'D':
                    parsedSuit = Suit.Diamonds;
                    break;
                case 'H':
                    parsedSuit = Suit.Hearts;
                    break;
                case 'S':
                    parsedSuit = Suit.Spades;
                    break;
                default:
                    throw new FormatException("Unknown card suit.");
            }

            var rankText = notation.Substring(0, notation.Length - 1).ToUpperInvariant();
            Rank rank;
            switch (rankText)
            {
                case "A":
                    rank = Rank.Ace;
                    break;
                case "2":
                    rank = Rank.Two;
                    break;
                case "3":
                    rank = Rank.Three;
                    break;
                case "4":
                    rank = Rank.Four;
                    break;
                case "5":
                    rank = Rank.Five;
                    break;
                case "6":
                    rank = Rank.Six;
                    break;
                case "7":
                    rank = Rank.Seven;
                    break;
                case "8":
                    rank = Rank.Eight;
                    break;
                case "9":
                    rank = Rank.Nine;
                    break;
                case "10":
                    rank = Rank.Ten;
                    break;
                case "J":
                    rank = Rank.Jack;
                    break;
                case "Q":
                    rank = Rank.Queen;
                    break;
                case "K":
                    rank = Rank.King;
                    break;
                default:
                    throw new FormatException("Unknown card rank.");
            }

            return Card.FromId((int)parsedSuit * Card.CardsPerDeck / 4 + (int)rank);
        }
    }
}