using System;
using Mejnun.Game;
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
        private IVisualElementScheduledItem transition;
        private bool waitingForGeometry;

        public DealerQuestionScreen(VisualElement documentRoot, MatchFlow flow)
        {
            root = documentRoot.Q<VisualElement>("dealer-question-screen");
            matchFlow = flow;
            questionLabel = root.Q<Label>("dealer-question-label");
            seatOptions = root.Q<VisualElement>("dealer-seat-options");
            root.Q<Button>("random-dealer-button").clicked += () => matchFlow.ChooseRandomFirstPlayer();
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
            for (var seat = 0; seat < matchFlow.SeatCount; seat++)
            {
                var seatIndex = seat;
                var button = new Button(() => matchFlow.ChooseFirstPlayer(seatIndex))
                {
                    text = matchFlow.GetPlayerName(seat)
                };
                button.AddToClassList("seat-choice");
                seatOptions.Add(button);
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
            transition = root.schedule.Execute(() =>
            {
                if (root.panel == null)
                {
                    CancelTransition();
                    completed?.Invoke();
                    return;
                }

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