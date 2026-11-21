using System;
using Mejnun.Localization;
using Mejnun.Net;
using Mejnun.Net.Unity;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class OnlineWaitingOverlayPresenter : IDisposable
    {
        private readonly VisualElement root;
        private readonly OnlineMatchBridge bridge;
        private readonly Action leaveRequested;
        private readonly VisualElement reconnectingBanner;
        private readonly Label reconnectingMessage;
        private readonly VisualElement handBackPanel;
        private readonly VisualElement waitingPanel;
        private readonly Label recoveryMessage;
        private readonly Label recoveryFailure;
        private readonly Label roomCode;
        private readonly VisualElement seatList;
        private readonly Button savePauseButton;
        private readonly Button resumeButton;
        private readonly Button leaveButton;
        private OnlineRosterViewModel roster;

        public OnlineWaitingOverlayPresenter(VisualElement hudRoot, OnlineMatchBridge bridge,
            Action leaveRequested)
        {
            if (hudRoot == null)
            {
                throw new ArgumentNullException(nameof(hudRoot));
            }

            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            this.leaveRequested = leaveRequested;
            root = hudRoot.Q<VisualElement>("online-waiting-overlay");
            if (root == null)
            {
                throw new InvalidOperationException("The online waiting overlay is missing from the HUD.");
            }

            reconnectingBanner = root.Q<VisualElement>("reconnecting-banner");
            reconnectingMessage = root.Q<Label>("reconnecting-message");
            handBackPanel = root.Q<VisualElement>("handback-message-panel");
            waitingPanel = root.Q<VisualElement>("full-waiting-panel");
            recoveryMessage = root.Q<Label>("host-recovery-message");
            recoveryFailure = root.Q<Label>("host-recovery-failure");
            roomCode = root.Q<Label>("room-code");
            seatList = root.Q<VisualElement>("seat-list");
            savePauseButton = root.Q<Button>("save-pause-button");
            resumeButton = root.Q<Button>("resume-button");
            leaveButton = root.Q<Button>("leave-button");

            if (savePauseButton != null)
            {
                savePauseButton.clicked += SaveAndPause;
            }

            if (resumeButton != null)
            {
                resumeButton.clicked += Resume;
            }

            if (leaveButton != null && leaveRequested != null)
            {
                leaveButton.clicked += leaveRequested;
            }

            bridge.RosterChanged += OnRosterChanged;
            Loc.LanguageChanged += OnLanguageChanged;
            Render(bridge.Roster);
        }

        public void Dispose()
        {
            bridge.RosterChanged -= OnRosterChanged;
            Loc.LanguageChanged -= OnLanguageChanged;
            if (savePauseButton != null)
            {
                savePauseButton.clicked -= SaveAndPause;
            }

            if (resumeButton != null)
            {
                resumeButton.clicked -= Resume;
            }

            if (leaveButton != null && leaveRequested != null)
            {
                leaveButton.clicked -= leaveRequested;
            }
        }

        private void OnRosterChanged(OnlineRosterViewModel viewModel)
        {
            roster = viewModel;
            Render(roster);
        }

        private void OnLanguageChanged()
        {
            Render(roster);
        }

        private void Render(OnlineRosterViewModel viewModel)
        {
            roster = viewModel;
            if (root == null || viewModel == null)
            {
                SetVisible(root, false);
                return;
            }

            var hasReconnectingSeat = false;
            var hasDisconnectedSeat = false;
            for (var index = 0; index < viewModel.Seats.Count; index++)
            {
                var presence = viewModel.Seats[index].Presence;
                hasReconnectingSeat |= presence == SeatPresence.Reconnecting;
                hasDisconnectedSeat |= presence == SeatPresence.Disconnected;
            }

            var showWaitingPanel = hasDisconnectedSeat || viewModel.HostRecoveryInProgress ||
                                   viewModel.HostRecoveryFailed ||
                                   viewModel.PauseReason == PauseReason.SavedByHost ||
                                   viewModel.PauseReason == PauseReason.HostAway;
            var showHandBack = viewModel.LocalPendingHandBack;
            SetVisible(root, showWaitingPanel || hasReconnectingSeat || showHandBack);
            root.EnableInClassList("full-waiting", showWaitingPanel);
            root.pickingMode = showWaitingPanel ? PickingMode.Position : PickingMode.Ignore;
            SetVisible(reconnectingBanner, hasReconnectingSeat);
            SetVisible(handBackPanel, showHandBack);
            SetVisible(waitingPanel, showWaitingPanel);

            RenderReconnectBanner(viewModel);
            RenderRecoveryState(viewModel);
            RenderRoomCode(viewModel.RoomCode);
            RenderSeats(viewModel);
            SetVisible(savePauseButton, viewModel.IsHost &&
                                        viewModel.PauseReason != PauseReason.SavedByHost &&
                                        !viewModel.HostRecoveryFailed);
            SetVisible(resumeButton, viewModel.IsHost &&
                                     viewModel.PauseReason == PauseReason.SavedByHost);
            SetVisible(leaveButton, showWaitingPanel);
        }

        private void RenderReconnectBanner(OnlineRosterViewModel viewModel)
        {
            if (reconnectingMessage == null)
            {
                return;
            }

            for (var index = 0; index < viewModel.Seats.Count; index++)
            {
                var seat = viewModel.Seats[index];
                if (seat.Presence != SeatPresence.Reconnecting)
                {
                    continue;
                }

                reconnectingMessage.text = Loc.Format("net.waiting.reconnecting", seat.DisplayName,
                    Math.Ceiling(seat.GraceRemaining));
                return;
            }

            reconnectingMessage.text = string.Empty;
        }

        private void RenderRecoveryState(OnlineRosterViewModel viewModel)
        {
            if (recoveryMessage != null)
            {
                recoveryMessage.text = viewModel.HostRecoveryInProgress
                    ? Loc.Get("net.waiting.finding_host")
                    : string.Empty;
                SetVisible(recoveryMessage, viewModel.HostRecoveryInProgress);
            }

            if (recoveryFailure != null)
            {
                recoveryFailure.text = viewModel.HostRecoveryFailed
                    ? Loc.Get("net.waiting.recovery_failed")
                    : string.Empty;
                SetVisible(recoveryFailure, viewModel.HostRecoveryFailed);
            }
        }

        private void RenderRoomCode(string code)
        {
            if (roomCode == null)
            {
                return;
            }

            roomCode.text = string.IsNullOrWhiteSpace(code)
                ? string.Empty
                : Loc.Format("net.waiting.room_code", code);
            SetVisible(roomCode, !string.IsNullOrWhiteSpace(code));
        }

        private void RenderSeats(OnlineRosterViewModel viewModel)
        {
            if (seatList == null)
            {
                return;
            }

            seatList.Clear();
            for (var index = 0; index < viewModel.Seats.Count; index++)
            {
                AddSeatRow(viewModel, viewModel.Seats[index]);
            }
        }

        private void AddSeatRow(OnlineRosterViewModel viewModel, SeatState seat)
        {
            var row = new VisualElement();
            row.AddToClassList("online-seat-row");
            var identity = new VisualElement();
            identity.AddToClassList("online-seat-identity");
            var nameLabel = new Label(seat.DisplayName) { name = "online-seat-name" }; // loc-ignore User-entered player name.
            nameLabel.AddToClassList("online-seat-name");
            identity.Add(nameLabel);
            if (seat.Seat == viewModel.HostSeat)
            {
                var badge = new Label(Loc.Get("net.seat.host_badge"));
                badge.AddToClassList("online-host-badge");
                identity.Add(badge);
            }

            row.Add(identity);
            var status = new Label(StatusText(seat));
            status.AddToClassList("online-seat-status");
            if (seat.Presence == SeatPresence.Reconnecting)
            {
                status.AddToClassList("reconnecting");
            }
            else if (seat.Presence == SeatPresence.Disconnected)
            {
                status.AddToClassList("disconnected");
            }

            row.Add(status);
            if (viewModel.IsHost && seat.Presence == SeatPresence.Disconnected)
            {
                AddHostActions(row, seat);
            }

            seatList.Add(row);
        }

        private void AddHostActions(VisualElement row, SeatState seat)
        {
            var actions = new VisualElement();
            actions.AddToClassList("online-seat-actions");
            var seatIndex = seat.Seat;
            var keepWaiting = new Button(() => bridge.KeepWaiting(seatIndex))
            {
                text = Loc.Get("net.host.keep_waiting")
            };
            keepWaiting.AddToClassList("hud-button");
            actions.Add(keepWaiting);

            var letAiPlay = new Button(() => bridge.LetAiPlay(seatIndex))
            {
                text = Loc.Format("net.host.let_ai_play", seat.DisplayName)
            };
            letAiPlay.AddToClassList("hud-button");
            actions.Add(letAiPlay);
            row.Add(actions);
        }

        private static string StatusText(SeatState seat)
        {
            switch (seat.Presence)
            {
                case SeatPresence.Connected:
                    return Loc.Get("net.seat.status.connected");
                case SeatPresence.Reconnecting:
                    return Loc.Get("net.seat.status.reconnecting");
                case SeatPresence.Disconnected:
                    return Loc.Get("net.seat.status.disconnected");
                case SeatPresence.BotControlled:
                    return seat.PendingHandBack
                        ? Loc.Get("net.seat.status.waiting_for_ai")
                        : Loc.Get("net.seat.status.ai_playing");
                case SeatPresence.Bot:
                    return Loc.Get("net.seat.status.ai_playing");
                default:
                    return Loc.Get("net.seat.status.empty");
            }
        }

        private void SaveAndPause()
        {
            bridge.SaveAndPauseMatch();
        }

        private void Resume()
        {
            bridge.ResumePausedMatch();
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element != null)
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}