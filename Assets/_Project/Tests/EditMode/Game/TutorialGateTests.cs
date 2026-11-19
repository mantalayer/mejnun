using System.Linq;
using Mejnun.Core;
using NUnit.Framework;

namespace Mejnun.Game.EditMode.Tests
{
    public sealed class TutorialGateTests
    {
        private static Card C(string notation)
        {
            return MejnunTutorial.C(notation);
        }

        private static (MejnunTutorial tutorial, MejnunGame game, TutorialGate gate) AtStep(string stepId)
        {
            var tutorial = MejnunTutorial.Create();
            var game = new MejnunGame(tutorial.CreateSetup());
            var gate = new TutorialGate(MejnunTutorial.PlayerSeat);
            foreach (var step in tutorial.Steps)
            {
                if (step.Id == stepId)
                {
                    gate.Step = step;
                    return (tutorial, game, gate);
                }

                if (step.ExpectRejection)
                {
                    continue;
                }

                if (step.Solution.Count > 0)
                {
                    Assert.That(CpuIntent.TryResolveAll(game, MejnunTutorial.PlayerSeat, step.Solution,
                        out var commands), Is.True, step.Id);
                    foreach (var command in commands)
                    {
                        Assert.That(game.Apply(command).Ok, Is.True, step.Id);
                    }
                }
                else if (step.IsComplete != null)
                {
                    while (game.State.CurrentSeat == MejnunTutorial.CpuSeat)
                    {
                        Assert.That(tutorial.CpuTurns.TryGetCommands(game, MejnunTutorial.CpuSeat,
                            game.State.TurnNumber, out var cpu), Is.True);
                        foreach (var command in cpu)
                        {
                            Assert.That(game.Apply(command).Ok, Is.True);
                        }
                    }
                }
            }

            Assert.Fail("Unknown step " + stepId);
            return default;
        }

        private static byte[] Ids(params string[] cards)
        {
            return cards.Select(card => C(card).Id).ToArray();
        }

        [Test]
        public void Gate_AllowsPartialFocusCardsAndRequiresTheSameMeldAfterPlacementStarts()
        {
            var (_, game, gate) = AtStep("h1b-go-down");
            var state = game.State;

            Assert.That(gate.Allows(GameCommand.MoveCards(0, Ids("7H", "8H", "9H"), CardTarget.NewMeld(0)),
                CommandOrigin.Gesture, state), Is.True);
            Assert.That(gate.Allows(GameCommand.MoveCards(0, Ids("7H", "8H"), CardTarget.NewMeld(0)),
                CommandOrigin.Gesture, state), Is.True, "a partial selection can start the set");
            Assert.That(gate.Allows(GameCommand.MoveCards(0, Ids("7H"), CardTarget.NewMeld(0)),
                CommandOrigin.Gesture, state), Is.True);
            Assert.That(gate.Allows(GameCommand.MoveCards(0, Ids("5C", "5D", "5S"), CardTarget.NewMeld(0)),
                CommandOrigin.Gesture, state), Is.False, "other cards are refused");
            Assert.That(gate.Allows(GameCommand.EndTurn(0), CommandOrigin.Command, state), Is.False);
            Assert.That(gate.Allows(GameCommand.DrawOrPass(0), CommandOrigin.Command, state), Is.False);

            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("7H"), CardTarget.NewMeld(0))).Ok, Is.True);
            var partialMeld = CpuIntent.FindMeldContaining(game.State, C("7H"), out _);
            Assert.That(gate.Allows(GameCommand.MoveCards(0, Ids("8H"),
                    CardTarget.Meld(partialMeld.Id, partialMeld.Cards.Count)),
                CommandOrigin.Gesture, game.State), Is.True);
            Assert.That(gate.Allows(GameCommand.MoveCards(0, Ids("8H"), CardTarget.NewMeld(0)),
                CommandOrigin.Gesture, game.State), Is.False, "the next card must join the partial set");
        }

        [Test]
        public void Gate_RequiresTheHighlightedSet()
        {
            var (_, game, gate) = AtStep("h1c-ace-high");
            var state = game.State;
            var spades = CpuIntent.FindMeldContaining(state, C("JS"), out _);
            var hearts = CpuIntent.FindMeldContaining(state, C("7H"), out _);

            Assert.That(gate.Allows(GameCommand.MoveCards(0, Ids("AS"), CardTarget.Meld(spades.Id, 3)),
                CommandOrigin.Gesture, state), Is.True);
            Assert.That(gate.Allows(GameCommand.MoveCards(0, Ids("AS"), CardTarget.Meld(hearts.Id, 3)),
                CommandOrigin.Gesture, state), Is.False);
            Assert.That(gate.Allows(GameCommand.MoveCards(0, Ids("AS"), CardTarget.NewMeld(0)),
                CommandOrigin.Gesture, state), Is.False);
        }

        [Test]
        public void Gate_AllowsTheSplitOnlyAtTheFocusTail()
        {
            var (_, game, gate) = AtStep("h2a-split");
            var state = game.State;
            var hearts = CpuIntent.FindMeldContaining(state, C("10H"), out var tenIndex);

            Assert.That(gate.Allows(GameCommand.SplitMeld(0, hearts.Id, tenIndex), CommandOrigin.Gesture, state),
                Is.True);
            Assert.That(gate.Allows(GameCommand.SplitMeld(0, hearts.Id, tenIndex - 1), CommandOrigin.Gesture, state),
                Is.False);
            Assert.That(gate.Allows(GameCommand.MoveCards(0, Ids("10H", "JH"), CardTarget.NewMeld(0)),
                CommandOrigin.Gesture, state), Is.True, "dragging the tail out is the same move");
        }

        [Test]
        public void Gate_UndoAndRedoAreAllowedOnlyInTheirSteps()
        {
            var (_, game, undoGate) = AtStep("h2c-undo");
            Assert.That(undoGate.Allows(GameCommand.Undo(0), CommandOrigin.Undo, game.State), Is.True);
            Assert.That(undoGate.Allows(GameCommand.MoveCards(0, Ids("9H'"), CardTarget.NewMeld(0)),
                CommandOrigin.Redo, game.State), Is.False);

            var (_, redoGame, redoGate) = AtStep("h2c-redo");
            Assert.That(redoGate.Allows(GameCommand.MoveCards(0, Ids("9H'"), CardTarget.NewMeld(0)),
                CommandOrigin.Redo, redoGame.State), Is.True);
            Assert.That(redoGate.Allows(GameCommand.Undo(0), CommandOrigin.Undo, redoGame.State), Is.False);
            Assert.That(redoGate.Allows(GameCommand.Revert(0), CommandOrigin.Command, redoGame.State), Is.False);
        }

        [Test]
        public void Gate_NeverFiltersTheCpuAndBlocksFixBoard()
        {
            var (_, game, gate) = AtStep("h1f-end-turn");

            Assert.That(gate.Allows(GameCommand.DrawOrPass(MejnunTutorial.CpuSeat), CommandOrigin.Command,
                game.State), Is.True);
            Assert.That(gate.Allows(GameCommand.EndTurn(0), CommandOrigin.Command, game.State), Is.True);
            Assert.That(gate.Allows(GameCommand.EndTurn(0), CommandOrigin.FixBoard, game.State), Is.False);

            gate.Step = null;
            Assert.That(gate.Allows(GameCommand.EndTurn(0), CommandOrigin.Command, game.State), Is.False);
        }

        [Test]
        public void NotDownBeatExplainsTheRuleAndWaitsForAcknowledgement()
        {
            var (tutorial, game, gate) = AtStep("h1a-not-down");
            var step = tutorial.Steps.First(candidate => candidate.Id == "h1a-not-down");

            Assert.That(step.CompletesOnAcknowledge, Is.True);
            Assert.That(step.ExpectRejection, Is.False);
            Assert.That(step.Allowed, Is.EqualTo(TutorialAction.None));
            Assert.That(step.Body, Does.Contain("can't use the table until you're Down"));
            Assert.That(gate.Allows(GameCommand.MoveCards(0, Ids("AS"), CardTarget.NewMeld(0)),
                CommandOrigin.Gesture, game.State), Is.False);
        }
    }
}
