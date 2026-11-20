using System;
using System.Collections.Generic;
using Mejnun.Game;
using Mejnun.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class SettingsPanel
    {
        private static readonly string[] SettingsPageKeys =
            { "gameplay", "table", "hints", "accessibility", "audio", "general", "rules", "credits" };
        private VisualElement root;
        private Label tooltipLabel;
        private TableView tableView;
        private TableController tableController;
        private Action languageChangedHandler;
        private Action localizedValuesHandler;

        public event Action Closed;

        public void Initialize(VisualElement panel, TableView view = null, TableController controller = null)
        {
            if (panel == null)
            {
                throw new ArgumentNullException(nameof(panel));
            }

            Dispose();
            root = panel;
            root.EnableInClassList("mobile-settings", DeviceProfile.IsMobilePlatform);
            tooltipLabel = root.Q<Label>("settings-tooltip");
            tableView = view;
            tableController = controller;
            var accountSection = root.Q<VisualElement>("account-settings-section");
            if (accountSection != null)
            {
                accountSection.style.display = DeviceProfile.IsMobilePlatform ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var uiScaleRow = root.Q<VisualElement>("ui-scale-row");
            if (uiScaleRow != null)
            {
                uiScaleRow.style.display = DeviceProfile.IsMobilePlatform ? DisplayStyle.Flex : DisplayStyle.None;
            }

            root.Q<Button>("settings-close-button")?.RegisterCallback<ClickEvent>(_ => Closed?.Invoke());
            BindSettingsTab("gameplay");
            BindSettingsTab("table");
            BindSettingsTab("hints");
            BindSettingsTab("accessibility");
            BindSettingsTab("audio");
            BindSettingsTab("general");
            BindSettingsTab("rules");
            BindSettingsTab("credits");
            ShowPage("gameplay");

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
            BindToggle("hover-highlight-toggle", () => GameSettings.CardHoverHighlight,
                value => GameSettings.CardHoverHighlight = value);
            BindToggle("reduced-motion-toggle", () => GameSettings.ReducedMotion,
                value => GameSettings.ReducedMotion = value);
            BindToggle("haptics-toggle", () => GameSettings.HapticsEnabled,
                value => GameSettings.HapticsEnabled = value);
            BindToggle("overlap-sets-toggle", () => !GameSettings.OverlapSets,
                value => GameSettings.OverlapSets = !value, RefreshTable);
            BindToggle("organic-board-toggle", () => !GameSettings.OrganicBoard,
                value => GameSettings.OrganicBoard = !value, RefreshTable);
            BindSlider("ui-scale-slider", () => GameSettings.UiScale,
                value => GameSettings.UiScale = value);

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
            BindTooltips();
            GameSettings.Changed += OnSettingsChanged;
            localizedValuesHandler = RefreshLocalizedValues;
            Loc.LanguageChanged += localizedValuesHandler;
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
            if (languageChangedHandler != null)
            {
                Loc.LanguageChanged -= languageChangedHandler;
                languageChangedHandler = null;
            }

            if (localizedValuesHandler != null)
            {
                Loc.LanguageChanged -= localizedValuesHandler;
                localizedValuesHandler = null;
            }
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
                UpdateSliderValue(name, changeEvent.newValue);
                afterChange?.Invoke();
            });
            slider.SetValueWithoutNotify(getValue());
            UpdateSliderValue(name, getValue());
        }

        private void BindTooltips()
        {
            root.Query<VisualElement>().ForEach(element =>
            {
                // Tooltips come from LocalizedString bindings, which may not
                // have resolved yet, so read the text when the pointer enters.
                if (string.IsNullOrWhiteSpace(element.tooltip) && !element.HasBinding(nameof(VisualElement.tooltip)))
                {
                    return;
                }

                var tooltipTarget = element.parent != null && element.parent.ClassListContains("settings-row")
                    ? element.parent
                    : element;
                tooltipTarget.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (tooltipLabel != null)
                    {
                        tooltipLabel.text = element.tooltip;
                    }
                }, TrickleDown.TrickleDown);
                tooltipTarget.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (tooltipLabel != null)
                    {
                        tooltipLabel.text = string.Empty;
                    }
                }, TrickleDown.TrickleDown);
            });
        }

        private void BindCpuSpeed()
        {
            var dropdown = root.Q<DropdownField>("cpu-speed-dropdown");
            if (dropdown == null)
            {
                return;
            }

            dropdown.choices = CpuSpeedLabels();
            dropdown.RegisterValueChangedCallback(changeEvent =>
            {
                var index = dropdown.index;
                if (index >= 0)
                {
                    GameSettings.CpuSpeed = index;
                }
            });
            SetDropdownIndex(dropdown, GameSettings.CpuSpeed);
        }

        private static List<string> CpuSpeedLabels()
        {
            return new List<string>
            {
                Loc.Get("settings.gameplay.cpu_speed.glacial"),
                Loc.Get("settings.gameplay.cpu_speed.very_slow"),
                Loc.Get("settings.gameplay.cpu_speed.slow"),
                Loc.Get("settings.gameplay.cpu_speed.normal"),
                Loc.Get("settings.gameplay.cpu_speed.fast"),
                Loc.Get("settings.gameplay.cpu_speed.very_fast")
            };
        }

        private static List<string> BotFillLabels()
        {
            return new List<string>
            {
                Loc.Get("settings.online.bot_fill.off"),
                Loc.Format("settings.online.bot_fill.seconds", 30),
                Loc.Format("settings.online.bot_fill.seconds", 60)
            };
        }

        private void BindLanguage()
        {
            var dropdown = root.Q<DropdownField>("language-dropdown");
            if (dropdown == null)
            {
                return;
            }

            // Choice 0 follows the platform (Steam game language, or the
            // device's languages); the rest are the shipped locales, each shown
            // in its own language so players can always find theirs.
            var codes = new List<string> { LanguagePreference.FollowPlatform };
            foreach (var code in Loc.AvailableLocaleCodes)
            {
                codes.Add(code);
            }

            void Populate()
            {
                var labels = new List<string> { Loc.Get("settings.language.automatic") };
                foreach (var locale in Loc.AvailableLocales)
                {
                    labels.Add(Loc.NativeName(locale));
                }

                dropdown.choices = labels;
                var selected = codes.IndexOf(GameSettings.Language);
                dropdown.SetValueWithoutNotify(labels[selected < 0 ? 0 : selected]);
            }

            dropdown.RegisterValueChangedCallback(changeEvent =>
            {
                var index = dropdown.index;
                if (index < 0 || index >= codes.Count)
                {
                    return;
                }

                GameSettings.Language = codes[index];
                Loc.SetLanguage(codes[index]);
            });
            languageChangedHandler = Populate;
            Loc.LanguageChanged += languageChangedHandler;
            Populate();
        }

        private void BindBotFill()
        {
            var dropdown = root.Q<DropdownField>("quick-match-bot-fill-dropdown");
            if (dropdown == null)
            {
                return;
            }

            dropdown.choices = BotFillLabels();
            dropdown.RegisterValueChangedCallback(changeEvent =>
            {
                var index = dropdown.index;
                if (index >= 0)
                {
                    GameSettings.QuickMatchBotFill = index * 30;
                }
            });
            SetDropdownIndex(dropdown, GameSettings.QuickMatchBotFill / 30);
        }

        // Rebuilds the text that code owns (dropdown choices and slider
        // values) in the new language.
        private void RefreshLocalizedValues()
        {
            if (root == null)
            {
                return;
            }

            var cpuSpeed = root.Q<DropdownField>("cpu-speed-dropdown");
            if (cpuSpeed != null)
            {
                cpuSpeed.choices = CpuSpeedLabels();
            }

            var botFill = root.Q<DropdownField>("quick-match-bot-fill-dropdown");
            if (botFill != null)
            {
                botFill.choices = BotFillLabels();
            }

            RefreshValues();
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
                tab.clicked += () => ShowPage(key);
            }
        }

        public void ShowPage(string selectedKey)
        {
            if (root == null)
            {
                return;
            }

            for (var index = 0; index < SettingsPageKeys.Length; index++)
            {
                var selected = string.Equals(SettingsPageKeys[index], selectedKey, StringComparison.Ordinal);
                var page = root.Q<VisualElement>("settings-page-" + SettingsPageKeys[index]);
                if (page != null)
                {
                    page.style.display = selected ? DisplayStyle.Flex : DisplayStyle.None;
                }

                var tab = root.Q<Button>("settings-tab-" + SettingsPageKeys[index]);
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
            SetToggle("hover-highlight-toggle", GameSettings.CardHoverHighlight);
            SetToggle("reduced-motion-toggle", GameSettings.ReducedMotion);
            SetToggle("haptics-toggle", GameSettings.HapticsEnabled);
            SetToggle("overlap-sets-toggle", !GameSettings.OverlapSets);
            SetToggle("organic-board-toggle", !GameSettings.OrganicBoard);
            SetSlider("ui-scale-slider", GameSettings.UiScale);
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
            SetDropdownIndex(root.Q<DropdownField>("cpu-speed-dropdown"), GameSettings.CpuSpeed);
            SetDropdownIndex(root.Q<DropdownField>("quick-match-bot-fill-dropdown"),
                GameSettings.QuickMatchBotFill / 30);
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
            UpdateSliderValue(name, value);
        }

        private void UpdateSliderValue(string name, float value)
        {
            var valueLabel = root.Q<Label>(name.Replace("-slider", "-value"));
            if (valueLabel == null)
            {
                return;
            }

            var isPercent = name.StartsWith("volume-", StringComparison.Ordinal) || name == "card-messiness-slider";
            valueLabel.text = isPercent
                ? Loc.Format("settings.value.percent", Mathf.RoundToInt(value * 100f))
                : Loc.Format("settings.value.multiplier", value.ToString("0.##"));
        }

        private static void SetDropdownIndex(DropdownField dropdown, int index)
        {
            var choices = dropdown?.choices;
            if (choices == null || choices.Count == 0)
            {
                return;
            }

            dropdown.SetValueWithoutNotify(choices[Mathf.Clamp(index, 0, choices.Count - 1)]);
        }
    }
}