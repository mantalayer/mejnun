using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public static class SoftKeyboardInset
    {
        private const float FallbackScreenFraction = 0.4f;

        public static float ToPanelUnits(bool visible, Rect area, int screenHeight, float panelHeight)
        {
            if (!visible || screenHeight <= 0 || panelHeight <= 0f || float.IsNaN(panelHeight) ||
                float.IsInfinity(panelHeight))
            {
                return 0f;
            }

            var keyboardHeight = area.height;
            if (keyboardHeight <= 0f || float.IsNaN(keyboardHeight) || float.IsInfinity(keyboardHeight))
            {
                keyboardHeight = screenHeight * FallbackScreenFraction;
            }

            return Mathf.Clamp(keyboardHeight / screenHeight * panelHeight, 0f, panelHeight);
        }

        public static TextField FindTextField(VisualElement element)
        {
            while (element != null)
            {
                if (element is TextField)
                {
                    return (TextField)element;
                }

                element = element.parent;
            }

            return null;
        }
    }
}