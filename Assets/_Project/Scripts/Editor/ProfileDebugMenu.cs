using UnityEditor;
using UnityEngine;
using Mejnun.Game;

namespace Mejnun.Editor
{
    public static class ProfileDebugMenu
    {
        [MenuItem("Mejnun/Debug/Profile/Add 100 XP")]
        public static void AddXp()
        {
            ProgressionService.Instance.AddDebugXp(100);
        }

        [MenuItem("Mejnun/Debug/Profile/Add Win")]
        public static void AddWin()
        {
            ProgressionService.Instance.AddDebugWin();
        }

        [MenuItem("Mejnun/Debug/Profile/Unlock All")]
        public static void UnlockAll()
        {
            ProgressionService.Instance.GrantAllItems();
        }

        [MenuItem("Mejnun/Debug/Profile/Reset")]
        public static void ResetProfile()
        {
            if (EditorUtility.DisplayDialog("Reset Mejnun profile?",
                    "This clears local XP, stats, achievements, unlock grants and picks.", "Reset", "Cancel"))
            {
                ProgressionService.Instance.ResetProfile();
                Debug.Log("Reset the local Mejnun profile.");
            }
        }
    }
}