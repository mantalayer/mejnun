using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    public sealed class TableDemoBootstrap : MonoBehaviour
    {
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

            var facePool = tableView == null || tableView.SkinCatalog == null
                ? new[] { DeckFaceAssigner.ClassicFace, DeckFaceAssigner.ClassicIvoryFace }
                : tableView.SkinCatalog.GetSkinIds();
            Game = new MejnunGame(new GameSetup(seatCount, unchecked((ulong)seed), dealer,
                facePool: facePool));
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