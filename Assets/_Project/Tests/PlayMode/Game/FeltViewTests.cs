using System.Collections;
using System.Linq;
using Mejnun.Cosmetics;
using Mejnun.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mejnun.Game.Tests
{
    public sealed class FeltViewTests
    {
        [UnityTest]
        public IEnumerator FeltModesCoverViewportAndFollowCameraLayout()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var feltView = Object.FindAnyObjectByType<FeltView>();
            var tableCamera = Object.FindAnyObjectByType<TableCamera>();
            Assert.That(feltView, Is.Not.Null);
            Assert.That(tableCamera, Is.Not.Null);

            var renderers = feltView.GetComponentsInChildren<SpriteRenderer>(true);
            var tile = renderers.Single(renderer => renderer.name == "Tiled Felt");
            var border = renderers.Single(renderer => renderer.name == "Felt Border");
            var image = renderers.Single(renderer => renderer.name == "Felt Image");
            var camera = tableCamera.Camera;
            camera.aspect = 1.3f;
            var anchorPosition = tableCamera.FeltAnchorPosition;
            var anchorWorldHeight = tableCamera.FeltAnchorSize * 2f;
            var anchorWorldWidth = anchorWorldHeight * camera.aspect;

            feltView.Apply("green");
            feltView.RefreshLayout();
            Assert.That(camera.transform.position, Is.EqualTo(anchorPosition));
            Assert.That(camera.orthographicSize, Is.EqualTo(tableCamera.FeltAnchorSize));
            Assert.That(tile.enabled, Is.True);
            Assert.That(border.enabled, Is.False);
            Assert.That(image.enabled, Is.False);
            Assert.That(tile.sortingOrder, Is.LessThan(0));
            Assert.That(tile.bounds.min.x,
                Is.EqualTo(anchorPosition.x - anchorWorldWidth * 0.5f).Within(0.001f));
            Assert.That(tile.bounds.max.y,
                Is.EqualTo(anchorPosition.y + anchorWorldHeight * 0.5f).Within(0.001f));
            Assert.That(tile.size.x * tile.transform.lossyScale.x,
                Is.GreaterThan(2f * tableCamera.FeltAnchorSize * camera.aspect));
            Assert.That(tile.size.y * tile.transform.lossyScale.y,
                Is.GreaterThan(2f * tableCamera.FeltAnchorSize));

            var originalTileScale = tile.transform.localScale.x;
            tableCamera.Pan(new Vector2(80f, 35f), new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            tableCamera.Zoom(1.25f, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            feltView.RefreshLayout();
            Assert.That(tableCamera.FeltAnchorPosition, Is.EqualTo(anchorPosition));
            Assert.That(tableCamera.FeltAnchorSize, Is.EqualTo(anchorWorldHeight * 0.5f));
            Assert.That(tile.transform.localScale.x, Is.EqualTo(originalTileScale).Within(0.001f));
            Assert.That(tile.bounds.min.x,
                Is.LessThanOrEqualTo(camera.transform.position.x - camera.orthographicSize * camera.aspect));
            Assert.That(tile.bounds.max.x,
                Is.GreaterThanOrEqualTo(camera.transform.position.x + camera.orthographicSize * camera.aspect));
            Assert.That(tile.bounds.min.y,
                Is.LessThanOrEqualTo(camera.transform.position.y - camera.orthographicSize));
            Assert.That(tile.bounds.max.y,
                Is.GreaterThanOrEqualTo(camera.transform.position.y + camera.orthographicSize));
            var tileWidth = tile.transform.lossyScale.x * tile.sprite.bounds.size.x;
            var tilePhase = (tile.bounds.min.x - (anchorPosition.x - anchorWorldWidth * 0.5f)) / tileWidth;
            Assert.That(tilePhase, Is.EqualTo(Mathf.Round(tilePhase)).Within(0.001f));

            camera.transform.position = anchorPosition;
            camera.orthographicSize = tableCamera.FeltAnchorSize;
            feltView.Apply("burgundy");
            feltView.RefreshLayout();
            Assert.That(feltView.ActiveFelt.Id, Is.EqualTo("burgundy"));
            Assert.That(tile.enabled, Is.True);
            Assert.That(border.enabled, Is.False);
            Assert.That(image.enabled, Is.False);
            Assert.That(tile.size.x * tile.transform.lossyScale.x,
                Is.GreaterThan(2f * camera.orthographicSize * camera.aspect));
            Assert.That(tile.size.y * tile.transform.lossyScale.y,
                Is.GreaterThan(2f * camera.orthographicSize));

            feltView.Apply("blue");
            feltView.RefreshLayout();
            Assert.That(feltView.ActiveFelt.Id, Is.EqualTo("blue"));
            Assert.That(tile.enabled, Is.True);
            Assert.That(border.enabled, Is.False);
            Assert.That(image.enabled, Is.False);
            Assert.That(camera.backgroundColor, Is.EqualTo(feltView.ActiveFelt.ClearColor));

            var texture = new Texture2D(256, 128);
            var imageSprite = Sprite.Create(texture, new Rect(0f, 0f, 256f, 128f), new Vector2(0.5f, 0.5f),
                100f);
            var imageFelt = ScriptableObject.CreateInstance<TableFelt>();
            imageFelt.Configure("test-image", "Test image", FeltRenderMode.Image, null, 1f, null, imageSprite,
                Color.white, Color.black, null);
            var imageCatalog = ScriptableObject.CreateInstance<TableFeltCatalog>();
            imageCatalog.Configure(new[] { imageFelt });
            feltView.Configure(tableCamera, imageCatalog, tile, border, image);
            camera.transform.position = tableCamera.FeltAnchorPosition;
            camera.orthographicSize = tableCamera.FeltAnchorSize;
            feltView.Apply("test-image");
            feltView.RefreshLayout();

            var viewportWidth = tableCamera.FeltAnchorSize * 2f * camera.aspect;
            var viewportHeight = tableCamera.FeltAnchorSize * 2f;
            Assert.That(tile.enabled, Is.False);
            Assert.That(image.enabled, Is.True);
            Assert.That(image.bounds.size.x, Is.GreaterThanOrEqualTo(viewportWidth));
            Assert.That(image.bounds.size.y, Is.GreaterThanOrEqualTo(viewportHeight));
            Assert.That(image.bounds.center.x, Is.EqualTo(tableCamera.FeltAnchorPosition.x).Within(0.001f));
            Assert.That(image.bounds.center.y, Is.EqualTo(tableCamera.FeltAnchorPosition.y).Within(0.001f));

            Object.Destroy(imageCatalog);
            Object.Destroy(imageFelt);
            Object.Destroy(imageSprite);
            Object.Destroy(texture);
        }

        [UnityTest]
        public IEnumerator RequestedFeltRemainsVisibleDuringDealerSelection()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            var feltView = Object.FindAnyObjectByType<FeltView>();
            Assert.That(matchFlow, Is.Not.Null);
            Assert.That(feltView, Is.Not.Null);
            var request = new MatchRequest(new[] { SeatConfig.Human("Sam"), SeatConfig.Human("Lee") },
                mode: MatchMode.PassAndPlay, feltPool: new[] { "burgundy" }, seed: 777UL);

            Assert.That(matchFlow.StartMatch(request), Is.True);
            Assert.That(feltView.ActiveFelt.Id, Is.EqualTo("burgundy"));

            yield return null;

            Assert.That(feltView.ActiveFelt.Id, Is.EqualTo("burgundy"));
        }
    }
}