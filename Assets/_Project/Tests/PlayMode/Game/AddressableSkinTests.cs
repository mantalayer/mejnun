using System.Collections;
using System.Text.RegularExpressions;
using Mejnun.Cosmetics;
using Mejnun.Core;
using Mejnun.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mejnun.Game.Tests
{
    public sealed class AddressableSkinTests
    {
        [UnityTest]
        public IEnumerator PixelPastelLoadsFromOptionalGroupAndBecomesAvailable()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var tableView = Object.FindAnyObjectByType<TableView>();
            var loader = Object.FindAnyObjectByType<DeckSkinLoader>();
            Assert.That(tableView, Is.Not.Null);
            Assert.That(loader, Is.Not.Null);

            var completed = false;
            DeckSkin loadedSkin = null;
            loader.Load("pixelPastel", skin =>
            {
                loadedSkin = skin;
                completed = true;
            });
            for (var frame = 0; frame < 300 && !completed; frame++)
            {
                yield return null;
            }

            Assert.That(completed, Is.True);
            Assert.That(loadedSkin, Is.Not.Null);
            Assert.That(loadedSkin.Id, Is.EqualTo("pixelPastel"));
            Assert.That(tableView.SkinCatalog.IsAddressableSkinLoaded("pixelPastel"), Is.True);
            CollectionAssert.Contains(tableView.SkinCatalog.GetAvailableSkinIds(), "pixelPastel");
        }

        [UnityTest]
        public IEnumerator FailedAddressableLoadFallsBackToBaseFace()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var tableView = Object.FindAnyObjectByType<TableView>();
            var failingCatalog = ScriptableObject.CreateInstance<DeckSkinCatalog>();
            failingCatalog.Configure(tableView.SkinCatalog.Skins);
            failingCatalog.ConfigureAddressableSkins(new[]
            {
                new AddressableDeckSkinEntry("unavailable", "Unavailable", UnlockRequirement.Free,
                    new AssetReferenceT<DeckSkin>("ffffffffffffffffffffffffffffffff"))
            });
            var loaderObject = new GameObject("Failed Addressable Skin Loader Test");
            var loader = loaderObject.AddComponent<DeckSkinLoader>();
            loader.Configure(failingCatalog);

            // Localization preloads its String Tables through Addressables, so
            // whether this load is chained behind Addressables initialization
            // (which adds a second "ChainOperation failed" error) depends on
            // test order. Finish initialization first so only the load fails.
            yield return Addressables.InitializeAsync();
            LogAssert.Expect(LogType.Error, new Regex("InvalidKeyException"));
            LogAssert.Expect(LogType.Warning, new Regex("Addressable deck skin could not be loaded: unavailable"));
            var completed = false;
            Assert.That(loader.Preload(new[] { "unavailable" }, () => completed = true), Is.True);
            for (var frame = 0; frame < 300 && !completed; frame++)
            {
                yield return null;
            }

            Assert.That(completed, Is.True);
            CollectionAssert.DoesNotContain(failingCatalog.GetAvailableSkinIds(), "unavailable");
            Assert.That(failingCatalog.Resolve("unavailable", 0),
                Is.SameAs(failingCatalog.Resolve(DeckFaceAssigner.ClassicFace, 0)));
            Object.Destroy(loaderObject);
            Object.Destroy(failingCatalog);
            yield return null;
        }
    }
}