using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Game
{
    /// <summary>
    /// Lets the tutorial player do only what the current step asks: move exactly the highlighted cards to the
    /// highlighted target, or press the highlighted button. Other seats (the scripted CPU) are never filtered.
    /// </summary>
    public sealed class TutorialGate : ICommandGate
    {
        private readonly int playerSeat;

        public TutorialGate(int playerSeat)
        {
            this.playerSeat = playerSeat;
        }

        public TutorialStep Step { get; set; }

        public bool Allows(in GameCommand command, CommandOrigin origin, GameState state)
        {
            if (command.Seat != playerSeat)
            {
                return true;
            }

            var step = Step;
            if (step == null || state == null)
            {
                return false;
            }

            switch (origin)
            {
                case CommandOrigin.Undo:
                    return Has(step, TutorialAction.Undo);
                case CommandOrigin.Redo:
                    return Has(step, TutorialAction.Redo);
                case CommandOrigin.FixBoard:
                    return false;
            }

            switch (command.Type)
            {
                case CommandType.MoveCards:
                    return Has(step, TutorialAction.MoveCards) && SameCards(command.CardIds, step.FocusCards) &&
                           TargetMatches(command.Target, step.Target, state);
                case CommandType.SplitMeld:
                    return Has(step, TutorialAction.Split) && SplitMatches(command, step, state);
                case CommandType.GoDown:
                    return Has(step, TutorialAction.MoveCards);
                case CommandType.Undo:
                    return Has(step, TutorialAction.Undo);
                case CommandType.EndTurn:
                    return Has(step, TutorialAction.EndTurn);
                case CommandType.DrawOrPass:
                    return Has(step, TutorialAction.Draw);
                case CommandType.TimeoutTurn:
                    return true;
                default:
                    return false;
            }
        }

        private static bool Has(TutorialStep step, TutorialAction action)
        {
            return (step.Allowed & action) == action;
        }

        private static bool SameCards(IReadOnlyList<byte> cardIds, IReadOnlyList<Card> focus)
        {
            if (cardIds.Count == 0 || cardIds.Count != focus.Count)
            {
                return false;
            }

            for (var index = 0; index < focus.Count; index++)
            {
                var found = false;
                for (var idIndex = 0; idIndex < cardIds.Count; idIndex++)
                {
                    if (cardIds[idIndex] == focus[index].Id)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TargetMatches(CardTarget target, TutorialTarget expected, GameState state)
        {
            switch (expected.Kind)
            {
                case TutorialTargetKind.EmptyBoard:
                    return target.Kind == TargetKind.NewMeld;
                case TutorialTargetKind.MeldContaining:
                {
                    if (target.Kind != TargetKind.Meld)
                    {
                        return false;
                    }

                    var meld = CpuIntent.FindMeldContaining(state, expected.Anchor, out _);
                    return meld != null && meld.Id == target.MeldId;
                }
                case TutorialTargetKind.None:
                    return target.Kind != TargetKind.Hand;
                default:
                    return false;
            }
        }

        private static bool SplitMatches(in GameCommand command, TutorialStep step, GameState state)
        {
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var meld = state.Board[meldIndex];
                if (meld.Id != command.MeldId)
                {
                    continue;
                }

                if (command.Index <= 0 || command.Index >= meld.Cards.Count)
                {
                    return false;
                }

                var tail = new byte[meld.Cards.Count - command.Index];
                for (var index = 0; index < tail.Length; index++)
                {
                    tail[index] = meld.Cards[command.Index + index].Id;
                }

                return SameCards(tail, step.FocusCards);
            }

            return false;
        }
    }
}
