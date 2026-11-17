using System;
using Mejnun.Core;
using NUnit.Framework;

namespace Mejnun.AI.Tests
{
    /// <summary>M3 exit criteria (§9): Hard beats Easy in more than 80% of 500 simulated games.</summary>
    public sealed class BotBenchmarkTests
    {
        private const int Games = 500;

        [Test]
        [Category("Benchmark")]
        [Timeout(600000)]
        public void HardBeatsEasyInMoreThanEightyPercentOfFiveHundredGames()
        {
            var hardWins = 0;
            var decided = 0;
            var slowest = 0d;
            for (var gameIndex = 0; gameIndex < Games; gameIndex++)
            {
                // Seat 0 is Hard and seat 1 is Easy; the dealer alternates so each starts half the games.
                var game = new MejnunGame(new GameSetup(2, 10000UL + (ulong)gameIndex, gameIndex % 2));
                BotSimulation.Play(game, seat => seat == 0 ? BotDifficulty.Hard : BotDifficulty.Easy,
                    (ulong)gameIndex, out var slowestTurn);
                slowest = Math.Max(slowest, slowestTurn);
                Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Finished), "Game " + gameIndex);
                if (game.State.Result.Winner.HasValue)
                {
                    decided++;
                    if (game.State.Result.Winner.Value == 0)
                    {
                        hardWins++;
                    }
                }
            }

            var winRate = (double)hardWins / Games;
            TestContext.WriteLine("Hard won " + hardWins + "/" + Games + " (" + decided + " decided), slowest turn " +
                                  slowest.ToString("F1") + " ms");
            Assert.That(winRate, Is.GreaterThan(0.8));
        }

        [Test]
        [Category("Benchmark")]
        public void HardTurnsOnCrowdedLateGameBoardsStayFast()
        {
            var slowest = 0d;
            for (var gameIndex = 0; gameIndex < 20; gameIndex++)
            {
                var game = new MejnunGame(new GameSetup(2, 777UL + (ulong)gameIndex, 0));
                BotSimulation.Play(game, seat => BotDifficulty.Hard, (ulong)gameIndex, out var slowestTurn);
                slowest = Math.Max(slowest, slowestTurn);
            }

            TestContext.WriteLine("Slowest Hard turn: " + slowest.ToString("F1") + " ms");
            // Desktop editor budget; the 1 s target is for a mid-range phone, which is several times slower.
            Assert.That(slowest, Is.LessThan(250d));
        }
    }
}
