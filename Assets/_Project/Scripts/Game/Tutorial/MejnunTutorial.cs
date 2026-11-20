using System;
using System.Collections.Generic;
using Mejnun.AI;
using Mejnun.Core;
using Mejnun.Cosmetics;
using Mejnun.Localization;

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

        /// <summary>Layla's name in the current language.</summary>
        public static string CpuName => Loc.Get("tutorial.name.cpu");

        /// <summary>The player's seat name ("You") in the current language.</summary>
        public static string PlayerName => Loc.Get("tutorial.name.player");

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
                SeatConfig.Human(SeatNames.TutorialPlayer),
                SeatConfig.Bot(SeatNames.TutorialCpu, BotDifficulty.Easy)
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
                    TitleSource = () => Loc.Get("tutorial.step.welcome.title"),
                    BodySource = () => Loc.Get("tutorial.step.welcome.body")
                },
                new TutorialStep("sets")
                {
                    Ui = TutorialUiKind.Modal,
                    HoldCpu = true,
                    TitleSource = () => Loc.Get("tutorial.step.sets.title"),
                    BodySource = () => Loc.Get("tutorial.step.sets.body"),
                    Diagram = new TutorialDiagram(
                        new TutorialDiagramGroup(true, () => Loc.Get("tutorial.diagram.same_kind"),
                            C("7C"), C("7D"), C("7S")),
                        new TutorialDiagramGroup(true, () => Loc.Get("tutorial.diagram.straight"),
                            C("4H"), C("5H"), C("6H")),
                        new TutorialDiagramGroup(false, () => Loc.Get("tutorial.diagram.mixed_suits"),
                            C("4H"), C("5C"), C("6H")))
                },

                // C1 — Layla goes down.
                new TutorialStep("c1-watch")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Format("tutorial.step.cpu_turn.title", CpuName),
                    BodySource = () => Loc.Format("tutorial.step.c1_watch.body", CpuName),
                    IsComplete = (state, turn) => IsPlayerTurn(state, 2)
                },
                new TutorialStep("c1-down")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Format("tutorial.step.c1_down.title", CpuName),
                    BodySource = () => Loc.Format("tutorial.step.c1_down.body", CpuName),
                    FocusCards = Cards("JS", "QS", "KS")
                },

                // H1 — your first turn.
                new TutorialStep("h1a-not-down")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.h1a_not_down.title"),
                    BodySource = () => Loc.Get("tutorial.step.h1a_not_down.body")
                },
                new TutorialStep("h1b-go-down")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.h1b_go_down.title"),
                    BodySource = () => Loc.Get("tutorial.step.h1b_go_down.body"),
                    PartialBodySource = () => Loc.Get("tutorial.step.h1b_go_down.partial"),
                    FocusCards = Cards("7H", "8H", "9H"),
                    Target = TutorialTarget.EmptyBoard,
                    Allowed = TutorialAction.MoveCards,
                    IsComplete = (state, turn) => state.PlayerPhases[PlayerSeat] == PlayerPhase.Down,
                    Solution = new[] { CpuIntent.NewMeld(C("7H"), C("8H"), C("9H")) }
                },
                new TutorialStep("h1b-board-open")
                {
                    Ui = TutorialUiKind.Modal,
                    TitleSource = () => Loc.Get("tutorial.step.h1b_board_open.title"),
                    BodySource = () => Loc.Get("tutorial.step.h1b_board_open.body")
                },
                new TutorialStep("h1c-ace-high")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.h1c_ace_high.title"),
                    BodySource = () => Loc.Format("tutorial.step.h1c_ace_high.body", CpuName),
                    FocusCards = Cards("AS"),
                    Target = TutorialTarget.MeldContaining(C("JS")),
                    Allowed = TutorialAction.MoveCards,
                    IsComplete = (state, turn) => MeldWith(state, C("JS")).Contains(C("AS")),
                    Solution = new[] { CpuIntent.AddToMeldContaining(C("JS"), C("AS")) }
                },
                new TutorialStep("h1c-ace-rule")
                {
                    Ui = TutorialUiKind.Modal,
                    TitleSource = () => Loc.Get("tutorial.step.h1c_ace_rule.title"),
                    BodySource = () => Loc.Get("tutorial.step.h1c_ace_rule.body"),
                    Diagram = new TutorialDiagram(
                        new TutorialDiagramGroup(true, () => Loc.Get("tutorial.diagram.ace_low"),
                            C("AS"), C("2S"), C("3S")),
                        new TutorialDiagramGroup(true, () => Loc.Get("tutorial.diagram.ace_high"),
                            C("QS"), C("KS"), C("AS")),
                        new TutorialDiagramGroup(false, () => Loc.Get("tutorial.diagram.no_wrap"),
                            C("KS"), C("AS"), C("2S")))
                },
                new TutorialStep("h1d-same-kind")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.h1d_same_kind.title"),
                    BodySource = () => Loc.Get("tutorial.step.h1d_same_kind.body"),
                    PartialBodySource = () => Loc.Get("tutorial.step.h1d_same_kind.partial"),
                    FocusCards = Cards("5C", "5D", "5S"),
                    Target = TutorialTarget.EmptyBoard,
                    Allowed = TutorialAction.MoveCards,
                    IsComplete = (state, turn) => HasMeldExactly(state, C("5C"), C("5D"), C("5S")),
                    Solution = new[] { CpuIntent.NewMeld(C("5C"), C("5D"), C("5S")) }
                },
                new TutorialStep("h1e-doubles")
                {
                    Ui = TutorialUiKind.Modal,
                    TitleSource = () => Loc.Get("tutorial.step.h1e_doubles.title"),
                    BodySource = () => Loc.Get("tutorial.step.h1e_doubles.body"),
                    FocusCards = Cards("9H'", "9H"),
                    Diagram = new TutorialDiagram(
                        new TutorialDiagramGroup(true, () => Loc.Get("tutorial.diagram.a_double"),
                            C("9H"), C("9H'")),
                        new TutorialDiagramGroup(false, () => Loc.Get("tutorial.diagram.same_set"),
                            C("8H"), C("9H"), C("9H'")))
                },
                new TutorialStep("h1f-end-turn")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.end_turn.title"),
                    BodySource = () => Loc.Get("tutorial.step.h1f_end_turn.body"),
                    Target = TutorialTarget.ForButton(TutorialButton.EndTurn),
                    Allowed = TutorialAction.EndTurn,
                    IsComplete = (state, turn) => state.TurnNumber >= 3,
                    Solution = new[] { CpuIntent.EndTurn() }
                },

                // C2 — Layla extends your sets.
                new TutorialStep("c2-watch")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Format("tutorial.step.cpu_turn.title", CpuName),
                    BodySource = () => Loc.Format("tutorial.step.cpu_watch.body", CpuName),
                    IsComplete = (state, turn) => IsPlayerTurn(state, 4)
                },
                new TutorialStep("c2-extend")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.c2_extend.title"),
                    BodySource = () => Loc.Format("tutorial.step.c2_extend.body", CpuName),
                    FocusCards = Cards("10H", "JH", "5H")
                },

                // H2 — splitting, temporary invalid groups, doubles in different sets, undo/redo.
                new TutorialStep("h2a-split")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.h2a_split.title"),
                    BodySource = () => Loc.Get("tutorial.step.h2a_split.body"),
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
                    TitleSource = () => Loc.Get("tutorial.step.h2a_invalid.title"),
                    BodySource = () => Loc.Get("tutorial.step.h2a_invalid.body"),
                    FocusCards = Cards("10H", "JH")
                },
                new TutorialStep("h2b-double")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.h2b_double.title"),
                    BodySource = () => Loc.Get("tutorial.step.h2b_double.body"),
                    FocusCards = Cards("9H'"),
                    Target = TutorialTarget.MeldContaining(C("10H")),
                    Allowed = TutorialAction.MoveCards,
                    IsComplete = (state, turn) => HasMeldExactly(state, C("9H'"), C("10H"), C("JH")),
                    Solution = new[] { CpuIntent.AddToMeldContaining(C("10H"), C("9H'")) }
                },
                new TutorialStep("h2c-undo")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.h2c_undo.title"),
                    BodySource = () => Loc.Get("tutorial.step.h2c_undo.body"),
                    Target = TutorialTarget.ForButton(TutorialButton.Undo),
                    Allowed = TutorialAction.Undo,
                    IsComplete = (state, turn) => state.Hands[PlayerSeat].Contains(C("9H'")) &&
                                                  HasMeldExactly(state, C("10H"), C("JH")),
                    Solution = new[] { CpuIntent.Undo() }
                },
                new TutorialStep("h2c-redo")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.h2c_redo.title"),
                    BodySource = () => Loc.Get("tutorial.step.h2c_redo.body"),
                    Target = TutorialTarget.ForButton(TutorialButton.Redo),
                    Allowed = TutorialAction.Redo,
                    IsComplete = (state, turn) => HasMeldExactly(state, C("9H'"), C("10H"), C("JH")),
                    Solution = new[] { CpuIntent.AddToMeldContaining(C("10H"), C("9H'")) }
                },
                new TutorialStep("h2d-end-turn")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.end_turn.title"),
                    BodySource = () => Loc.Get("tutorial.step.h2d_end_turn.body"),
                    Target = TutorialTarget.ForButton(TutorialButton.EndTurn),
                    Allowed = TutorialAction.EndTurn,
                    IsComplete = (state, turn) => state.TurnNumber >= 5,
                    Solution = new[] { CpuIntent.EndTurn() }
                },

                // C3 — Layla draws.
                new TutorialStep("c3-watch")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Format("tutorial.step.cpu_turn.title", CpuName),
                    BodySource = () => Loc.Format("tutorial.step.cpu_watch.body", CpuName),
                    IsComplete = (state, turn) => IsPlayerTurn(state, 6)
                },
                new TutorialStep("c3-draw")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Format("tutorial.step.c3_draw.title", CpuName),
                    BodySource = () => Loc.Format("tutorial.step.c3_draw.body", CpuName)
                },

                // H3 — taking from a four of a kind, last-card warning.
                new TutorialStep("h3a-group")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.h3a_group.title"),
                    BodySource = () => Loc.Get("tutorial.step.h3a_group.body"),
                    PartialBodySource = () => Loc.Get("tutorial.step.h3a_group.partial"),
                    FocusCards = Cards("3H", "4H"),
                    Target = TutorialTarget.EmptyBoard,
                    Allowed = TutorialAction.MoveCards,
                    IsComplete = (state, turn) => HasMeldExactly(state, C("3H"), C("4H")),
                    Solution = new[] { CpuIntent.NewMeld(C("3H"), C("4H")) }
                },
                new TutorialStep("h3a-take")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.h3a_take.title"),
                    BodySource = () => Loc.Get("tutorial.step.h3a_take.body"),
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
                    TitleSource = () => Loc.Get("tutorial.step.end_turn.title"),
                    BodySource = () => Loc.Get("tutorial.step.h3b_end_turn.body"),
                    Target = TutorialTarget.ForButton(TutorialButton.EndTurn),
                    Allowed = TutorialAction.EndTurn,
                    IsComplete = (state, turn) => state.TurnNumber >= 7,
                    Solution = new[] { CpuIntent.EndTurn() }
                },
                new TutorialStep("h3b-last-card")
                {
                    Ui = TutorialUiKind.Popup,
                    HoldCpu = true,
                    TitleSource = () => Loc.Get("tutorial.step.h3b_last_card.title"),
                    BodySource = () => Loc.Get("tutorial.step.h3b_last_card.body"),
                    FocusCards = Cards("AH")
                },

                // C4 — Layla starts a new set.
                new TutorialStep("c4-watch")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Format("tutorial.step.cpu_turn.title", CpuName),
                    BodySource = () => Loc.Format("tutorial.step.cpu_watch.body", CpuName),
                    IsComplete = (state, turn) => IsPlayerTurn(state, 8)
                },
                new TutorialStep("c4-new-set")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.c4_new_set.title"),
                    BodySource = () => Loc.Get("tutorial.step.c4_new_set.body"),
                    FocusCards = Cards("7D", "8D", "9D")
                },

                // H4 — stuck, so draw.
                new TutorialStep("h4-stuck")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.h4_stuck.title"),
                    BodySource = () => Loc.Get("tutorial.step.h4_stuck.body"),
                    FocusCards = Cards("AH")
                },
                new TutorialStep("h4-draw")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.h4_draw.title"),
                    BodySource = () => Loc.Get("tutorial.step.h4_draw.body"),
                    Target = TutorialTarget.ForButton(TutorialButton.Draw),
                    Allowed = TutorialAction.Draw,
                    IsComplete = (state, turn) => state.TurnNumber >= 9,
                    Solution = new[] { CpuIntent.Draw() }
                },

                // C5 — Layla draws again.
                new TutorialStep("c5-watch")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Format("tutorial.step.cpu_turn.title", CpuName),
                    BodySource = () => Loc.Format("tutorial.step.c5_watch.body", CpuName),
                    IsComplete = (state, turn) => IsPlayerTurn(state, 10)
                },

                // H5 — ace low and MEJNUN!
                new TutorialStep("h5a-two")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.h5a_two.title"),
                    BodySource = () => Loc.Get("tutorial.step.h5a_two.body"),
                    FocusCards = Cards("2H"),
                    Target = TutorialTarget.MeldContaining(C("3H")),
                    Allowed = TutorialAction.MoveCards,
                    IsComplete = (state, turn) => MeldWith(state, C("3H")).Contains(C("2H")),
                    Solution = new[] { CpuIntent.AddToMeldContaining(C("3H"), C("2H")) }
                },
                new TutorialStep("h5b-ace-low")
                {
                    Ui = TutorialUiKind.Popup,
                    TitleSource = () => Loc.Get("tutorial.step.h5b_ace_low.title"),
                    BodySource = () => Loc.Get("tutorial.step.h5b_ace_low.body"),
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
                    TitleSource = () => Loc.Get("tutorial.step.good_to_know.title"),
                    BodySource = () => Loc.Get("tutorial.step.good_to_know.body"),
                    Pages = goodToKnow
                }
            };
        }

        private static IReadOnlyList<TutorialPage> GoodToKnowPages()
        {
            return new[]
            {
                new TutorialPage(() => Loc.Get("tutorial.page.empty_pile.title"),
                    () => Loc.Get("tutorial.page.empty_pile.body")),
                new TutorialPage(() => Loc.Get("tutorial.page.timed_turns.title"),
                    () => Loc.Get("tutorial.page.timed_turns.body")),
                new TutorialPage(() => Loc.Get("tutorial.page.fix_and_revert.title"),
                    () => Loc.Get("tutorial.page.fix_and_revert.body")),
                new TutorialPage(() => Loc.Get("tutorial.page.hints_and_table.title"),
                    () => Loc.Get("tutorial.page.hints_and_table.body")),
                new TutorialPage(() => Loc.Get("tutorial.page.next_game.title"),
                    () => Loc.Get("tutorial.page.next_game.body"))
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
