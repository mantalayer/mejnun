using System;
using System.Collections.Generic;

namespace Mejnun.Core
{
    public sealed class MejnunGame
    {
        private const ulong CosmeticSeedSalt = 0x9E3779B97F4A7C15UL;
        private const ulong FeltSeedSalt = 0xD1B54A32D192ED03UL;
        private readonly GameSetup setup;

        public MejnunGame(GameSetup setup)
            : this(setup, true)
        {
        }

        internal MejnunGame(GameSetup setup, bool startWithProvidedDealer)
        {
            this.setup = setup ?? throw new ArgumentNullException(nameof(setup));
            State = new GameState(setup.SeatCount, new Pcg32(setup.Seed));
            Events = new GameEvents();
            AssignCosmetics();

            if (startWithProvidedDealer && setup.Dealer.HasValue)
            {
                StartGame(setup.Dealer.Value);
            }
        }

        private void AssignCosmetics()
        {
            var cosmeticRng = new Pcg32(setup.Seed ^ CosmeticSeedSalt);
            State.DeckFaces = setup.DeckPicks == null
                ? DeckFaceAssigner.Assign(setup.FacePool, cosmeticRng)
                : DeckFaceAssigner.AssignFromPicks(setup.DeckPicks, setup.FacePool, cosmeticRng);
            State.TableFeltId = AssignTableFelt(setup.FeltPool, setup.Seed);
        }

        /// <summary>Resolves the felt a game with this pool and seed will use, before the game exists.</summary>
        public static string AssignTableFelt(IReadOnlyList<string> feltPool, ulong seed)
        {
            return TableFeltAssigner.Assign(feltPool, new Pcg32(seed ^ FeltSeedSalt));
        }

        public GameSetup Setup => setup;
        public GameState State { get; }
        public GameEvents Events { get; }
        public TurnSession Turn { get; private set; }

        public bool CanEndTurn()
        {
            return Turn != null && Turn.CanEndTurn();
        }

        public bool CanGoDown()
        {
            return Turn != null && Turn.CanGoDown();
        }

        internal void RefreshTurnSession()
        {
            if (State.Phase == GamePhase.Playing)
            {
                Turn = new TurnSession(State, Events);
            }
        }

        internal void RestoreTurnSession(BoardSnapshot startSnapshot, System.Collections.Generic.List<Move> moves)
        {
            Turn = startSnapshot == null ? null : new TurnSession(State, Events, startSnapshot, moves);
        }

        public CommandResult Apply(in GameCommand command)
        {
            if (State.Phase == GamePhase.Finished)
            {
                return CommandResult.Failure(RuleError.GameFinished);
            }

            switch (command.Type)
            {
                case CommandType.ChooseDealer:
                    if (State.Phase != GamePhase.ChoosingDealer)
                    {
                        return CommandResult.Failure(RuleError.WrongPhase);
                    }

                    if (command.Seat < 0 || command.Seat >= setup.SeatCount)
                    {
                        return CommandResult.Failure(RuleError.InvalidSeat);
                    }

                    StartGame(command.Seat);
                    return CommandResult.Success();

                case CommandType.ChooseRandomDealer:
                    if (State.Phase != GamePhase.ChoosingDealer)
                    {
                        return CommandResult.Failure(RuleError.WrongPhase);
                    }

                    StartGame(State.Rng.NextInt(setup.SeatCount));
                    return CommandResult.Success();

            }

            if (State.Phase != GamePhase.Playing)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            if (command.Seat < 0 || command.Seat >= setup.SeatCount)
            {
                return CommandResult.Failure(RuleError.InvalidSeat);
            }

            if (command.Seat != State.CurrentSeat)
            {
                return CommandResult.Failure(RuleError.NotYourTurn);
            }

            switch (command.Type)
            {
                case CommandType.MoveCards:
                {
                    var result = Turn.MoveCards(command);
                    if (result.Ok)
                    {
                        CheckEmptyHandWin(command.Seat);
                    }

                    return result;
                }
                case CommandType.SplitMeld:
                {
                    var result = Turn.SplitMeld(command);
                    if (result.Ok)
                    {
                        CheckEmptyHandWin(command.Seat);
                    }

                    return result;
                }
                case CommandType.MergeMelds:
                {
                    var result = Turn.MergeMelds(command);
                    if (result.Ok)
                    {
                        CheckEmptyHandWin(command.Seat);
                    }

                    return result;
                }
                case CommandType.GoDown:
                {
                    var result = Turn.GoDown(command);
                    if (result.Ok)
                    {
                        CheckEmptyHandWin(command.Seat);
                    }

                    return result;
                }
                case CommandType.Undo:
                    return Turn.Undo();
                case CommandType.Revert:
                    return Turn.Revert();
                case CommandType.EndTurn:
                    return EndTurn();
                case CommandType.DrawOrPass:
                    return DrawOrPass();
                case CommandType.TimeoutTurn:
                    return TimeoutTurn();
                default:
                    return CommandResult.Failure(RuleError.UnsupportedCommand);
            }
        }

        public bool IsBoardValid()
        {
            return MeldValidator.IsBoardValid(State.Board);
        }

        private void StartGame(int dealer)
        {
            State.Dealer = dealer;
            State.Phase = GamePhase.Playing;
            Events.RaiseDealerChosen(dealer);

            var shoe = new Shoe();
            shoe.Shuffle(State.Rng);
            Events.RaiseDeckFacesAssigned(State.DeckFaces[0], State.DeckFaces[1]);

            var shoeIndex = 0;
            for (var cardIndex = 0; cardIndex < 11; cardIndex++)
            {
                for (var offset = 1; offset <= setup.SeatCount; offset++)
                {
                    var seat = (dealer + offset) % setup.SeatCount;
                    var card = shoe[shoeIndex++];
                    State.Hands[seat].Add(card);
                    Events.RaiseCardDealt(seat, card);
                }
            }

            for (; shoeIndex < shoe.Count; shoeIndex++)
            {
                State.DrawPile.Add(shoe[shoeIndex]);
            }

            Events.RaiseTableFeltAssigned(State.TableFeltId);

            State.CurrentSeat = (dealer + 1) % setup.SeatCount;
            State.TurnNumber = 1;
            Turn = new TurnSession(State, Events);
            Events.RaiseTurnStarted(State.CurrentSeat, State.TurnNumber);
        }

        private CommandResult EndTurn()
        {
            if (!Turn.CanEndTurn())
            {
                return CommandResult.Failure(RuleError.CannotEndTurn);
            }

            if (State.PlayerPhases[State.CurrentSeat] == PlayerPhase.NotDown)
            {
                var goDownResult = Turn.GoDownImplicit();
                if (!goDownResult.Ok)
                {
                    return goDownResult;
                }
            }

            if (Turn.StartSnapshot.PlayerPhase == PlayerPhase.NotDown &&
                State.PlayerPhases[State.CurrentSeat] == PlayerPhase.Down)
            {
                Events.RaiseDownRevealed(State.CurrentSeat, Turn.NewMeldIds());
            }

            State.ConsecutivePasses = 0;
            AdvanceTurn();
            return CommandResult.Success();
        }

        private CommandResult DrawOrPass()
        {
            return DrawOrPass(true);
        }

        private CommandResult TimeoutTurn()
        {
            var hasPlacedHandCard = Turn.HasHandCardOnBoard();
            var boardIsValid = IsBoardValid();
            if (hasPlacedHandCard && boardIsValid)
            {
                return EndTurn();
            }

            return DrawOrPass(!boardIsValid);
        }

        private CommandResult DrawOrPass(bool revertBoard)
        {
            if (revertBoard)
            {
                Turn.RevertForTurnEnd();
            }

            var seat = State.CurrentSeat;
            if (State.DrawPile.Count > 0)
            {
                var topIndex = State.DrawPile.Count - 1;
                var card = State.DrawPile[topIndex];
                State.DrawPile.RemoveAt(topIndex);
                State.Hands[seat].Add(card);
                State.ConsecutivePasses = 0;
                Events.RaiseDrew(seat, card);
                AdvanceTurn();
            }
            else
            {
                if (!revertBoard)
                {
                    Turn.RevertForTurnEnd();
                }

                State.ConsecutivePasses++;
                Events.RaisePassed(seat);
                AdvanceAfterPass();
            }

            return CommandResult.Success();
        }

        private void AdvanceTurn()
        {
            var previousSeat = State.CurrentSeat;
            State.CurrentSeat = (previousSeat + 1) % setup.SeatCount;
            State.TurnNumber++;
            Turn = new TurnSession(State, Events);
            Events.RaiseTurnEnded(previousSeat, State.CurrentSeat);
            Events.RaiseTurnStarted(State.CurrentSeat, State.TurnNumber);
        }

        private void AdvanceAfterPass()
        {
            var previousSeat = State.CurrentSeat;
            State.CurrentSeat = (previousSeat + 1) % setup.SeatCount;
            State.TurnNumber++;
            Events.RaiseTurnEnded(previousSeat, State.CurrentSeat);

            if (State.ConsecutivePasses >= setup.SeatCount)
            {
                ResolveEmptyPile();
                return;
            }

            Turn = new TurnSession(State, Events);
            Events.RaiseTurnStarted(State.CurrentSeat, State.TurnNumber);
        }

        private void CheckEmptyHandWin(int seat)
        {
            if (State.Phase != GamePhase.Playing || State.Hands[seat].Count != 0 || !IsBoardValid())
            {
                return;
            }

            if (State.PlayerPhases[seat] == PlayerPhase.NotDown)
            {
                State.PlayerPhases[seat] = PlayerPhase.Down;
                Events.RaiseWentDown(seat);
            }

            var result = new GameResult(GameEndKind.EmptyHand, seat, seat);
            State.Result = result;
            State.Phase = GamePhase.Finished;
            Events.RaiseWon(result);
        }

        private void ResolveEmptyPile()
        {
            var winner = -1;
            var fewestCards = int.MaxValue;
            var tied = false;
            for (var seat = 0; seat < State.Hands.Count; seat++)
            {
                var cardCount = State.Hands[seat].Count;
                if (cardCount < fewestCards)
                {
                    fewestCards = cardCount;
                    winner = seat;
                    tied = false;
                }
                else if (cardCount == fewestCards)
                {
                    tied = true;
                }
            }

            if (tied)
            {
                var stalemate = new GameResult(GameEndKind.Stalemate, null, null);
                State.Result = stalemate;
                State.Phase = GamePhase.Finished;
                Events.RaiseStalemate(stalemate);
                return;
            }

            var result = new GameResult(GameEndKind.FewestCards, winner, winner);
            State.Result = result;
            State.Phase = GamePhase.Finished;
            Events.RaiseWon(result);
        }
    }
}