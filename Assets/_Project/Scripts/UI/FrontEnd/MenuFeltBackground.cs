using System.Runtime.CompilerServices;
using Mejnun.Cosmetics;
using Mejnun.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public static class MenuFeltBackground
    {
        private sealed class BackgroundLayoutState
        {
            public TableFelt Felt;
            public bool IsRegistered;

            public void OnGeometryChanged(GeometryChangedEvent evt)
            {
                ApplyBackground(evt.currentTarget as VisualElement, Felt);
            }
        }

        private static readonly ConditionalWeakTable<VisualElement, BackgroundLayoutState> LayoutStates =
            new ConditionalWeakTable<VisualElement, BackgroundLayoutState>();

        public static string ResolveFeltId(PlayerProfile profile)
        {
            if (profile != null && !string.IsNullOrWhiteSpace(profile.LastFeltPick))
            {
                return profile.LastFeltPick;
            }

            if (profile != null && !string.IsNullOrWhiteSpace(profile.LastUsedFelt))
            {
                return profile.LastUsedFelt;
            }

            return TableFeltAssigner.GreenFelt;
        }

        public static void Apply(VisualElement element, TableFelt felt)
        {
            if (element == null)
            {
                return;
            }

            var state = LayoutStates.GetValue(element, _ => new BackgroundLayoutState());
            state.Felt = felt;
            if (!state.IsRegistered)
            {
                element.RegisterCallback<GeometryChangedEvent>(state.OnGeometryChanged);
                state.IsRegistered = true;
            }

            ApplyBackground(element, felt);
        }

        public static int ContentInsertIndex(VisualElement element)
        {
            var border = element?.Q<VisualElement>("felt-border");
            if (border == null)
            {
                return 0;
            }

            var index = element.IndexOf(border);
            return index < 0 ? 0 : index + 1;
        }

        private static void ApplyBackground(VisualElement element, TableFelt felt)
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
                RemoveBorder(element);
                return;
            }

            element.style.backgroundColor = felt.ClearColor;
            element.style.unityBackgroundImageTintColor = felt.Tint;
            if (felt.Mode == FeltRenderMode.Image)
            {
                RemoveBorder(element);
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
            var viewHeight = element.resolvedStyle.height;
            if (felt.Tile != null && viewHeight > 0f)
            {
                var tileSize = TableFeltLayout.TileSize(felt, viewHeight);
                element.style.backgroundSize = new BackgroundSize(tileSize.x, tileSize.y);
            }
            else
            {
                element.style.backgroundSize = StyleKeyword.Null;
            }

            element.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left);
            element.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top);
            ApplyBorder(element, felt);
        }

        private static void ApplyBorder(VisualElement element, TableFelt felt)
        {
            var settings = felt.BorderSettings;
            if (felt.Mode != FeltRenderMode.TiledWithBorder || !settings.Enabled || felt.Border == null)
            {
                RemoveBorder(element);
                return;
            }

            var border = element.Q<VisualElement>("felt-border");
            if (border == null)
            {
                border = new VisualElement { name = "felt-border", pickingMode = PickingMode.Ignore };
                border.style.position = Position.Absolute;
                border.style.left = 0f;
                border.style.right = 0f;
                border.style.top = 0f;
                border.style.bottom = 0f;
                element.Insert(0, border);
            }

            var viewHeight = Mathf.Max(element.resolvedStyle.height, 0f);
            var thickness = TableFeltLayout.BorderThickness(settings, viewHeight);
            var inset = TableFeltLayout.Inset(settings, viewHeight, Screen.height);
            var slice = felt.Border.border;
            var slicePixels = Mathf.Max(slice.x, Mathf.Max(slice.y, Mathf.Max(slice.z, slice.w)));
            var pixelsPerUnit = Mathf.Max(felt.Border.pixelsPerUnit, 0.0001f);
            var sourceSliceSize = slicePixels * 100f / pixelsPerUnit;
            var sliceScale = sourceSliceSize > 0f ? thickness / sourceSliceSize : 1f;

            border.style.left = inset;
            border.style.right = inset;
            border.style.top = inset;
            border.style.bottom = inset;
            border.style.backgroundImage = new StyleBackground(felt.Border);
            border.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            border.style.unityBackgroundImageTintColor = settings.Tint;
            border.style.unitySliceLeft = new StyleInt(Mathf.RoundToInt(slice.x));
            border.style.unitySliceRight = new StyleInt(Mathf.RoundToInt(slice.z));
            border.style.unitySliceTop = new StyleInt(Mathf.RoundToInt(slice.w));
            border.style.unitySliceBottom = new StyleInt(Mathf.RoundToInt(slice.y));
            border.style.unitySliceScale = new StyleFloat(sliceScale);
        }

        private static void RemoveBorder(VisualElement element)
        {
            element.Q<VisualElement>("felt-border")?.RemoveFromHierarchy();
        }
    }
}