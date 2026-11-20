using System;
using System.Collections.Generic;
using System.Text;
using Mejnun.Game;
using Mejnun.Localization;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public enum OnlineAction
    {
        Create,
        JoinByCode,
        QuickMatch
    }

    public sealed class OnlineScreen : IDisposable
    {
        private static readonly int[] TimerChoices = { 0, 30, 60, 90, 120 };

        private readonly VisualElement root;
        private readonly VisualElement setupContainer;
        private readonly TextField nicknameField;
        private readonly TextField codeField;
        private readonly SliderInt seatCountField;
        private readonly DropdownField turnTimerField;
        private readonly Label statusLabel;
        private readonly Label codeLabel;
        private readonly Label playersLabel;
        private readonly Button createButton;
        private readonly Button joinButton;
        private readonly Button quickMatchButton;
        private readonly Button continueButton;
        private readonly Button backButton;
        private bool isLobby;
        private bool isHost;
        private bool lobbyTransitionRunning;
        private bool transitioningToLobby;
        private int lobbyTransitionVersion;
        private string lobbyCode;
        private IReadOnlyList<string> lobbyPlayerNames;
        private int lobbyMaxPlayers;
        private Func<string> statusResolver;
        private string statusText;

        public event Action<OnlineAction, string, string> Requested;
        public event Action ContinueRequested;

        public OnlineScreen(VisualElement documentRoot, Action back)
        {
            root = documentRoot.Q<VisualElement>("online-screen");
            setupContainer = root.Q<VisualElement>("online-setup");
            nicknameField = root.Q<TextField>("online-nickname-field");
            RefreshNickname();
            codeField = root.Q<TextField>("online-code-field");
            seatCountField = root.Q<SliderInt>("online-seat-count-field");
            seatCountField.lowValue = 2;
            seatCountField.highValue = 6;
            turnTimerField = root.Q<DropdownField>("online-turn-timer-field");
            statusLabel = root.Q<Label>("online-status");
            codeLabel = root.Q<Label>("online-session-code");
            playersLabel = root.Q<Label>("online-lobby-players");
            createButton = root.Q<Button>("online-create-button");
            joinButton = root.Q<Button>("online-join-button");
            quickMatchButton = root.Q<Button>("online-quick-match-button");
            continueButton = root.Q<Button>("online-continue-button");
            backButton = root.Q<Button>("online-back-button");

            RenderTimerChoices();

            createButton.clicked += () => Request(OnlineAction.Create);
            joinButton.clicked += () => Request(OnlineAction.JoinByCode);
            quickMatchButton.clicked += () => Request(OnlineAction.QuickMatch);
            continueButton.clicked += () => ContinueRequested?.Invoke();
            backButton.clicked += back;
            continueButton.style.display = DisplayStyle.None;
            ApplySetupContent();
            Loc.LanguageChanged += OnLanguageChanged;
        }

        public void Dispose()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        public string Nickname => nicknameField.value;
        public int SeatCount => Math.Max(2, Math.Min(6, seatCountField.value));
        public int TimerSeconds
        {
            get
            {
                var index = turnTimerField.index;
                return index < 0 || index >= TimerChoices.Length ? 0 : TimerChoices[index];
            }
        }

        private void RenderTimerChoices()
        {
            var selected = turnTimerField.index;
            var labels = new List<string>
            {
                Loc.Get("online.timer.off"),
                Loc.Format("online.timer.seconds", 30),
                Loc.Format("online.timer.seconds", 60),
                Loc.Format("online.timer.seconds", 90),
                Loc.Format("online.timer.seconds", 120)
            };
            turnTimerField.choices = labels;
            turnTimerField.SetValueWithoutNotify(labels[selected < 0 || selected >= labels.Count ? 0 : selected]);
        }

        private void OnLanguageChanged()
        {
            RenderTimerChoices();
            if (!lobbyTransitionRunning)
            {
                var resolver = statusResolver;
                var text = statusText;
                if (isLobby)
                {
                    ApplyLobbyContent();
                }
                else
                {
                    RenderSetupButtons();
                }

                statusResolver = resolver;
                statusText = text;
            }

            RenderStatus();
        }

        public void RefreshNickname()
        {
            var savedNickname = GameSettings.OnlineNickname;
            if (!string.IsNullOrWhiteSpace(savedNickname))
            {
                nicknameField.SetValueWithoutNotify(savedNickname);
            }
        }

        public void SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetBusy(bool busy)
        {
            createButton.SetEnabled(!busy);
            joinButton.SetEnabled(!busy);
            quickMatchButton.SetEnabled(!busy);
            continueButton.SetEnabled(!busy && isLobby && isHost);
            backButton.SetEnabled(!busy);
        }

        // Shows a localized status line, for example
        // SetLocalizedStatus(() => Loc.Get("online.status.left_lobby")). The
        // resolver is kept so the line is re-rendered when the language changes.
        public void SetLocalizedStatus(Func<string> resolve)
        {
            statusResolver = resolve;
            statusText = null;
            RenderStatus();
        }

        // Shows text that is already resolved or not localizable (for
        // example a service error message).
        public void SetStatus(string status)
        {
            statusResolver = null;
            statusText = status;
            RenderStatus();
        }

        private void RenderStatus()
        {
            if (isLobby && statusResolver == null && statusText == null)
            {
                statusLabel.text = FormatWaitingStatus();
                return;
            }

            statusLabel.text = statusResolver != null ? statusResolver() : statusText ?? string.Empty;
        }

        private string FormatWaitingStatus()
        {
            return Loc.Format("online.lobby.waiting", lobbyPlayerNames == null ? 0 : lobbyPlayerNames.Count,
                lobbyMaxPlayers);
        }

        public void ShowSessionCode(string code)
        {
            codeLabel.text = string.IsNullOrWhiteSpace(code) ? Loc.Get("online.lobby.code_unavailable") : code;
            codeLabel.style.display = DisplayStyle.Flex;
            continueButton.style.display = DisplayStyle.Flex;
        }

        public void ShowLobby(string code, bool host, IReadOnlyList<string> playerNames, int maxPlayers)
        {
            var wasLobby = isLobby;
            isLobby = true;
            isHost = host;
            lobbyCode = code;
            lobbyPlayerNames = playerNames;
            lobbyMaxPlayers = maxPlayers;
            if (lobbyTransitionRunning && transitioningToLobby)
            {
                return;
            }

            if (wasLobby && !lobbyTransitionRunning)
            {
                ApplyLobbyContent();
                return;
            }

            BeginLobbyTransition(true);
        }

        public void HideLobby()
        {
            var wasLobby = isLobby;
            isLobby = false;
            isHost = false;
            if (lobbyTransitionRunning && !transitioningToLobby)
            {
                return;
            }

            if (!wasLobby && !lobbyTransitionRunning)
            {
                ApplySetupContent();
                return;
            }

            BeginLobbyTransition(false);
        }

        private void BeginLobbyTransition(bool showLobby)
        {
            ScreenTransition.Cancel(root);
            var version = ++lobbyTransitionVersion;
            if (root.panel == null)
            {
                lobbyTransitionRunning = false;
                if (showLobby)
                {
                    ApplyLobbyContent();
                }
                else
                {
                    ApplySetupContent();
                }

                return;
            }

            lobbyTransitionRunning = true;
            transitioningToLobby = showLobby;
            ScreenTransition.ExitVertical(root, !showLobby, () =>
            {
                if (version != lobbyTransitionVersion)
                {
                    return;
                }

                if (showLobby)
                {
                    ApplyLobbyContent();
                }
                else
                {
                    ApplySetupContent();
                }

                ScreenTransition.EnterVertical(root, showLobby, () =>
                {
                    if (version != lobbyTransitionVersion)
                    {
                        return;
                    }

                    if (showLobby)
                    {
                        ApplyLobbyContent();
                    }

                    lobbyTransitionRunning = false;
                });
            });
        }

        private void ApplyLobbyContent()
        {
            setupContainer.style.display = DisplayStyle.None;
            codeLabel.text = string.IsNullOrWhiteSpace(lobbyCode) ? Loc.Get("online.lobby.code_unavailable") : lobbyCode;
            codeLabel.style.display = DisplayStyle.Flex;
            playersLabel.text = FormatPlayerNames(lobbyPlayerNames);
            playersLabel.style.display = DisplayStyle.Flex;
            statusResolver = null;
            statusText = null;
            RenderStatus();
            continueButton.text = Loc.Get("online.lobby.start_now");
            continueButton.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
            continueButton.SetEnabled(isHost);
            backButton.text = Loc.Get("online.lobby.leave");
        }

        private void ApplySetupContent()
        {
            setupContainer.style.display = DisplayStyle.Flex;
            codeLabel.style.display = DisplayStyle.None;
            playersLabel.text = string.Empty;
            playersLabel.style.display = DisplayStyle.None;
            continueButton.style.display = DisplayStyle.None;
            RenderSetupButtons();
        }

        private void RenderSetupButtons()
        {
            continueButton.text = Loc.Get("common.continue");
            backButton.text = Loc.Get("common.back");
        }

        private void Request(OnlineAction action)
        {
            GameSettings.OnlineNickname = Nickname;
            codeLabel.style.display = DisplayStyle.None;
            playersLabel.style.display = DisplayStyle.None;
            continueButton.style.display = DisplayStyle.None;
            SetLocalizedStatus(() => Loc.Get("online.status.connecting"));
            Requested?.Invoke(action, Nickname, codeField.value);
        }

        private static string FormatPlayerNames(IReadOnlyList<string> playerNames)
        {
            if (playerNames == null || playerNames.Count == 0)
            {
                return Loc.Get("online.lobby.no_players");
            }

            var names = new StringBuilder();
            for (var index = 0; index < playerNames.Count; index++)
            {
                if (index > 0)
                {
                    names.AppendLine();
                }

                names.Append(Loc.Format("online.lobby.player_entry", playerNames[index]));
            }

            return names.ToString();
        }
    }
}