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

            var pending = new bool[targets.Count];
            var pendingCount = 0;
            for (var index = 0; index < targets.Count; index++)
            {
                pending[index] = bases[index] != 0;
                if (pending[index])
                {
                    pendingCount++;
                }
            }

            while (pendingCount > 0 && game.State.Phase == GamePhase.Playing)
            {
                var progressed = false;
                for (var index = 0; index < targets.Count; index++)
                {
                    if (!pending[index])
                    {
                        continue;
                    }

                    var meld = FindMeld(game.State.Board, bases[index]);
                    if (meld == null)
                    {
                        return false;
                    }

                    var missing = MissingCards(meld, targets[index]);
                    if (missing.Count == 0)
                    {
                        pending[index] = false;
                        pendingCount--;
                        progressed = true;
                        break;
                    }

                    var resultingCards = new List<Card>(meld.Cards.Count + missing.Count);
                    resultingCards.AddRange(meld.Cards);
                    resultingCards.AddRange(missing);
                    if (!MeldValidator.CanPlaceInto(meld.Cards, resultingCards))
                    {
                        var cardsOutsideTarget = new List<Card>();
                        for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                        {
                            var card = meld.Cards[cardIndex];
                            if (!targetSets[index].Contains(card.Id))
                            {
                                cardsOutsideTarget.Add(card);
                            }
                        }

                        if (cardsOutsideTarget.Count == 0 ||
                            !MoveToTemporaryMelds(game, seat, cardsOutsideTarget, output))
                        {
                            return false;
                        }

                        progressed = true;
                        break;
                    }

                    if (!CanAddCards(meld.Cards, missing))
                    {
                        continue;
                    }

                    if (!Apply(game, GameCommand.MoveCards(seat, Ids(missing),
                            CardTarget.Meld(meld.Id, meld.Cards.Count)), output))
                    {
                        return false;
                    }

                    pending[index] = false;
                    pendingCount--;
                    progressed = true;
                    break;
                }

                if (progressed)
                {
                    continue;
                }

                var blockedIndex = 0;
                while (!pending[blockedIndex])
                {
                    blockedIndex++;
                }

                var blockedMeld = FindMeld(game.State.Board, bases[blockedIndex]);
                if (blockedMeld == null)
                {
                    return false;
                }

                var blockedMissing = MissingCards(blockedMeld, targets[blockedIndex]);
                var extraneous = FindExtraneousCards(blockedMeld, targetSets[blockedIndex], blockedMissing);
                var requiredRemovals = blockedMeld.Cards.Count + blockedMissing.Count - 13;
                for (var cardIndex = 0; extraneous.Count < requiredRemovals &&
                     cardIndex < blockedMeld.Cards.Count; cardIndex++)
                {
                    var card = blockedMeld.Cards[cardIndex];
                    if (!targetSets[blockedIndex].Contains(card.Id) && !Contains(extraneous, card.Id))
                    {
                        extraneous.Add(card);
                    }
                }

                if (extraneous.Count == 0 || extraneous.Count < requiredRemovals ||
                    !MoveToTemporaryMelds(game, seat, extraneous, output))
                {
                    return false;
                }
            }

            return game.State.Phase != GamePhase.Playing || FixStraightOrder(game, seat, output);
        }

        private static List<Card> MissingCards(Meld meld, IReadOnlyList<Card> target)
        {
            var missing = new List<Card>();
            for (var index = 0; index < target.Count; index++)
            {
                if (!Contains(meld, target[index].Id))
                {
                    missing.Add(target[index]);
                }
            }

            return missing;
        }

        private static bool CanAddCards(IReadOnlyList<Card> existing, IReadOnlyList<Card> added)
        {
            if (existing.Count + added.Count > 13)
            {
                return false;
            }

            var cards = new List<Card>(existing.Count + added.Count);
            for (var index = 0; index < existing.Count; index++)
            {
                for (var cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    if (existing[index].IsDoubleOf(cards[cardIndex]))
                    {
                        return false;
                    }
                }

                cards.Add(existing[index]);
            }

            for (var index = 0; index < added.Count; index++)
            {
                for (var cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    if (added[index].IsDoubleOf(cards[cardIndex]))
                    {
                        return false;
                    }
                }

                cards.Add(added[index]);
            }

            return true;
        }

        private static List<Card> FindExtraneousCards(Meld meld, HashSet<byte> target, IReadOnlyList<Card> missing)
        {
            var extraneous = new List<Card>();
            for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
            {
                var card = meld.Cards[cardIndex];
                if (target.Contains(card.Id))
                {
                    continue;
                }

                for (var missingIndex = 0; missingIndex < missing.Count; missingIndex++)
                {
                    if (card.IsDoubleOf(missing[missingIndex]))
                    {
                        extraneous.Add(card);
                        break;
                    }
                }
            }

            return extraneous;
        }

        private static bool MoveToTemporaryMelds(MejnunGame game, int seat, IReadOnlyList<Card> cards,
            List<GameCommand> output)
        {
            var groups = new List<List<Card>>();
            for (var index = 0; index < cards.Count; index++)
            {
                var card = cards[index];
                List<Card> group = null;
                for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    var candidate = groups[groupIndex];
                    if (candidate.Count >= 13)
                    {
                        continue;
                    }

                    var hasDouble = false;
                    for (var cardIndex = 0; cardIndex < candidate.Count; cardIndex++)
                    {
                        hasDouble |= card.IsDoubleOf(candidate[cardIndex]);
                    }

                    if (!hasDouble)
                    {
                        group = candidate;
                        break;
                    }
                }

                if (group == null)
                {
                    group = new List<Card>();
                    groups.Add(group);
                }

                group.Add(card);
            }

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                if (!Apply(game, GameCommand.MoveCards(seat, Ids(groups[groupIndex]), CardTarget.NewMeld(0)), output))
                {
                    return false;
                }
            }

            return true;
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

        private static bool Contains(IReadOnlyList<Card> cards, byte cardId)
        {
            for (var index = 0; index < cards.Count; index++)
            {
                if (cards[index].Id == cardId)
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
