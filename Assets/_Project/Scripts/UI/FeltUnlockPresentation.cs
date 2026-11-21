using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Game;
using Mejnun.Localization;
using Mejnun.UI.FrontEnd;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class FeltUnlockPresentationItem
    {
        private readonly string displayName;

        public FeltUnlockPresentationItem(TableFelt felt, string displayName)
        {
            Felt = felt ?? throw new ArgumentNullException(nameof(felt));
            this.displayName = string.IsNullOrWhiteSpace(displayName) ? felt.DisplayName : displayName;
        }

        public TableFelt Felt { get; }
        public string DisplayName => TableFelt.ResolveDisplayName(Felt.Id, displayName);
    }

    public sealed class FeltUnlockPresentation
    {
        private const float UnrollDuration = 0.55f;
        private const float SettleDuration = 0.55f;
        private const float WobblePeriod = 1.2f;
        private const float MaxTickSeconds = 0.1f;

        public const float FullDuration = UnrollDuration + SettleDuration;

        private readonly VisualElement overlay;
        private readonly VisualElement stage;
        private readonly VisualElement outerGlow;
        private readonly VisualElement innerGlow;
        private readonly Label caption;
        private readonly Label feltName;
        private readonly Label hint;
        private readonly List<FeltUnlockPresentationItem> items = new List<FeltUnlockPresentationItem>();

        private VisualElement swatch;
        private Action onFinished;
        private int currentIndex;
        private float elapsed;
        private bool isPlaying;

        public FeltUnlockPresentation(VisualElement documentRoot)
        {
            if (documentRoot == null)
            {
                throw new ArgumentNullException(nameof(documentRoot));
            }

            overlay = new VisualElement { name = "felt-unlock-overlay", pickingMode = PickingMode.Position };
            overlay.AddToClassList("felt-unlock-overlay");
            overlay.style.display = DisplayStyle.None;
            caption = new Label(Loc.Get("felt_unlock.caption"));
            caption.name = "felt-unlock-caption";
            caption.AddToClassList("felt-unlock-caption");
            feltName = new Label { name = "felt-unlock-name" };
            feltName.AddToClassList("felt-unlock-name");
            hint = new Label(Loc.Get("felt_unlock.hint"));
            hint.name = "felt-unlock-hint";
            hint.AddToClassList("felt-unlock-hint");
            stage = new VisualElement { name = "felt-unlock-stage", pickingMode = PickingMode.Ignore };
            stage.AddToClassList("felt-unlock-stage");
            outerGlow = new VisualElement { name = "felt-unlock-glow-outer", pickingMode = PickingMode.Ignore };
            outerGlow.AddToClassList("felt-unlock-glow-outer");
            innerGlow = new VisualElement { name = "felt-unlock-glow-inner", pickingMode = PickingMode.Ignore };
            innerGlow.AddToClassList("felt-unlock-glow-inner");
            overlay.Add(caption);
            overlay.Add(feltName);
            overlay.Add(stage);
            overlay.Add(hint);
            documentRoot.Add(overlay);
            overlay.RegisterCallback<PointerDownEvent>(OnPointerDown);
            overlay.RegisterCallback<AttachToPanelEvent>(_ => SubscribeLanguage());
            overlay.RegisterCallback<DetachFromPanelEvent>(_ => Loc.LanguageChanged -= RefreshText);
            if (overlay.panel != null)
            {
                SubscribeLanguage();
            }

            overlay.schedule.Execute(Tick).Every(0);
        }

        public bool IsPlaying => isPlaying;
        public bool IsRevealed { get; private set; }

        public void Play(IReadOnlyList<FeltUnlockPresentationItem> unlockedFelts, Action finished)
        {
            Cancel();
            if (unlockedFelts != null)
            {
                for (var index = 0; index < unlockedFelts.Count; index++)
                {
                    if (unlockedFelts[index] != null)
                    {
                        items.Add(unlockedFelts[index]);
                    }
                }
            }

            onFinished = finished;
            if (items.Count == 0)
            {
                Finish();
                return;
            }

            currentIndex = 0;
            isPlaying = true;
            FrameRateGovernor.NotifyActivity();
            overlay.style.display = DisplayStyle.Flex;
            ShowCurrentFelt();
            Evaluate(0f);
        }

        public void Evaluate(float time)
        {
            if (!isPlaying)
            {
                return;
            }

            elapsed = Mathf.Max(0f, time);
            IsRevealed = elapsed >= FullDuration;
            ApplyTimeline();
        }

        public void Advance()
        {
            if (!isPlaying)
            {
                return;
            }

            if (!IsRevealed)
            {
                Evaluate(FullDuration);
                return;
            }

            currentIndex++;
            if (currentIndex >= items.Count)
            {
                Finish();
                return;
            }

            elapsed = 0f;
            IsRevealed = false;
            ShowCurrentFelt();
            Evaluate(0f);
        }

        public void Cancel()
        {
            isPlaying = false;
            IsRevealed = false;
            elapsed = 0f;
            currentIndex = 0;
            items.Clear();
            swatch = null;
            onFinished = null;
            stage.Clear();
            overlay.style.display = DisplayStyle.None;
        }

        private void SubscribeLanguage()
        {
            Loc.LanguageChanged -= RefreshText;
            Loc.LanguageChanged += RefreshText;
        }

        private void Tick(TimerState timer)
        {
            if (!isPlaying || stage.contentRect.width <= 0f || stage.contentRect.height <= 0f)
            {
                return;
            }

            FrameRateGovernor.NotifyActivity();
            var deltaSeconds = Mathf.Clamp(timer.deltaTime / 1000f, 0f, MaxTickSeconds);
            Evaluate(elapsed + deltaSeconds);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            Advance();
        }

        private void RefreshText()
        {
            caption.text = Loc.Get("felt_unlock.caption");
            hint.text = Loc.Get("felt_unlock.hint");
            if (isPlaying && currentIndex < items.Count)
            {
                feltName.text = items[currentIndex].DisplayName;
            }
        }

        private void ShowCurrentFelt()
        {
            var item = items[currentIndex];
            feltName.text = item.DisplayName;
            stage.Clear();
            stage.Add(outerGlow);
            stage.Add(innerGlow);
            swatch = CosmeticPreviews.CreateFeltSwatch(item.Felt);
            swatch.name = "felt-unlock-swatch";
            swatch.AddToClassList("felt-unlock-swatch");
            swatch.pickingMode = PickingMode.Ignore;
            swatch.style.position = Position.Absolute;
            stage.Add(swatch);
        }

        private void ApplyTimeline()
        {
            var width = stage.contentRect.width;
            var height = stage.contentRect.height;
            if (width <= 0f || height <= 0f)
            {
                width = Mathf.Max(overlay.contentRect.width, 1280f);
                height = Mathf.Max(overlay.contentRect.height, 720f);
            }

            var swatchWidth = Mathf.Min(width * 0.52f, height * 0.46f * 1.6f);
            var swatchHeight = swatchWidth / 1.6f;
            var swatchLeft = (width - swatchWidth) * 0.5f;
            var swatchTop = (height - swatchHeight) * 0.5f + height * 0.04f;
            var unroll = EaseOutCubic(elapsed / UnrollDuration);
            var settle = EaseInOutCubic((elapsed - UnrollDuration) / SettleDuration);
            var lift = Mathf.Lerp(24f, 0f, settle);

            swatch.style.left = swatchLeft;
            swatch.style.top = swatchTop + lift;
            swatch.style.width = swatchWidth;
            swatch.style.height = swatchHeight;
            swatch.style.scale = new Scale(new Vector2(unroll, 1f));
            swatch.style.opacity = unroll;

            outerGlow.style.left = swatchLeft - 18f;
            outerGlow.style.top = swatchTop - 18f + lift;
            outerGlow.style.width = swatchWidth + 36f;
            outerGlow.style.height = swatchHeight + 36f;
            innerGlow.style.left = swatchLeft - 8f;
            innerGlow.style.top = swatchTop - 8f + lift;
            innerGlow.style.width = swatchWidth + 16f;
            innerGlow.style.height = swatchHeight + 16f;

            if (IsRevealed)
            {
                var pulse = 0.65f + Mathf.Sin((elapsed - FullDuration) * Mathf.PI * 2f / 1.8f) * 0.2f;
                outerGlow.style.opacity = pulse;
                innerGlow.style.opacity = Mathf.Min(1f, pulse + 0.2f);
                stage.style.rotate = new Rotate(new Angle(
                    Mathf.Sin((elapsed - FullDuration) * Mathf.PI * 2f / WobblePeriod) * 2f,
                    AngleUnit.Degree));
            }
            else
            {
                outerGlow.style.opacity = 0f;
                innerGlow.style.opacity = 0f;
                stage.style.rotate = new Rotate(new Angle(0f, AngleUnit.Degree));
            }
        }

        private void Finish()
        {
            var callback = onFinished;
            Cancel();
            callback?.Invoke();
        }

        private static float EaseOutCubic(float value)
        {
            var t = Mathf.Clamp01(value);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private static float EaseInOutCubic(float value)
        {
            var t = Mathf.Clamp01(value);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }
    }
}