using System;
using System.Collections.Generic;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public static class ScreenTransition
    {
        private const float Duration = 0.78f;
        private const int UpdateIntervalMilliseconds = 0;
        private static readonly Dictionary<VisualElement, TransitionState> Running =
            new Dictionary<VisualElement, TransitionState>();

        public static void Enter(VisualElement element, bool fromRight)
        {
            Animate(element, fromRight, false, null, false);
        }

        public static void Exit(VisualElement element, bool toRight, Action completed)
        {
            Animate(element, toRight, true, completed, false);
        }

        public static void EnterVertical(VisualElement element, bool fromBottom, Action completed = null)
        {
            Animate(element, fromBottom, false, completed, true);
        }

        public static void ExitVertical(VisualElement element, bool toBottom, Action completed)
        {
            Animate(element, toBottom, true, completed, true);
        }

        public static void Cancel(VisualElement element)
        {
            if (element == null || !Running.TryGetValue(element, out var state))
            {
                return;
            }

            Running.Remove(element);
            state.Cancel();
        }

        private static void Animate(VisualElement element, bool positiveDirection, bool exiting, Action completed,
            bool vertical)
        {
            if (element == null)
            {
                completed?.Invoke();
                return;
            }

            Cancel(element);
            if (element.panel == null)
            {
                completed?.Invoke();
                return;
            }

            FrameRateGovernor.NotifyActivity();
            var state = new TransitionState(element, completed);
            Running.Add(element, state);
            var parentExtent = element.parent == null
                ? 0f
                : vertical ? element.parent.layout.height : element.parent.layout.width;
            var elementExtent = vertical ? element.layout.height : element.layout.width;
            var distance = Mathf.Max(parentExtent, elementExtent);
            var direction = positiveDirection ? 1f : -1f;
            state.StartOffset = exiting ? 0f : direction * distance;
            state.EndOffset = exiting ? direction * distance : 0f;
            state.StartOpacity = exiting ? 1f : 0f;
            state.EndOpacity = exiting ? 0f : 1f;
            state.Vertical = vertical;

            if (exiting)
            {
                state.MakeAbsolute(element.layout);
            }

            Apply(element, state.StartOffset, state.StartOpacity, state.Vertical);
            state.StartTime = Time.unscaledTime;
            state.Scheduled = element.schedule.Execute(() => Update(state)).Every(UpdateIntervalMilliseconds);
        }

        private static void Update(TransitionState state)
        {
            if (!state.Active)
            {
                return;
            }

            if (state.Element.panel == null)
            {
                Running.Remove(state.Element);
                state.Complete();
                return;
            }

            FrameRateGovernor.NotifyActivity();
            var progress = Mathf.Clamp01((Time.unscaledTime - state.StartTime) / Duration);
            var eased = progress * progress * (3f - 2f * progress);
            var offset = Mathf.Lerp(state.StartOffset, state.EndOffset, eased);
            var opacity = Mathf.Lerp(state.StartOpacity, state.EndOpacity, eased);
            Apply(state.Element, offset, opacity, state.Vertical);
            if (progress < 1f)
            {
                return;
            }

            Running.Remove(state.Element);
            state.Complete();
        }

        private static void Apply(VisualElement element, float offset, float opacity, bool vertical)
        {
            element.style.translate = vertical
                ? new Translate(new Length(0f, LengthUnit.Pixel), new Length(offset, LengthUnit.Pixel))
                : new Translate(new Length(offset, LengthUnit.Pixel), new Length(0f, LengthUnit.Pixel));
            element.style.opacity = opacity;
        }

        private sealed class TransitionState
        {
            private readonly ScrollView scrollView;
            private readonly ScrollerVisibility verticalScrollerVisibility;
            private readonly ScrollerVisibility horizontalScrollerVisibility;
            private readonly StyleEnum<Position> position;
            private readonly StyleLength left;
            private readonly StyleLength right;
            private readonly StyleLength top;
            private readonly StyleLength bottom;
            private readonly StyleLength width;
            private readonly StyleLength height;
            private readonly StyleTranslate translate;
            private readonly StyleFloat opacity;
            private readonly Action completed;

            public TransitionState(VisualElement element, Action onCompleted)
            {
                Element = element;
                completed = onCompleted;
                scrollView = element as ScrollView;
                if (scrollView != null)
                {
                    verticalScrollerVisibility = scrollView.verticalScrollerVisibility;
                    horizontalScrollerVisibility = scrollView.horizontalScrollerVisibility;
                }

                position = element.style.position;
                left = element.style.left;
                right = element.style.right;
                top = element.style.top;
                bottom = element.style.bottom;
                width = element.style.width;
                height = element.style.height;
                translate = element.style.translate;
                opacity = element.style.opacity;
                Active = true;
            }

            public VisualElement Element { get; }
            public IVisualElementScheduledItem Scheduled { get; set; }
            public float StartTime { get; set; }
            public float StartOffset { get; set; }
            public float EndOffset { get; set; }
            public float StartOpacity { get; set; }
            public float EndOpacity { get; set; }
            public bool Vertical { get; set; }
            public bool Active { get; private set; }

            public void MakeAbsolute(Rect rect)
            {
                if (scrollView != null)
                {
                    scrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                    scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                }

                Element.style.position = Position.Absolute;
                Element.style.left = rect.x;
                Element.style.right = StyleKeyword.Auto;
                Element.style.top = rect.y;
                Element.style.bottom = StyleKeyword.Auto;
                Element.style.width = rect.width;
                Element.style.height = rect.height;
            }

            public void Cancel()
            {
                if (!Active)
                {
                    return;
                }

                Active = false;
                Scheduled?.Pause();
                Restore();
            }

            public void Complete()
            {
                if (!Active)
                {
                    return;
                }

                Active = false;
                Scheduled?.Pause();
                Restore();
                completed?.Invoke();
            }

            private void Restore()
            {
                Element.style.position = position;
                Element.style.left = left;
                Element.style.right = right;
                Element.style.top = top;
                Element.style.bottom = bottom;
                Element.style.width = width;
                Element.style.height = height;
                Element.style.translate = translate;
                Element.style.opacity = opacity;
                if (scrollView != null)
                {
                    scrollView.verticalScrollerVisibility = verticalScrollerVisibility;
                    scrollView.horizontalScrollerVisibility = horizontalScrollerVisibility;
                }
            }
        }
    }
}