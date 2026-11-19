using UnityEditor;
using UnityEngine;
using Mejnun.Game;

namespace Mejnun.Editor
{
    public static class ProfileDebugMenu
    {
        [MenuItem("Mejnun/Debug/Force Mobile Layout")]
        public static void ToggleForceMobileLayout()
        {
            DeviceProfile.SetForceMobile(!DeviceProfile.ForceMobile);
        }

        [MenuItem("Mejnun/Debug/Force Mobile Layout", true)]
        public static bool ValidateForceMobileLayout()
        {
            Menu.SetChecked("Mejnun/Debug/Force Mobile Layout", DeviceProfile.ForceMobile);
            return true;
        }

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

        [MenuItem("Mejnun/Debug/Match/Win Current Game")]
        public static void WinCurrentGame()
        {
            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            if (matchFlow != null)
            {
                matchFlow.DebugWinCurrentGame();
            }
        }

        [MenuItem("Mejnun/Debug/Match/Win Current Game", true)]
        public static bool CanWinCurrentGame()
        {
            return EditorApplication.isPlaying && Object.FindAnyObjectByType<MatchFlow>() != null;
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