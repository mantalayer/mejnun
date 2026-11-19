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
            camera.orthographicSize = 6f;
            camera.transform.position = new Vector3(2.4f, -1.2f, -10f);

            feltView.Apply("green");
            feltView.RefreshLayout();
            Assert.That(tile.enabled, Is.True);
            Assert.That(border.enabled, Is.False);
            Assert.That(image.enabled, Is.False);
            Assert.That(tile.sortingOrder, Is.LessThan(0));
            Assert.That(tile.bounds.min.x,
                Is.EqualTo(camera.transform.position.x - camera.orthographicSize * camera.aspect).Within(0.001f));
            Assert.That(tile.bounds.max.y,
                Is.EqualTo(camera.transform.position.y + camera.orthographicSize).Within(0.001f));
            Assert.That(tile.size.x * tile.transform.lossyScale.x,
                Is.GreaterThan(2f * camera.orthographicSize * camera.aspect));
            Assert.That(tile.size.y * tile.transform.lossyScale.y,
                Is.GreaterThan(2f * camera.orthographicSize));

            var originalTileScale = tile.transform.localScale.x;
            camera.transform.position = new Vector3(2.6f, -1.3f, -10f);
            camera.orthographicSize = 8f;
            feltView.RefreshLayout();
            Assert.That(tile.bounds.min.x,
                Is.EqualTo(camera.transform.position.x - camera.orthographicSize * camera.aspect).Within(0.001f));
            Assert.That(tile.bounds.max.y,
                Is.EqualTo(camera.transform.position.y + camera.orthographicSize).Within(0.001f));
            Assert.That(tile.transform.localScale.x, Is.EqualTo(originalTileScale * 8f / 6f).Within(0.001f));

            camera.transform.position = new Vector3(-3.1f, 4.2f, -10f);
            camera.aspect = 0.8f;
            camera.orthographicSize = 4f;
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
            feltView.Apply("test-image");
            feltView.RefreshLayout();

            var viewportWidth = camera.orthographicSize * 2f * camera.aspect;
            var viewportHeight = camera.orthographicSize * 2f;
            Assert.That(tile.enabled, Is.False);
            Assert.That(image.enabled, Is.True);
            Assert.That(image.bounds.size.x, Is.GreaterThanOrEqualTo(viewportWidth));
            Assert.That(image.bounds.size.y, Is.GreaterThanOrEqualTo(viewportHeight));
            Assert.That(image.bounds.center.x, Is.EqualTo(camera.transform.position.x).Within(0.001f));
            Assert.That(image.bounds.center.y, Is.EqualTo(camera.transform.position.y).Within(0.001f));

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