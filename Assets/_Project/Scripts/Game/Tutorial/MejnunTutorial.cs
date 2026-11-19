using System;
using System.Collections.Generic;
using Mejnun.AI;
using Mejnun.Core;
using Mejnun.Cosmetics;

namespace Mejnun.Game
{
    /// <summary>
    /// The authored tutorial match: You (seat 0) against Layla (seat 1). The dealer is seat 0, so Layla deals
    /// first-in and takes turn 1. Layla plays the odd turns, you play the even ones.
    /// </summary>
    public sealed class MejnunTutorial
    {
        public const int SeatCount = 2;
        public const int PlayerSeat = 0;
        public const int CpuSeat = 1;
        public const int Dealer = 0;
        public const ulong Seed = 0x4D454A4E554EUL;
        public const string CpuName = "Layla";
        public const string PlayerName = "You";

        private MejnunTutorial(IReadOnlyList<Card>[] hands, IReadOnlyList<Card> drawsTopFirst, ScriptedCpuTurns cpuTurns,
            IReadOnlyList<TutorialStep> steps, IReadOnlyList<TutorialPage> goodToKnow)
        {
            Hands = hands;
            DrawsTopFirst = drawsTopFirst;
            CpuTurns = cpuTurns;
            Steps = steps;
            GoodToKnow = goodToKnow;
            ShoeOrder = TutorialDeck.Build(Dealer, hands, drawsTopFirst);
        }

        public IReadOnlyList<Card>[] Hands { get; }
        public IReadOnlyList<Card> DrawsTopFirst { get; }
        public byte[] ShoeOrder { get; }
        public ScriptedCpuTurns CpuTurns { get; }
        public IReadOnlyList<TutorialStep> Steps { get; }
        public IReadOnlyList<TutorialPage> GoodToKnow { get; }

        public GameSetup CreateSetup(IReadOnlyList<string> facePool = null, IReadOnlyList<string> deckPicks = null,
            IReadOnlyList<string> feltPool = null)
        {
            return new GameSetup(SeatCount, Seed, Dealer, 0, facePool, deckPicks, feltPool, ShoeOrder);
        }

        /// <summary>The Table-scene request for the tutorial: You vs Layla, untimed, stacked deck, Layla first.</summary>
        public static MatchRequest CreateMatchRequest()
        {
            var tutorial = Create();
            var seats = new[]
            {
                SeatConfig.Human(PlayerName),
                SeatConfig.Bot(CpuName, BotDifficulty.Easy)
            };
            return new MatchRequest(seats, 0, MatchMode.Tutorial, seed: Seed, shoeOrder: tutorial.ShoeOrder,
                dealer: Dealer);
        }

        public static MejnunTutorial Create()
        {
            var hands = new IReadOnlyList<Card>[]
            {
                Cards("7H", "8H", "9H", "5C", "5D", "5S", "AS", "9H'", "3H", "4H", "AH"),
                Cards("JS", "QS", "KS", "10H", "JH", "5H", "7D", "8D", "9D", "2C", "6S")
            };
            var draws = Cards("QD", "2H", "KC");

            var cpuTurns = new ScriptedCpuTurns(CpuSeat)
                .Add(1, CpuIntent.NewMeld(C("JS"), C("QS"), C("KS")), CpuIntent.EndTurn())
                .Add(3, CpuIntent.AddToMeldContaining(C("7H"), C("10H"), C("JH")),
                    CpuIntent.AddToMeldContaining(C("5C"), C("5H")), CpuIntent.EndTurn())
                .Add(5, CpuIntent.Draw())
                .Add(7, CpuIntent.NewMeld(C("7D"), C("8D"), C("9D")), CpuIntent.EndTurn())
                .Add(9, CpuIntent.Draw());

            var goodToKnow = GoodToKnowPages();
            return new MejnunTutorial(hands, draws, cpuTurns, BuildSteps(goodToKnow), goodToKnow);
        }

        private static IReadOnlyList<TutorialStep> BuildSteps(IReadOnlyList<TutorialPage> goodToKnow)
        {
            return new List<TutorialStep>
            {
                // 0 — components, objective and valid sets.
                new TutorialStep("welcome")
                {
                    Ui = TutorialUiKind.Modal,
                    HoldCpu = true,
                    Title = "Welcome to Mejnun",
                    Body = "Mejnun is played with two full decks: 104 cards. Everyone is dealt 11. " +
                           "Be the first to empty your hand onto the table to win."
                },
                new TutorialStep("sets")
                {
                    Ui = TutorialUiKind.Modal,
                    HoldCpu = true,
                    Title = "Sets",
                    Body = "Cards go on the table in sets. A same kind is 3 or 4 cards of one rank, all different " +
                           "suits. A straight is 3 or more cards of one suit, in order.",
                    Diagram = new TutorialDiagram(
                        new TutorialDiagramGroup(true, "Same kind", C("7C"), C("7D"), C("7S")),
                        new TutorialDiagramGroup(true, "Straight", C("4H"), C("5H"), C("6H")),
                        new TutorialDiagramGroup(false, "Mixed suits", C("4H"), C("5C"), C("6H")))
                },

                // C1 — Layla goes down.
                new TutorialStep("c1-watch")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = CpuName + "'s turn",
                    Body = CpuName + " goes first. Watch what she does.",
                    IsComplete = (state, turn) => IsPlayerTurn(state, 2)
                },
                new TutorialStep("c1-down")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = CpuName + " is Down",
                    Body = CpuName + " put down a set made only from her hand. That's called going Down, " +
                           "and one set is enough.",
                    FocusCards = Cards("JS", "QS", "KS")
                },

                // H1 — your first turn.
                new TutorialStep("h1a-not-down")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "You're not Down yet",
                    Body = "You can't use the table until you're Down. First, put down a set from your own hand."
                },
                new TutorialStep("h1b-go-down")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "Go Down first",
                    Body = "Until you're Down you can only play sets from your own hand. " +
                           "Put 7♥ 8♥ 9♥ on the empty table.",
                    PartialBody = "Keep going—finish placing all three cards from your hand to go Down.",
                    FocusCards = Cards("7H", "8H", "9H"),
                    Target = TutorialTarget.EmptyBoard,
                    Allowed = TutorialAction.MoveCards,
                    IsComplete = (state, turn) => state.PlayerPhases[PlayerSeat] == PlayerPhase.Down,
                    Solution = new[] { CpuIntent.NewMeld(C("7H"), C("8H"), C("9H")) }
                },
                new TutorialStep("h1b-board-open")
                {
                    Ui = TutorialUiKind.Modal,
                    Title = "You're Down!",
                    Body = "Now the whole table is yours to use: add to any set, rearrange, split. " +
                           "And you can keep playing this same turn."
                },
                new TutorialStep("h1c-ace-high")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "Add to a set",
                    Body = "Now put your A♠ on " + CpuName + "'s J♠ Q♠ K♠.",
                    FocusCards = Cards("AS"),
                    Target = TutorialTarget.MeldContaining(C("JS")),
                    Allowed = TutorialAction.MoveCards,
                    IsComplete = (state, turn) => MeldWith(state, C("JS")).Contains(C("AS")),
                    Solution = new[] { CpuIntent.AddToMeldContaining(C("JS"), C("AS")) }
                },
                new TutorialStep("h1c-ace-rule")
                {
                    Ui = TutorialUiKind.Modal,
                    Title = "Aces",
                    Body = "An ace can be low (A-2-3) or high (Q-K-A), but a straight never wraps around " +
                           "(K-A-2).",
                    Diagram = new TutorialDiagram(
                        new TutorialDiagramGroup(true, "Low", C("AS"), C("2S"), C("3S")),
                        new TutorialDiagramGroup(true, "High", C("QS"), C("KS"), C("AS")),
                        new TutorialDiagramGroup(false, "No wrap", C("KS"), C("AS"), C("2S")))
                },
                new TutorialStep("h1d-same-kind")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "Same kind",
                    Body = "Put 5♣ 5♦ 5♠ on the empty table.",
                    PartialBody = "Keep going—finish placing all three cards.",
                    FocusCards = Cards("5C", "5D", "5S"),
                    Target = TutorialTarget.EmptyBoard,
                    Allowed = TutorialAction.MoveCards,
                    IsComplete = (state, turn) => HasMeldExactly(state, C("5C"), C("5D"), C("5S")),
                    Solution = new[] { CpuIntent.NewMeld(C("5C"), C("5D"), C("5S")) }
                },
                new TutorialStep("h1e-doubles")
                {
                    Ui = TutorialUiKind.Modal,
                    Title = "Doubles",
                    Body = "With two decks every card has a double: same suit and rank, from the other deck " +
                           "(its card back looks different). A double can never go in the same set.",
                    FocusCards = Cards("9H'", "9H"),
                    Diagram = new TutorialDiagram(
                        new TutorialDiagramGroup(true, "A double", C("9H"), C("9H'")),
                        new TutorialDiagramGroup(false, "Same set", C("8H"), C("9H"), C("9H'")))
                },
                new TutorialStep("h1f-end-turn")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "End your turn",
                    Body = "You can end your turn when every set on the table is valid and you've played at " +
                           "least one card from your hand.",
                    Target = TutorialTarget.ForButton(TutorialButton.EndTurn),
                    Allowed = TutorialAction.EndTurn,
                    IsComplete = (state, turn) => state.TurnNumber >= 3,
                    Solution = new[] { CpuIntent.EndTurn() }
                },

                // C2 — Layla extends your sets.
                new TutorialStep("c2-watch")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = CpuName + "'s turn",
                    Body = "Watch " + CpuName + ".",
                    IsComplete = (state, turn) => IsPlayerTurn(state, 4)
                },
                new TutorialStep("c2-extend")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "Any set is fair game",
                    Body = CpuName + " added 10♥ J♥ to your hearts and 5♥ to your fives. " +
                           "Once you're Down you can add to any set on the table.",
                    FocusCards = Cards("10H", "JH", "5H")
                },

                // H2 — splitting, temporary invalid groups, doubles in different sets, undo/redo.
                new TutorialStep("h2a-split")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "Split a straight",
                    Body = "Long-press the 10♥ and drag it to the empty table. The 10♥ J♥ come with it.",
                    FocusCards = Cards("10H", "JH"),
                    Target = TutorialTarget.EmptyBoard,
                    Allowed = TutorialAction.Split | TutorialAction.MoveCards,
                    IsComplete = (state, turn) => HasMeldExactly(state, C("10H"), C("JH")) &&
                                                  HasMeldExactly(state, C("7H"), C("8H"), C("9H")),
                    Solution = new[] { CpuIntent.SplitAt(C("10H")) }
                },
                new TutorialStep("h2a-invalid")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "Not a set yet",
                    Body = "10♥ J♥ has a red outline: it isn't a valid set. That's fine in the middle of a turn, " +
                           "but you can't end your turn until every set is valid.",
                    FocusCards = Cards("10H", "JH")
                },
                new TutorialStep("h2b-double")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "Use your double",
                    Body = "Put your 9♥ on 10♥ J♥. It's the double of the 9♥ in the other set, so it's allowed " +
                           "here.",
                    FocusCards = Cards("9H'"),
                    Target = TutorialTarget.MeldContaining(C("10H")),
                    Allowed = TutorialAction.MoveCards,
                    IsComplete = (state, turn) => HasMeldExactly(state, C("9H'"), C("10H"), C("JH")),
                    Solution = new[] { CpuIntent.AddToMeldContaining(C("10H"), C("9H'")) }
                },
                new TutorialStep("h2c-undo")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "Undo",
                    Body = "Made a mistake? Undo steps back one move. Try it.",
                    Target = TutorialTarget.ForButton(TutorialButton.Undo),
                    Allowed = TutorialAction.Undo,
                    IsComplete = (state, turn) => state.Hands[PlayerSeat].Contains(C("9H'")) &&
                                                  HasMeldExactly(state, C("10H"), C("JH")),
                    Solution = new[] { CpuIntent.Undo() }
                },
                new TutorialStep("h2c-redo")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "Redo",
                    Body = "Redo puts the move back. Revert resets the whole turn, and Fix board tries to " +
                           "repair the table for you.",
                    Target = TutorialTarget.ForButton(TutorialButton.Redo),
                    Allowed = TutorialAction.Redo,
                    IsComplete = (state, turn) => HasMeldExactly(state, C("9H'"), C("10H"), C("JH")),
                    Solution = new[] { CpuIntent.AddToMeldContaining(C("10H"), C("9H'")) }
                },
                new TutorialStep("h2d-end-turn")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "End your turn",
                    Body = "Every set is valid again. End your turn.",
                    Target = TutorialTarget.ForButton(TutorialButton.EndTurn),
                    Allowed = TutorialAction.EndTurn,
                    IsComplete = (state, turn) => state.TurnNumber >= 5,
                    Solution = new[] { CpuIntent.EndTurn() }
                },

                // C3 — Layla draws.
                new TutorialStep("c3-watch")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = CpuName + "'s turn",
                    Body = "Watch " + CpuName + ".",
                    IsComplete = (state, turn) => IsPlayerTurn(state, 6)
                },
                new TutorialStep("c3-draw")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = CpuName + " drew",
                    Body = CpuName + " couldn't play, so she drew. Drawing takes one card from the pile and " +
                           "ends your turn."
                },

                // H3 — taking from a four of a kind, last-card warning.
                new TutorialStep("h3a-group")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "Start a group",
                    Body = "Put 3♥ 4♥ on the empty table. It's only two cards, so it shows red for now.",
                    PartialBody = "Keep going—finish placing both cards.",
                    FocusCards = Cards("3H", "4H"),
                    Target = TutorialTarget.EmptyBoard,
                    Allowed = TutorialAction.MoveCards,
                    IsComplete = (state, turn) => HasMeldExactly(state, C("3H"), C("4H")),
                    Solution = new[] { CpuIntent.NewMeld(C("3H"), C("4H")) }
                },
                new TutorialStep("h3a-take")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "Borrow from a set",
                    Body = "A four of a kind still works with three. Drag the 5♥ out of the fives onto 3♥ 4♥.",
                    FocusCards = Cards("5H"),
                    Target = TutorialTarget.MeldContaining(C("3H")),
                    Allowed = TutorialAction.MoveCards,
                    IsComplete = (state, turn) => HasMeldExactly(state, C("3H"), C("4H"), C("5H")) &&
                                                  HasMeldExactly(state, C("5C"), C("5D"), C("5S")),
                    Solution = new[] { CpuIntent.AddToMeldContaining(C("3H"), C("5H")) }
                },
                new TutorialStep("h3b-end-turn")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "End your turn",
                    Body = "All sets are valid. End your turn.",
                    Target = TutorialTarget.ForButton(TutorialButton.EndTurn),
                    Allowed = TutorialAction.EndTurn,
                    IsComplete = (state, turn) => state.TurnNumber >= 7,
                    Solution = new[] { CpuIntent.EndTurn() }
                },
                new TutorialStep("h3b-last-card")
                {
                    Ui = TutorialUiKind.Popup,
                    HoldCpu = true,
                    Title = "One card left!",
                    Body = "When a player is down to their last card, their seat shakes and shows a ! — " +
                           "everyone can see they're about to win.",
                    FocusCards = Cards("AH")
                },

                // C4 — Layla starts a new set.
                new TutorialStep("c4-watch")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = CpuName + "'s turn",
                    Body = "Watch " + CpuName + ".",
                    IsComplete = (state, turn) => IsPlayerTurn(state, 8)
                },
                new TutorialStep("c4-new-set")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "New sets",
                    Body = "Players who are Down can also start brand-new sets from their hand.",
                    FocusCards = Cards("7D", "8D", "9D")
                },

                // H4 — stuck, so draw.
                new TutorialStep("h4-stuck")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "Stuck",
                    Body = "Your A♥ doesn't fit anywhere on the table.",
                    FocusCards = Cards("AH")
                },
                new TutorialStep("h4-draw")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "Draw",
                    Body = "Tap Draw to take a card and end your turn. Drawing also returns any cards you " +
                           "placed this turn.",
                    Target = TutorialTarget.ForButton(TutorialButton.Draw),
                    Allowed = TutorialAction.Draw,
                    IsComplete = (state, turn) => state.TurnNumber >= 9,
                    Solution = new[] { CpuIntent.Draw() }
                },

                // C5 — Layla draws again.
                new TutorialStep("c5-watch")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = CpuName + "'s turn",
                    Body = CpuName + " is stuck too.",
                    IsComplete = (state, turn) => IsPlayerTurn(state, 10)
                },

                // H5 — ace low and MEJNUN!
                new TutorialStep("h5a-two")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "You drew the 2♥",
                    Body = "Put the 2♥ on 3♥ 4♥ 5♥.",
                    FocusCards = Cards("2H"),
                    Target = TutorialTarget.MeldContaining(C("3H")),
                    Allowed = TutorialAction.MoveCards,
                    IsComplete = (state, turn) => MeldWith(state, C("3H")).Contains(C("2H")),
                    Solution = new[] { CpuIntent.AddToMeldContaining(C("3H"), C("2H")) }
                },
                new TutorialStep("h5b-ace-low")
                {
                    Ui = TutorialUiKind.Popup,
                    Title = "Ace low",
                    Body = "Now the A♥ fits at the low end: A-2-3-4-5♥. Play your last card!",
                    FocusCards = Cards("AH"),
                    Target = TutorialTarget.MeldContaining(C("3H")),
                    Allowed = TutorialAction.MoveCards,
                    IsComplete = (state, turn) => state.Phase == GamePhase.Finished && state.Result != null &&
                                                  state.Result.Kind == GameEndKind.EmptyHand &&
                                                  state.Result.Winner == PlayerSeat,
                    Solution = new[] { CpuIntent.AddToMeldContaining(C("3H"), C("AH")) }
                },

                // End — rules we don't stage.
                new TutorialStep("good-to-know")
                {
                    Ui = TutorialUiKind.Modal,
                    Title = "MEJNUN!",
                    Body = "You emptied your hand and won. A few more things are good to know.",
                    Pages = goodToKnow
                }
            };
        }

        private static IReadOnlyList<TutorialPage> GoodToKnowPages()
        {
            return new[]
            {
                new TutorialPage("Empty draw pile",
                    "When the draw pile runs out, Draw becomes Pass. If everyone passes in a row, the player " +
                    "with the fewest cards wins. A tie is a Stalemate."),
                new TutorialPage("Timed turns",
                    "In timed games a clock counts down each turn. When it runs out — Halas! — a valid turn " +
                    "ends, otherwise your cards go back and you draw."),
                new TutorialPage("Fix board and Revert",
                    "Fix board tries to make every set valid while keeping your moves. Revert puts the whole " +
                    "table back to how it was at the start of your turn."),
                new TutorialPage("Hints and the table",
                    "Hints outline cards you can play. Pinch to zoom, drag to pan, and use Fit to see the " +
                    "whole table."),
                new TutorialPage("Next game",
                    "The winner goes first in the next game.")
            };
        }

        private static bool IsPlayerTurn(GameState state, int turnNumber)
        {
            return state.Phase == GamePhase.Playing && state.CurrentSeat == PlayerSeat &&
                   state.TurnNumber >= turnNumber;
        }

        private static List<Card> MeldWith(GameState state, Card anchor)
        {
            var meld = CpuIntent.FindMeldContaining(state, anchor, out _);
            return meld == null ? new List<Card>() : meld.Cards;
        }

        private static bool HasMeldExactly(GameState state, params Card[] cards)
        {
            var meld = CpuIntent.FindMeldContaining(state, cards[0], out _);
            if (meld == null || meld.Cards.Count != cards.Length)
            {
                return false;
            }

            for (var index = 0; index < cards.Length; index++)
            {
                if (!meld.Cards.Contains(cards[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public static Card[] Cards(params string[] notations)
        {
            var cards = new Card[notations.Length];
            for (var index = 0; index < notations.Length; index++)
            {
                cards[index] = C(notations[index]);
            }

            return cards;
        }

        /// <summary>Parses "10H", "AS", "KD"… A trailing "'" picks the second deck's copy, e.g. "9H'".</summary>
        public static Card C(string notation)
        {
            if (string.IsNullOrEmpty(notation) || notation.Length < 2)
            {
                throw new FormatException("Card notation must contain a rank followed by a suit.");
            }

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
                    rank = (int)Rank.Ace;
                    break;
                case "J":
                    rank = (int)Rank.Jack;
                    break;
                case "Q":
                    rank = (int)Rank.Queen;
                    break;
                case "K":
                    rank = (int)Rank.King;
                    break;
                default:
                    if (!int.TryParse(rankText, out rank) || rank < 2 || rank > 10)
                    {
                        throw new FormatException("Unknown rank in " + notation);
                    }

                    rank--;
                    break;
            }

            return Card.FromId(deck * Card.CardsPerDeck + (int)suit * 13 + rank);
        }
    }
}
