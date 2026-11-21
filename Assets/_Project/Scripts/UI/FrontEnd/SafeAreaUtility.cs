using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public static class SafeAreaUtility
    {
        public static void Apply(VisualElement root, Rect safeArea, int screenWidth, int screenHeight,
            float cornerMargin = 0f, float bottomInset = 0f)
        {
            if (root == null)
            {
                return;
            }

            var scaleX = 1f;
            var scaleY = 1f;
            var panelRect = root.panel?.visualTree.layout ?? Rect.zero;
            if (screenWidth > 0 && screenHeight > 0 && panelRect.width > 0f && panelRect.height > 0f &&
                !float.IsNaN(panelRect.width) && !float.IsNaN(panelRect.height))
            {
                scaleX = panelRect.width / screenWidth;
                scaleY = panelRect.height / screenHeight;
            }

            var left = Mathf.Max(0f, safeArea.xMin) * scaleX + cornerMargin;
            var right = Mathf.Max(0f, screenWidth - safeArea.xMax) * scaleX + cornerMargin;
            var bottom = Mathf.Max(Mathf.Max(0f, safeArea.yMin) * scaleY + cornerMargin, bottomInset);
            var top = Mathf.Max(0f, screenHeight - safeArea.yMax) * scaleY + cornerMargin;
            root.style.paddingLeft = left;
            root.style.paddingRight = right;
            root.style.paddingBottom = bottom;
            root.style.paddingTop = top;
            root.EnableInClassList("portrait-layout", screenHeight > screenWidth);
            root.EnableInClassList("landscape-layout", screenWidth >= screenHeight);
        }
    }
}