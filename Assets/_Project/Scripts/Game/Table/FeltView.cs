using System;
using Mejnun.Cosmetics;
using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    public sealed class FeltView : MonoBehaviour
    {
        [SerializeField] private TableCamera tableCamera;
        [SerializeField] private TableFeltCatalog catalog;
        [SerializeField] private SpriteRenderer tiledRenderer;
        [SerializeField] private SpriteRenderer borderRenderer;
        [SerializeField] private SpriteRenderer imageRenderer;

        private TableFelt activeFelt;
        private string activeFeltId;

        public TableFelt ActiveFelt => activeFelt;

        public void Configure(TableCamera camera, TableFeltCatalog feltCatalog, SpriteRenderer tiled,
            SpriteRenderer border, SpriteRenderer image)
        {
            tableCamera = camera;
            catalog = feltCatalog;
            tiledRenderer = tiled;
            borderRenderer = border;
            imageRenderer = image;
        }

        public void Apply(string feltId)
        {
            if (string.Equals(activeFeltId, feltId, StringComparison.Ordinal))
            {
                return;
            }

            var felt = catalog == null ? null : catalog.Resolve(feltId);
            if (felt == null && catalog != null)
            {
                felt = catalog.Resolve(TableFeltAssigner.GreenFelt);
            }

            activeFelt = felt;
            activeFeltId = felt == null ? TableFeltAssigner.GreenFelt : felt.Id;
            if (tableCamera != null && tableCamera.Camera != null && felt != null)
            {
                tableCamera.Camera.backgroundColor = felt.ClearColor;
            }

            SetRenderer(tiledRenderer, felt != null && felt.Mode != FeltRenderMode.Image,
                felt == null ? null : felt.Tile, felt == null ? Color.white : felt.Tint);
            var borderSettings = felt == null ? null : felt.BorderSettings;
            SetRenderer(borderRenderer, felt != null && felt.Mode == FeltRenderMode.TiledWithBorder &&
                (borderSettings == null || borderSettings.Enabled), felt == null ? null : felt.Border,
                borderSettings == null ? Color.white : borderSettings.Tint);
            SetRenderer(imageRenderer, felt != null && felt.Mode == FeltRenderMode.Image,
                felt == null ? null : felt.Image, felt == null ? Color.white : felt.Tint);

            if (tiledRenderer != null)
            {
                tiledRenderer.drawMode = SpriteDrawMode.Tiled;
            }

            if (borderRenderer != null)
            {
                borderRenderer.drawMode = SpriteDrawMode.Sliced;
            }

            if (imageRenderer != null)
            {
                imageRenderer.drawMode = SpriteDrawMode.Simple;
            }

            RefreshLayout();
        }

        public void RefreshLayout()
        {
            if (tableCamera == null || tableCamera.Camera == null || activeFelt == null)
            {
                return;
            }

            var camera = tableCamera.Camera;
            var worldHeight = camera.orthographicSize * 2f;
            var worldWidth = worldHeight * camera.aspect;
            var cameraPosition = camera.transform.position;
            var viewportSize = new Vector2(worldWidth, worldHeight);
            var screenHeight = Mathf.Max(camera.pixelHeight, 1f);
            var worldUnitsPerPixel = worldHeight / screenHeight;

            if (tiledRenderer != null && tiledRenderer.enabled && tiledRenderer.sprite != null)
            {
                var spriteWidth = Mathf.Max(tiledRenderer.sprite.bounds.size.x, 0.0001f);
                var scale = activeFelt.TileWorldSize / spriteWidth;
                tiledRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                var spriteHeight = Mathf.Max(tiledRenderer.sprite.bounds.size.y, 0.0001f);
                var tileWidth = Mathf.Max(activeFelt.TileWorldSize, 0.0001f);
                var tileHeight = spriteHeight * scale;
                var tileCountX = Mathf.Max(2, Mathf.CeilToInt(worldWidth / tileWidth) + 2);
                var tileCountY = Mathf.Max(2, Mathf.CeilToInt(worldHeight / tileHeight) + 2);
                if ((tileCountX & 1) != 0)
                {
                    tileCountX++;
                }

                if ((tileCountY & 1) != 0)
                {
                    tileCountY++;
                }

                tiledRenderer.size = new Vector2(tileCountX * spriteWidth, tileCountY * spriteHeight);
                tiledRenderer.transform.SetPositionAndRotation(new Vector3(
                    Mathf.Floor(cameraPosition.x / tileWidth) * tileWidth,
                    Mathf.Floor(cameraPosition.y / tileHeight) * tileHeight,
                    0f), Quaternion.identity);
            }

            var fixedPosition = cameraPosition + camera.transform.forward * (camera.nearClipPlane + 0.1f);
            if (borderRenderer != null && borderRenderer.enabled && borderRenderer.sprite != null)
            {
                var settings = activeFelt.BorderSettings;
                var thicknessPixels = settings == null ? 16f : settings.ThicknessPixels;
                var referenceHeight = settings == null ? 1080f : Mathf.Max(settings.ReferenceScreenHeight, 1f);
                var insetPixels = settings == null ? 0f : settings.InsetPixels;
                var slice = borderRenderer.sprite.border;
                var slicePixels = Mathf.Max(slice.x, Mathf.Max(slice.y, Mathf.Max(slice.z, slice.w)));
                var pixelsPerUnit = Mathf.Max(borderRenderer.sprite.pixelsPerUnit, 0.0001f);
                var sourceSliceWorld = slicePixels > 0f
                    ? slicePixels / pixelsPerUnit
                    : Mathf.Max(borderRenderer.sprite.bounds.size.x * 0.1f, 0.0001f);
                var targetThicknessWorld = thicknessPixels * worldHeight / referenceHeight;
                var scale = targetThicknessWorld / sourceSliceWorld;
                var insetWorld = insetPixels * worldUnitsPerPixel;
                borderRenderer.transform.SetPositionAndRotation(fixedPosition, camera.transform.rotation);
                borderRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                borderRenderer.size = new Vector2(
                    Mathf.Max(0.01f, viewportSize.x - insetWorld * 2f) / scale,
                    Mathf.Max(0.01f, viewportSize.y - insetWorld * 2f) / scale);
            }

            if (imageRenderer != null && imageRenderer.enabled && imageRenderer.sprite != null)
            {
                var imageSize = imageRenderer.sprite.bounds.size;
                var maxHeight = tableCamera.MaxOrthographicSize * 2f;
                var maxWidth = maxHeight * camera.aspect;
                var scale = Mathf.Max((maxWidth + 2f) / Mathf.Max(imageSize.x, 0.0001f),
                    (maxHeight + 2f) / Mathf.Max(imageSize.y, 0.0001f));
                imageRenderer.transform.SetPositionAndRotation(new Vector3(0f, 0f, fixedPosition.z),
                    Quaternion.identity);
                imageRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void LateUpdate()
        {
            RefreshLayout();
        }

        private static void SetRenderer(SpriteRenderer renderer, bool visible, Sprite sprite, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.sprite = sprite;
            renderer.color = color;
            renderer.enabled = visible && sprite != null;
        }
    }
}