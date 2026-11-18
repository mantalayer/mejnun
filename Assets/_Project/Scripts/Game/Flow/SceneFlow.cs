using System;
using UnityEngine.SceneManagement;

namespace Mejnun.Game
{
    public static class SceneFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string TableScene = "Table";

        private static MenuDestination pendingDestination = MenuDestination.MainMenu;
        private static bool hasPendingDestination;

        public static MenuDestination PendingDestination => hasPendingDestination
            ? pendingDestination
            : MenuDestination.MainMenu;

        public static void LoadTable(MatchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            MatchLaunch.Set(request);
            SceneManager.LoadScene(TableScene);
        }

        public static void LoadMainMenu(MenuDestination destination = default)
        {
            pendingDestination = destination;
            hasPendingDestination = true;
            SceneManager.LoadScene(MainMenuScene);
        }

        public static bool TryTakeDestination(out MenuDestination destination)
        {
            if (!hasPendingDestination)
            {
                destination = MenuDestination.MainMenu;
                return false;
            }

            destination = pendingDestination;
            pendingDestination = MenuDestination.MainMenu;
            hasPendingDestination = false;
            return true;
        }

        public static void ClearPendingState()
        {
            hasPendingDestination = false;
            pendingDestination = MenuDestination.MainMenu;
            MatchLaunch.Clear();
        }
    }
}