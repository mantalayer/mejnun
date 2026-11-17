using System;
using System.Collections.Generic;
using Mejnun.AI;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    /// <summary>
    /// Match setup: Solo vs AI (you plus bots) or Pass &amp; play (any mix of local humans and bots).
    /// Every seat has a name and a controller: Human, or an Easy / Medium / Hard bot.
    /// </summary>
    public sealed class SetupScreen
    {
        internal const string SoloMode = "Solo vs AI";
        internal const string PassAndPlayMode = "Pass & play";

        private const int MaximumSeats = 6;
        private const string HumanKind = "Human";
        private static readonly string[] BotKinds = { "Easy bot", "Medium bot", "Hard bot" };
        private static readonly string[] BotNames = { "Leyla", "Kian", "Nora", "Omid", "Sara", "Darius" };

        private readonly VisualElement root;
        private readonly MatchFlow matchFlow;
        private readonly DropdownField modeField;
        private readonly IntegerField seatCountField;
        private readonly Label errorLabel;
        private readonly VisualElement[] seatRows = new VisualElement[MaximumSeats];
        private readonly TextField[] nameFields = new TextField[MaximumSeats];
        private readonly DropdownField[] kindFields = new DropdownField[MaximumSeats];

        public SetupScreen(VisualElement documentRoot, MatchFlow flow)
        {
            root = documentRoot.Q<VisualElement>("setup-screen");
            matchFlow = flow;
            modeField = root.Q<DropdownField>("mode-field");
            seatCountField = root.Q<IntegerField>("seat-count-field");
            errorLabel = root.Q<Label>("setup-error");

            var kinds = new List<string> { HumanKind };
            kinds.AddRange(BotKinds);
            for (var seat = 0; seat < MaximumSeats; seat++)
            {
                seatRows[seat] = root.Q<VisualElement>("seat-row-" + seat);
                nameFields[seat] = root.Q<TextField>("player-name-" + seat);
                kindFields[seat] = root.Q<DropdownField>("seat-kind-" + seat);
                kindFields[seat].choices = new List<string>(kinds);
                kindFields[seat].SetValueWithoutNotify(HumanKind);
                var seatIndex = seat;
                kindFields[seat].RegisterValueChangedCallback(changeEvent => OnKindChanged(seatIndex));
            }

            modeField.choices = new List<string> { SoloMode, PassAndPlayMode };
            modeField.RegisterValueChangedCallback(changeEvent => ApplyMode(changeEvent.newValue));
            seatCountField.RegisterValueChangedCallback(OnSeatCountChanged);
            root.Q<Button>("start-match-button").clicked += StartMatch;

            modeField.SetValueWithoutNotify(SoloMode);
            ApplyMode(SoloMode);
        }

        public void SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Solo: seat 1 is you, every other seat is a Medium bot. Pass &amp; play: every seat is human.</summary>
        private void ApplyMode(string mode)
        {
            var solo = mode == SoloMode;
            for (var seat = 0; seat < MaximumSeats; seat++)
            {
                var kind = solo && seat > 0 ? BotKinds[(int)BotDifficulty.Medium] : HumanKind;
                kindFields[seat].SetValueWithoutNotify(kind);
                OnKindChanged(seat);
            }

            SetError(string.Empty);
            RefreshRows();
        }

        private void OnKindChanged(int seat)
        {
            var name = nameFields[seat].value;
            if (!IsDefaultName(name))
            {
                return;
            }

            nameFields[seat].SetValueWithoutNotify(IsBot(seat) ? BotNames[seat] : "Player " + (seat + 1));
        }

        private void OnSeatCountChanged(ChangeEvent<int> changeEvent)
        {
            var clamped = Mathf.Clamp(changeEvent.newValue, 2, MaximumSeats);
            if (clamped != changeEvent.newValue)
            {
                seatCountField.SetValueWithoutNotify(clamped);
            }

            RefreshRows();
        }

        private void RefreshRows()
        {
            var seatCount = Mathf.Clamp(seatCountField.value, 2, MaximumSeats);
            for (var seat = 0; seat < MaximumSeats; seat++)
            {
                seatRows[seat].style.display = seat < seatCount ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void StartMatch()
        {
            var seatCount = Mathf.Clamp(seatCountField.value, 2, MaximumSeats);
            var seats = new SeatConfig[seatCount];
            var humans = 0;
            for (var seat = 0; seat < seatCount; seat++)
            {
                if (IsBot(seat))
                {
                    seats[seat] = SeatConfig.Bot(nameFields[seat].value, Difficulty(seat));
                }
                else
                {
                    seats[seat] = SeatConfig.Human(nameFields[seat].value);
                    humans++;
                }
            }

            if (humans == 0)
            {
                SetError("At least one seat must be a human player.");
                return;
            }

            SetError(string.Empty);
            matchFlow.StartMatch(seats);
        }

        private bool IsBot(int seat)
        {
            return kindFields[seat].value != HumanKind;
        }

        private BotDifficulty Difficulty(int seat)
        {
            var index = Array.IndexOf(BotKinds, kindFields[seat].value);
            return index < 0 ? BotDifficulty.Medium : (BotDifficulty)index;
        }

        private static bool IsDefaultName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return true;
            }

            for (var seat = 0; seat < MaximumSeats; seat++)
            {
                if (name == "Player " + (seat + 1) || name == BotNames[seat])
                {
                    return true;
                }
            }

            return false;
        }

        private void SetError(string message)
        {
            if (errorLabel == null)
            {
                return;
            }

            errorLabel.text = message;
            errorLabel.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
