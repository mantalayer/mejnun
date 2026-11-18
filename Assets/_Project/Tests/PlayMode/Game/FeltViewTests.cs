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
            Assert.That(tile.transform.position.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(tile.transform.position.y, Is.EqualTo(-2f).Within(0.001f));
            Assert.That(tile.size.x * tile.transform.lossyScale.x,
                Is.GreaterThan(2f * camera.orthographicSize * camera.aspect));
            Assert.That(tile.size.y * tile.transform.lossyScale.y,
                Is.GreaterThan(2f * camera.orthographicSize));

            var tileOrigin = tile.transform.position;
            camera.transform.position = new Vector3(2.6f, -1.3f, -10f);
            camera.orthographicSize = 8f;
            feltView.RefreshLayout();
            Assert.That(tile.transform.position.x, Is.EqualTo(tileOrigin.x).Within(0.001f));
            Assert.That(tile.transform.position.y, Is.EqualTo(tileOrigin.y).Within(0.001f));

            camera.transform.position = new Vector3(-3.1f, 4.2f, -10f);
            camera.aspect = 0.8f;
            camera.orthographicSize = 4f;
            feltView.Apply("burgundy");
            feltView.RefreshLayout();
            Assert.That(tile.enabled, Is.True);
            Assert.That(border.enabled, Is.True);
            Assert.That(image.enabled, Is.False);
            Assert.That(border.sortingOrder, Is.LessThan(0));
            Assert.That(border.transform.position.x, Is.EqualTo(camera.transform.position.x).Within(0.001f));
            var thicknessAtZoom = BorderThicknessPixels(border, camera);
            Assert.That(border.size.x * border.transform.lossyScale.x,
                Is.LessThanOrEqualTo(2f * camera.orthographicSize * camera.aspect));
            Assert.That(border.size.y * border.transform.lossyScale.y,
                Is.LessThanOrEqualTo(2f * camera.orthographicSize));

            camera.orthographicSize = 9f;
            feltView.RefreshLayout();
            Assert.That(BorderThicknessPixels(border, camera), Is.EqualTo(thicknessAtZoom).Within(0.05f));

            feltView.Apply("midnight");
            feltView.RefreshLayout();
            Assert.That(tile.enabled, Is.False);
            Assert.That(border.enabled, Is.False);
            Assert.That(image.enabled, Is.True);
            Assert.That(image.bounds.size.x, Is.GreaterThanOrEqualTo(2f * camera.orthographicSize * camera.aspect));
            Assert.That(image.bounds.size.y, Is.GreaterThanOrEqualTo(2f * camera.orthographicSize));
            Assert.That(camera.backgroundColor, Is.EqualTo(feltView.ActiveFelt.ClearColor));
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

        private static float BorderThicknessPixels(SpriteRenderer border, Camera camera)
        {
            var slice = border.sprite.border;
            var slicePixels = Mathf.Max(slice.x, Mathf.Max(slice.y, Mathf.Max(slice.z, slice.w)));
            var worldThickness = slicePixels / border.sprite.pixelsPerUnit * border.transform.lossyScale.y;
            return worldThickness / (2f * camera.orthographicSize / camera.pixelHeight);
        }
    }
}