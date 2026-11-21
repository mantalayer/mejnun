using System;
using Mejnun.Cosmetics;
using UnityEditor;
using UnityEngine;

namespace Mejnun.Editor
{
    [CustomPropertyDrawer(typeof(UnlockRequirement))]
    public sealed class UnlockRequirementDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var kind = property.FindPropertyRelative("kind");
            var hasDetail = kind.enumValueIndex == (int)UnlockKind.Level ||
                            kind.enumValueIndex == (int)UnlockKind.Wins ||
                            kind.enumValueIndex == (int)UnlockKind.Achievement;
            return hasDetail
                ? EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing
                : EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var line = position;
            line.height = EditorGUIUtility.singleLineHeight;
            var kind = property.FindPropertyRelative("kind");
            EditorGUI.PropertyField(line, kind, label);

            if (kind.enumValueIndex == (int)UnlockKind.Level || kind.enumValueIndex == (int)UnlockKind.Wins)
            {
                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.indentLevel++;
                var target = property.FindPropertyRelative("target");
                target.intValue = Mathf.Max(1, EditorGUI.IntField(line, "Target", target.intValue));
                EditorGUI.indentLevel--;
            }
            else if (kind.enumValueIndex == (int)UnlockKind.Achievement)
            {
                line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.indentLevel++;
                DrawAchievementPopup(line, property.FindPropertyRelative("achievementId"));
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private static void DrawAchievementPopup(Rect position, SerializedProperty achievementId)
        {
            var achievementIds = AchievementIds.All;
            var selectedIndex = Array.IndexOf(achievementIds, achievementId.stringValue);
            var options = new string[achievementIds.Length + 1];
            options[0] = string.IsNullOrEmpty(achievementId.stringValue)
                ? "Select achievement..."
                : "Unknown: " + achievementId.stringValue;
            Array.Copy(achievementIds, 0, options, 1, achievementIds.Length);

            var popupIndex = selectedIndex < 0 ? 0 : selectedIndex + 1;
            var nextIndex = EditorGUI.Popup(position, "Achievement", popupIndex, options);
            if (nextIndex != popupIndex)
            {
                achievementId.stringValue = nextIndex == 0 ? string.Empty : achievementIds[nextIndex - 1];
            }
        }
    }
}