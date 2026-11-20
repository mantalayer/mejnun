using Mejnun.Localization;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class UiDirection
    {
        private readonly VisualElement root;

        private UiDirection(VisualElement root)
        {
            this.root = root;
            Loc.LanguageChanged += UpdateDirection;
            UpdateDirection();
        }

        public static UiDirection Attach(VisualElement root)
        {
            return root == null ? null : new UiDirection(root);
        }

        public void Dispose()
        {
            Loc.LanguageChanged -= UpdateDirection;
            if (root != null)
            {
                root.RemoveFromClassList("rtl");
                root.languageDirection = LanguageDirection.LTR;
            }
        }

        private void UpdateDirection()
        {
            if (root == null)
                return;

            bool rightToLeft = Loc.IsRightToLeft;
            root.EnableInClassList("rtl", rightToLeft);
            root.languageDirection = rightToLeft ? LanguageDirection.RTL : LanguageDirection.LTR;
        }
    }
}