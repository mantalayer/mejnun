using System;
using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.AI
{
    public static class BoardFixer
    {
        /// <param name="allowRearrange">
        /// When false (hints off), Fix board never searches for better plays: it only returns hand cards sitting in
        /// invalid sets, and falls back to Revert if that doesn't leave a valid board.
        /// </param>
        public static IReadOnlyList<GameCommand> Plan(MejnunGame game, bool allowRearrange = true)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            if (game.State.Phase != GamePhase.Playing || game.Turn == null)
            {
                return Array.Empty<GameCommand>();
            }

            if (!allowRearrange)
            {
                var returned = TryReturnInvalidHandCards(game);
                return returned.Feasible ? returned.Commands : new[] { GameCommand.Revert(game.State.CurrentSeat) };
            }

            var local = TryPlan(game, false);
            var chosen = local;
            if (game.State.PlayerPhases[game.State.CurrentSeat] == PlayerPhase.Down)
            {
                var global = TryPlan(game, true);
                if (global.Feasible && (!local.Feasible || global.RetainedHandCards > local.RetainedHandCards))
                {
                    chosen = global;
                }
            }

            return chosen.Feasible ? chosen.Commands : new[] { GameCommand.Revert(game.State.CurrentSeat) };
        }

        private static Candidate TryReturnInvalidHandCards(MejnunGame game)
        {
            var scratch = SnapshotSerializer.Read(SnapshotSerializer.Write(game));
            var seat = scratch.State.CurrentSeat;
            var turn = scratch.Turn;
            var startHandIds = new HashSet<byte>();
            for (var index = 0; index < turn.StartSnapshot.Hand.Count; index++)
            {
                startHandIds.Add(turn.StartSnapshot.Hand[index].Id);
            }

            var returnIds = new List<byte>();
            for (var meldIndex = 0; meldIndex < scratch.State.Board.Count; meldIndex++)
            {
                var cards = scratch.State.Board[meldIndex].Cards;
                if (MeldValidator.Classify(cards).IsValid)
                {
                    continue;
                }

                for (var cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    var cardId = cards[cardIndex].Id;
                    if (startHandIds.Contains(cardId) && !turn.IsGoDownCard(cardId))
                    {
                        returnIds.Add(cardId);
                    }
                }
            }

            var commands = new List<GameCommand>();
            if (returnIds.Count > 0)
            {
                var returnCards = GameCommand.MoveCards(seat, returnIds.ToArray(), CardTarget.Hand(0));
                if (!scratch.Apply(returnCards).Ok)
                {
                    return Candidate.Infeasible;
                }

                commands.Add(returnCards);
            }

            return MeldValidator.IsBoardValid(scratch.State.Board)
                ? new Candidate(true, 0, commands)
                : Candidate.Infeasible;
        }

        private static Candidate TryPlan(MejnunGame game, bool global)
        {
            var scratch = SnapshotSerializer.Read(SnapshotSerializer.Write(game));
            var seat = scratch.State.CurrentSeat;
            var turn = scratch.Turn;
            var startBoardCards = new List<Card>();
            var startBoardIds = new HashSet<byte>();
            var startHandIds = new HashSet<byte>();
            var placedHandCards = new List<Card>();
            var goDownCards = new List<Card>();
            var targets = new List<IReadOnlyList<Card>>();
            var required = new List<Card>();
            var optional = new List<Card>();
            var targetCardIds = new HashSet<byte>();

            for (var index = 0; index < turn.StartSnapshot.Board.Count; index++)
            {
                var cards = turn.StartSnapshot.Board[index].Cards;
                for (var cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    startBoardCards.Add(cards[cardIndex]);
                    startBoardIds.Add(cards[cardIndex].Id);
                }
            }

            for (var index = 0; index < turn.StartSnapshot.Hand.Count; index++)
            {
                startHandIds.Add(turn.StartSnapshot.Hand[index].Id);
            }

            for (var meldIndex = 0; meldIndex < scratch.State.Board.Count; meldIndex++)
            {
                var meld = scratch.State.Board[meldIndex];
                for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                {
                    var card = meld.Cards[cardIndex];
                    if (startHandIds.Contains(card.Id))
                    {
                        placedHandCards.Add(card);
                        if (turn.IsGoDownCard(card.Id))
                        {
                            goDownCards.Add(card);
                        }
                    }
                }

                if (!global && MeldValidator.Classify(meld.Cards).IsValid)
                {
                    targets.Add(meld.Cards);
                    AddCardIds(targetCardIds, meld.Cards);
                    continue;
                }

                if (!global)
                {
                    for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                    {
                        var card = meld.Cards[cardIndex];
                        if (startBoardIds.Contains(card.Id))
                        {
                            if (turn.StartSnapshot.PlayerPhase != PlayerPhase.Down)
                            {
                                return Candidate.Infeasible;
                            }

                            required.Add(card);
                        }
                        else if (turn.IsGoDownCard(card.Id))
                        {
                            // Go-down cards can't return to the hand, so they must be re-homed on the board.
                            required.Add(card);
                        }
                        else if (startHandIds.Contains(card.Id))
                        {
                            optional.Add(card);
                        }
                        else
                        {
                            return Candidate.Infeasible;
                        }
                    }
                }
            }

            if (global)
            {
                required.AddRange(startBoardCards);
                required.AddRange(goDownCards);
                foreach (var card in placedHandCards)
                {
                    if (!turn.IsGoDownCard(card.Id))
                    {
                        optional.Add(card);
                    }
                }
            }

            var solution = MeldSolver.Solve(required, optional);
            if (!solution.Feasible || solution.BudgetExceeded)
            {
                return Candidate.Infeasible;
            }

            for (var meldIndex = 0; meldIndex < solution.Melds.Count; meldIndex++)
            {
                targets.Add(solution.Melds[meldIndex]);
                AddCardIds(targetCardIds, solution.Melds[meldIndex]);
            }

            var commands = new List<GameCommand>();
            var unusedHandIds = new List<byte>();
            for (var index = 0; index < placedHandCards.Count; index++)
            {
                var card = placedHandCards[index];
                if (!targetCardIds.Contains(card.Id))
                {
                    unusedHandIds.Add(card.Id);
                }
            }

            if (unusedHandIds.Count > 0)
            {
                var returnCards = GameCommand.MoveCards(seat, unusedHandIds.ToArray(), CardTarget.Hand(0));
                if (!scratch.Apply(returnCards).Ok)
                {
                    return Candidate.Infeasible;
                }

                commands.Add(returnCards);
            }

            if (!TargetBoardExecutor.Rearrange(scratch, seat, targets, commands) ||
                !MeldValidator.IsBoardValid(scratch.State.Board))
            {
                return Candidate.Infeasible;
            }

            var retainedHandCards = 0;
            foreach (var card in placedHandCards)
            {
                if (targetCardIds.Contains(card.Id))
                {
                    retainedHandCards++;
                }
            }

            return new Candidate(true, retainedHandCards, commands);
        }

        private static void AddCardIds(HashSet<byte> ids, IReadOnlyList<Card> cards)
        {
            for (var index = 0; index < cards.Count; index++)
            {
                ids.Add(cards[index].Id);
            }
        }

        private sealed class Candidate
        {
            public static readonly Candidate Infeasible = new Candidate(false, 0, null);

            public Candidate(bool feasible, int retainedHandCards, IReadOnlyList<GameCommand> commands)
            {
                Feasible = feasible;
                RetainedHandCards = retainedHandCards;
                Commands = commands;
            }

            public bool Feasible { get; }
            public int RetainedHandCards { get; }
            public IReadOnlyList<GameCommand> Commands { get; }
        }
    }
}