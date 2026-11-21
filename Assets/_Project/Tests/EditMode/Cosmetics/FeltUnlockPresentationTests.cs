using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.Cosmetics.Tests
{
    public sealed class FeltUnlockPresentationTests
    {
        private Texture2D texture;
        private Sprite tileSprite;
        private TableFelt firstFelt;
        private TableFelt secondFelt;
        private VisualElement root;
        private FeltUnlockPresentation presentation;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            tileSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            firstFelt = CreateFelt("firstFelt", "First Felt");
            secondFelt = CreateFelt("secondFelt", "Second Felt");
            root = new VisualElement();
            presentation = new FeltUnlockPresentation(root);
        }

        [TearDown]
        public void TearDown()
        {
            presentation?.Cancel();
            UnityEngine.Object.DestroyImmediate(firstFelt);
            UnityEngine.Object.DestroyImmediate(secondFelt);
            UnityEngine.Object.DestroyImmediate(tileSprite);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test]
        public void PresentationShowsNameAndSwatchThenAddsGlowAtFullDuration()
        {
            presentation.Play(new[] { new FeltUnlockPresentationItem(firstFelt, "First Felt") }, null);

            Assert.That(root.Q<Label>("felt-unlock-name").text, Is.EqualTo("First Felt"));
            Assert.That(root.Q<VisualElement>("felt-unlock-swatch"), Is.Not.Null);
            Assert.That(presentation.IsRevealed, Is.False);
            Assert.That(root.Q<VisualElement>("felt-unlock-glow-outer").style.opacity.value, Is.EqualTo(0f));

            presentation.Evaluate(FeltUnlockPresentation.FullDuration);

            Assert.That(presentation.IsRevealed, Is.True);
            Assert.That(root.Q<VisualElement>("felt-unlock-glow-outer").style.opacity.value, Is.GreaterThan(0f));
        }

        [Test]
        public void FirstTapRevealsAndSecondTapFinishes()
        {
            var finished = false;
            presentation.Play(new[] { new FeltUnlockPresentationItem(firstFelt, "First Felt") }, () => finished = true);

            presentation.Advance();

            Assert.That(presentation.IsRevealed, Is.True);
            Assert.That(finished, Is.False);

            presentation.Advance();

            Assert.That(presentation.IsPlaying, Is.False);
            Assert.That(finished, Is.True);
        }

        [Test]
        public void MultipleFeltsAdvanceInSequence()
        {
            var finished = false;
            var items = new List<FeltUnlockPresentationItem>
            {
                new FeltUnlockPresentationItem(firstFelt, "First Felt"),
                new FeltUnlockPresentationItem(secondFelt, "Second Felt")
            };
            presentation.Play(items, () => finished = true);

            presentation.Advance();
            presentation.Advance();

            Assert.That(root.Q<Label>("felt-unlock-name").text, Is.EqualTo("Second Felt"));
            Assert.That(presentation.IsRevealed, Is.False);
            Assert.That(finished, Is.False);

            presentation.Advance();
            presentation.Advance();

            Assert.That(presentation.IsPlaying, Is.False);
            Assert.That(finished, Is.True);
        }

        private TableFelt CreateFelt(string id, string name)
        {
            var felt = ScriptableObject.CreateInstance<TableFelt>();
            felt.Configure(id, name, FeltRenderMode.Tiled, tileSprite, 1f, null, null, Color.white,
                Color.green, UnlockRequirement.Free);
            return felt;
        }
    }
}