using System;
using Mejnun.Cosmetics;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class MatchFlowPresenter : MonoBehaviour
    {
        private const float TableExitDuration = 0.7f;
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private MatchFlow matchFlow;

        private VisualElement flowRoot;
        private DealerQuestionScreen dealerQuestionScreen;
        private HandoffScreen handoffScreen;
        private ResultsScreen resultsScreen;
        private MatchFlowScreen currentScreen;
        private MatchFlowScreen pendingScreen;
        private bool hasCurrentScreen;
        private bool dealerExitPending;
        private bool waitingForResultCollection;
        private Action tableExitCompleted;

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (matchFlow == null)
            {
                matchFlow = FindAnyObjectByType<MatchFlow>();
            }

            var root = uiDocument.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            flowRoot = root.Q<VisualElement>("flow-root");
            dealerQuestionScreen = new DealerQuestionScreen(root, matchFlow);
            handoffScreen = new HandoffScreen(root, matchFlow);
            resultsScreen = new ResultsScreen(root, matchFlow, FindAnyObjectByType<DeckSkinLoader>());
            ShowScreen(matchFlow.Screen);
        }

        private void OnEnable()
        {
            SceneFlow.RegisterTableExit(PlayTableExit);
            if (matchFlow != null)
            {
                matchFlow.ScreenChanged += ShowScreen;
            }
        }

        private void OnDisable()
        {
            SceneFlow.UnregisterTableExit(PlayTableExit);
            StopWaitingForResultCollection();
            CompleteTableExit();
            if (matchFlow != null)
            {
                matchFlow.ScreenChanged -= ShowScreen;
            }
        }

        private void ShowScreen(MatchFlowScreen screen)
        {
            if (flowRoot == null)
            {
                return;
            }

            var table = matchFlow == null ? null : matchFlow.Table;
            if (screen == MatchFlowScreen.Results && table != null && table.IsCollectingCards)
            {
                if (!waitingForResultCollection)
                {
                    waitingForResultCollection = true;
                    table.CollectionCompleted += OnResultCollectionCompleted;
                }

                pendingScreen = screen;
                return;
            }

            StopWaitingForResultCollection();

            if (screen == MatchFlowScreen.DealerQuestion)
            {
                dealerExitPending = false;
                ShowScreenNow(screen);
                dealerQuestionScreen.PlayEntrance();
                return;
            }

            if (hasCurrentScreen && currentScreen == MatchFlowScreen.DealerQuestion)
            {
                pendingScreen = screen;
                if (dealerExitPending)
                {
                    return;
                }

                dealerExitPending = true;
                dealerQuestionScreen.PlayExit(() =>
                {
                    if (!dealerExitPending)
                    {
                        return;
                    }

                    dealerExitPending = false;
                    ShowScreenNow(pendingScreen);
                });
                return;
            }

            ShowScreenNow(screen);
        }

        private void ShowScreenNow(MatchFlowScreen screen)
        {
            currentScreen = screen;
            hasCurrentScreen = true;
            var dealerQuestion = screen == MatchFlowScreen.DealerQuestion;
            flowRoot.style.display = screen == MatchFlowScreen.Turn || screen == MatchFlowScreen.Idle
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            flowRoot.EnableInClassList("flow-root-clear", dealerQuestion);
            dealerQuestionScreen.SetVisible(dealerQuestion);
            handoffScreen.SetVisible(screen == MatchFlowScreen.Handoff);
            resultsScreen.SetVisible(screen == MatchFlowScreen.Results);

            if (screen == MatchFlowScreen.DealerQuestion)
            {
                dealerQuestionScreen.Refresh();
            }
            else if (screen == MatchFlowScreen.Handoff)
            {
                handoffScreen.Refresh();
            }
            else if (screen == MatchFlowScreen.Results)
            {
                resultsScreen.Refresh();
            }
        }

        private void PlayTableExit(Action completed)
        {
            var table = matchFlow == null ? null : matchFlow.Table;
            var collectDelay = table == null ? 0f : table.CollectBoardToPile();
            if (matchFlow != null)
            {
                matchFlow.Controller?.SetInputPaused(true);
                if (matchFlow.TurnTimer != null)
                {
                    matchFlow.TurnTimer.Paused = true;
                }

                table?.PlayHandOutro(TableExitDuration, collectDelay);
            }

            if (flowRoot == null || flowRoot.panel == null)
            {
                completed?.Invoke();
                return;
            }

            var rootBounds = flowRoot.panel.visualTree.worldBound;
            var distance = Mathf.Max(flowRoot.worldBound.yMax - rootBounds.yMin, flowRoot.worldBound.height);
            var startedAt = Time.unscaledTime + collectDelay;
            tableExitCompleted = completed;
            tableExitStartedAt = startedAt;
            tableExitDistance = distance;
            tableExitAnimating = true;
        }

        private float tableExitStartedAt;
        private float tableExitDistance;
        private bool tableExitAnimating;

        private void Update()
        {
            if (!tableExitAnimating || Time.unscaledTime < tableExitStartedAt)
            {
                return;
            }

            if (flowRoot == null || flowRoot.panel == null)
            {
                CompleteTableExit();
                return;
            }

            var progress = Mathf.Clamp01((Time.unscaledTime - tableExitStartedAt) / TableExitDuration);
            var eased = progress * progress * (3f - 2f * progress);
            flowRoot.style.translate = new Translate(new Length(0f, LengthUnit.Pixel),
                new Length(-tableExitDistance * eased, LengthUnit.Pixel));
            if (progress >= 1f)
            {
                CompleteTableExit();
            }
        }

        private void OnResultCollectionCompleted()
        {
            StopWaitingForResultCollection();
            ShowScreen(MatchFlowScreen.Results);
        }

        private void StopWaitingForResultCollection()
        {
            if (!waitingForResultCollection)
            {
                return;
            }

            var table = matchFlow == null ? null : matchFlow.Table;
            if (table != null)
            {
                table.CollectionCompleted -= OnResultCollectionCompleted;
            }

            waitingForResultCollection = false;
        }

        private void CompleteTableExit()
        {
            var completed = tableExitCompleted;
            tableExitCompleted = null;
            tableExitAnimating = false;
            completed?.Invoke();
        }
    }
}