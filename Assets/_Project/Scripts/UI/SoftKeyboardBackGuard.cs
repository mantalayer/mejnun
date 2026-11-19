using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class SoftKeyboardBackGuard
    {
        public const float DismissGraceSeconds = 0.35f;

        private float lastTextInputActiveTime = float.NegativeInfinity;

        public void Track(VisualElement focusedElement, bool touchScreenKeyboardVisible, float currentTime)
        {
            if (touchScreenKeyboardVisible || HasTextFieldAncestor(focusedElement))
            {
                lastTextInputActiveTime = currentTime;
            }
        }

        public bool ShouldSuppressBack(float currentTime)
        {
            return currentTime - lastTextInputActiveTime <= DismissGraceSeconds;
        }

        private static bool HasTextFieldAncestor(VisualElement element)
        {
            while (element != null)
            {
                if (element is TextField)
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }
    }
}