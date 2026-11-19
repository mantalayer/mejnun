using System;
using Mejnun.Cosmetics;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public sealed class FeltWipe
    {
        private const float Duration = 0.7f;

        private readonly VisualElement host;
        private VisualElement overlay;
        private VisualElement mask;
        private VisualElement fill;
        private TableFelt felt;
        private Action completed;
        private IVisualElementScheduledItem scheduled;
        private float startTime;
        private float width;
        private float height;
        private bool wipeVertically;

        public FeltWipe(VisualElement host)
        {
            this.host = host;
        }

        public void Play(TableFelt nextFelt, Action onCompleted = null)
        {
            Finish();
            felt = nextFelt;
            completed = onCompleted;
            if (host == null || host.panel == null || host.resolvedStyle.width <= 0f ||
                host.resolvedStyle.height <= 0f)
            {
                Finish();
                return;
            }

            width = host.resolvedStyle.width;
            height = host.resolvedStyle.height;
            wipeVertically = DeviceProfile.IsPhoneLayout;
            overlay = new VisualElement { pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0f;
            overlay.style.right = 0f;
            overlay.style.top = 0f;
            overlay.style.bottom = 0f;
            mask = new VisualElement { pickingMode = PickingMode.Ignore };
            mask.style.position = Position.Absolute;
            mask.style.left = 0f;
            mask.style.top = 0f;
            mask.style.overflow = Overflow.Hidden;
            fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.style.position = Position.Absolute;
            fill.style.width = width;
            fill.style.height = height;
            fill.style.left = 0f;
            fill.style.top = 0f;
            MenuFeltBackground.Apply(fill, felt);
            mask.Add(fill);
            overlay.Add(mask);
            host.Insert(MenuFeltBackground.ContentInsertIndex(host), overlay);

            startTime = Time.unscaledTime;
            ApplyProgress(0f);
            scheduled = host.schedule.Execute(Update).Every(0);
        }

        public void Cancel()
        {
            scheduled?.Pause();
            scheduled = null;
            RemoveOverlay();
            felt = null;
            completed = null;
        }

        private void Update()
        {
            if (overlay == null || host == null || host.panel == null)
            {
                Finish();
                return;
            }

            var progress = Mathf.Clamp01((Time.unscaledTime - startTime) / Duration);
            ApplyProgress(progress * progress * (3f - 2f * progress));
            if (progress >= 1f)
            {
                Finish();
            }
        }

        private void ApplyProgress(float progress)
        {
            mask.style.width = wipeVertically ? width : width * progress;
            mask.style.height = wipeVertically ? height * progress : height;
        }

        private void Finish()
        {
            if (overlay == null && felt == null && completed == null)
            {
                return;
            }

            scheduled?.Pause();
            scheduled = null;
            var finalFelt = felt;
            var callback = completed;
            RemoveOverlay();
            felt = null;
            completed = null;
            MenuFeltBackground.Apply(host, finalFelt);
            callback?.Invoke();
        }

        private void RemoveOverlay()
        {
            overlay?.RemoveFromHierarchy();
            overlay = null;
            mask = null;
            fill = null;
        }
    }
}