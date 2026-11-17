using Mejnun.Game;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class ResultsScreen
    {
        private readonly VisualElement root;
        private readonly MatchFlow matchFlow;
        private readonly Label title;
        private readonly Label message;

        public ResultsScreen(VisualElement documentRoot, MatchFlow flow)
        {
            root = documentRoot.Q<VisualElement>("results-screen");
            matchFlow = flow;
            title = root.Q<Label>("result-title");
            message = root.Q<Label>("result-message");
            root.Q<Button>("rematch-button").clicked += () => matchFlow.Rematch();
        }

        public void SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Refresh()
        {
            title.text = matchFlow.ResultTitle;
            message.text = matchFlow.ResultMessage;
        }
    }
}