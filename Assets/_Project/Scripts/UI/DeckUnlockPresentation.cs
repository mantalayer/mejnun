using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Core;
using Mejnun.Game;
using Mejnun.Localization;
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
        public struct GridLayout
        {
            public readonly int Columns;
            public readonly int Rows;
            public readonly float CardWidth;
            public readonly float CardHeight;
            public readonly float GapX;
            public readonly float GapY;
            public readonly float Left;
            public readonly float Top;

            public GridLayout(int columns, int rows, float cardWidth, float cardHeight, float gapX, float gapY, float left, float top)
            {
                Columns = columns;
                Rows = rows;
                CardWidth = cardWidth;
                CardHeight = cardHeight;
                GapX = gapX;
                GapY = gapY;
                Left = left;
                Top = top;
            }
        }

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
            caption = new Label(Loc.Get("deck_unlock.caption"));
            caption.name = "deck-unlock-caption";
            caption.AddToClassList("deck-unlock-caption");
            deckName = new Label { name = "deck-unlock-name" };
            deckName.AddToClassList("deck-unlock-name");
            artist = new Label { name = "deck-unlock-artist" };
            artist.AddToClassList("deck-unlock-artist");
            hint = new Label(Loc.Get("deck_unlock.hint"));
            hint.name = "deck-unlock-hint";
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
            overlay.RegisterCallback<AttachToPanelEvent>(_ => SubscribeLanguage());
            overlay.RegisterCallback<DetachFromPanelEvent>(_ => Loc.LanguageChanged -= RefreshText);
            if (overlay.panel != null)
            {
                SubscribeLanguage();
            }

            overlay.schedule.Execute(Tick).Every(0);
        }

        private void SubscribeLanguage()
        {
            Loc.LanguageChanged -= RefreshText;
            Loc.LanguageChanged += RefreshText;
        }

        public bool IsPlaying => isPlaying;
        public bool IsSquared { get; private set; }
        public bool AreFacesShowing { get; private set; }
        public IReadOnlyList<Image> Cards => cards;

        public static GridLayout ComputeGridLayout(float width, float height, int cardCount)
        {
            const float aspect = 0.72f;
            const float gapRatio = 0.08f;
            var isPortrait = height > width;
            var availableWidth = width * (isPortrait ? 0.92f : 0.9f);
            var availableHeight = height * (isPortrait ? 0.68f : 0.76f);
            var verticalOffset = height * (isPortrait ? 0.05f : 0.04f);
            if (cardCount <= 0 || width <= 0f || height <= 0f)
            {
                return new GridLayout(0, 0, 0f, 0f, 0f, 0f, width * 0.5f, height * 0.5f);
            }

            // Keep the established 13-by-4 presentation in landscape; portrait layouts
            // search for the widest cards that fit both available dimensions.
            var bestColumns = isPortrait ? 1 : Mathf.Min(13, cardCount);
            var bestRows = Mathf.CeilToInt(cardCount / (float)bestColumns);
            var bestCardWidth = 0f;
            var firstColumn = isPortrait ? 1 : bestColumns;
            var lastColumn = isPortrait ? cardCount : bestColumns;
            for (var columns = firstColumn; columns <= lastColumn; columns++)
            {
                var rows = Mathf.CeilToInt(cardCount / (float)columns);
                var widthPerCard = availableWidth / (columns + (columns - 1) * gapRatio);
                var heightPerCard = availableHeight * aspect / (rows + (rows - 1) * gapRatio);
                var cardWidth = Mathf.Min(widthPerCard, heightPerCard);
                if (cardWidth > bestCardWidth)
                {
                    bestColumns = columns;
                    bestRows = rows;
                    bestCardWidth = cardWidth;
                }
            }

            var cardHeight = bestCardWidth / aspect;
            var gapX = bestCardWidth * gapRatio;
            var gapY = cardHeight * gapRatio;
            var totalWidth = bestCardWidth * bestColumns + gapX * (bestColumns - 1);
            var totalHeight = cardHeight * bestRows + gapY * (bestRows - 1);
            var left = (width - totalWidth) * 0.5f;
            var top = (height - totalHeight) * 0.5f + verticalOffset;
            return new GridLayout(bestColumns, bestRows, bestCardWidth, cardHeight, gapX, gapY, left, top);
        }

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
            FrameRateGovernor.NotifyActivity();
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

            FrameRateGovernor.NotifyActivity();
            var deltaSeconds = Mathf.Clamp(timer.deltaTime / 1000f, 0f, MaxTickSeconds);
            Evaluate(elapsed + deltaSeconds);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            Advance();
        }

        public void Advance()
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

        private void RefreshText()
        {
            caption.text = Loc.Get("deck_unlock.caption");
            hint.text = Loc.Get("deck_unlock.hint");
            if (isPlaying && currentIndex < items.Count)
            {
                RefreshDeckText(items[currentIndex]);
            }
        }

        private void RefreshDeckText(DeckUnlockPresentationItem item)
        {
            deckName.text = item.DisplayName;
            artist.text = string.IsNullOrWhiteSpace(item.Artist)
                ? string.Empty
                : Loc.Format("deck_unlock.artist", item.Artist);
            artist.style.display = string.IsNullOrWhiteSpace(item.Artist) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void ShowCurrentDeck()
        {
            var item = items[currentIndex];
            RefreshDeckText(item);

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
            var isPortrait = height > width;
            var grid = ComputeGridLayout(width, height, cards.Count);
            var deckWidth = Mathf.Min(width * (isPortrait ? 0.45f : 0.26f), height * 0.42f * aspect);
            var deckHeight = deckWidth / aspect;
            var deckLeft = (width - deckWidth) * 0.5f;
            var deckTop = (height - deckHeight) * 0.5f + height * (isPortrait ? 0.05f : 0.04f);

            for (var index = 0; index < cards.Count; index++)
            {
                var row = index / grid.Columns;
                var column = index % grid.Columns;
                var rowCount = Mathf.Min(grid.Columns, cards.Count - row * grid.Columns);
                var rowLeft = grid.Left + (grid.Columns - rowCount) * (grid.CardWidth + grid.GapX) * 0.5f;
                var targetX = rowLeft + column * (grid.CardWidth + grid.GapX);
                var targetY = grid.Top + row * (grid.CardHeight + grid.GapY);
                var stackOffset = (Card.CardsPerDeck - index - 1) * 0.035f;
                var stackX = deckLeft + stackOffset;
                var stackY = deckTop + stackOffset;
                var dealProgress = EaseOutCubic((elapsed - index * DealStagger) / DealDuration);
                var dealX = Mathf.Lerp(deckLeft + (Card.CardsPerDeck - index) * 0.06f, targetX, dealProgress);
                var dealY = Mathf.Lerp(deckTop + (Card.CardsPerDeck - index) * 0.06f, targetY, dealProgress);
                var squareProgress = EaseInOutCubic((elapsed - SquareStart - index * SquareStagger) / SquareDuration);
                var currentX = Mathf.Lerp(dealX, stackX, squareProgress);
                var currentY = Mathf.Lerp(dealY, stackY, squareProgress);
                var currentWidth = Mathf.Lerp(deckWidth, grid.CardWidth, dealProgress);
                var currentHeight = Mathf.Lerp(deckHeight, grid.CardHeight, dealProgress);
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