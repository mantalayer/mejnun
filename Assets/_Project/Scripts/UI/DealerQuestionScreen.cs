using System;
using Mejnun.Game;
using Mejnun.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class DealerQuestionScreen
    {
        private const float TransitionDuration = 0.45f;
        private readonly VisualElement root;
        private readonly MatchFlow matchFlow;
        private readonly Label questionLabel;
        private readonly VisualElement seatOptions;
        private readonly VisualElement randomVoteMarks;
        private readonly Button randomButton;
        private IVisualElementScheduledItem transition;
        private bool waitingForGeometry;
        private bool listeningForLanguage;

        public DealerQuestionScreen(VisualElement documentRoot, MatchFlow flow)
        {
            root = documentRoot.Q<VisualElement>("dealer-question-screen");
            matchFlow = flow;
            questionLabel = root.Q<Label>("dealer-question-label");
            seatOptions = root.Q<VisualElement>("dealer-seat-options");
            randomVoteMarks = root.Q<VisualElement>("random-vote-marks");
            randomButton = root.Q<Button>("random-dealer-button");
            randomButton.clicked += () => matchFlow.ChooseRandomFirstPlayer();
            root.RegisterCallback<AttachToPanelEvent>(_ => ListenForLanguage(true));
            root.RegisterCallback<DetachFromPanelEvent>(_ => ListenForLanguage(false));
            if (root.panel != null)
            {
                ListenForLanguage(true);
            }
        }

        public void SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void PlayEntrance()
        {
            CancelTransition();
            if (!TryGetTopOffset(out var offset))
            {
                waitingForGeometry = true;
                root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                return;
            }

            waitingForGeometry = false;
            root.pickingMode = PickingMode.Position;
            Animate(-offset, 0f, null);
        }

        public void PlayExit(Action completed)
        {
            CancelTransition();
            if (!TryGetTopOffset(out var offset))
            {
                root.style.translate = StyleKeyword.Null;
                root.pickingMode = PickingMode.Position;
                SetVisible(false);
                completed?.Invoke();
                return;
            }

            root.pickingMode = PickingMode.Ignore;
            Animate(0f, -offset, () =>
            {
                root.style.translate = StyleKeyword.Null;
                root.pickingMode = PickingMode.Position;
                SetVisible(false);
                completed?.Invoke();
            });
        }

        public void Refresh()
        {
            questionLabel.text = matchFlow.DealerQuestion;
            seatOptions.Clear();
            var onlineVote = matchFlow.IsOnlineFirstPlayerVoteOpen;
            for (var seat = 0; seat < matchFlow.SeatCount; seat++)
            {
                if (onlineVote && !matchFlow.IsOnlineFirstPlayerVoteCandidate(seat))
                {
                    continue;
                }

                var seatIndex = seat;
                var button = new Button(() => matchFlow.ChooseFirstPlayer(seatIndex))
                {
                    text = matchFlow.GetPlayerName(seat)
                };
                button.AddToClassList("seat-choice");
                button.SetEnabled(!onlineVote || matchFlow.CanVoteOnlineFirstPlayer);
                if (onlineVote && matchFlow.OnlineFirstPlayerLocalVote == seat)
                {
                    button.AddToClassList("seat-choice--selected");
                }

                var column = new VisualElement();
                column.AddToClassList("seat-choice-column");
                column.Add(CreateVoteMarks(onlineVote ? matchFlow.GetOnlineFirstPlayerVoteCount(seat) : 0));
                column.Add(button);
                seatOptions.Add(column);
            }

            randomButton.SetEnabled(!onlineVote || matchFlow.CanVoteOnlineFirstPlayer);
            randomVoteMarks.Clear();
            AddVoteMarks(randomVoteMarks,
                onlineVote ? matchFlow.GetOnlineFirstPlayerVoteCount(-1) : 0);
            if (onlineVote && matchFlow.OnlineFirstPlayerLocalVote == -1)
            {
                randomButton.AddToClassList("seat-choice--selected");
            }
            else
            {
                randomButton.RemoveFromClassList("seat-choice--selected");
            }
        }

        private void ListenForLanguage(bool listen)
        {
            if (listen == listeningForLanguage)
            {
                return;
            }

            listeningForLanguage = listen;
            if (listen)
            {
                Loc.LanguageChanged += OnLanguageChanged;
                matchFlow.OnlineSeatsChanged += OnOnlineSeatsChanged;
                matchFlow.OnlineFirstPlayerVoteChanged += OnOnlineFirstPlayerVoteChanged;
            }
            else
            {
                Loc.LanguageChanged -= OnLanguageChanged;
                matchFlow.OnlineSeatsChanged -= OnOnlineSeatsChanged;
                matchFlow.OnlineFirstPlayerVoteChanged -= OnOnlineFirstPlayerVoteChanged;
            }
        }

        private void OnOnlineSeatsChanged()
        {
            if (matchFlow != null && matchFlow.Screen == MatchFlowScreen.DealerQuestion)
            {
                Refresh();
            }
        }

        private void OnOnlineFirstPlayerVoteChanged()
        {
            if (matchFlow != null && matchFlow.Screen == MatchFlowScreen.DealerQuestion)
            {
                Refresh();
            }
        }

        private static VisualElement CreateVoteMarks(int count)
        {
            var marks = new VisualElement();
            marks.AddToClassList("vote-marks");
            AddVoteMarks(marks, count);
            return marks;
        }

        private static void AddVoteMarks(VisualElement marks, int count)
        {
            for (var index = 0; index < count; index++)
            {
                var check = new VisualElement();
                check.AddToClassList("vote-check");
                marks.Add(check);
            }
        }

        private void OnLanguageChanged()
        {
            if (matchFlow != null && matchFlow.Screen == MatchFlowScreen.DealerQuestion)
            {
                Refresh();
            }
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (!waitingForGeometry)
            {
                return;
            }

            root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            PlayEntrance();
        }

        private bool TryGetTopOffset(out float offset)
        {
            offset = 0f;
            if (root.panel == null || root.resolvedStyle.height <= 0f || root.parent == null)
            {
                return false;
            }

            offset = Mathf.Max(root.worldBound.yMax - root.parent.worldBound.yMin + 1f,
                root.worldBound.height);
            return offset > 0f;
        }

        private void Animate(float fromY, float toY, Action completed)
        {
            var startedAt = Time.unscaledTime;
            root.style.translate = new Translate(new Length(0f, LengthUnit.Pixel),
                new Length(fromY, LengthUnit.Pixel));
            FrameRateGovernor.NotifyActivity();
            transition = root.schedule.Execute(() =>
            {
                if (root.panel == null)
                {
                    CancelTransition();
                    completed?.Invoke();
                    return;
                }

                FrameRateGovernor.NotifyActivity();
                var progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / TransitionDuration);
                var eased = progress * progress * (3f - 2f * progress);
                root.style.translate = new Translate(new Length(0f, LengthUnit.Pixel),
                    new Length(Mathf.Lerp(fromY, toY, eased), LengthUnit.Pixel));
                if (progress >= 1f)
                {
                    CancelTransition();
                    if (Mathf.Approximately(toY, 0f))
                    {
                        root.style.translate = StyleKeyword.Null;
                    }

                    completed?.Invoke();
                }
            }).Every(0);
        }

        private void CancelTransition()
        {
            transition?.Pause();
            transition = null;
            if (waitingForGeometry)
            {
                root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                waitingForGeometry = false;
            }
        }
    }
}