using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public static class SafeAreaUtility
    {
        public static void Apply(VisualElement root, Rect safeArea, int screenWidth, int screenHeight)
        {
            if (root == null)
            {
                return;
            }

            var left = Mathf.Max(0f, safeArea.xMin);
            var right = Mathf.Max(0f, screenWidth - safeArea.xMax);
            var bottom = Mathf.Max(0f, safeArea.yMin);
            var top = Mathf.Max(0f, screenHeight - safeArea.yMax);
            root.style.paddingLeft = left;
            root.style.paddingRight = right;
            root.style.paddingBottom = bottom;
            root.style.paddingTop = top;
            root.EnableInClassList("portrait-layout", screenHeight > screenWidth);
            root.EnableInClassList("landscape-layout", screenWidth >= screenHeight);
        }
    }
}