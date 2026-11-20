using System;
using System.Collections.Generic;
using Mejnun.AI;
using Mejnun.Cosmetics;
using Mejnun.Game;
using Mejnun.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public sealed class SetupScreen : IDisposable
    {
        public static string SoloMode => Loc.Get("setup.mode.solo");
        public static string PassAndPlayMode => Loc.Get("setup.mode.pass_and_play");

        private const int MaximumSeats = 6;
        private const int HumanKindIndex = 0;
        private static readonly int[] TimerChoices = { 0, 30, 60, 90, 120 };

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
        private readonly string[] assignedDefaultNames = new string[MaximumSeats];
        private string[] seatBotNames;
        private Func<string> errorResolver;
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

            for (var seat = 0; seat < MaximumSeats; seat++)
            {
                seatRows[seat] = root.Q<VisualElement>("seat-row-" + seat);
                nameFields[seat] = root.Q<TextField>("player-name-" + seat);
                kindFields[seat] = root.Q<DropdownField>("seat-kind-" + seat);
                styleFields[seat] = root.Q<DropdownField>("seat-style-" + seat);
                var seatIndex = seat;
                kindFields[seat].RegisterValueChangedCallback(_ => OnKindChanged(seatIndex));
            }

            modeField.SetEnabled(false);
            RenderChoices(HumanKindIndex, (int)BotStyle.Balanced, 0);
            seatCountField.RegisterValueChangedCallback(OnSeatCountChanged);
            root.Q<Button>("setup-back-button").clicked += back;
            startButton.clicked += StartMatch;

            ApplyMode(mode);
            Loc.LanguageChanged += OnLanguageChanged;
        }

        public void Dispose()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        private static List<string> KindLabels()
        {
            return new List<string>
            {
                Loc.Get("setup.seat.kind.human"),
                Loc.Get("setup.seat.kind.easy_bot"),
                Loc.Get("setup.seat.kind.medium_bot"),
                Loc.Get("setup.seat.kind.hard_bot")
            };
        }

        private static List<string> StyleLabels()
        {
            return new List<string>
            {
                Loc.Get("setup.seat.style.balanced"),
                Loc.Get("setup.seat.style.hold_back"),
                Loc.Get("setup.seat.style.blocker")
            };
        }

        private static List<string> TimerLabels()
        {
            return new List<string>
            {
                Loc.Get("setup.timer.off"),
                Loc.Format("setup.timer.seconds", 30),
                Loc.Format("setup.timer.seconds", 60),
                Loc.Format("setup.timer.seconds", 90),
                Loc.Format("setup.timer.seconds", 120)
            };
        }

        // Rebuilds every dropdown in the current language. Kind and style
        // keep their per-seat index; the given defaults are used for fields
        // that have no selection yet.
        private void RenderChoices(int defaultKind, int defaultStyle, int defaultTimer)
        {
            var kinds = KindLabels();
            var styles = StyleLabels();
            for (var seat = 0; seat < MaximumSeats; seat++)
            {
                var kind = kindFields[seat].index;
                var style = styleFields[seat].index;
                kindFields[seat].choices = new List<string>(kinds);
                SetIndexWithoutNotify(kindFields[seat], kind < 0 ? defaultKind : kind);
                styleFields[seat].choices = new List<string>(styles);
                SetIndexWithoutNotify(styleFields[seat], style < 0 ? defaultStyle : style);
                nameFields[seat].label = Loc.Format("setup.seat.label", seat + 1);
            }

            modeField.choices = new List<string> { Loc.Get("setup.mode.solo"), Loc.Get("setup.mode.pass_and_play") };
            SetIndexWithoutNotify(modeField, mode == MatchMode.PassAndPlay ? 1 : 0);
            var timer = turnTimerField.index;
            turnTimerField.choices = TimerLabels();
            SetIndexWithoutNotify(turnTimerField, timer < 0 ? defaultTimer : timer);
        }

        private static void SetIndexWithoutNotify(DropdownField field, int index)
        {
            var choices = field.choices;
            if (choices == null || choices.Count == 0)
            {
                return;
            }

            field.SetValueWithoutNotify(choices[Mathf.Clamp(index, 0, choices.Count - 1)]);
        }

        private void OnLanguageChanged()
        {
            RenderChoices(HumanKindIndex, (int)BotStyle.Balanced, 0);
            for (var seat = 0; seat < MaximumSeats; seat++)
            {
                if (!IsBot(seat) && assignedDefaultNames[seat] != null &&
                    nameFields[seat].value == assignedDefaultNames[seat])
                {
                    SetDefaultName(seat, Loc.Format("setup.seat.default_name", seat + 1));
                }
            }

            RenderError();
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
            SetIndexWithoutNotify(modeField, mode == MatchMode.PassAndPlay ? 1 : 0);
            ApplyMode(mode);
        }

        private void ApplyMode(MatchMode matchMode)
        {
            seatBotNames = BotNames.PickDistinct(MaximumSeats);
            for (var seat = 0; seat < MaximumSeats; seat++)
            {
                var kind = matchMode == MatchMode.Solo && seat > 0
                    ? KindIndex(BotDifficulty.Medium)
                    : HumanKindIndex;
                SetIndexWithoutNotify(kindFields[seat], kind);
                OnKindChanged(seat);
            }

            var savedNickname = GameSettings.OnlineNickname;
            if (matchMode == MatchMode.Solo && !IsBot(0) && !string.IsNullOrWhiteSpace(savedNickname))
            {
                nameFields[0].SetValueWithoutNotify(savedNickname);
            }

            SetError(null);
            RefreshRows();
        }

        private void OnKindChanged(int seat)
        {
            styleFields[seat].SetEnabled(IsBot(seat));
            if (!IsDefaultName(nameFields[seat].value))
            {
                return;
            }

            SetDefaultName(seat, IsBot(seat) ? seatBotNames[seat] : Loc.Format("setup.seat.default_name", seat + 1));
        }

        private void SetDefaultName(int seat, string name)
        {
            assignedDefaultNames[seat] = name;
            nameFields[seat].SetValueWithoutNotify(name);
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
                SetError(() => Loc.Get("setup.error.no_human"));
                return;
            }

            SetError(null);
            if (mode == MatchMode.Solo && !IsBot(0) && !IsDefaultName(nameFields[0].value))
            {
                GameSettings.OnlineNickname = nameFields[0].value;
            }

            onNext(new MatchRequest(seats, TimerSeconds(), mode));
        }

        private int TimerSeconds()
        {
            var index = turnTimerField.index;
            return index < 0 || index >= TimerChoices.Length ? 0 : TimerChoices[index];
        }

        private static int KindIndex(BotDifficulty difficulty)
        {
            return HumanKindIndex + 1 + (int)difficulty;
        }

        private bool IsBot(int seat)
        {
            return kindFields[seat].index > HumanKindIndex;
        }

        private BotDifficulty Difficulty(int seat)
        {
            var index = kindFields[seat].index - (HumanKindIndex + 1);
            return index < 0 || index > (int)BotDifficulty.Hard ? BotDifficulty.Medium : (BotDifficulty)index;
        }

        private BotStyle Style(int seat)
        {
            var index = styleFields[seat].index;
            return index < 0 || index > (int)BotStyle.Blocker ? BotStyle.Balanced : (BotStyle)index;
        }

        private bool IsDefaultName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return true;
            }

            for (var seat = 0; seat < MaximumSeats; seat++)
            {
                if (name == assignedDefaultNames[seat] || name == Loc.Format("setup.seat.default_name", seat + 1))
                {
                    return true;
                }
            }

            return BotNames.Contains(name);
        }

        // The resolver is kept so the message is re-rendered when the
        // language changes; null clears the error.
        private void SetError(Func<string> resolve)
        {
            errorResolver = resolve;
            RenderError();
        }

        private void RenderError()
        {
            if (errorLabel != null)
            {
                errorLabel.text = errorResolver == null ? string.Empty : errorResolver();
            }
        }
    }
}