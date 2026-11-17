using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.AI
{
    /// <summary>What a bot wants to do this turn, before it is turned into engine commands.</summary>
    internal sealed class TurnIntent
    {
        public static readonly TurnIntent Draw = new TurnIntent(null, null);

        public TurnIntent(List<IReadOnlyList<Card>> goDownMelds, List<IReadOnlyList<Card>> finalBoard)
        {
            GoDownMelds = goDownMelds ?? new List<IReadOnlyList<Card>>();
            FinalBoard = finalBoard;
        }

        /// <summary>New sets built only from the hand, placed first (this is the go-down when not down).</summary>
        public List<IReadOnlyList<Card>> GoDownMelds { get; }

        /// <summary>The whole board the bot wants to end with, or null to leave the board alone.</summary>
        public List<IReadOnlyList<Card>> FinalBoard { get; }

        public bool IsDraw => GoDownMelds.Count == 0 && FinalBoard == null;
    }

    /// <summary>A bot's legal view of the table: its own hand, the board and its phase. No hidden information.</summary>
    internal sealed class BotContext
    {
        public BotContext(List<Card> hand, List<List<Card>> board, bool isDown)
        {
            Hand = hand;
            Board = board;
            IsDown = isDown;
        }

        public List<Card> Hand { get; }
        public List<List<Card>> Board { get; }
        public bool IsDown { get; }

        public static BotContext From(GameState state, int seat)
        {
            var board = new List<List<Card>>(state.Board.Count);
            for (var index = 0; index < state.Board.Count; index++)
            {
                board.Add(new List<Card>(state.Board[index].Cards));
            }

            return new BotContext(new List<Card>(state.Hands[seat]), board,
                state.PlayerPhases[seat] == PlayerPhase.Down);
        }

        public List<Card> BoardCards()
        {
            var cards = new List<Card>();
            for (var index = 0; index < Board.Count; index++)
            {
                cards.AddRange(Board[index]);
            }

            return cards;
        }
    }

    internal static class BotStrategies
    {
        internal const int EasyMissPercent = 30;
        private const int HardGoDownCandidates = 24;

        public static TurnIntent Plan(BotDifficulty difficulty, BotContext context, Pcg32 rng)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    return PlanEasy(context, rng);
                case BotDifficulty.Medium:
                    return PlanMedium(context);
                default:
                    return PlanHard(context);
            }
        }

        /// <summary>New sets from the hand plus simple end extensions, missing some plays on purpose.</summary>
        internal static TurnIntent PlanEasy(BotContext context, Pcg32 rng)
        {
            var melds = HandMelds(context.Hand, rng, EasyMissPercent);
            if (!context.IsDown)
            {
                return melds.Count == 0 ? TurnIntent.Draw : new TurnIntent(melds, null);
            }

            var board = CopyBoard(context.Board);
            var hand = new List<Card>(context.Hand);
            var placed = AddMelds(board, hand, melds);
            placed += ExtendGreedy(board, hand, rng, EasyMissPercent);
            return placed == 0 ? TurnIntent.Draw : new TurnIntent(null, AsTargets(board));
        }

        /// <summary>Hand sets, then greedy local search: extensions, splitting straights and borrowing spare cards.</summary>
        internal static TurnIntent PlanMedium(BotContext context)
        {
            var melds = HandMelds(context.Hand, null, 0);
            if (!context.IsDown && melds.Count == 0)
            {
                return TurnIntent.Draw;
            }

            var board = CopyBoard(context.Board);
            var hand = new List<Card>(context.Hand);
            var fromHand = AddMelds(board, hand, melds);
            var rearranged = LocalSearch(board, hand);
            if (fromHand + rearranged == 0)
            {
                return TurnIntent.Draw;
            }

            if (!context.IsDown)
            {
                return new TurnIntent(melds, rearranged > 0 ? AsTargets(board) : null);
            }

            return new TurnIntent(null, AsTargets(board));
        }

        /// <summary>Full board re-partition with the exact solver.</summary>
        internal static TurnIntent PlanHard(BotContext context)
        {
            var boardCards = context.BoardCards();
            var full = MeldSolver.Solve(boardCards, context.Hand);
            if (!full.Feasible)
            {
                return PlanMedium(context);
            }

            if (context.IsDown)
            {
                return full.OptionalUsed == 0 ? TurnIntent.Draw : new TurnIntent(null, ToList(full.Melds));
            }

            if (full.OptionalUsed < 3)
            {
                return TurnIntent.Draw;
            }

            // Not down: the turn must start with sets made only from the hand. If the best board already
            // contains such a set among the cards it plays, go down with it and then rearrange.
            var goDown = MeldSolver.SolveHandOnly(full.UsedOptional);
            if (goDown.Feasible && goDown.OptionalUsed >= 3)
            {
                return new TurnIntent(ToList(goDown.Melds), ToList(full.Melds));
            }

            var handOnly = MeldSolver.SolveHandOnly(context.Hand);
            if (!handOnly.Feasible || handOnly.OptionalUsed == 0)
            {
                return TurnIntent.Draw;
            }

            var bestCount = handOnly.OptionalUsed;
            TurnIntent best = new TurnIntent(ToList(handOnly.Melds), null);
            var candidates = HandTriples(context.Hand, HardGoDownCandidates);
            for (var index = 0; index < candidates.Count; index++)
            {
                var required = new List<Card>(boardCards);
                required.AddRange(candidates[index]);
                var rest = Without(context.Hand, candidates[index]);
                var solution = MeldSolver.Solve(required, rest);
                if (!solution.Feasible || solution.OptionalUsed + 3 <= bestCount)
                {
                    continue;
                }

                bestCount = solution.OptionalUsed + 3;
                best = new TurnIntent(new List<IReadOnlyList<Card>> { candidates[index] }, ToList(solution.Melds));
            }

            return best;
        }

        internal static List<IReadOnlyList<Card>> HandMelds(List<Card> hand, Pcg32 rng, int missPercent)
        {
            var solution = MeldSolver.SolveHandOnly(hand);
            var melds = new List<IReadOnlyList<Card>>();
            if (!solution.Feasible)
            {
                return melds;
            }

            for (var index = 0; index < solution.Melds.Count; index++)
            {
                if (rng != null && missPercent > 0 && rng.NextInt(100) < missPercent)
                {
                    continue;
                }

                melds.Add(solution.Melds[index]);
            }

            return melds;
        }

        internal static int ExtendGreedy(List<List<Card>> board, List<Card> hand, Pcg32 rng, int missPercent)
        {
            var placed = 0;
            var missed = new HashSet<byte>();
            var changed = true;
            while (changed)
            {
                changed = false;
                for (var handIndex = hand.Count - 1; handIndex >= 0 && !changed; handIndex--)
                {
                    var card = hand[handIndex];
                    if (missed.Contains(card.Id))
                    {
                        continue;
                    }

                    for (var meldIndex = 0; meldIndex < board.Count; meldIndex++)
                    {
                        if (!IsValidWith(board[meldIndex], card))
                        {
                            continue;
                        }

                        if (rng != null && missPercent > 0 && rng.NextInt(100) < missPercent)
                        {
                            missed.Add(card.Id);
                            break;
                        }

                        board[meldIndex].Add(card);
                        MeldValidator.CanonicalizeStraight(board[meldIndex]);
                        hand.RemoveAt(handIndex);
                        placed++;
                        changed = true;
                        break;
                    }
                }
            }

            return placed;
        }

        /// <summary>Greedy improvement loop used by Medium. Returns the number of hand cards placed.</summary>
        internal static int LocalSearch(List<List<Card>> board, List<Card> hand)
        {
            var placed = 0;
            var progress = true;
            while (progress && hand.Count > 0)
            {
                progress = false;
                for (var handIndex = 0; handIndex < hand.Count && !progress; handIndex++)
                {
                    var card = hand[handIndex];
                    if (TryExtend(board, card) || TrySplitInsert(board, card))
                    {
                        hand.RemoveAt(handIndex);
                        placed++;
                        progress = true;
                    }
                    else
                    {
                        var stolen = TryBorrow(board, hand, handIndex);
                        if (stolen > 0)
                        {
                            placed += stolen;
                            progress = true;
                        }
                    }
                }
            }

            return placed;
        }

        private static bool TryExtend(List<List<Card>> board, Card card)
        {
            for (var meldIndex = 0; meldIndex < board.Count; meldIndex++)
            {
                if (IsValidWith(board[meldIndex], card))
                {
                    board[meldIndex].Add(card);
                    MeldValidator.CanonicalizeStraight(board[meldIndex]);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Insert a card into a straight by splitting it, e.g. 5♥ into 3-4-5-6-7♥ gives 3-4-5 and 5-6-7.</summary>
        private static bool TrySplitInsert(List<List<Card>> board, Card card)
        {
            for (var meldIndex = 0; meldIndex < board.Count; meldIndex++)
            {
                var meld = board[meldIndex];
                if (meld.Count < 4 || meld[0].Suit != card.Suit)
                {
                    continue;
                }

                var kind = MeldValidator.Classify(meld).Kind;
                if (kind != MeldKind.StraightLow && kind != MeldKind.StraightHigh)
                {
                    continue;
                }

                for (var split = 2; split <= meld.Count - 2; split++)
                {
                    var left = meld.GetRange(0, split);
                    var right = meld.GetRange(split, meld.Count - split);
                    var leftWith = new List<Card>(left) { card };
                    var rightWith = new List<Card>(right) { card };
                    if (MeldValidator.Classify(leftWith).IsValid && MeldValidator.Classify(right).IsValid)
                    {
                        ReplaceWithParts(board, meldIndex, leftWith, right);
                        return true;
                    }

                    if (MeldValidator.Classify(left).IsValid && MeldValidator.Classify(rightWith).IsValid)
                    {
                        ReplaceWithParts(board, meldIndex, left, rightWith);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Build a new set around a hand card using other hand cards and "spare" board cards: cards that can leave
        /// their set without breaking it (ends of long straights, any card of a 4-card same-kind set).
        /// </summary>
        private static int TryBorrow(List<List<Card>> board, List<Card> hand, int handIndex)
        {
            var card = hand[handIndex];
            var candidates = new List<(Card card, int meldIndex)>();
            for (var index = 0; index < hand.Count; index++)
            {
                if (index != handIndex && IsNeighbour(card, hand[index]))
                {
                    candidates.Add((hand[index], -1));
                }
            }

            for (var meldIndex = 0; meldIndex < board.Count; meldIndex++)
            {
                var meld = board[meldIndex];
                for (var cardIndex = 0; cardIndex < meld.Count; cardIndex++)
                {
                    if (!IsNeighbour(card, meld[cardIndex]))
                    {
                        continue;
                    }

                    var without = new List<Card>(meld);
                    without.RemoveAt(cardIndex);
                    if (MeldValidator.Classify(without).IsValid)
                    {
                        candidates.Add((meld[cardIndex], meldIndex));
                    }
                }
            }

            var bestScore = -1;
            var bestFirst = -1;
            var bestSecond = -1;
            var triple = new List<Card>(3);
            for (var first = 0; first < candidates.Count; first++)
            {
                for (var second = first + 1; second < candidates.Count; second++)
                {
                    triple.Clear();
                    triple.Add(card);
                    triple.Add(candidates[first].card);
                    triple.Add(candidates[second].card);
                    if (!MeldValidator.Classify(triple).IsValid)
                    {
                        continue;
                    }

                    var firstMeld = candidates[first].meldIndex;
                    var secondMeld = candidates[second].meldIndex;
                    if (firstMeld >= 0 && firstMeld == secondMeld)
                    {
                        var without = new List<Card>(board[firstMeld]);
                        without.Remove(candidates[first].card);
                        without.Remove(candidates[second].card);
                        if (!MeldValidator.Classify(without).IsValid)
                        {
                            continue;
                        }
                    }

                    var score = (firstMeld < 0 ? 1 : 0) + (secondMeld < 0 ? 1 : 0);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestFirst = first;
                        bestSecond = second;
                    }
                }
            }

            if (bestScore < 0)
            {
                return 0;
            }

            var newMeld = new List<Card> { card, candidates[bestFirst].card, candidates[bestSecond].card };
            MeldValidator.CanonicalizeStraight(newMeld);
            var placed = 1;
            hand.RemoveAt(handIndex);
            for (var pick = 0; pick < 2; pick++)
            {
                var candidate = pick == 0 ? candidates[bestFirst] : candidates[bestSecond];
                if (candidate.meldIndex < 0)
                {
                    hand.Remove(candidate.card);
                    placed++;
                }
                else
                {
                    board[candidate.meldIndex].Remove(candidate.card);
                    MeldValidator.CanonicalizeStraight(board[candidate.meldIndex]);
                }
            }

            board.Add(newMeld);
            return placed;
        }

        private static bool IsNeighbour(Card card, Card other)
        {
            if (card.Id == other.Id)
            {
                return false;
            }

            if (card.Rank == other.Rank)
            {
                return card.Suit != other.Suit;
            }

            if (card.Suit != other.Suit)
            {
                return false;
            }

            var low = System.Math.Abs((int)card.Rank - (int)other.Rank);
            var high = System.Math.Abs(HighValue(card.Rank) - HighValue(other.Rank));
            return low <= 2 || high <= 2;
        }

        private static int HighValue(Rank rank)
        {
            return rank == Rank.Ace ? 13 : (int)rank;
        }

        private static void ReplaceWithParts(List<List<Card>> board, int meldIndex, List<Card> first,
            List<Card> second)
        {
            MeldValidator.CanonicalizeStraight(first);
            MeldValidator.CanonicalizeStraight(second);
            board[meldIndex] = first;
            board.Add(second);
        }

        private static bool IsValidWith(List<Card> meld, Card card)
        {
            if (meld.Count >= 13)
            {
                return false;
            }

            meld.Add(card);
            var valid = MeldValidator.Classify(meld).IsValid;
            meld.RemoveAt(meld.Count - 1);
            return valid;
        }

        private static int AddMelds(List<List<Card>> board, List<Card> hand, List<IReadOnlyList<Card>> melds)
        {
            var placed = 0;
            for (var index = 0; index < melds.Count; index++)
            {
                board.Add(new List<Card>(melds[index]));
                for (var cardIndex = 0; cardIndex < melds[index].Count; cardIndex++)
                {
                    hand.Remove(melds[index][cardIndex]);
                    placed++;
                }
            }

            return placed;
        }

        /// <summary>Distinct valid 3-card sets from the hand (doubles collapse to one candidate).</summary>
        internal static List<IReadOnlyList<Card>> HandTriples(List<Card> hand, int limit)
        {
            var triples = new List<IReadOnlyList<Card>>();
            var signatures = new HashSet<int>();
            var triple = new List<Card>(3);
            for (var first = 0; first < hand.Count; first++)
            {
                for (var second = first + 1; second < hand.Count; second++)
                {
                    for (var third = second + 1; third < hand.Count; third++)
                    {
                        triple.Clear();
                        triple.Add(hand[first]);
                        triple.Add(hand[second]);
                        triple.Add(hand[third]);
                        if (!MeldValidator.Classify(triple).IsValid || !signatures.Add(Signature(triple)))
                        {
                            continue;
                        }

                        triples.Add(new List<Card>(triple).AsReadOnly());
                        if (triples.Count >= limit)
                        {
                            return triples;
                        }
                    }
                }
            }

            return triples;
        }

        private static int Signature(List<Card> cards)
        {
            var slots = new int[cards.Count];
            for (var index = 0; index < cards.Count; index++)
            {
                slots[index] = (int)cards[index].Suit * 13 + (int)cards[index].Rank;
            }

            System.Array.Sort(slots);
            var signature = 0;
            for (var index = 0; index < slots.Length; index++)
            {
                signature = signature * 64 + slots[index];
            }

            return signature;
        }

        private static List<Card> Without(List<Card> cards, IReadOnlyList<Card> removed)
        {
            var result = new List<Card>(cards);
            for (var index = 0; index < removed.Count; index++)
            {
                result.Remove(removed[index]);
            }

            return result;
        }

        private static List<List<Card>> CopyBoard(List<List<Card>> board)
        {
            var copy = new List<List<Card>>(board.Count);
            for (var index = 0; index < board.Count; index++)
            {
                copy.Add(new List<Card>(board[index]));
            }

            return copy;
        }

        private static List<IReadOnlyList<Card>> AsTargets(List<List<Card>> board)
        {
            var targets = new List<IReadOnlyList<Card>>(board.Count);
            for (var index = 0; index < board.Count; index++)
            {
                if (board[index].Count > 0)
                {
                    targets.Add(board[index]);
                }
            }

            return targets;
        }

        private static List<IReadOnlyList<Card>> ToList(IReadOnlyList<IReadOnlyList<Card>> melds)
        {
            return new List<IReadOnlyList<Card>>(melds);
        }
    }
}
