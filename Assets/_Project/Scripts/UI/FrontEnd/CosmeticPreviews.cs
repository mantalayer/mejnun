using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Core;
using Mejnun.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public sealed class CosmeticPreviews
    {
        private static readonly Suit[] NewDeckSuitOrder = { Suit.Spades, Suit.Diamonds, Suit.Clubs, Suit.Hearts };

        private readonly DeckSkinLoader skinLoader;
        private readonly HashSet<string> requestedSkins = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> failedSkins = new HashSet<string>(StringComparer.Ordinal);

        public CosmeticPreviews(DeckSkinLoader loader)
        {
            skinLoader = loader;
        }

        public void AddDeckFan(VisualElement container, DeckSkinInfo skin, Action refreshed = null)
        {
            if (skin.Skin != null)
            {
                container.Add(BuildDeckFan(skin.Skin));
                return;
            }

            var message = !skin.IsAddressable || failedSkins.Contains(skin.Id) || skinLoader == null
                ? Loc.Get("gallery.preview.unavailable")
                : Loc.Get("gallery.preview.loading");
            container.Add(new Label(message));
            if (!skin.IsAddressable || skinLoader == null || !requestedSkins.Add(skin.Id))
            {
                return;
            }

            skinLoader.Load(skin.Id, loaded =>
            {
                if (loaded == null)
                {
                    failedSkins.Add(skin.Id);
                }

                refreshed?.Invoke();
            });
        }

        public static VisualElement BuildDeckFan(DeckSkin skin)
        {
            var fan = new VisualElement();
            fan.AddToClassList("hand-fan-preview");
            if (skin == null)
            {
                fan.Add(new Label(Loc.Get("gallery.preview.unavailable")));
                return fan;
            }

            var ranks = new[] { Rank.Queen, Rank.King, Rank.Ace };
            var rotations = new[] { -12f, 0f, 12f };
            for (var index = 0; index < ranks.Length; index++)
            {
                var image = new Image { sprite = skin.GetFace(Suit.Hearts, ranks[index]) };
                image.AddToClassList("fan-card");
                image.style.rotate = new Rotate(new Angle(rotations[index], AngleUnit.Degree));
                fan.Add(image);
            }

            return fan;
        }

        public static VisualElement BuildDeckGrid(DeckSkin skin)
        {
            var grid = new VisualElement();
            grid.AddToClassList("gallery-deck-grid");
            var faces = GetNewDeckFaces(skin);
            var cards = new List<Image>(faces.Count);
            for (var index = 0; index < faces.Count; index++)
            {
                var image = new Image
                {
                    sprite = faces[index],
                    scaleMode = ScaleMode.ScaleToFit
                };
                image.AddToClassList("gallery-face");
                grid.Add(image);
                cards.Add(image);
            }

            if (skin.CardBack != null)
            {
                var image = new Image
                {
                    sprite = skin.CardBack,
                    scaleMode = ScaleMode.ScaleToFit
                };
                image.AddToClassList("gallery-face");
                image.AddToClassList("gallery-card-back");
                grid.Add(image);
                cards.Add(image);
            }

            var layoutColumns = 0;
            grid.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var width = grid.contentRect.width;
                var height = grid.contentRect.height;
                if (width <= 0f || height <= 0f)
                {
                    return;
                }

                const float cardAspect = 0.72f;
                const float gap = 2f;
                var bestWidth = 0f;
                var bestColumns = 0;
                for (var columns = 1; columns <= cards.Count; columns++)
                {
                    var rows = (cards.Count + columns - 1) / columns;
                    var columnWidth = (width - gap * (columns - 1)) / columns;
                    var rowHeight = (height - gap * (rows - 1)) / rows;
                    var cardWidth = Mathf.Min(columnWidth, rowHeight * cardAspect);
                    if (cardWidth > bestWidth)
                    {
                        bestWidth = cardWidth;
                        bestColumns = columns;
                    }
                }

                bestWidth = Mathf.Floor(bestWidth);
                if (bestWidth <= 0f || bestColumns <= 0)
                {
                    return;
                }

                if (bestColumns != layoutColumns)
                {
                    grid.Clear();
                    for (var start = 0; start < cards.Count; start += bestColumns)
                    {
                        var row = new VisualElement();
                        row.AddToClassList("gallery-deck-row");
                        var end = Mathf.Min(start + bestColumns, cards.Count);
                        for (var index = start; index < end; index++)
                        {
                            row.Add(cards[index]);
                        }

                        if (end < cards.Count)
                        {
                            row.style.marginBottom = gap;
                        }

                        grid.Add(row);
                    }

                    layoutColumns = bestColumns;
                }

                var cardHeight = bestWidth / cardAspect;
                for (var index = 0; index < cards.Count; index++)
                {
                    cards[index].style.width = bestWidth;
                    cards[index].style.height = cardHeight;
                    cards[index].style.marginRight = index % bestColumns < bestColumns - 1 ? gap : 0f;
                    cards[index].style.marginBottom = 0f;
                }
            });
            return grid;
        }

        public static IReadOnlyList<Sprite> GetNewDeckFaces(DeckSkin skin)
        {
            if (skin == null)
            {
                throw new ArgumentNullException(nameof(skin));
            }

            var faces = new List<Sprite>(Card.CardsPerDeck);
            for (var suitIndex = 0; suitIndex < NewDeckSuitOrder.Length; suitIndex++)
            {
                for (var rankIndex = 0; rankIndex < 13; rankIndex++)
                {
                    faces.Add(skin.GetFace(NewDeckSuitOrder[suitIndex], (Rank)rankIndex));
                }
            }

            return faces.AsReadOnly();
        }

        public static VisualElement CreateFeltSwatch(TableFelt felt)
        {
            var preview = new VisualElement();
            preview.AddToClassList("felt-swatch");
            if (felt == null)
            {
                preview.style.backgroundColor = new Color(0.16f, 0.38f, 0.26f);
                var randomLogo = new VisualElement();
                randomLogo.AddToClassList("random-deck-logo");
                preview.Add(randomLogo);
                return preview;
            }

            preview.style.backgroundColor = felt.ClearColor;
            var sprite = felt.Mode == FeltRenderMode.Image ? felt.Image : felt.Tile;
            if (sprite != null)
            {
                preview.style.backgroundImage = new StyleBackground(sprite);
            }

            return preview;
        }
    }
}