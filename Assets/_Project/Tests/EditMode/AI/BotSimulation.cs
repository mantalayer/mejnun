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
                    var command = plan.Commands[index];
                    var result = game.Apply(command);
                    Assert.That(result.Ok, Is.True,
                        "Turn " + turns + " " + command.Type + ": " + result.Error);
                    if (command.Type == CommandType.MoveCards && command.Target.Kind == TargetKind.Meld)
                    {
                        var foundMeld = false;
                        for (var meldIndex = 0; meldIndex < game.State.Board.Count; meldIndex++)
                        {
                            var meld = game.State.Board[meldIndex];
                            if (meld.Id == command.Target.MeldId)
                            {
                                foundMeld = true;
                                Assert.That(MeldValidator.Classify(meld.Cards).IsValid, Is.True,
                                    "Turn " + turns + " left meld " + meld.Id + " invalid after adding cards");
                                break;
                            }
                        }

                        Assert.That(foundMeld, Is.True,
                            "Turn " + turns + " added cards to missing meld " + command.Target.MeldId);
                    }
                }

                turns++;
            }

            return turns;
        }
    }
}
