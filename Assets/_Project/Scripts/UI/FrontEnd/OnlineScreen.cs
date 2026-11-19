using System;
using System.Collections.Generic;
using System.Text;
using Mejnun.Game;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public enum OnlineAction
    {
        Create,
        JoinByCode,
        QuickMatch
    }

    public sealed class OnlineScreen
    {
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

            turnTimerField.choices = new List<string>
                { "Off", "30 seconds", "60 seconds", "90 seconds", "120 seconds" };
            turnTimerField.SetValueWithoutNotify("Off");

            createButton.clicked += () => Request(OnlineAction.Create);
            joinButton.clicked += () => Request(OnlineAction.JoinByCode);
            quickMatchButton.clicked += () => Request(OnlineAction.QuickMatch);
            continueButton.clicked += () => ContinueRequested?.Invoke();
            backButton.clicked += back;
            continueButton.style.display = DisplayStyle.None;
        }

        public string Nickname => nicknameField.value;
        public int SeatCount => Math.Max(2, Math.Min(6, seatCountField.value));
        public int TimerSeconds
        {
            get
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

        public void SetStatus(string status)
        {
            statusLabel.text = status ?? string.Empty;
        }

        public void ShowSessionCode(string code)
        {
            codeLabel.text = string.IsNullOrWhiteSpace(code) ? "Session code unavailable" : code;
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
            codeLabel.text = string.IsNullOrWhiteSpace(lobbyCode) ? "Session code unavailable" : lobbyCode;
            codeLabel.style.display = DisplayStyle.Flex;
            playersLabel.text = FormatPlayerNames(lobbyPlayerNames);
            playersLabel.style.display = DisplayStyle.Flex;
            statusLabel.text = "Waiting for players (" + (lobbyPlayerNames == null ? 0 : lobbyPlayerNames.Count) +
                               "/" + lobbyMaxPlayers + ")…";
            continueButton.text = "Start now";
            continueButton.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
            continueButton.SetEnabled(isHost);
            backButton.text = "Leave lobby";
        }

        private void ApplySetupContent()
        {
            setupContainer.style.display = DisplayStyle.Flex;
            codeLabel.style.display = DisplayStyle.None;
            playersLabel.text = string.Empty;
            playersLabel.style.display = DisplayStyle.None;
            continueButton.text = "Continue";
            continueButton.style.display = DisplayStyle.None;
            backButton.text = "Back";
        }

        private void Request(OnlineAction action)
        {
            GameSettings.OnlineNickname = Nickname;
            codeLabel.style.display = DisplayStyle.None;
            playersLabel.style.display = DisplayStyle.None;
            continueButton.style.display = DisplayStyle.None;
            SetStatus("Connecting…");
            Requested?.Invoke(action, Nickname, codeField.value);
        }

        private static string FormatPlayerNames(IReadOnlyList<string> playerNames)
        {
            if (playerNames == null || playerNames.Count == 0)
            {
                return "No players connected yet.";
            }

            var names = new StringBuilder();
            for (var index = 0; index < playerNames.Count; index++)
            {
                if (index > 0)
                {
                    names.AppendLine();
                }

                names.Append("• ").Append(playerNames[index]);
            }

            return names.ToString();
        }
    }
}