using System;
using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Game
{
    public enum TutorialUiKind
    {
        None,
        Popup,
        Modal
    }

    [Flags]
    public enum TutorialAction
    {
        None = 0,
        MoveCards = 1 << 0,
        Split = 1 << 1,
        Undo = 1 << 2,
        Redo = 1 << 3,
        EndTurn = 1 << 4,
        Draw = 1 << 5
    }

    public enum TutorialButton
    {
        None,
        EndTurn,
        Draw,
        Undo,
        Redo
    }

    public enum TutorialTargetKind
    {
        None,
        EmptyBoard,
        MeldContaining,
        Button
    }

    /// <summary>Where the highlighted cards should go: the empty board, the set holding a card, or a HUD button.</summary>
    public readonly struct TutorialTarget
    {
        private TutorialTarget(TutorialTargetKind kind, Card anchor, TutorialButton button)
        {
            Kind = kind;
            Anchor = anchor;
            Button = button;
        }

        public TutorialTargetKind Kind { get; }
        public Card Anchor { get; }
        public TutorialButton Button { get; }

        public static TutorialTarget None => default;
        public static TutorialTarget EmptyBoard => new TutorialTarget(TutorialTargetKind.EmptyBoard, default, TutorialButton.None);

        public static TutorialTarget MeldContaining(Card anchor)
        {
            return new TutorialTarget(TutorialTargetKind.MeldContaining, anchor, TutorialButton.None);
        }

        public static TutorialTarget ForButton(TutorialButton button)
        {
            return new TutorialTarget(TutorialTargetKind.Button, default, button);
        }
    }

    /// <summary>One row of cards drawn in a rule modal, marked valid (green) or invalid (red).</summary>
    public sealed class TutorialDiagramGroup
    {
        public TutorialDiagramGroup(bool valid, string caption, params Card[] cards)
        {
            Valid = valid;
            Caption = caption ?? string.Empty;
            Cards = cards == null ? new Card[0] : (Card[])cards.Clone();
        }

        public bool Valid { get; }
        public string Caption { get; }
        public IReadOnlyList<Card> Cards { get; }
    }

    public sealed class TutorialDiagram
    {
        public TutorialDiagram(params TutorialDiagramGroup[] groups)
        {
            Groups = groups == null ? new TutorialDiagramGroup[0] : (TutorialDiagramGroup[])groups.Clone();
        }

        public IReadOnlyList<TutorialDiagramGroup> Groups { get; }
    }

    /// <summary>A page of the paged "Good to know" modal.</summary>
    public sealed class TutorialPage
    {
        public TutorialPage(string title, string body, TutorialDiagram diagram = null)
        {
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            Diagram = diagram;
        }

        public string Title { get; }
        public string Body { get; }
        public TutorialDiagram Diagram { get; }
    }

    /// <summary>
    /// One beat of the tutorial: what to show, which cards and target to highlight, which actions are allowed,
    /// and when the beat is done. Completion is checked on the resulting game state, never on a gesture.
    /// </summary>
    public sealed class TutorialStep
    {
        private static readonly Card[] NoCards = new Card[0];
        private static readonly CpuIntent[] NoIntents = new CpuIntent[0];
        private static readonly TutorialPage[] NoPages = new TutorialPage[0];
        private IReadOnlyList<Card> focusCards = NoCards;
        private IReadOnlyList<CpuIntent> solution = NoIntents;
        private IReadOnlyList<TutorialPage> pages = NoPages;

        public TutorialStep(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A step needs an id.", nameof(id));
            }

            Id = id;
            Title = string.Empty;
            Body = string.Empty;
        }

        public string Id { get; }
        public TutorialUiKind Ui { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string PartialBody { get; set; }
        public TutorialDiagram Diagram { get; set; }
        public TutorialTarget Target { get; set; }
        public TutorialAction Allowed { get; set; }

        /// <summary>Null means the step completes when the player presses Next / Got it.</summary>
        public Func<GameState, TurnSession, bool> IsComplete { get; set; }

        /// <summary>The step completes when the player's attempt on the focus cards is rejected (H1a).</summary>
        public bool ExpectRejection { get; set; }

        /// <summary>Keeps the CPU from starting its turn while this step is showing.</summary>
        public bool HoldCpu { get; set; }

        public IReadOnlyList<Card> FocusCards
        {
            get => focusCards;
            set => focusCards = value ?? NoCards;
        }

        /// <summary>The moves the player is expected to make; drives the ghost hand and the headless replay.</summary>
        public IReadOnlyList<CpuIntent> Solution
        {
            get => solution;
            set => solution = value ?? NoIntents;
        }

        public IReadOnlyList<TutorialPage> Pages
        {
            get => pages;
            set => pages = value ?? NoPages;
        }

        public bool CompletesOnAcknowledge => IsComplete == null && !ExpectRejection;

        public IReadOnlyCollection<byte> FocusCardIds()
        {
            var ids = new HashSet<byte>();
            for (var index = 0; index < focusCards.Count; index++)
            {
                ids.Add(focusCards[index].Id);
            }

            return ids;
        }
    }
}
