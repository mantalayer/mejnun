using System.Collections.Generic;
using Mejnun.Core;
using NUnit.Framework;
using static Mejnun.AI.Tests.AiTestUtil;

namespace Mejnun.AI.Tests
{
    public sealed class MeldSolverTests
    {
        [Test]
        public void HandOnlyFindsAStraightAndASameKindSet()
        {
            var hand = Cards("3H", "4H", "5H", "9C", "9D", "9S", "KC");

            var solution = MeldSolver.SolveHandOnly(hand);

            Assert.That(solution.Feasible, Is.True);
            Assert.That(solution.OptionalUsed, Is.EqualTo(6));
            AssertValidPartition(solution, new Card[0], hand);
        }

        [Test]
        public void NothingPlayableUsesNoCards()
        {
            var hand = Cards("2C", "5D", "9H", "KS", "KS'");

            var solution = MeldSolver.SolveHandOnly(hand);

            Assert.That(solution.Feasible, Is.True);
            Assert.That(solution.OptionalUsed, Is.Zero);
            Assert.That(solution.Melds, Is.Empty);
        }

        [Test]
        public void DoublesNeverShareASet()
        {
            var hand = Cards("7H", "7H'", "7S");

            var solution = MeldSolver.SolveHandOnly(hand);

            Assert.That(solution.OptionalUsed, Is.Zero);
        }

        [Test]
        public void TwoCopiesOfEveryCardMakeTwoParallelStraights()
        {
            var hand = Cards("4D", "5D", "6D", "4D'", "5D'", "6D'");

            var solution = MeldSolver.SolveHandOnly(hand);

            Assert.That(solution.OptionalUsed, Is.EqualTo(6));
            Assert.That(solution.Melds.Count, Is.EqualTo(2));
            AssertValidPartition(solution, new Card[0], hand);
        }

        [Test]
        public void AceCanBeLowOrHighButNotBothAndNeverWraps()
        {
            Assert.That(MeldSolver.SolveHandOnly(Cards("AS", "2S", "3S")).OptionalUsed, Is.EqualTo(3));
            Assert.That(MeldSolver.SolveHandOnly(Cards("QS", "KS", "AS")).OptionalUsed, Is.EqualTo(3));
            Assert.That(MeldSolver.SolveHandOnly(Cards("KS", "AS", "2S")).OptionalUsed, Is.Zero);
        }

        [Test]
        public void TheTwoAcesOfASuitCanServeBothEndsAtOnce()
        {
            var hand = Cards("AC", "2C", "3C", "QC", "KC", "AC'");

            var solution = MeldSolver.SolveHandOnly(hand);

            Assert.That(solution.OptionalUsed, Is.EqualTo(6));
            AssertValidPartition(solution, new Card[0], hand);
        }

        [Test]
        public void AFullSuitRunFromAceToAceSplitsIntoTwoValidStraights()
        {
            var hand = Cards("AH", "2H", "3H", "4H", "5H", "6H", "7H", "8H", "9H", "10H", "JH", "QH", "KH", "AH'");

            var solution = MeldSolver.SolveHandOnly(hand);

            Assert.That(solution.OptionalUsed, Is.EqualTo(14));
            AssertValidPartition(solution, new Card[0], hand);
        }

        [Test]
        public void ABoardThatCannotBeRepartitionedIsInfeasible()
        {
            var solution = MeldSolver.Solve(Cards("5H", "6H"), Cards("9C"));

            Assert.That(solution.Feasible, Is.False);
            Assert.That(solution.BudgetExceeded, Is.False);
        }

        [Test]
        public void RearrangingSplitsAStraightToPlaceAMiddleCard()
        {
            var board = Cards("3H", "4H", "5H", "6H", "7H");
            var hand = Cards("5H'", "KC");

            var solution = MeldSolver.Solve(board, hand);

            Assert.That(solution.Feasible, Is.True);
            Assert.That(solution.OptionalUsed, Is.EqualTo(1));
            AssertValidPartition(solution, board, hand);
        }

        [Test]
        public void RearrangingTakesACardFromAFourCardSameKindSet()
        {
            var board = Cards("7C", "7D", "7H", "7S", "8S", "9S", "10S");
            var hand = Cards("6S");

            var solution = MeldSolver.Solve(board, hand);

            Assert.That(solution.OptionalUsed, Is.EqualTo(1));
            AssertValidPartition(solution, board, hand);
        }

        [Test]
        public void DuplicateCardIdsAreRejected()
        {
            var card = Parse("4C");

            var solution = MeldSolver.Solve(new[] { card }, new[] { card });

            Assert.That(solution.Feasible, Is.False);
        }

        [Test]
        public void SolverMatchesBruteForceOnSmallAceHeavyPositions()
        {
            var rng = new Pcg32(20261117UL);
            int[] ranks = { 0, 1, 2, 3, 10, 11, 12 };
            for (var trial = 0; trial < 600; trial++)
            {
                var pool = new List<Card>();
                var suits = 1 + rng.NextInt(4);
                for (var deck = 0; deck < 2; deck++)
                {
                    for (var suit = 0; suit < suits; suit++)
                    {
                        for (var index = 0; index < ranks.Length; index++)
                        {
                            pool.Add(Card.FromId(deck * Card.CardsPerDeck + suit * 13 + ranks[index]));
                        }
                    }
                }

                Shuffle(pool, rng);
                var requiredCount = rng.NextInt(7);
                var optionalCount = rng.NextInt(7);
                var required = pool.GetRange(0, requiredCount);
                var optional = pool.GetRange(requiredCount, optionalCount);

                var expected = BruteForce(required, optional);
                var solution = MeldSolver.Solve(required, optional);

                Assert.That(solution.Feasible ? solution.OptionalUsed : -1, Is.EqualTo(expected),
                    "Trial " + trial);
                if (solution.Feasible)
                {
                    AssertValidPartition(solution, required, optional);
                }
            }
        }

        [Test]
        public void RandomLargePositionsAlwaysReturnValidPartitionsQuickly()
        {
            var rng = new Pcg32(42UL);
            var all = new List<Card>(Card.CardCount);
            for (var id = 0; id < Card.CardCount; id++)
            {
                all.Add(Card.FromId(id));
            }

            for (var trial = 0; trial < 40; trial++)
            {
                Shuffle(all, rng);
                var hand = all.GetRange(0, 10 + rng.NextInt(25));
                var solution = MeldSolver.SolveHandOnly(hand);
                Assert.That(solution.Feasible, Is.True);
                Assert.That(solution.BudgetExceeded, Is.False);
                AssertValidPartition(solution, new Card[0], hand);

                // The cards it placed form a valid board, so they must be repartitionable as required cards.
                var board = new List<Card>(solution.UsedOptional);
                var rest = new List<Card>(hand);
                rest.RemoveAll(card => board.Contains(card));
                var again = MeldSolver.Solve(board, rest);
                Assert.That(again.Feasible, Is.True);
                Assert.That(again.OptionalUsed, Is.Zero, "An optimal hand-only partition cannot be improved.");
            }
        }

        private static void Shuffle(List<Card> cards, Pcg32 rng)
        {
            for (var index = cards.Count - 1; index > 0; index--)
            {
                var other = rng.NextInt(index + 1);
                var swap = cards[index];
                cards[index] = cards[other];
                cards[other] = swap;
            }
        }

        private static int BruteForce(List<Card> required, List<Card> optional)
        {
            var best = -1;
            for (var mask = 0; mask < 1 << optional.Count; mask++)
            {
                var used = 0;
                var cards = new List<Card>(required);
                for (var bit = 0; bit < optional.Count; bit++)
                {
                    if ((mask & (1 << bit)) != 0)
                    {
                        cards.Add(optional[bit]);
                        used++;
                    }
                }

                if (used > best && CanPartition(cards))
                {
                    best = used;
                }
            }

            return best;
        }

        private static bool CanPartition(List<Card> cards)
        {
            if (cards.Count == 0)
            {
                return true;
            }

            var first = cards[0];
            var rest = cards.GetRange(1, cards.Count - 1);
            return TryGroup(rest, 0, new List<Card> { first });
        }

        private static bool TryGroup(List<Card> rest, int start, List<Card> group)
        {
            if (group.Count >= 3 && MeldValidator.Classify(group).IsValid)
            {
                var remaining = new List<Card>(rest);
                remaining.RemoveAll(group.Contains);
                if (CanPartition(remaining))
                {
                    return true;
                }
            }

            if (group.Count >= 13)
            {
                return false;
            }

            for (var index = start; index < rest.Count; index++)
            {
                group.Add(rest[index]);
                var found = TryGroup(rest, index + 1, group);
                group.RemoveAt(group.Count - 1);
                if (found)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
