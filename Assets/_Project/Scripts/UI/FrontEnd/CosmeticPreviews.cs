using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public sealed class CosmeticPreviews
    {
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
                ? "Preview unavailable"
                : "Preview loading";
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
                fan.Add(new Label("Preview unavailable"));
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

        public static VisualElement CreateFeltSwatch(TableFelt felt)
        {
            var preview = new VisualElement();
            preview.AddToClassList("felt-swatch");
            if (felt == null)
            {
                preview.style.backgroundColor = new Color(0.16f, 0.38f, 0.26f);
                preview.Add(new Label("Random felt"));
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