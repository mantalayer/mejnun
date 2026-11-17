using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    public sealed class TableDemoBootstrap : MonoBehaviour
    {
        private static readonly string[] FacePool = { "classic", "classic_ivory" };

        [SerializeField] private TableView tableView;
        [SerializeField] private TableController tableController;
        [SerializeField] private int seatCount = 3;
        [SerializeField] private int dealer = 0;
        [SerializeField] private long seed = 202601;

        public MejnunGame Game { get; private set; }

        private void Start()
        {
            if (tableView == null)
            {
                tableView = FindAnyObjectByType<TableView>();
            }

            if (tableController == null)
            {
                tableController = FindAnyObjectByType<TableController>();
            }

            Game = new MejnunGame(new GameSetup(seatCount, unchecked((ulong)seed), dealer,
                facePool: FacePool));
            if (tableController != null)
            {
                tableController.Initialize(Game);
            }
            else if (tableView != null)
            {
                tableView.Sync(Game.State, Game.Turn);
            }
        }
    }
}