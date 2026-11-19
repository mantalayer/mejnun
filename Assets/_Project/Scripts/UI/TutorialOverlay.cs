using System;
using System.Collections.Generic;
using Mejnun.Core;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    /// <summary>
    /// Renders the tutorial in the HUD: a non-blocking coach popup anchored above the highlighted cards, a blocking
    /// rule modal with a mini card diagram (paged for "Good to know"), a ghost hand that drags from the focus cards
    /// to the target, and the soft "Try the highlighted cards" nudge. Gameplay rules stay in <see cref="TutorialDirector"/>.
    /// </summary>
    public sealed class TutorialOverlay
    {
        private const float GhostCycleSeconds = 1.8f;
        private const float GhostMoveSeconds = 1.1f;
        private const float PopupGap = 18f;
        private const long NudgeMilliseconds = 2600;
        private const float ButtonPulseSeconds = 1.1f;
        private const float ButtonPulseScale = 0.07f;

        private static readonly TutorialButton[] PulseButtons =
        {
            TutorialButton.EndTurn, TutorialButton.Draw, TutorialButton.Undo, TutorialButton.Redo
        };

        private readonly VisualElement root;
        private readonly VisualElement layer;
        private readonly VisualElement coachPopup;
        private readonly Label coachTitle;
        private readonly Label coachBody;
        private readonly Button coachNextButton;
        private readonly VisualElement modalScrim;
        private readonly Label ruleTitle;
        private readonly VisualElement ruleDiagram;
        private readonly Label ruleBody;
        private readonly Label rulePage;
        private readonly Button ruleBackButton;
        private readonly Button ruleOkButton;
        private readonly Label ghostHand;
        private readonly TableView tableView;
        private readonly TableController tableController;
        private readonly Action requestSkip;
        private readonly Action refreshButtons;
        private readonly List<TutorialPage> pages = new List<TutorialPage>();
        private TutorialDirector director;
        private IVisualElementScheduledItem nudgeHide;
        private int pageIndex;
        private float ghostStartTime;

        public TutorialOverlay(VisualElement documentRoot, TableView view, TableController controller,
            Action onSkipRequested, Action onButtonsChanged)
        {
            root = documentRoot;
            tableView = view;
            tableController = controller;
            requestSkip = onSkipRequested;
            refreshButtons = onButtonsChanged;
            layer = root?.Q<VisualElement>("tutorial-layer");
            if (layer == null)
            {
                return;
            }

            coachPopup = layer.Q<VisualElement>("coach-popup");
            coachTitle = layer.Q<Label>("coach-title");
            coachBody = layer.Q<Label>("coach-body");
            coachNextButton = layer.Q<Button>("coach-next-button");
            modalScrim = layer.Q<VisualElement>("rule-modal-scrim");
            ruleTitle = layer.Q<Label>("rule-title");
            ruleDiagram = layer.Q<VisualElement>("rule-diagram");
            ruleBody = layer.Q<Label>("rule-body");
            rulePage = layer.Q<Label>("rule-page");
            ruleBackButton = layer.Q<Button>("rule-back-button");
            ruleOkButton = layer.Q<Button>("rule-ok-button");
            ghostHand = layer.Q<Label>("ghost-hand");

            coachNextButton.clicked += Acknowledge;
            layer.Q<Button>("coach-skip-button").clicked += () => requestSkip?.Invoke();
            ruleOkButton.clicked += NextPage;
            ruleBackButton.clicked += PreviousPage;
        }

        public bool IsBound => director != null;
        public bool IsModalVisible => modalScrim != null && modalScrim.resolvedStyle.display == DisplayStyle.Flex;
        public TutorialStep CurrentStep => director == null ? null : director.CurrentStep;

        public void Bind(TutorialDirector tutorialDirector)
        {
            if (director == tutorialDirector)
            {
                return;
            }

            Unbind();
            director = tutorialDirector;
            if (director == null || layer == null)
            {
                return;
            }

            director.StepStarted += ShowStep;
            director.Nudge += ShowNudge;
            director.Finished += Hide;
            director.Skipped += Hide;
            if (director.IsRunning && director.CurrentStep != null)
            {
                ShowStep(director.CurrentStep);
            }
        }

        public void Unbind()
        {
            if (director != null)
            {
                director.StepStarted -= ShowStep;
                director.Nudge -= ShowNudge;
                director.Finished -= Hide;
                director.Skipped -= Hide;
            }

            director = null;
            Hide();
        }

        /// <summary>Called every frame: keeps the popup anchored after layout / aspect changes and moves the ghost hand.</summary>
        public void Tick()
        {
            UpdateButtonPulse();
            var step = CurrentStep;
            if (layer == null || step == null || step.Ui == TutorialUiKind.Modal)
            {
                SetGhostVisible(false);
                return;
            }

            AnchorCoachPopup(step);
            UpdateGhostHand(step);
        }

        private void ShowStep(TutorialStep step)
        {
            if (layer == null || step == null)
            {
                return;
            }

            layer.style.display = DisplayStyle.Flex;
            ClearNudge();
            ghostStartTime = Time.unscaledTime;
            if (step.Ui == TutorialUiKind.Modal)
            {
                coachPopup.RemoveFromClassList("coach-visible");
                coachPopup.style.display = DisplayStyle.None;
                ShowModal(step);
            }
            else if (step.Ui == TutorialUiKind.Popup)
            {
                modalScrim.style.display = DisplayStyle.None;
                coachPopup.style.display = DisplayStyle.Flex;
                coachTitle.text = step.Title;
                coachBody.text = step.Body;
                coachNextButton.style.display = step.CompletesOnAcknowledge ? DisplayStyle.Flex : DisplayStyle.None;
                coachPopup.AddToClassList("coach-visible");
                AnchorCoachPopup(step);
            }
            else
            {
                modalScrim.style.display = DisplayStyle.None;
                coachPopup.style.display = DisplayStyle.None;
            }

            refreshButtons?.Invoke();
        }

        private void ShowModal(TutorialStep step)
        {
            pages.Clear();
            pages.Add(new TutorialPage(step.Title, step.Body, step.Diagram));
            for (var index = 0; index < step.Pages.Count; index++)
            {
                pages.Add(step.Pages[index]);
            }

            pageIndex = 0;
            modalScrim.style.display = DisplayStyle.Flex;
            ShowPage();
        }

        private void ShowPage()
        {
            var page = pages[pageIndex];
            ruleTitle.text = page.Title;
            ruleBody.text = page.Body;
            BuildDiagram(page.Diagram);
            var paged = pages.Count > 1;
            rulePage.style.display = paged ? DisplayStyle.Flex : DisplayStyle.None;
            rulePage.text = (pageIndex + 1) + " / " + pages.Count;
            ruleBackButton.style.display = paged && pageIndex > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            ruleOkButton.text = !paged ? "Got it" : pageIndex + 1 < pages.Count ? "Next" : "Done";
        }

        private void NextPage()
        {
            if (pageIndex + 1 < pages.Count)
            {
                pageIndex++;
                ShowPage();
                return;
            }

            modalScrim.style.display = DisplayStyle.None;
            Acknowledge();
        }

        private void PreviousPage()
        {
            if (pageIndex > 0)
            {
                pageIndex--;
                ShowPage();
            }
        }

        private void Acknowledge()
        {
            director?.Advance();
        }

        private void BuildDiagram(TutorialDiagram diagram)
        {
            ruleDiagram.Clear();
            ruleDiagram.style.display = diagram == null || diagram.Groups.Count == 0
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            if (diagram == null)
            {
                return;
            }

            for (var groupIndex = 0; groupIndex < diagram.Groups.Count; groupIndex++)
            {
                var group = diagram.Groups[groupIndex];
                var groupElement = new VisualElement();
                groupElement.AddToClassList("diagram-group");
                groupElement.EnableInClassList("diagram-invalid", !group.Valid);
                var cards = new VisualElement();
                cards.AddToClassList("diagram-cards");
                for (var cardIndex = 0; cardIndex < group.Cards.Count; cardIndex++)
                {
                    cards.Add(CreateDiagramCard(group.Cards[cardIndex]));
                }

                groupElement.Add(cards);
                var caption = new Label((group.Valid ? "✓ " : "✗ ") + group.Caption);
                caption.AddToClassList("diagram-caption");
                groupElement.Add(caption);
                ruleDiagram.Add(groupElement);
            }
        }

        public static string CardText(Card card)
        {
            string rank;
            switch (card.Rank)
            {
                case Rank.Ace:
                    rank = "A";
                    break;
                case Rank.Jack:
                    rank = "J";
                    break;
                case Rank.Queen:
                    rank = "Q";
                    break;
                case Rank.King:
                    rank = "K";
                    break;
                default:
                    rank = ((int)card.Rank + 1).ToString();
                    break;
            }

            string suit;
            switch (card.Suit)
            {
                case Suit.Clubs:
                    suit = "♣";
                    break;
                case Suit.Diamonds:
                    suit = "♦";
                    break;
                case Suit.Hearts:
                    suit = "♥";
                    break;
                default:
                    suit = "♠";
                    break;
            }

            return rank + suit + (card.Deck > 0 ? "′" : string.Empty);
        }

        private static Label CreateDiagramCard(Card card)
        {
            var label = new Label(CardText(card));
            label.AddToClassList("diagram-card");
            label.EnableInClassList("diagram-red", card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds);
            label.EnableInClassList("diagram-second-deck", card.Deck > 0);
            return label;
        }

        private void ShowNudge(RuleError error)
        {
            if (coachPopup == null || CurrentStep == null || CurrentStep.Ui != TutorialUiKind.Popup)
            {
                return;
            }

            nudgeHide?.Pause();
            coachPopup.AddToClassList("coach-nudging");
            nudgeHide = coachPopup.schedule.Execute(ClearNudge).StartingIn(NudgeMilliseconds);
            ghostStartTime = Time.unscaledTime;
        }

        private void ClearNudge()
        {
            nudgeHide?.Pause();
            coachPopup?.RemoveFromClassList("coach-nudging");
        }

        private void Hide()
        {
            if (layer == null)
            {
                return;
            }

            ClearNudge();
            coachPopup.RemoveFromClassList("coach-visible");
            coachPopup.style.display = DisplayStyle.None;
            modalScrim.style.display = DisplayStyle.None;
            SetGhostVisible(false);
            layer.style.display = DisplayStyle.None;
            refreshButtons?.Invoke();
        }

        // The popup sits just above the highest highlighted card or target, so it never covers them; with nothing
        // to point at (or no room above) it docks under the turn panel.
        private void AnchorCoachPopup(TutorialStep step)
        {
            if (coachPopup == null || coachPopup.resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            if (!TryGetFocusBounds(step, out var top, out var centerX) ||
                top - PopupGap - coachPopup.resolvedStyle.height < 72f)
            {
                coachPopup.RemoveFromClassList("coach-anchored");
                coachPopup.style.left = StyleKeyword.Null;
                coachPopup.style.top = StyleKeyword.Null;
                return;
            }

            coachPopup.AddToClassList("coach-anchored");
            coachPopup.style.top = top - PopupGap;
            if (root.ClassListContains("phone-layout"))
            {
                // Phones keep the full-width bubble from the stylesheet and only move it vertically.
                coachPopup.style.left = StyleKeyword.Null;
                return;
            }

            var width = coachPopup.resolvedStyle.width;
            var panelWidth = root.resolvedStyle.width;
            var halfWidth = float.IsNaN(width) ? 0f : width * 0.5f;
            if (panelWidth > 0f && !float.IsNaN(panelWidth))
            {
                centerX = Mathf.Clamp(centerX, halfWidth + 8f, Mathf.Max(halfWidth + 8f, panelWidth - halfWidth - 8f));
            }

            coachPopup.style.left = centerX;
        }

        private bool TryGetFocusBounds(TutorialStep step, out float top, out float centerX)
        {
            top = float.MaxValue;
            centerX = 0f;
            var count = 0;
            for (var index = 0; index < step.FocusCards.Count; index++)
            {
                if (TryGetCardPanelPoint(step.FocusCards[index].Id, out var point))
                {
                    top = Mathf.Min(top, point.y);
                    centerX += point.x;
                    count++;
                }
            }

            if (TryGetTargetPanelPoint(step, out var target) && step.Target.Kind != TutorialTargetKind.Button)
            {
                top = Mathf.Min(top, target.y);
                centerX += target.x;
                count++;
            }

            if (count == 0)
            {
                return false;
            }

            centerX /= count;
            return true;
        }

        private void UpdateGhostHand(TutorialStep step)
        {
            if (ghostHand == null)
            {
                return;
            }

            if (step.Allowed == TutorialAction.None || step.Solution.Count == 0 ||
                !TryGetGhostStart(step, out var start) || !TryGetTargetPanelPoint(step, out var end))
            {
                SetGhostVisible(false);
                return;
            }

            SetGhostVisible(true);
            ghostHand.EnableInClassList("ghost-hand-down", step.Target.Kind == TutorialTargetKind.Button);
            if (GameSettings.ReducedMotion)
            {
                ghostHand.style.left = end.x;
                ghostHand.style.top = end.y;
                ghostHand.style.opacity = 0.9f;
                return;
            }

            var cycle = Mathf.Repeat(Time.unscaledTime - ghostStartTime, GhostCycleSeconds);
            var amount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(cycle / GhostMoveSeconds));
            var position = Vector2.Lerp(start, end, amount);
            ghostHand.style.left = position.x;
            ghostHand.style.top = position.y;
            ghostHand.style.opacity = cycle < GhostMoveSeconds ? 0.95f : Mathf.Lerp(0.95f, 0f,
                (cycle - GhostMoveSeconds) / (GhostCycleSeconds - GhostMoveSeconds));
        }

        /// <summary>Gently pulses the scale of any HUD button highlighted with .tutorial-focus.</summary>
        private void UpdateButtonPulse()
        {
            if (root == null)
            {
                return;
            }

            var phase = Mathf.Repeat(Time.unscaledTime, ButtonPulseSeconds) / ButtonPulseSeconds;
            var factor = 1f + ButtonPulseScale * (0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f));
            for (var index = 0; index < PulseButtons.Length; index++)
            {
                var button = root.Q<Button>(ButtonName(PulseButtons[index]));
                if (button == null)
                {
                    continue;
                }

                if (button.ClassListContains("tutorial-focus") && !GameSettings.ReducedMotion)
                {
                    button.style.scale = new Scale(new Vector3(factor, factor, 1f));
                }
                else if (button.style.scale.keyword != StyleKeyword.Null)
                {
                    button.style.scale = StyleKeyword.Null;
                }
            }
        }

        private bool TryGetGhostStart(TutorialStep step, out Vector2 start)
        {
            start = default;
            if (step.Target.Kind == TutorialTargetKind.Button)
            {
                // A button press: the hand (flipped to point down via .ghost-hand-down) moves from above the button
                // down onto it, so it stays on screen when buttons sit near the bottom edge.
                if (!TryGetTargetPanelPoint(step, out var button))
                {
                    return false;
                }

                start = button + new Vector2(0f, -40f);
                return true;
            }

            return step.FocusCards.Count > 0 && TryGetCardPanelPoint(step.FocusCards[0].Id, out start);
        }

        private bool TryGetTargetPanelPoint(TutorialStep step, out Vector2 point)
        {
            point = default;
            switch (step.Target.Kind)
            {
                case TutorialTargetKind.EmptyBoard:
                    return tableView != null && tableView.TryGetNewGroupAnchor(out var slot) &&
                           TryWorldToPanel(slot, out point);
                case TutorialTargetKind.MeldContaining:
                {
                    var game = tableController == null ? null : tableController.Game;
                    var meld = game == null ? null : CpuIntent.FindMeldContaining(game.State, step.Target.Anchor, out _);
                    return meld != null && tableView != null && tableView.TryGetMeldAnchor(meld.Id, out var world) &&
                           TryWorldToPanel(world, out point);
                }
                case TutorialTargetKind.Button:
                {
                    var button = root?.Q<Button>(ButtonName(step.Target.Button));
                    if (button == null || button.resolvedStyle.display == DisplayStyle.None)
                    {
                        return false;
                    }

                    var bounds = button.worldBound;
                    point = root.WorldToLocal(bounds.center);
                    return bounds.width > 0f;
                }
                default:
                    return false;
            }
        }

        private bool TryGetCardPanelPoint(byte cardId, out Vector2 point)
        {
            point = default;
            return tableView != null && tableView.TryGetCardAnchor(cardId, out var world) &&
                   TryWorldToPanel(world, out point);
        }

        private bool TryWorldToPanel(Vector3 world, out Vector2 point)
        {
            point = default;
            if (root == null || root.panel == null || tableView == null || tableView.ViewCamera == null)
            {
                return false;
            }

            point = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, tableView.ViewCamera);
            return !float.IsNaN(point.x) && !float.IsNaN(point.y);
        }

        private void SetGhostVisible(bool visible)
        {
            if (ghostHand == null)
            {
                return;
            }

            ghostHand.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static string ButtonName(TutorialButton button)
        {
            switch (button)
            {
                case TutorialButton.EndTurn:
                    return "end-turn-button";
                case TutorialButton.Draw:
                    return "draw-pass-button";
                case TutorialButton.Undo:
                    return "undo-button";
                case TutorialButton.Redo:
                    return "redo-button";
                default:
                    return string.Empty;
            }
        }
    }
}
