using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.Game
{
    internal static class UiPointerBlocker
    {
        public static bool IsOverUi(Vector2 screenPosition)
        {
            var documents = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (var index = 0; index < documents.Length; index++)
            {
                var document = documents[index];
                var root = document.rootVisualElement;
                var panel = root == null ? null : root.panel;
                if (panel == null)
                {
                    continue;
                }

                var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);
                var pickedElement = panel.Pick(panelPosition);
                if (pickedElement != null && pickedElement != root && pickedElement != panel.visualTree)
                {
                    return true;
                }
            }

            return false;
        }
    }
}