using System.Text;
using Mejnun.Core;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class ResultsScreen
    {
        private readonly VisualElement root;
        private readonly MatchFlow matchFlow;
        private readonly Label title;
        private readonly Label message;
        private readonly Label counts;
        private readonly Label mejnunBanner;
        private readonly VisualElement rematchConfirm;
        private readonly Toggle rematchDontAsk;

        public ResultsScreen(VisualElement documentRoot, MatchFlow flow)
        {
            root = documentRoot.Q<VisualElement>("results-screen");
            matchFlow = flow;
            title = root.Q<Label>("result-title");
            message = root.Q<Label>("result-message");
            counts = root.Q<Label>("result-counts");
            mejnunBanner = root.Q<Label>("mejnun-banner");
            rematchConfirm = root.Q<VisualElement>("rematch-confirm");
            rematchDontAsk = root.Q<Toggle>("rematch-dont-ask");
            root.Q<Button>("rematch-button").clicked += RequestRematch;
            root.Q<Button>("rematch-cancel-button").clicked += () => SetRematchConfirmation(false);
            root.Q<Button>("rematch-accept-button").clicked += ConfirmRematch;
            root.Q<Button>("results-menu-button").clicked += matchFlow.ReturnToSetup;
        }

        public void SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Refresh()
        {
            title.text = matchFlow.ResultTitle;
            message.text = matchFlow.ResultMessage;
            mejnunBanner.style.display = matchFlow.LastResult != null &&
                                         matchFlow.LastResult.Kind == GameEndKind.EmptyHand
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            var result = new StringBuilder("Cards remaining\n");
            if (matchFlow.Game != null)
            {
                for (var seat = 0; seat < matchFlow.Game.State.Hands.Count; seat++)
                {
                    result.Append(matchFlow.GetPlayerName(seat)).Append(": ")
                        .Append(matchFlow.Game.State.Hands[seat].Count);
                    if (seat + 1 < matchFlow.Game.State.Hands.Count) result.Append('\n');
                }
            }

            counts.text = result.ToString();
            SetRematchConfirmation(false);
        }

        private void SetRematchConfirmation(bool visible)
        {
            rematchDontAsk.SetValueWithoutNotify(false);
            rematchConfirm.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RequestRematch()
        {
            if (GameSettings.ConfirmLeave)
            {
                SetRematchConfirmation(true);
            }
            else
            {
                ConfirmRematch();
            }
        }

        private void ConfirmRematch()
        {
            if (rematchDontAsk.value)
            {
                GameSettings.ConfirmLeave = false;
            }

            SetRematchConfirmation(false);
            matchFlow.Rematch();
        }
    }
}