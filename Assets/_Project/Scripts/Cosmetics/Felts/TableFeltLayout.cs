using UnityEngine;

namespace Mejnun.Cosmetics
{
    public static class TableFeltLayout
    {
        public const float ReferenceViewHeight = 10f;

        public static Vector2 TileSize(TableFelt felt, float viewHeight)
        {
            if (felt == null || felt.Tile == null)
            {
                return Vector2.zero;
            }

            var width = felt.TileWorldSize * Mathf.Max(viewHeight, 0f) / ReferenceViewHeight;
            var aspect = felt.Tile.rect.width / Mathf.Max(felt.Tile.rect.height, 0.0001f);
            return new Vector2(width, width / Mathf.Max(aspect, 0.0001f));
        }

        public static float BorderThickness(FeltBorderSettings settings, float viewHeight)
        {
            if (settings == null)
            {
                return 16f * Mathf.Max(viewHeight, 0f) / 1080f;
            }

            return settings.ThicknessPixels * Mathf.Max(viewHeight, 0f) /
                   Mathf.Max(settings.ReferenceScreenHeight, 1f);
        }

        public static float Inset(FeltBorderSettings settings, float viewHeight, float screenPixelHeight)
        {
            return settings == null
                ? 0f
                : settings.InsetPixels * Mathf.Max(viewHeight, 0f) / Mathf.Max(screenPixelHeight, 1f);
        }
    }
}