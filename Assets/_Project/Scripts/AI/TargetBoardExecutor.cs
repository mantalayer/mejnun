using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.AI
{
    /// <summary>
    /// Turns a target partition of the board into real <see cref="GameCommand"/>s, applying them to a scratch game
    /// so every command is validated by the engine. Sets that already exist are kept and only receive the cards
    /// they are missing, which keeps bot turns short and readable on screen.
    /// </summary>
    internal static class TargetBoardExecutor
    {
        public static bool PlaceNewMelds(MejnunGame game, int seat, IReadOnlyList<IReadOnlyList<Card>> melds,
            List<GameCommand> output)
        {
            for (var index = 0; index < melds.Count && game.State.Phase == GamePhase.Playing; index++)
            {
                if (!Apply(game, GameCommand.MoveCards(seat, Ids(melds[index]), CardTarget.NewMeld(0)), output))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool Rearrange(MejnunGame game, int seat, IReadOnlyList<IReadOnlyList<Card>> targets,
            List<GameCommand> output)
        {
            var board = game.State.Board;
            var targetSets = new List<HashSet<byte>>(targets.Count);
            for (var index = 0; index < targets.Count; index++)
            {
                targetSets.Add(new HashSet<byte>(Ids(targets[index])));
            }

            var bases = AssignBases(board, targetSets);

            for (var index = 0; index < targets.Count && game.State.Phase == GamePhase.Playing; index++)
            {
                if (bases[index] != 0)
                {
                    continue;
                }

                if (!Apply(game, GameCommand.MoveCards(seat, Ids(targets[index]), CardTarget.NewMeld(0)), output))
                {
                    return false;
                }
            }

            for (var index = 0; index < targets.Count && game.State.Phase == GamePhase.Playing; index++)
            {
                if (bases[index] == 0)
                {
                    continue;
                }

                var meld = FindMeld(game.State.Board, bases[index]);
                if (meld == null)
                {
                    return false;
                }

                var missing = new List<byte>();
                for (var cardIndex = 0; cardIndex < targets[index].Count; cardIndex++)
                {
                    var id = targets[index][cardIndex].Id;
                    if (!Contains(meld, id))
                    {
                        missing.Add(id);
                    }
                }

                if (missing.Count > 0 && !Apply(game,
                        GameCommand.MoveCards(seat, missing.ToArray(), CardTarget.Meld(meld.Id, meld.Cards.Count)),
                        output))
                {
                    return false;
                }
            }

            return game.State.Phase != GamePhase.Playing || FixStraightOrder(game, seat, output);
        }

        private static bool FixStraightOrder(MejnunGame game, int seat, List<GameCommand> output)
        {
            var board = game.State.Board;
            for (var meldIndex = 0; meldIndex < board.Count && game.State.Phase == GamePhase.Playing; meldIndex++)
            {
                var meld = board[meldIndex];
                var kind = MeldValidator.Classify(meld.Cards).Kind;
                if (kind != MeldKind.StraightLow && kind != MeldKind.StraightHigh)
                {
                    continue;
                }

                var canonical = new List<Card>(meld.Cards);
                MeldValidator.CanonicalizeStraight(canonical);
                var ordered = true;
                for (var index = 0; index < canonical.Count; index++)
                {
                    ordered &= canonical[index].Id == meld.Cards[index].Id;
                }

                if (!ordered && !Apply(game,
                        GameCommand.MoveCards(seat, new[] { meld.Cards[0].Id }, CardTarget.Meld(meld.Id, 0)),
                        output))
                {
                    return false;
                }
            }

            return true;
        }

        private static int[] AssignBases(IReadOnlyList<Meld> board, List<HashSet<byte>> targets)
        {
            var bases = new int[targets.Count];
            var pairs = new List<(int overlap, int target, int meldIndex)>();
            for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                for (var meldIndex = 0; meldIndex < board.Count; meldIndex++)
                {
                    var overlap = 0;
                    var cards = board[meldIndex].Cards;
                    for (var cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                    {
                        if (targets[targetIndex].Contains(cards[cardIndex].Id))
                        {
                            overlap++;
                        }
                    }

                    if (overlap > 0)
                    {
                        pairs.Add((overlap, targetIndex, meldIndex));
                    }
                }
            }

            pairs.Sort((left, right) =>
            {
                var byOverlap = right.overlap.CompareTo(left.overlap);
                if (byOverlap != 0)
                {
                    return byOverlap;
                }

                var byTarget = left.target.CompareTo(right.target);
                return byTarget != 0 ? byTarget : left.meldIndex.CompareTo(right.meldIndex);
            });

            var usedMelds = new bool[board.Count];
            for (var index = 0; index < pairs.Count; index++)
            {
                var pair = pairs[index];
                if (bases[pair.target] != 0 || usedMelds[pair.meldIndex])
                {
                    continue;
                }

                bases[pair.target] = board[pair.meldIndex].Id;
                usedMelds[pair.meldIndex] = true;
            }

            return bases;
        }

        private static bool Apply(MejnunGame game, GameCommand command, List<GameCommand> output)
        {
            if (!game.Apply(command).Ok)
            {
                return false;
            }

            output.Add(command);
            return true;
        }

        internal static byte[] Ids(IReadOnlyList<Card> cards)
        {
            var ids = new byte[cards.Count];
            for (var index = 0; index < ids.Length; index++)
            {
                ids[index] = cards[index].Id;
            }

            return ids;
        }

        private static bool Contains(Meld meld, byte cardId)
        {
            for (var index = 0; index < meld.Cards.Count; index++)
            {
                if (meld.Cards[index].Id == cardId)
                {
                    return true;
                }
            }

            return false;
        }

        private static Meld FindMeld(IReadOnlyList<Meld> board, int meldId)
        {
            for (var index = 0; index < board.Count; index++)
            {
                if (board[index].Id == meldId)
                {
                    return board[index];
                }
            }

            return null;
        }
    }
}
