using System;

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

            if (SceneFader.IsTransitioning)
            {
                return;
            }

            MatchLaunch.Set(request);
            SceneFader.LoadScene(TableScene);
        }

        public static void LoadMainMenu(MenuDestination destination = default)
        {
            if (SceneFader.IsTransitioning)
            {
                return;
            }

            pendingDestination = destination;
            hasPendingDestination = true;
            SceneFader.LoadScene(MainMenuScene);
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