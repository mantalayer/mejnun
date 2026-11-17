using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class SetupScreen
    {
        private readonly VisualElement root;
        private readonly MatchFlow matchFlow;
        private readonly IntegerField seatCountField;
        private readonly TextField[] nameFields = new TextField[6];

        public SetupScreen(VisualElement documentRoot, MatchFlow flow)
        {
            root = documentRoot.Q<VisualElement>("setup-screen");
            matchFlow = flow;
            seatCountField = root.Q<IntegerField>("seat-count-field");
            for (var seat = 0; seat < nameFields.Length; seat++)
            {
                nameFields[seat] = root.Q<TextField>("player-name-" + seat);
            }

            seatCountField.RegisterValueChangedCallback(OnSeatCountChanged);
            root.Q<Button>("start-match-button").clicked += StartMatch;
            RefreshNameFields();
        }

        public void SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnSeatCountChanged(ChangeEvent<int> changeEvent)
        {
            var clamped = Mathf.Clamp(changeEvent.newValue, 2, 6);
            if (clamped != changeEvent.newValue)
            {
                seatCountField.SetValueWithoutNotify(clamped);
            }

            RefreshNameFields();
        }

        private void RefreshNameFields()
        {
            var seatCount = Mathf.Clamp(seatCountField.value, 2, 6);
            for (var seat = 0; seat < nameFields.Length; seat++)
            {
                nameFields[seat].style.display = seat < seatCount ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void StartMatch()
        {
            var names = new string[nameFields.Length];
            for (var seat = 0; seat < names.Length; seat++)
            {
                names[seat] = nameFields[seat].value;
            }

            matchFlow.StartMatch(Mathf.Clamp(seatCountField.value, 2, 6), names);
        }
    }
}