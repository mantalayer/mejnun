using System.Collections.Generic;
using System.Linq;
using Mejnun.Core;
using NUnit.Framework;

namespace Mejnun.Game.EditMode.Tests
{
    public sealed class TutorialScriptTests
    {
        [Test]
        public void Deck_HasEveryCardOnceAndDealsTheAuthoredHandsAndDraws()
        {
            var tutorial = MejnunTutorial.Create();

            Assert.That(tutorial.ShoeOrder.Length, Is.EqualTo(Card.CardCount));
            Assert.That(tutorial.ShoeOrder.Distinct().Count(), Is.EqualTo(Card.CardCount));

            var game = new MejnunGame(tutorial.CreateSetup());
            CollectionAssert.AreEqual(tutorial.Hands[MejnunTutorial.PlayerSeat],
                game.State.Hands[MejnunTutorial.PlayerSeat]);
            CollectionAssert.AreEqual(tutorial.Hands[MejnunTutorial.CpuSeat],
                game.State.Hands[MejnunTutorial.CpuSeat]);
            Assert.That(game.State.CurrentSeat, Is.EqualTo(MejnunTutorial.CpuSeat));

            var pile = game.State.DrawPile;
            for (var index = 0; index < tutorial.DrawsTopFirst.Count; index++)
            {
                Assert.That(pile[pile.Count - 1 - index], Is.EqualTo(tutorial.DrawsTopFirst[index]));
            }
        }

        [Test]
        public void Deck_RejectsDuplicateAuthoredCards()
        {
            var hand = MejnunTutorial.Cards("2C", "3C", "4C", "5C", "6C", "7C", "8C", "9C", "10C", "JC", "QC");

            Assert.Throws<System.ArgumentException>(() =>
                TutorialDeck.Build(0, new IReadOnlyList<Card>[] { hand, hand }, null));
        }

        [Test]
        public void Script_StepIdsAreUniqueAndFocusStepsAllowAnAction()
        {
            var steps = MejnunTutorial.Create().Steps;

            Assert.That(steps.Select(step => step.Id).Distinct().Count(), Is.EqualTo(steps.Count));
            foreach (var step in steps.Where(step => step.Solution.Count > 0))
            {
                Assert.That(step.Allowed, Is.Not.EqualTo(TutorialAction.None), step.Id);
                Assert.That(step.IsComplete != null || step.ExpectRejection, Is.True, step.Id);
            }
        }

        [Test]
        public void Script_ReplaysFromDealToMejnunWithEveryStepCompletingInOrder()
        {
            var tutorial = MejnunTutorial.Create();
            var game = new MejnunGame(tutorial.CreateSetup());
            var completed = new List<string>();

            foreach (var step in tutorial.Steps)
            {
                if (step.IsComplete != null)
                {
                    Assert.That(step.IsComplete(game.State, game.Turn), Is.False,
                        step.Id + " was already complete before it started");
                }

                if (step.ExpectRejection)
                {
                    var before = SnapshotSerializer.Write(game);
                    var rejected = false;
                    foreach (var intent in step.Solution)
                    {
                        Assert.That(intent.TryResolve(game.State, MejnunTutorial.PlayerSeat, out var command),
                            Is.True, step.Id);
                        var result = game.Apply(command);
                        Assert.That(result.Ok, Is.False, step.Id + " should be rejected");
                        Assert.That(result.Error, Is.EqualTo(RuleError.NotDownBoardAccess), step.Id);
                        rejected = true;
                    }

                    Assert.That(rejected, Is.True, step.Id);
                    CollectionAssert.AreEqual(before, SnapshotSerializer.Write(game), step.Id + " changed state");
                }
                else if (step.Solution.Count > 0)
                {
                    Assert.That(game.State.CurrentSeat, Is.EqualTo(MejnunTutorial.PlayerSeat), step.Id);
                    Assert.That(CpuIntent.TryResolveAll(game, MejnunTutorial.PlayerSeat, step.Solution,
                        out var commands), Is.True, step.Id + " could not be resolved");
                    foreach (var command in commands)
                    {
                        var result = game.Apply(command);
                        Assert.That(result.Ok, Is.True, step.Id + " " + command.Type + ": " + result.Error);
                    }
                }
                else if (step.IsComplete != null)
                {
                    PlayCpuTurns(tutorial, game);
                }

                if (step.IsComplete != null)
                {
                    Assert.That(step.IsComplete(game.State, game.Turn), Is.True, step.Id + " did not complete");
                }

                completed.Add(step.Id);
            }

            Assert.That(completed.Count, Is.EqualTo(tutorial.Steps.Count));
            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Finished));
            Assert.That(game.State.Result.Kind, Is.EqualTo(GameEndKind.EmptyHand));
            Assert.That(game.State.Result.Winner, Is.EqualTo(MejnunTutorial.PlayerSeat));
            Assert.That(game.State.Hands[MejnunTutorial.PlayerSeat], Is.Empty);
            Assert.That(game.State.Hands[MejnunTutorial.CpuSeat], Is.Not.Empty);
        }

        [Test]
        public void CpuTurns_CoverEveryCpuTurnOfTheScript()
        {
            var tutorial = MejnunTutorial.Create();

            CollectionAssert.AreEquivalent(new[] { 1, 3, 5, 7, 9 }, tutorial.CpuTurns.TurnNumbers);
            Assert.That(tutorial.CpuTurns.TryGetCommands(new MejnunGame(tutorial.CreateSetup()),
                MejnunTutorial.PlayerSeat, 1, out _), Is.False);
        }

        [Test]
        public void CpuIntent_ResolvesMeldIdsAgainstTheLiveBoard()
        {
            var tutorial = MejnunTutorial.Create();
            var game = new MejnunGame(tutorial.CreateSetup());
            PlayCpuTurns(tutorial, game);

            var add = CpuIntent.AddToMeldContaining(MejnunTutorial.C("JS"), MejnunTutorial.C("AS"));
            Assert.That(add.TryResolve(game.State, MejnunTutorial.PlayerSeat, out var command), Is.True);
            Assert.That(command.Type, Is.EqualTo(CommandType.MoveCards));
            Assert.That(command.Target.Kind, Is.EqualTo(TargetKind.Meld));
            Assert.That(command.Target.MeldId, Is.EqualTo(game.State.Board[0].Id));

            var missing = CpuIntent.AddToMeldContaining(MejnunTutorial.C("2D"), MejnunTutorial.C("AS"));
            Assert.That(missing.TryResolve(game.State, MejnunTutorial.PlayerSeat, out _), Is.False);
        }

        private static void PlayCpuTurns(MejnunTutorial tutorial, MejnunGame game)
        {
            while (game.State.Phase == GamePhase.Playing && game.State.CurrentSeat == MejnunTutorial.CpuSeat)
            {
                var turnNumber = game.State.TurnNumber;
                Assert.That(tutorial.CpuTurns.TryGetCommands(game, MejnunTutorial.CpuSeat, turnNumber,
                    out var commands), Is.True, "No scripted CPU commands for turn " + turnNumber);
                foreach (var command in commands)
                {
                    var result = game.Apply(command);
                    Assert.That(result.Ok, Is.True, "CPU turn " + turnNumber + " " + command.Type + ": " + result.Error);
                }

                Assert.That(game.State.TurnNumber, Is.GreaterThan(turnNumber), "CPU turn " + turnNumber + " did not end");
            }
        }
    }
}
