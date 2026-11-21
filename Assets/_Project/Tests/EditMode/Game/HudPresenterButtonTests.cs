using Mejnun.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Mejnun.Game.EditMode.Tests
{
    public sealed class HudPresenterButtonTests
    {
        [Test]
        public void EnabledButtonAncestorsAreRecognizedButDisabledButtonsAreNot()
        {
            var root = new VisualElement();
            var button = new Button();
            var child = new VisualElement();
            root.Add(button);
            button.Add(child);

            Assert.That(HudPresenter.IsInsideEnabledButton(child), Is.True);

            button.SetEnabled(false);
            Assert.That(HudPresenter.IsInsideEnabledButton(child), Is.False);

            button.SetEnabled(true);
            root.SetEnabled(false);
            Assert.That(HudPresenter.IsInsideEnabledButton(child), Is.False);
            Assert.That(HudPresenter.IsInsideEnabledButton(new VisualElement()), Is.False);
        }
    }
}