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
                {
                    if (!Has(step, TutorialAction.MoveCards))
                    {
                        return false;
                    }

                    var partialMeld = step.Target.Kind == TutorialTargetKind.EmptyBoard
                        ? FindPartialMeld(state, step.FocusCards)
                        : null;
                    if (partialMeld == null && SameCards(command.CardIds, step.FocusCards) &&
                        TargetMatches(command.Target, step.Target, state))
                    {
                        return true;
                    }

                    return PartialCardsMatch(command, step, state, partialMeld);
                }
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

        private bool PartialCardsMatch(in GameCommand command, TutorialStep step, GameState state, Meld partialMeld)
        {
            if (step.Target.Kind != TutorialTargetKind.EmptyBoard || command.CardIds.Length == 0 ||
                command.CardIds.Length >= step.FocusCards.Count)
            {
                return false;
            }

            var selected = new HashSet<byte>();
            for (var index = 0; index < command.CardIds.Length; index++)
            {
                var id = command.CardIds[index];
                if (!selected.Add(id) || !ContainsFocusCard(step.FocusCards, id) ||
                    !IsInHand(state, playerSeat, id))
                {
                    return false;
                }
            }

            return partialMeld == null
                ? command.Target.Kind == TargetKind.NewMeld
                : command.Target.Kind == TargetKind.Meld && command.Target.MeldId == partialMeld.Id;
        }

        private static Meld FindPartialMeld(GameState state, IReadOnlyList<Card> focus)
        {
            Meld partialMeld = null;
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var meld = state.Board[meldIndex];
                if (meld.Cards.Count == 0 || meld.Cards.Count >= focus.Count)
                {
                    continue;
                }

                var containsFocusOnly = true;
                for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                {
                    if (!ContainsFocusCard(focus, meld.Cards[cardIndex].Id))
                    {
                        containsFocusOnly = false;
                        break;
                    }
                }

                if (!containsFocusOnly)
                {
                    continue;
                }

                if (partialMeld != null)
                {
                    return null;
                }

                partialMeld = meld;
            }

            return partialMeld;
        }

        private static bool ContainsFocusCard(IReadOnlyList<Card> focus, byte id)
        {
            for (var index = 0; index < focus.Count; index++)
            {
                if (focus[index].Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInHand(GameState state, int seat, byte id)
        {
            for (var index = 0; index < state.Hands[seat].Count; index++)
            {
                if (state.Hands[seat][index].Id == id)
                {
                    return true;
                }
            }

            return false;
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
