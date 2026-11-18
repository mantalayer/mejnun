using Mejnun.Cosmetics;
using Mejnun.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public static class MenuFeltBackground
    {
        public static string ResolveFeltId(PlayerProfile profile)
        {
            if (profile != null && !string.IsNullOrWhiteSpace(profile.LastUsedFelt))
            {
                return profile.LastUsedFelt;
            }

            if (profile != null && !string.IsNullOrWhiteSpace(profile.LastFeltPick))
            {
                return profile.LastFeltPick;
            }

            return TableFeltAssigner.GreenFelt;
        }

        public static void Apply(VisualElement element, TableFelt felt)
        {
            if (element == null)
            {
                return;
            }

            if (felt == null)
            {
                element.style.backgroundColor = StyleKeyword.Null;
                element.style.backgroundImage = StyleKeyword.Null;
                element.style.backgroundRepeat = StyleKeyword.Null;
                element.style.backgroundSize = StyleKeyword.Null;
                element.style.backgroundPositionX = StyleKeyword.Null;
                element.style.backgroundPositionY = StyleKeyword.Null;
                element.style.unityBackgroundImageTintColor = StyleKeyword.Null;
                return;
            }

            element.style.backgroundColor = felt.ClearColor;
            element.style.unityBackgroundImageTintColor = felt.Tint;
            if (felt.Mode == FeltRenderMode.Image)
            {
                element.style.backgroundImage = felt.Image == null
                    ? StyleKeyword.Null
                    : new StyleBackground(felt.Image);
                element.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                element.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                element.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                element.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
                return;
            }

            element.style.backgroundImage = felt.Tile == null
                ? StyleKeyword.Null
                : new StyleBackground(felt.Tile);
            element.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
            element.style.backgroundSize = felt.Tile == null
                ? StyleKeyword.Null
                : new BackgroundSize(felt.Tile.rect.width, felt.Tile.rect.height);
            element.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left);
            element.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top);
        }
    }
}