using System;
using System.Collections.Generic;
using Mejnun.AI;
using Mejnun.Cosmetics;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public sealed class SetupScreen
    {
        public const string SoloMode = "Solo vs AI";
        public const string PassAndPlayMode = "Pass & play";

        private const int MaximumSeats = 6;
        private const string HumanKind = "Human";
        private static readonly string[] BotKinds = { "Easy bot", "Medium bot", "Hard bot" };
        private static readonly string[] BotStyles = { "Balanced", "Hold back", "Blocker" };

        private readonly VisualElement root;
        private readonly Action<MatchRequest> onNext;
        private readonly DropdownField modeField;
        private readonly SliderInt seatCountField;
        private readonly DropdownField turnTimerField;
        private readonly Label errorLabel;
        private readonly Button startButton;
        private readonly VisualElement[] seatRows = new VisualElement[MaximumSeats];
        private readonly TextField[] nameFields = new TextField[MaximumSeats];
        private readonly DropdownField[] kindFields = new DropdownField[MaximumSeats];
        private readonly DropdownField[] styleFields = new DropdownField[MaximumSeats];
        private string[] seatBotNames;
        private MatchMode mode;
        private Func<bool> canStart;

        public SetupScreen(VisualElement documentRoot, Action<MatchRequest> next, Action back, MatchMode matchMode)
        {
            root = documentRoot.Q<VisualElement>("setup-screen");
            onNext = next ?? throw new ArgumentNullException(nameof(next));
            mode = matchMode;
            seatBotNames = BotNames.PickDistinct(MaximumSeats);
            modeField = root.Q<DropdownField>("mode-field");
            seatCountField = root.Q<SliderInt>("seat-count-field");
            seatCountField.lowValue = 2;
            seatCountField.highValue = MaximumSeats;
            turnTimerField = root.Q<DropdownField>("turn-timer-field");
            errorLabel = root.Q<Label>("setup-error");
            startButton = root.Q<Button>("start-match-button");

            var kinds = new List<string> { HumanKind };
            kinds.AddRange(BotKinds);
            for (var seat = 0; seat < MaximumSeats; seat++)
            {
                seatRows[seat] = root.Q<VisualElement>("seat-row-" + seat);
                nameFields[seat] = root.Q<TextField>("player-name-" + seat);
                kindFields[seat] = root.Q<DropdownField>("seat-kind-" + seat);
                styleFields[seat] = root.Q<DropdownField>("seat-style-" + seat);
                kindFields[seat].choices = new List<string>(kinds);
                kindFields[seat].SetValueWithoutNotify(HumanKind);
                styleFields[seat].choices = new List<string>(BotStyles);
                styleFields[seat].SetValueWithoutNotify(BotStyles[0]);
                var seatIndex = seat;
                kindFields[seat].RegisterValueChangedCallback(_ => OnKindChanged(seatIndex));
            }

            modeField.choices = new List<string> { SoloMode, PassAndPlayMode };
            modeField.SetValueWithoutNotify(mode == MatchMode.PassAndPlay ? PassAndPlayMode : SoloMode);
            modeField.SetEnabled(false);
            turnTimerField.choices = new List<string> { "Off", "30 seconds", "60 seconds", "90 seconds", "120 seconds" };
            turnTimerField.SetValueWithoutNotify("Off");
            seatCountField.RegisterValueChangedCallback(OnSeatCountChanged);
            root.Q<Button>("setup-back-button").clicked += back;
            startButton.clicked += StartMatch;

            ApplyMode(mode);
        }

        public void SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                RefreshCanStart();
            }
        }

        public void SetCanStart(Func<bool> canStartCondition)
        {
            canStart = canStartCondition;
            RefreshCanStart();
        }

        public void RefreshCanStart()
        {
            startButton.SetEnabled(canStart == null || canStart());
        }

        public void SetMode(MatchMode matchMode)
        {
            mode = matchMode;
            modeField.SetValueWithoutNotify(mode == MatchMode.PassAndPlay ? PassAndPlayMode : SoloMode);
            ApplyMode(mode);
        }

        private void ApplyMode(MatchMode matchMode)
        {
            seatBotNames = BotNames.PickDistinct(MaximumSeats);
            for (var seat = 0; seat < MaximumSeats; seat++)
            {
                var kind = matchMode == MatchMode.Solo && seat > 0
                    ? BotKinds[(int)BotDifficulty.Medium]
                    : HumanKind;
                kindFields[seat].SetValueWithoutNotify(kind);
                OnKindChanged(seat);
            }

            var savedNickname = GameSettings.OnlineNickname;
            if (matchMode == MatchMode.Solo && !IsBot(0) && !string.IsNullOrWhiteSpace(savedNickname))
            {
                nameFields[0].SetValueWithoutNotify(savedNickname);
            }

            SetError(string.Empty);
            RefreshRows();
        }

        private void OnKindChanged(int seat)
        {
            styleFields[seat].SetEnabled(IsBot(seat));
            if (!IsDefaultName(nameFields[seat].value))
            {
                return;
            }

            nameFields[seat].SetValueWithoutNotify(IsBot(seat) ? seatBotNames[seat] : "Player " + (seat + 1));
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
                styleFields[seat].style.display = IsBot(seat) ? DisplayStyle.Flex : DisplayStyle.None;
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
                    seats[seat] = SeatConfig.Bot(nameFields[seat].value, Difficulty(seat), Style(seat));
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
            if (mode == MatchMode.Solo && !IsBot(0) && !IsDefaultName(nameFields[0].value))
            {
                GameSettings.OnlineNickname = nameFields[0].value;
            }

            onNext(new MatchRequest(seats, TimerSeconds(), mode));
        }

        private int TimerSeconds()
        {
            switch (turnTimerField.value)
            {
                case "30 seconds": return 30;
                case "60 seconds": return 60;
                case "90 seconds": return 90;
                case "120 seconds": return 120;
                default: return 0;
            }
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

        private BotStyle Style(int seat)
        {
            var index = Array.IndexOf(BotStyles, styleFields[seat].value);
            return index < 0 ? BotStyle.Balanced : (BotStyle)index;
        }

        private static bool IsDefaultName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return true;
            }

            for (var seat = 0; seat < MaximumSeats; seat++)
            {
                if (name == "Player " + (seat + 1))
                {
                    return true;
                }
            }

            return BotNames.Contains(name);
        }

        private void SetError(string message)
        {
            if (errorLabel != null)
            {
                errorLabel.text = message;
            }
        }
    }
}