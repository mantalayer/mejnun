using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mejnun.Game
{
    public static class SceneFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string TableScene = "Table";

        private static MenuDestination pendingDestination = MenuDestination.MainMenu;
        private static bool hasPendingDestination;
        private static bool pendingTableIntro;
        private static bool exitingTable;
        private static readonly List<Action<Action>> TableExitHandlers = new List<Action<Action>>();

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
            exitingTable = false;
            pendingTableIntro = true;
            SceneFader.LoadScene(TableScene);
        }

        public static void RegisterTableExit(Action<Action> handler)
        {
            if (handler != null && !TableExitHandlers.Contains(handler))
            {
                TableExitHandlers.Add(handler);
            }
        }

        public static void UnregisterTableExit(Action<Action> handler)
        {
            if (handler != null)
            {
                TableExitHandlers.Remove(handler);
            }
        }

        public static void LoadMainMenu(MenuDestination destination = default)
        {
            if (SceneFader.IsTransitioning || exitingTable)
            {
                return;
            }

            pendingDestination = destination;
            hasPendingDestination = true;
            pendingTableIntro = false;
            if (!Application.isPlaying || TableExitHandlers.Count == 0)
            {
                SceneFader.LoadScene(MainMenuScene);
                return;
            }

            exitingTable = true;
            var handlers = TableExitHandlers.ToArray();
            var remaining = handlers.Length;
            for (var index = 0; index < handlers.Length; index++)
            {
                var handlerCompleted = false;
                Action completed = () =>
                {
                    if (handlerCompleted)
                    {
                        return;
                    }

                    handlerCompleted = true;
                    remaining--;
                    if (remaining == 0 && exitingTable)
                    {
                        exitingTable = false;
                        SceneFader.LoadScene(MainMenuScene);
                    }
                };

                try
                {
                    handlers[index](completed);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    completed();
                }
            }
        }

        public static bool TryTakeTableIntro()
        {
            if (!pendingTableIntro)
            {
                return false;
            }

            pendingTableIntro = false;
            return true;
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
            pendingTableIntro = false;
            exitingTable = false;
            MatchLaunch.Clear();
        }
    }
}