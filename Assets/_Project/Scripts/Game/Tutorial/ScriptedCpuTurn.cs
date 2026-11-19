using System;
using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Game
{
    /// <summary>Supplies authored commands for a bot turn; <see cref="BotTurnDriver"/> asks before planning.</summary>
    public interface IScriptedTurnSource
    {
        bool TryGetCommands(MejnunGame game, int seat, int turnNumber, out IReadOnlyList<GameCommand> commands);
    }

    public enum CpuIntentKind
    {
        NewMeld,
        AddToMeldContaining,
        SplitAt,
        Undo,
        EndTurn,
        Draw
    }

    /// <summary>
    /// An authored move that names cards instead of runtime meld ids ("add these cards to the set holding X"),
    /// resolved against the live state when it is played.
    /// </summary>
    public sealed class CpuIntent
    {
        private static readonly Card[] NoCards = new Card[0];
        private readonly Card[] cards;

        private CpuIntent(CpuIntentKind kind, Card anchor, Card[] cards)
        {
            Kind = kind;
            Anchor = anchor;
            this.cards = cards == null ? NoCards : (Card[])cards.Clone();
        }

        public CpuIntentKind Kind { get; }
        public Card Anchor { get; }
        public IReadOnlyList<Card> Cards => cards;

        public static CpuIntent NewMeld(params Card[] cards)
        {
            return new CpuIntent(CpuIntentKind.NewMeld, default, RequireCards(cards));
        }

        public static CpuIntent AddToMeldContaining(Card anchor, params Card[] cards)
        {
            return new CpuIntent(CpuIntentKind.AddToMeldContaining, anchor, RequireCards(cards));
        }

        /// <summary>Splits the straight holding <paramref name="firstOfTail"/> so that card starts the new group.</summary>
        public static CpuIntent SplitAt(Card firstOfTail)
        {
            return new CpuIntent(CpuIntentKind.SplitAt, firstOfTail, null);
        }

        public static CpuIntent Undo()
        {
            return new CpuIntent(CpuIntentKind.Undo, default, null);
        }

        public static CpuIntent EndTurn()
        {
            return new CpuIntent(CpuIntentKind.EndTurn, default, null);
        }

        public static CpuIntent Draw()
        {
            return new CpuIntent(CpuIntentKind.Draw, default, null);
        }

        public bool TryResolve(GameState state, int seat, out GameCommand command)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            command = default;
            switch (Kind)
            {
                case CpuIntentKind.NewMeld:
                    command = GameCommand.MoveCards(seat, CardIds(), CardTarget.NewMeld(0));
                    return true;
                case CpuIntentKind.AddToMeldContaining:
                {
                    var meld = FindMeldContaining(state, Anchor, out _);
                    if (meld == null)
                    {
                        return false;
                    }

                    var remaining = meld.Cards.Count;
                    for (var index = 0; index < cards.Length; index++)
                    {
                        if (meld.Cards.Contains(cards[index]))
                        {
                            remaining--;
                        }
                    }

                    command = GameCommand.MoveCards(seat, CardIds(), CardTarget.Meld(meld.Id, remaining));
                    return true;
                }
                case CpuIntentKind.SplitAt:
                {
                    var meld = FindMeldContaining(state, Anchor, out var index);
                    if (meld == null)
                    {
                        return false;
                    }

                    command = GameCommand.SplitMeld(seat, meld.Id, index);
                    return true;
                }
                case CpuIntentKind.Undo:
                    command = GameCommand.Undo(seat);
                    return true;
                case CpuIntentKind.EndTurn:
                    command = GameCommand.EndTurn(seat);
                    return true;
                case CpuIntentKind.Draw:
                    command = GameCommand.DrawOrPass(seat);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Resolves a sequence of intents by playing them on a copy of the game, so later intents see the meld ids
        /// that earlier ones create. Fails (without touching the live game) if any command would be rejected.
        /// </summary>
        public static bool TryResolveAll(MejnunGame game, int seat, IReadOnlyList<CpuIntent> intents,
            out IReadOnlyList<GameCommand> commands)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            commands = null;
            if (intents == null)
            {
                return false;
            }

            var copy = SnapshotSerializer.Read(SnapshotSerializer.Write(game));
            var resolved = new List<GameCommand>(intents.Count);
            for (var index = 0; index < intents.Count; index++)
            {
                if (intents[index] == null || !intents[index].TryResolve(copy.State, seat, out var command) ||
                    !copy.Apply(command).Ok)
                {
                    return false;
                }

                resolved.Add(command);
            }

            commands = resolved;
            return true;
        }

        public static Meld FindMeldContaining(GameState state, Card card, out int index)
        {
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var meld = state.Board[meldIndex];
                index = meld.Cards.IndexOf(card);
                if (index >= 0)
                {
                    return meld;
                }
            }

            index = -1;
            return null;
        }

        private byte[] CardIds()
        {
            var ids = new byte[cards.Length];
            for (var index = 0; index < cards.Length; index++)
            {
                ids[index] = cards[index].Id;
            }

            return ids;
        }

        private static Card[] RequireCards(Card[] cards)
        {
            if (cards == null || cards.Length == 0)
            {
                throw new ArgumentException("At least one card is required.", nameof(cards));
            }

            return cards;
        }
    }

    /// <summary>Authored bot turns keyed by game turn number, for one scripted seat.</summary>
    public sealed class ScriptedCpuTurns : IScriptedTurnSource
    {
        private readonly Dictionary<int, CpuIntent[]> turns = new Dictionary<int, CpuIntent[]>();

        public ScriptedCpuTurns(int seat)
        {
            Seat = seat;
        }

        public int Seat { get; }
        public IEnumerable<int> TurnNumbers => turns.Keys;

        public ScriptedCpuTurns Add(int turnNumber, params CpuIntent[] intents)
        {
            if (intents == null || intents.Length == 0)
            {
                throw new ArgumentException("A scripted turn needs at least one intent.", nameof(intents));
            }

            turns[turnNumber] = (CpuIntent[])intents.Clone();
            return this;
        }

        public IReadOnlyList<CpuIntent> IntentsFor(int turnNumber)
        {
            return turns.TryGetValue(turnNumber, out var intents) ? intents : null;
        }

        public bool TryGetCommands(MejnunGame game, int seat, int turnNumber, out IReadOnlyList<GameCommand> commands)
        {
            commands = null;
            if (game == null || seat != Seat || !turns.TryGetValue(turnNumber, out var intents))
            {
                return false;
            }

            return CpuIntent.TryResolveAll(game, seat, intents, out commands);
        }
    }
}
