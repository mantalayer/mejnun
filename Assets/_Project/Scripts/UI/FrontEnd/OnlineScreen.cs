using System;
using System.Collections.Generic;
using System.Text;
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
        private readonly IntegerField seatCountField;
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

        public event Action<OnlineAction, string, string> Requested;
        public event Action ContinueRequested;

        public OnlineScreen(VisualElement documentRoot, Action back)
        {
            root = documentRoot.Q<VisualElement>("online-screen");
            setupContainer = root.Q<VisualElement>("online-setup");
            nicknameField = root.Q<TextField>("online-nickname-field");
            codeField = root.Q<TextField>("online-code-field");
            seatCountField = root.Q<IntegerField>("online-seat-count-field");
            statusLabel = root.Q<Label>("online-status");
            codeLabel = root.Q<Label>("online-session-code");
            playersLabel = root.Q<Label>("online-lobby-players");
            createButton = root.Q<Button>("online-create-button");
            joinButton = root.Q<Button>("online-join-button");
            quickMatchButton = root.Q<Button>("online-quick-match-button");
            continueButton = root.Q<Button>("online-continue-button");
            backButton = root.Q<Button>("online-back-button");

            createButton.clicked += () => Request(OnlineAction.Create);
            joinButton.clicked += () => Request(OnlineAction.JoinByCode);
            quickMatchButton.clicked += () => Request(OnlineAction.QuickMatch);
            continueButton.clicked += () => ContinueRequested?.Invoke();
            backButton.clicked += back;
            continueButton.style.display = DisplayStyle.None;
        }

        public string Nickname => nicknameField.value;
        public int SeatCount => Math.Max(2, Math.Min(6, seatCountField.value));

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
            isLobby = true;
            isHost = host;
            setupContainer.style.display = DisplayStyle.None;
            codeLabel.text = string.IsNullOrWhiteSpace(code) ? "Session code unavailable" : code;
            codeLabel.style.display = DisplayStyle.Flex;
            playersLabel.text = FormatPlayerNames(playerNames);
            playersLabel.style.display = DisplayStyle.Flex;
            statusLabel.text = "Waiting for players (" + (playerNames == null ? 0 : playerNames.Count) + "/" +
                               maxPlayers + ")…";
            continueButton.text = "Start now";
            continueButton.style.display = host ? DisplayStyle.Flex : DisplayStyle.None;
            continueButton.SetEnabled(host);
            backButton.text = "Leave lobby";
        }

        public void HideLobby()
        {
            isLobby = false;
            isHost = false;
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