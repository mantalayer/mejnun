using System;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class SettingsPanel
    {
        private static readonly string[] CpuSpeedLabels =
            { "Glacial", "Very slow", "Slow", "Normal", "Fast", "Very fast" };
        private static readonly string[] BotFillLabels = { "Off", "30 seconds", "60 seconds" };
        private VisualElement root;
        private TableView tableView;
        private TableController tableController;

        public event Action Closed;

        public void Initialize(VisualElement panel, TableView view = null, TableController controller = null)
        {
            if (panel == null)
            {
                throw new ArgumentNullException(nameof(panel));
            }

            Dispose();
            root = panel;
            tableView = view;
            tableController = controller;
            var accountSection = root.Q<VisualElement>("account-settings-section");
            if (accountSection != null)
            {
                accountSection.style.display = Application.isMobilePlatform ? DisplayStyle.Flex : DisplayStyle.None;
            }

            root.Q<Button>("settings-close-button")?.RegisterCallback<ClickEvent>(_ => Closed?.Invoke());
            BindSettingsTab("gameplay");
            BindSettingsTab("table");
            BindSettingsTab("hints");
            BindSettingsTab("accessibility");
            BindSettingsTab("audio");
            BindSettingsTab("general");
            ShowSettingsPage("gameplay");

            BindToggle("confirm-revert-toggle", () => GameSettings.ConfirmRevert,
                value => GameSettings.ConfirmRevert = value);
            BindToggle("confirm-draw-toggle", () => GameSettings.ConfirmDraw,
                value => GameSettings.ConfirmDraw = value);
            BindToggle("confirm-fix-toggle", () => GameSettings.ConfirmFix,
                value => GameSettings.ConfirmFix = value);
            BindToggle("confirm-leave-toggle", () => GameSettings.ConfirmLeave,
                value => GameSettings.ConfirmLeave = value);
            BindToggle("easy-opening-deal-toggle", () => GameSettings.EasyOpeningDeal,
                value => GameSettings.EasyOpeningDeal = value);
            BindToggle("show-fast-forward-toggle", () => GameSettings.ShowFastForward,
                value => GameSettings.ShowFastForward = value);
            BindToggle("hint-board-moves-toggle", () => GameSettings.HintBoardMoves,
                value => GameSettings.HintBoardMoves = value);
            BindToggle("hint-jiggle-toggle", () => GameSettings.HintJiggle,
                value => GameSettings.HintJiggle = value);
            BindToggle("reduced-motion-toggle", () => GameSettings.ReducedMotion,
                value => GameSettings.ReducedMotion = value);
            BindToggle("haptics-toggle", () => GameSettings.HapticsEnabled,
                value => GameSettings.HapticsEnabled = value);
            BindToggle("overlap-sets-toggle", () => GameSettings.OverlapSets,
                value => GameSettings.OverlapSets = value, RefreshTable);
            BindToggle("organic-board-toggle", () => GameSettings.OrganicBoard,
                value => GameSettings.OrganicBoard = value, RefreshTable);

            BindSlider("hand-arc-amount-slider", () => GameSettings.HandArcAmount,
                value => GameSettings.HandArcAmount = value, () => tableView?.RefreshHandLayout());
            BindSlider("hand-spread-slider", () => GameSettings.HandSpread,
                value => GameSettings.HandSpread = value, () => tableView?.RefreshHandLayout());
            BindSlider("hand-card-scale-slider", () => GameSettings.HandCardScale,
                value => GameSettings.HandCardScale = value, () => tableView?.RefreshHandLayout());
            BindSlider("hand-lift-slider", () => GameSettings.HandLift,
                value => GameSettings.HandLift = value, () => tableView?.RefreshHandLayout());
            BindSlider("opponent-card-scale-slider", () => GameSettings.OpponentCardScale,
                value => GameSettings.OpponentCardScale = value, RefreshTable);
            BindSlider("zoom-sensitivity-slider", () => GameSettings.ZoomSensitivity,
                value => GameSettings.ZoomSensitivity = value);
            BindSlider("board-card-spread-slider", () => GameSettings.BoardCardSpread,
                value => GameSettings.BoardCardSpread = value, RefreshTable);
            BindSlider("card-messiness-slider", () => GameSettings.CardMessiness,
                value => GameSettings.CardMessiness = value, RefreshTable);
            BindSlider("volume-master-slider", () => GameSettings.VolumeMaster,
                value => GameSettings.VolumeMaster = value);
            BindSlider("volume-music-slider", () => GameSettings.VolumeMusic,
                value => GameSettings.VolumeMusic = value);
            BindSlider("volume-sfx-slider", () => GameSettings.VolumeSfx,
                value => GameSettings.VolumeSfx = value);
            BindSlider("volume-voice-slider", () => GameSettings.VolumeVoice,
                value => GameSettings.VolumeVoice = value);

            BindCpuSpeed();
            BindLanguage();
            BindBotFill();
            GameSettings.Changed += OnSettingsChanged;
            RefreshValues();
        }

        public void Show()
        {
            if (root != null)
            {
                root.style.display = DisplayStyle.Flex;
            }
        }

        public void Hide()
        {
            if (root != null)
            {
                root.style.display = DisplayStyle.None;
            }
        }

        public void Dispose()
        {
            GameSettings.Changed -= OnSettingsChanged;
        }

        private void BindToggle(string name, Func<bool> getValue, Action<bool> setValue,
            Action afterChange = null)
        {
            var toggle = root.Q<Toggle>(name);
            if (toggle == null)
            {
                return;
            }

            toggle.RegisterValueChangedCallback(changeEvent =>
            {
                setValue(changeEvent.newValue);
                afterChange?.Invoke();
            });
            toggle.SetValueWithoutNotify(getValue());
        }

        private void BindSlider(string name, Func<float> getValue, Action<float> setValue,
            Action afterChange = null)
        {
            var slider = root.Q<Slider>(name);
            if (slider == null)
            {
                return;
            }

            slider.RegisterValueChangedCallback(changeEvent =>
            {
                setValue(changeEvent.newValue);
                afterChange?.Invoke();
            });
            slider.SetValueWithoutNotify(getValue());
        }

        private void BindCpuSpeed()
        {
            var dropdown = root.Q<DropdownField>("cpu-speed-dropdown");
            if (dropdown == null)
            {
                return;
            }

            dropdown.RegisterValueChangedCallback(changeEvent =>
            {
                var index = Array.IndexOf(CpuSpeedLabels, changeEvent.newValue);
                if (index >= 0)
                {
                    GameSettings.CpuSpeed = index;
                }
            });
            dropdown.SetValueWithoutNotify(CpuSpeedLabels[GameSettings.CpuSpeed]);
        }

        private void BindLanguage()
        {
            var dropdown = root.Q<DropdownField>("language-dropdown");
            if (dropdown == null)
            {
                return;
            }

            dropdown.RegisterValueChangedCallback(changeEvent => GameSettings.Language = "en");
            dropdown.SetValueWithoutNotify("English");
        }

        private void BindBotFill()
        {
            var dropdown = root.Q<DropdownField>("quick-match-bot-fill-dropdown");
            if (dropdown == null)
            {
                return;
            }

            dropdown.RegisterValueChangedCallback(changeEvent =>
            {
                var index = Array.IndexOf(BotFillLabels, changeEvent.newValue);
                if (index >= 0)
                {
                    GameSettings.QuickMatchBotFill = index * 30;
                }
            });
            dropdown.SetValueWithoutNotify(BotFillLabels[GameSettings.QuickMatchBotFill / 30]);
        }

        private void OnSettingsChanged(string key)
        {
            RefreshValues();
            if (key == "settings.hints.boardMoves")
            {
                tableController?.RefreshHintOptions();
            }
        }

        private void BindSettingsTab(string key)
        {
            var tab = root.Q<Button>("settings-tab-" + key);
            if (tab != null)
            {
                tab.clicked += () => ShowSettingsPage(key);
            }
        }

        private void ShowSettingsPage(string selectedKey)
        {
            var keys = new[] { "gameplay", "table", "hints", "accessibility", "audio", "general" };
            for (var index = 0; index < keys.Length; index++)
            {
                var selected = string.Equals(keys[index], selectedKey, StringComparison.Ordinal);
                var page = root.Q<VisualElement>("settings-page-" + keys[index]);
                if (page != null)
                {
                    page.style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
                }

                var tab = root.Q<Button>("settings-tab-" + keys[index]);
                if (tab != null)
                {
                    tab.EnableInClassList("selected-tab", selected);
                }
            }
        }

        private void RefreshValues()
        {
            if (root == null)
            {
                return;
            }

            SetToggle("confirm-revert-toggle", GameSettings.ConfirmRevert);
            SetToggle("confirm-draw-toggle", GameSettings.ConfirmDraw);
            SetToggle("confirm-fix-toggle", GameSettings.ConfirmFix);
            SetToggle("confirm-leave-toggle", GameSettings.ConfirmLeave);
            SetToggle("easy-opening-deal-toggle", GameSettings.EasyOpeningDeal);
            SetToggle("show-fast-forward-toggle", GameSettings.ShowFastForward);
            SetToggle("hint-board-moves-toggle", GameSettings.HintBoardMoves);
            SetToggle("hint-jiggle-toggle", GameSettings.HintJiggle);
            SetToggle("reduced-motion-toggle", GameSettings.ReducedMotion);
            SetToggle("haptics-toggle", GameSettings.HapticsEnabled);
            SetToggle("overlap-sets-toggle", GameSettings.OverlapSets);
            SetToggle("organic-board-toggle", GameSettings.OrganicBoard);
            SetSlider("hand-arc-amount-slider", GameSettings.HandArcAmount);
            SetSlider("hand-spread-slider", GameSettings.HandSpread);
            SetSlider("hand-card-scale-slider", GameSettings.HandCardScale);
            SetSlider("hand-lift-slider", GameSettings.HandLift);
            SetSlider("opponent-card-scale-slider", GameSettings.OpponentCardScale);
            SetSlider("zoom-sensitivity-slider", GameSettings.ZoomSensitivity);
            SetSlider("board-card-spread-slider", GameSettings.BoardCardSpread);
            SetSlider("card-messiness-slider", GameSettings.CardMessiness);
            SetSlider("volume-master-slider", GameSettings.VolumeMaster);
            SetSlider("volume-music-slider", GameSettings.VolumeMusic);
            SetSlider("volume-sfx-slider", GameSettings.VolumeSfx);
            SetSlider("volume-voice-slider", GameSettings.VolumeVoice);
            SetDropdown("cpu-speed-dropdown", CpuSpeedLabels[GameSettings.CpuSpeed]);
            SetDropdown("language-dropdown", "English");
            SetDropdown("quick-match-bot-fill-dropdown", BotFillLabels[GameSettings.QuickMatchBotFill / 30]);
        }

        private void RefreshTable()
        {
            if (tableController?.Game != null)
            {
                tableView?.Sync(tableController.Game.State, tableController.Game.Turn);
            }
        }

        private void SetToggle(string name, bool value)
        {
            root.Q<Toggle>(name)?.SetValueWithoutNotify(value);
        }

        private void SetSlider(string name, float value)
        {
            root.Q<Slider>(name)?.SetValueWithoutNotify(value);
        }

        private void SetDropdown(string name, string value)
        {
            root.Q<DropdownField>(name)?.SetValueWithoutNotify(value);
        }
    }
}