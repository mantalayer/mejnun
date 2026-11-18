using Mejnun.Cosmetics;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class MatchFlowPresenter : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private MatchFlow matchFlow;

        private VisualElement flowRoot;
        private DealerQuestionScreen dealerQuestionScreen;
        private HandoffScreen handoffScreen;
        private ResultsScreen resultsScreen;

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
            if (matchFlow != null)
            {
                matchFlow.ScreenChanged += ShowScreen;
            }
        }

        private void OnDisable()
        {
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

            flowRoot.style.display = screen == MatchFlowScreen.Turn || screen == MatchFlowScreen.Idle
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            dealerQuestionScreen.SetVisible(screen == MatchFlowScreen.DealerQuestion);
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
    }
}