using System;
using System.Collections.Generic;
using Mejnun.Core;
using Mejnun.Cosmetics;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public sealed class MenuCardCursor : IDisposable
    {
        private const float CardWidth = 32f;
        private const float CardHeight = 45f;
        private const float CardGap = 8f;
        private const float WobblePeriod = 2.4f;
        private const float WobbleAngle = 7f;
        private const float SpinDuration = 0.45f;
        private const float FollowSharpness = 14f;
        private const float MaxFrameDelta = 0.1f;

        private readonly VisualElement root;
        private readonly VisualElement tutorialPrompt;
        private readonly CosmeticCatalog catalog;
        private readonly ProgressionService progression;
        private readonly DeckSkinLoader loader;
        private readonly Image image;
        private readonly List<Button> buttons = new List<Button>();
        private IVisualElementScheduledItem scheduledTick;
        private Button targetButton;
        private Vector2 currentPosition;
        private bool hasCurrentPosition;
        private bool isVisible;
        private bool isSpinning;
        private bool disposed;
        private float spinStartTime;
        private float lastTickTime;
        private int selectionVersion;

        public MenuCardCursor(VisualElement menuRoot, VisualElement prompt, CosmeticCatalog cosmeticCatalog,
            ProgressionService progressionService, DeckSkinLoader skinLoader)
        {
            root = menuRoot ?? throw new ArgumentNullException(nameof(menuRoot));
            tutorialPrompt = prompt;
            catalog = cosmeticCatalog;
            progression = progressionService ?? throw new ArgumentNullException(nameof(progressionService));
            loader = skinLoader;

            image = new Image
            {
                name = "menu-card-cursor",
                pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleToFit
            };
            image.style.position = Position.Absolute;
            image.style.left = 0f;
            image.style.top = 0f;
            image.style.width = CardWidth;
            image.style.height = CardHeight;
            image.style.display = DisplayStyle.None;
            root.Add(image);

            root.Query<Button>(className: "frontend-button").ForEach(button =>
            {
                if (tutorialPrompt != null && tutorialPrompt.Contains(button))
                {
                    return;
                }

                buttons.Add(button);
                button.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
                button.RegisterCallback<FocusInEvent>(OnFocusIn);
                button.clicked += OnButtonClicked;
            });

            scheduledTick = image.schedule.Execute(Tick).Every(0);
        }

        public void SetVisible(bool visible)
        {
            if (disposed)
            {
                return;
            }

            isVisible = visible;
            if (!visible)
            {
                image.style.display = DisplayStyle.None;
                targetButton = null;
                return;
            }

            targetButton = FindFirstVisibleButton();
            hasCurrentPosition = false;
            SelectRandomCard();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            scheduledTick?.Pause();
            for (var index = 0; index < buttons.Count; index++)
            {
                var button = buttons[index];
                button.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
                button.UnregisterCallback<FocusInEvent>(OnFocusIn);
                button.clicked -= OnButtonClicked;
            }

            buttons.Clear();
            image.RemoveFromHierarchy();
        }

        private void SelectRandomCard()
        {
            var version = ++selectionVersion;
            image.sprite = null;
            image.style.display = DisplayStyle.None;
            if (!TryChooseSkin(out var choice))
            {
                return;
            }

            if (choice.Skin != null)
            {
                ApplySkin(choice.Skin, version);
            }
            else if (loader != null)
            {
                loader.Load(choice.Id, skin => ApplySkin(skin, version));
            }
        }

        private bool TryChooseSkin(out DeckSkinInfo choice)
        {
            choice = default;
            var deckCatalog = catalog == null ? null : catalog.Decks;
            var entries = deckCatalog == null ? null : deckCatalog.GetEntries();
            if (entries == null || entries.Count == 0)
            {
                return false;
            }

            var evaluator = progression.CreateUnlockEvaluator();
            var recent = progression.Profile.LastDeckPicks;
            var candidates = new List<DeckSkinInfo>();
            for (var pickIndex = 0; pickIndex < recent.Length; pickIndex++)
            {
                var skinId = string.IsNullOrWhiteSpace(recent[pickIndex])
                    ? null
                    : DeckFaceId.GetSkinId(recent[pickIndex]);
                if (string.IsNullOrWhiteSpace(skinId))
                {
                    continue;
                }

                for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    var entry = entries[entryIndex];
                    if (string.Equals(entry.Id, skinId, StringComparison.Ordinal) &&
                        evaluator.IsOwned(entry.Id, entry.Unlock))
                    {
                        candidates.Add(entry);
                        break;
                    }
                }
            }

            if (candidates.Count > 0)
            {
                choice = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                return true;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry.Skin != null && evaluator.IsOwned(entry.Id, entry.Unlock))
                {
                    choice = entry;
                    return true;
                }
            }

            return false;
        }

        private void ApplySkin(DeckSkin skin, int version)
        {
            if (disposed || version != selectionVersion || skin == null)
            {
                return;
            }

            var sprite = skin.GetFace((Suit)UnityEngine.Random.Range(0, 4), (Rank)UnityEngine.Random.Range(0, 13));
            image.sprite = sprite;
            image.style.display = isVisible && sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            SetTarget(evt.currentTarget as Button);
        }

        private void OnFocusIn(FocusInEvent evt)
        {
            SetTarget(evt.currentTarget as Button);
        }

        private void OnButtonClicked()
        {
            if (isVisible && image.sprite != null)
            {
                spinStartTime = Time.realtimeSinceStartup;
                isSpinning = true;
            }
        }

        private void SetTarget(Button button)
        {
            if (IsButtonVisible(button))
            {
                targetButton = button;
            }
        }

        private void Tick()
        {
            var now = Time.realtimeSinceStartup;
            var deltaTime = Mathf.Clamp(now - lastTickTime, 0f, MaxFrameDelta);
            lastTickTime = now;
            if (disposed || !isVisible)
            {
                return;
            }

            if (tutorialPrompt != null && tutorialPrompt.resolvedStyle.display != DisplayStyle.None)
            {
                image.style.display = DisplayStyle.None;
                return;
            }

            if (!IsButtonVisible(targetButton))
            {
                targetButton = FindFirstVisibleButton();
            }

            if (targetButton == null || image.sprite == null)
            {
                image.style.display = DisplayStyle.None;
                return;
            }

            image.style.display = DisplayStyle.Flex;
            var middleLeft = targetButton.ChangeCoordinatesTo(root,
                new Vector2(0f, targetButton.layout.height * 0.5f));
            var targetPosition = new Vector2(middleLeft.x - CardWidth - CardGap, middleLeft.y - CardHeight * 0.5f);
            if (!hasCurrentPosition)
            {
                currentPosition = targetPosition;
                hasCurrentPosition = true;
            }
            else
            {
                currentPosition = Vector2.Lerp(currentPosition, targetPosition,
                    1f - Mathf.Exp(-FollowSharpness * deltaTime));
            }

            image.style.translate = new Translate(new Length(currentPosition.x, LengthUnit.Pixel),
                new Length(currentPosition.y, LengthUnit.Pixel));

            var spin = 0f;
            if (isSpinning)
            {
                var progress = Mathf.Clamp01((now - spinStartTime) / SpinDuration);
                spin = 360f * (1f - Mathf.Pow(1f - progress, 3f));
                isSpinning = progress < 1f;
            }

            var wobble = Mathf.Sin(now * Mathf.PI * 2f / WobblePeriod) * WobbleAngle;
            image.style.rotate = new Rotate(new Angle(spin + wobble, AngleUnit.Degree));
        }

        private Button FindFirstVisibleButton()
        {
            for (var index = 0; index < buttons.Count; index++)
            {
                if (IsButtonVisible(buttons[index]))
                {
                    return buttons[index];
                }
            }

            return null;
        }

        private static bool IsButtonVisible(Button button)
        {
            return button != null && button.enabledInHierarchy &&
                   button.resolvedStyle.display != DisplayStyle.None && button.layout.width > 0f;
        }
    }
}