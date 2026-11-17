using System;
using System.Diagnostics;
using Mejnun.Core;
using NUnit.Framework;

namespace Mejnun.AI.Tests
{
    /// <summary>Plays a whole game with bots in every seat, asserting every planned command is legal.</summary>
    internal static class BotSimulation
    {
        public const int MaximumTurns = 3000;

        public static int Play(MejnunGame game, Func<int, BotDifficulty> difficultyForSeat, ulong seed)
        {
            return Play(game, difficultyForSeat, seed, out _);
        }

        public static int Play(MejnunGame game, Func<int, BotDifficulty> difficultyForSeat, ulong seed,
            out double slowestTurnMilliseconds)
        {
            slowestTurnMilliseconds = 0d;
            var stopwatch = new Stopwatch();
            var turns = 0;
            while (game.State.Phase == GamePhase.Playing && turns < MaximumTurns)
            {
                var seat = game.State.CurrentSeat;
                stopwatch.Restart();
                var plan = BotBrain.PlanTurn(game, difficultyForSeat(seat), seed * 7919UL + (ulong)turns);
                stopwatch.Stop();
                slowestTurnMilliseconds = Math.Max(slowestTurnMilliseconds, stopwatch.Elapsed.TotalMilliseconds);
                Assert.That(plan.FallbackReason, Is.Null, "Turn " + turns + " fell back");
                for (var index = 0; index < plan.Commands.Count; index++)
                {
                    var result = game.Apply(plan.Commands[index]);
                    Assert.That(result.Ok, Is.True,
                        "Turn " + turns + " " + plan.Commands[index].Type + ": " + result.Error);
                }

                turns++;
            }

            return turns;
        }
    }
}
