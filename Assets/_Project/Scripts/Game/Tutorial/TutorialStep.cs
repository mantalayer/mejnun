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

    /// <summary>
    /// Player-facing tutorial text. Steps hold a lookup (a <c>Loc.Get</c> call) rather than the resolved string, so
    /// reading the text again after a language change returns the new language.
    /// </summary>
    internal static class TutorialText
    {
        public static readonly Func<string> Empty = () => string.Empty;

        public static Func<string> Fixed(string text)
        {
            var value = text ?? string.Empty;
            return () => value;
        }

        public static string Read(Func<string> source)
        {
            return source == null ? null : source() ?? string.Empty;
        }
    }

    /// <summary>One row of cards drawn in a rule modal, marked valid (green) or invalid (red).</summary>
    public sealed class TutorialDiagramGroup
    {
        private readonly Func<string> caption;

        public TutorialDiagramGroup(bool valid, string caption, params Card[] cards)
            : this(valid, TutorialText.Fixed(caption), cards)
        {
        }

        public TutorialDiagramGroup(bool valid, Func<string> caption, params Card[] cards)
        {
            Valid = valid;
            this.caption = caption ?? TutorialText.Empty;
            Cards = cards == null ? new Card[0] : (Card[])cards.Clone();
        }

        public bool Valid { get; }
        public string Caption => TutorialText.Read(caption);
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
        private readonly Func<string> title;
        private readonly Func<string> body;

        public TutorialPage(string title, string body, TutorialDiagram diagram = null)
            : this(TutorialText.Fixed(title), TutorialText.Fixed(body), diagram)
        {
        }

        public TutorialPage(Func<string> title, Func<string> body, TutorialDiagram diagram = null)
        {
            this.title = title ?? TutorialText.Empty;
            this.body = body ?? TutorialText.Empty;
            Diagram = diagram;
        }

        public string Title => TutorialText.Read(title);
        public string Body => TutorialText.Read(body);
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
        private Func<string> title = TutorialText.Empty;
        private Func<string> body = TutorialText.Empty;
        private Func<string> partialBody;

        public TutorialStep(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A step needs an id.", nameof(id));
            }

            Id = id;
        }

        public string Id { get; }
        public TutorialUiKind Ui { get; set; }

        /// <summary>The title in the current language; setting it stores a fixed text.</summary>
        public string Title
        {
            get => TutorialText.Read(title);
            set => title = TutorialText.Fixed(value);
        }

        public string Body
        {
            get => TutorialText.Read(body);
            set => body = TutorialText.Fixed(value);
        }

        /// <summary>Shown instead of <see cref="Body"/> while the focus cards are partly placed; null when unused.</summary>
        public string PartialBody
        {
            get => TutorialText.Read(partialBody);
            set => partialBody = value == null ? null : TutorialText.Fixed(value);
        }

        /// <summary>Lookups for the localized text, resolved each time the text is read.</summary>
        public Func<string> TitleSource
        {
            get => title;
            set => title = value ?? TutorialText.Empty;
        }

        public Func<string> BodySource
        {
            get => body;
            set => body = value ?? TutorialText.Empty;
        }

        public Func<string> PartialBodySource
        {
            get => partialBody;
            set => partialBody = value;
        }

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
