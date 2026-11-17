using Mejnun.Game;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class HandoffScreen
    {
        private readonly VisualElement root;
        private readonly MatchFlow matchFlow;
        private readonly Label message;

        public HandoffScreen(VisualElement documentRoot, MatchFlow flow)
        {
            root = documentRoot.Q<VisualElement>("handoff-screen");
            matchFlow = flow;
            message = root.Q<Label>("handoff-message");
            root.Q<Button>("reveal-hand-button").clicked += () => matchFlow.RevealHand();
        }

        public void SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Refresh()
        {
            message.text = matchFlow.HandoffMessage;
        }
    }
}