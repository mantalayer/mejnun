using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Core;
using Mejnun.UI.FrontEnd;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class DeckUnlockPresentationItem
    {
        public DeckUnlockPresentationItem(DeckSkin skin, string displayName, string artist = null)
        {
            Skin = skin ?? throw new ArgumentNullException(nameof(skin));
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? skin.DisplayName : displayName;
            Artist = string.IsNullOrWhiteSpace(artist) ? skin.Artist : artist;
        }

        public DeckSkin Skin { get; }
        public string DisplayName { get; }
        public string Artist { get; }
    }

    public sealed class DeckUnlockPresentation
    {
        private const float DealDuration = 0.75f;
        private const float DealStagger = 0.01f;
        private const float HoldDuration = 0.3f;
        private const float FlipDuration = 0.3f;
        private const float FlipStagger = 0.02f;
        private const float FlipHoldDuration = 1.3f;
        private const float SquareDuration = 0.6f;
        private const float SquareStagger = 0.01f;
        private const float WobblePeriod = 1.2f;
        private const float MaxTickSeconds = 0.1f;
        private const float LastCardStagger = (Card.CardsPerDeck - 1) * DealStagger;
        private const float LastSquareStagger = (Card.CardsPerDeck - 1) * SquareStagger;
        private const float LastFlipStagger = (Card.CardsPerDeck - 1) * FlipStagger;
        private const float DealEnd = DealDuration + LastCardStagger;
        private const float FlipStart = DealEnd + HoldDuration;
        private const float FlipEnd = FlipStart + FlipDuration + LastFlipStagger;
        private const float SquareStart = FlipEnd + FlipHoldDuration;
        private const float SquaredAt = SquareStart + SquareDuration + LastSquareStagger;

        public const float FullDuration = SquaredAt;

        private readonly VisualElement overlay;
        private readonly VisualElement stage;
        private readonly VisualElement outerGlow;
        private readonly VisualElement innerGlow;
        private readonly Label caption;
        private readonly Label deckName;
        private readonly Label artist;
        private readonly Label hint;
        private readonly List<Image> cards = new List<Image>(Card.CardsPerDeck);
        private readonly List<Sprite> cardFaces = new List<Sprite>(Card.CardsPerDeck);
        private Sprite cardBack;
        private readonly List<DeckUnlockPresentationItem> items = new List<DeckUnlockPresentationItem>();

        private Action onFinished;
        private int currentIndex;
        private float elapsed;
        private bool isPlaying;

        public DeckUnlockPresentation(VisualElement documentRoot)
        {
            if (documentRoot == null)
            {
                throw new ArgumentNullException(nameof(documentRoot));
            }

            overlay = new VisualElement { name = "deck-unlock-overlay", pickingMode = PickingMode.Position };
            overlay.AddToClassList("deck-unlock-overlay");
            overlay.style.display = DisplayStyle.None;
            caption = new Label("New deck unlocked") { name = "deck-unlock-caption" };
            caption.AddToClassList("deck-unlock-caption");
            deckName = new Label { name = "deck-unlock-name" };
            deckName.AddToClassList("deck-unlock-name");
            artist = new Label { name = "deck-unlock-artist" };
            artist.AddToClassList("deck-unlock-artist");
            hint = new Label("Tap to continue") { name = "deck-unlock-hint" };
            hint.AddToClassList("deck-unlock-hint");
            stage = new VisualElement { name = "deck-unlock-stage", pickingMode = PickingMode.Ignore };
            stage.AddToClassList("deck-unlock-stage");
            outerGlow = new VisualElement { name = "deck-unlock-glow-outer", pickingMode = PickingMode.Ignore };
            outerGlow.AddToClassList("deck-unlock-glow-outer");
            innerGlow = new VisualElement { name = "deck-unlock-glow-inner", pickingMode = PickingMode.Ignore };
            innerGlow.AddToClassList("deck-unlock-glow-inner");
            overlay.Add(caption);
            overlay.Add(deckName);
            overlay.Add(artist);
            overlay.Add(stage);
            overlay.Add(hint);
            documentRoot.Add(overlay);
            overlay.RegisterCallback<PointerDownEvent>(OnPointerDown);
            overlay.schedule.Execute(Tick).Every(0);
        }

        public bool IsPlaying => isPlaying;
        public bool IsSquared { get; private set; }
        public bool AreFacesShowing { get; private set; }
        public IReadOnlyList<Image> Cards => cards;

        public void Play(IReadOnlyList<DeckUnlockPresentationItem> unlockedDecks, Action finished)
        {
            Cancel();
            if (unlockedDecks != null)
            {
                for (var index = 0; index < unlockedDecks.Count; index++)
                {
                    if (unlockedDecks[index] != null)
                    {
                        items.Add(unlockedDecks[index]);
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
            overlay.style.display = DisplayStyle.Flex;
            ShowCurrentDeck();
            Evaluate(0f);
        }

        public void Evaluate(float time)
        {
            if (!isPlaying)
            {
                return;
            }

            elapsed = Mathf.Max(0f, time);
            IsSquared = elapsed >= FullDuration;
            AreFacesShowing = elapsed >= FlipEnd;
            ApplyTimeline();
        }

        public void Cancel()
        {
            isPlaying = false;
            IsSquared = false;
            AreFacesShowing = false;
            elapsed = 0f;
            currentIndex = 0;
            items.Clear();
            cards.Clear();
            cardFaces.Clear();
            cardBack = null;
            onFinished = null;
            overlay.style.display = DisplayStyle.None;
        }

        private void Tick(TimerState timer)
        {
            if (!isPlaying || stage.contentRect.width <= 0f || stage.contentRect.height <= 0f)
            {
                return;
            }

            var deltaSeconds = Mathf.Clamp(timer.deltaTime / 1000f, 0f, MaxTickSeconds);
            Evaluate(elapsed + deltaSeconds);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!isPlaying)
            {
                return;
            }

            if (!IsSquared)
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
            IsSquared = false;
            ShowCurrentDeck();
            Evaluate(0f);
        }

        private void ShowCurrentDeck()
        {
            var item = items[currentIndex];
            deckName.text = item.DisplayName;
            artist.text = string.IsNullOrWhiteSpace(item.Artist) ? string.Empty : "by " + item.Artist;
            artist.style.display = string.IsNullOrWhiteSpace(item.Artist) ? DisplayStyle.None : DisplayStyle.Flex;

            stage.Clear();
            stage.Add(outerGlow);
            stage.Add(innerGlow);
            cards.Clear();
            cardFaces.Clear();
            cardBack = item.Skin.CardBack;
            var faces = CosmeticPreviews.GetNewDeckFaces(item.Skin);
            for (var index = 0; index < faces.Count; index++)
            {
                cardFaces.Add(faces[index]);
                var image = new Image
                {
                    sprite = cardBack == null ? faces[index] : cardBack,
                    scaleMode = ScaleMode.ScaleToFit
                };
                image.AddToClassList("deck-unlock-card");
                image.pickingMode = PickingMode.Ignore;
                image.style.position = Position.Absolute;
                stage.Add(image);
                cards.Add(image);
            }
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

            const float aspect = 0.72f;
            var gridWidth = Mathf.Min(width * 0.9f / 13f, height * 0.82f / 4f * aspect);
            var gridHeight = gridWidth / aspect;
            var gridGapX = gridWidth * 0.08f;
            var gridGapY = gridHeight * 0.08f;
            var gridTotalWidth = gridWidth * 13 + gridGapX * 12;
            var gridTotalHeight = gridHeight * 4 + gridGapY * 3;
            var gridLeft = (width - gridTotalWidth) * 0.5f;
            var gridTop = (height - gridTotalHeight) * 0.5f;
            var deckWidth = Mathf.Min(width * 0.26f, height * 0.42f * aspect);
            var deckHeight = deckWidth / aspect;
            var deckLeft = (width - deckWidth) * 0.5f;
            var deckTop = (height - deckHeight) * 0.5f;

            for (var index = 0; index < cards.Count; index++)
            {
                var column = index % 13;
                var row = index / 13;
                var targetX = gridLeft + column * (gridWidth + gridGapX);
                var targetY = gridTop + row * (gridHeight + gridGapY);
                var stackOffset = (Card.CardsPerDeck - index - 1) * 0.035f;
                var stackX = deckLeft + stackOffset;
                var stackY = deckTop + stackOffset;
                var dealProgress = EaseOutCubic((elapsed - index * DealStagger) / DealDuration);
                var dealX = Mathf.Lerp(deckLeft + (Card.CardsPerDeck - index) * 0.06f, targetX, dealProgress);
                var dealY = Mathf.Lerp(deckTop + (Card.CardsPerDeck - index) * 0.06f, targetY, dealProgress);
                var squareProgress = EaseInOutCubic((elapsed - SquareStart - index * SquareStagger) / SquareDuration);
                var currentX = Mathf.Lerp(dealX, stackX, squareProgress);
                var currentY = Mathf.Lerp(dealY, stackY, squareProgress);
                var currentWidth = Mathf.Lerp(deckWidth, gridWidth, dealProgress);
                var currentHeight = Mathf.Lerp(deckHeight, gridHeight, dealProgress);
                currentWidth = Mathf.Lerp(currentWidth, deckWidth, squareProgress);
                currentHeight = Mathf.Lerp(currentHeight, deckHeight, squareProgress);

                cards[index].style.left = currentX;
                cards[index].style.top = currentY;
                cards[index].style.width = currentWidth;
                cards[index].style.height = currentHeight;

                var flipProgress = Mathf.Clamp01((elapsed - FlipStart - index * FlipStagger) / FlipDuration);
                var showFace = flipProgress >= 0.5f || cardBack == null;
                var sprite = showFace ? cardFaces[index] : cardBack;
                if (cards[index].sprite != sprite)
                {
                    cards[index].sprite = sprite;
                }

                var flipScale = Mathf.Max(0.02f, Mathf.Abs(Mathf.Cos(flipProgress * Mathf.PI)));
                var lift = 1f + Mathf.Sin(flipProgress * Mathf.PI) * 0.12f;
                cards[index].style.scale = new Scale(new Vector2(flipScale * lift, lift));
            }

            outerGlow.style.left = deckLeft - 16f;
            outerGlow.style.top = deckTop - 16f;
            outerGlow.style.width = deckWidth + 32f;
            outerGlow.style.height = deckHeight + 32f;
            innerGlow.style.left = deckLeft - 7f;
            innerGlow.style.top = deckTop - 7f;
            innerGlow.style.width = deckWidth + 14f;
            innerGlow.style.height = deckHeight + 14f;

            if (IsSquared)
            {
                var pulse = 0.65f + Mathf.Sin((elapsed - FullDuration) * Mathf.PI * 2f / 1.8f) * 0.2f;
                outerGlow.style.opacity = pulse;
                innerGlow.style.opacity = Mathf.Min(1f, pulse + 0.2f);
                stage.style.rotate = new Rotate(new Angle(
                    Mathf.Sin((elapsed - FullDuration) * Mathf.PI * 2f / WobblePeriod) * 3f,
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