using System.Collections.Generic;
using Mejnun.Core;
using NUnit.Framework;
using static Mejnun.AI.Tests.AiTestUtil;

namespace Mejnun.AI.Tests
{
    public sealed class BotBrainTests
    {
        [TestCase(BotDifficulty.Easy)]
        [TestCase(BotDifficulty.Medium)]
        [TestCase(BotDifficulty.Hard)]
        public void NotDownBotGoesDownWithHandSetsOnly(BotDifficulty difficulty)
        {
            var game = Game(new[] { "3H", "4H", "5H", "KD" }, new[] { new[] { "9C", "9D", "9S" } }, false);

            var plan = PlanWithoutMisses(game, difficulty);

            Assert.That(plan.FallbackReason, Is.Null);
            Assert.That(plan.CardsPlayed, Is.EqualTo(3));
            ApplyAll(game, plan);
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));
            Assert.That(game.State.CurrentSeat, Is.EqualTo(1));
            Assert.That(game.State.Board.Count, Is.EqualTo(2));
        }

        [TestCase(BotDifficulty.Easy)]
        [TestCase(BotDifficulty.Medium)]
        [TestCase(BotDifficulty.Hard)]
        public void BotWithNothingToPlayDraws(BotDifficulty difficulty)
        {
            var game = Game(new[] { "2C", "7D", "QS" }, new[] { new[] { "9C", "9D", "9S" } }, true);

            var plan = BotBrain.PlanTurn(game, difficulty, 1UL);

            Assert.That(plan.Draws, Is.True);
            Assert.That(plan.Commands.Count, Is.EqualTo(1));
            ApplyAll(game, plan);
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(4));
        }

        [Test]
        public void NotDownBotNeverTouchesTheBoardWithoutHandSets()
        {
            // 6S would extend the board straight, but a player who is not down may not use the board.
            var game = Game(new[] { "6S", "2D" }, new[] { new[] { "7S", "8S", "9S" } }, false);

            for (var difficulty = BotDifficulty.Easy; difficulty <= BotDifficulty.Hard; difficulty++)
            {
                Assert.That(BotBrain.PlanTurn(game, difficulty, 3UL).Draws, Is.True, difficulty.ToString());
            }
        }

        [Test]
        public void HardSplitsAStraightAndBorrowsFromASameKindSet()
        {
            var game = Game(new[] { "5H'", "6S" },
                new[] { new[] { "3H", "4H", "5H", "6H", "7H" }, new[] { "7C", "7D", "7H'", "7S" }, new[] { "8S", "9S", "10S" } },
                true);

            var plan = BotBrain.PlanTurn(game, BotDifficulty.Hard, 5UL);

            Assert.That(plan.FallbackReason, Is.Null);
            ApplyAll(game, plan);
            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Finished));
            Assert.That(game.State.Result.Winner, Is.EqualTo(0));
            Assert.That(MeldValidator.IsBoardValid(game.State.Board), Is.True);
        }

        [Test]
        public void MediumExtendsAndSplitsWithLocalSearch()
        {
            var game = Game(new[] { "8H", "5H'", "KC" }, new[] { new[] { "3H", "4H", "5H", "6H", "7H" } }, true);

            var plan = BotBrain.PlanTurn(game, BotDifficulty.Medium, 5UL);

            Assert.That(plan.FallbackReason, Is.Null);
            Assert.That(plan.CardsPlayed, Is.EqualTo(2));
            ApplyAll(game, plan);
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(1));
        }

        [Test]
        public void HardGoesDownAndThenUsesTheBoardInTheSameTurn()
        {
            var game = Game(new[] { "JC", "JD", "JS", "10H" }, new[] { new[] { "7H", "8H", "9H" } }, false);

            var plan = BotBrain.PlanTurn(game, BotDifficulty.Hard, 9UL);

            Assert.That(plan.FallbackReason, Is.Null);
            Assert.That(plan.Commands, Has.None.Matches<GameCommand>(command => command.Type == CommandType.GoDown));
            ApplyAll(game, plan);
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));
            Assert.That(game.State.Result.Winner, Is.EqualTo(0));
        }

        [Test]
        public void PlanningNeverChangesTheLiveGame()
        {
            var game = Game(new[] { "3H", "4H", "5H" }, new string[0][], false);
            var before = SnapshotSerializer.Write(game);

            BotBrain.PlanTurn(game, BotDifficulty.Hard, 1UL);

            CollectionAssert.AreEqual(before, SnapshotSerializer.Write(game));
        }

        [Test]
        public void PlansAreDeterministicForTheSameSnapshotAndSeed()
        {
            var game = new MejnunGame(new GameSetup(3, 77UL, 0));
            var snapshot = SnapshotSerializer.Write(game);

            for (var difficulty = BotDifficulty.Easy; difficulty <= BotDifficulty.Hard; difficulty++)
            {
                var first = BotBrain.PlanTurn(snapshot, difficulty, 11UL);
                var second = BotBrain.PlanTurn(snapshot, difficulty, 11UL);
                Assert.That(Describe(first), Is.EqualTo(Describe(second)), difficulty.ToString());
            }
        }

        [Test]
        public void HoldBackBotSometimesDrawsInsteadOfPlayingAValidMeld()
        {
            var game = Game(new[] { "3H", "4H", "5H", "KD" }, new string[0][], false);
            var foundHoldBack = false;
            for (var seed = 0UL; seed < 256UL; seed++)
            {
                var balanced = BotBrain.PlanTurn(game, BotDifficulty.Medium, seed, BotStyle.Balanced);
                var holdBack = BotBrain.PlanTurn(game, BotDifficulty.Medium, seed, BotStyle.HoldBack);
                if (!balanced.Draws && holdBack.Draws)
                {
                    foundHoldBack = true;
                    break;
                }
            }

            Assert.That(foundHoldBack, Is.True);
        }

        [Test]
        public void KnownExtenderIsIgnoredAfterOpponentDrewWhileDown()
        {
            var known = new BotKnowledge(new[] { new byte[0], new[] { Parse("6S").Id } },
                new[] { false, false }, new[] { 14, 2 });
            var likelyStuck = new BotKnowledge(new[] { new byte[0], new[] { Parse("6S").Id } },
                new[] { false, true }, new[] { 14, 2 });
            var board = new List<IReadOnlyList<Card>> { Cards("7S", "8S", "9S") };

            Assert.That(known.WouldEnableKnownExtender(board, 0), Is.True);
            Assert.That(likelyStuck.WouldEnableKnownExtender(board, 0), Is.False);
        }

        [TestCase(BotDifficulty.Easy, 2)]
        [TestCase(BotDifficulty.Medium, 4)]
        [TestCase(BotDifficulty.Hard, 6)]
        public void BotsFinishWholeGamesWithOnlyLegalCommands(BotDifficulty difficulty, int seats)
        {
            for (var gameIndex = 0; gameIndex < 6; gameIndex++)
            {
                var game = new MejnunGame(new GameSetup(seats, 500UL + (ulong)gameIndex, gameIndex % seats));
                var turns = BotSimulation.Play(game, seat => difficulty, (ulong)gameIndex);
                Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Finished), "Game " + gameIndex);
                Assert.That(turns, Is.LessThan(BotSimulation.MaximumTurns));
            }
        }

        [Test]
        public void MixedTablesFinishWithAWinnerOrStalemate()
        {
            var game = new MejnunGame(new GameSetup(3, 9001UL, 1));

            BotSimulation.Play(game, seat => (BotDifficulty)(seat % 3), 4UL);

            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Finished));
            Assert.That(game.State.Result, Is.Not.Null);
        }

        private static BotTurnPlan PlanWithoutMisses(MejnunGame game, BotDifficulty difficulty)
        {
            // Easy misses plays at random; try a few seeds and keep the first plan that plays.
            for (var seed = 1UL; seed < 64UL; seed++)
            {
                var plan = BotBrain.PlanTurn(game, difficulty, seed);
                if (!plan.Draws)
                {
                    return plan;
                }
            }

            return BotBrain.PlanTurn(game, difficulty, 0UL);
        }

        private static void ApplyAll(MejnunGame game, BotTurnPlan plan)
        {
            for (var index = 0; index < plan.Commands.Count; index++)
            {
                var result = game.Apply(plan.Commands[index]);
                Assert.That(result.Ok, Is.True, plan.Commands[index].Type + ": " + result.Error);
            }
        }

        private static string Describe(BotTurnPlan plan)
        {
            var parts = new List<string>();
            for (var index = 0; index < plan.Commands.Count; index++)
            {
                var command = plan.Commands[index];
                parts.Add(command.Type + ":" + string.Join(",", command.CardIds) + ">" + command.Target.Kind + "/" +
                          command.Target.MeldId + "/" + command.Target.Index);
            }

            return string.Join(" ", parts);
        }
    }
}
