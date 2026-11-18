using Mejnun.Game;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class DealerQuestionScreen
    {
        private readonly VisualElement root;
        private readonly MatchFlow matchFlow;
        private readonly Label questionLabel;
        private readonly VisualElement seatOptions;

        public DealerQuestionScreen(VisualElement documentRoot, MatchFlow flow)
        {
            root = documentRoot.Q<VisualElement>("dealer-question-screen");
            matchFlow = flow;
            questionLabel = root.Q<Label>("dealer-question-label");
            seatOptions = root.Q<VisualElement>("dealer-seat-options");
            root.Q<Button>("random-dealer-button").clicked += () => matchFlow.ChooseRandomFirstPlayer();
        }

        public void SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Refresh()
        {
            questionLabel.text = matchFlow.DealerQuestion;
            seatOptions.Clear();
            for (var seat = 0; seat < matchFlow.SeatCount; seat++)
            {
                var seatIndex = seat;
                var button = new Button(() => matchFlow.ChooseFirstPlayer(seatIndex))
                {
                    text = matchFlow.GetPlayerName(seat)
                };
                button.AddToClassList("seat-choice");
                seatOptions.Add(button);
            }
        }
    }
}