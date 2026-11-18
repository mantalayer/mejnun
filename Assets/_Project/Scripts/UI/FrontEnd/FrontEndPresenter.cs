using Mejnun.Cosmetics;
using Mejnun.Game;
using Mejnun.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public sealed class FrontEndPresenter : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private CosmeticCatalog cosmeticCatalog;
        [SerializeField] private DeckSkinLoader deckSkinLoader;

        private FrontEndFlow flow;
        private ProgressionService progressionService;
        private DeckUnlockPlayer deckUnlockPlayer;
        private VisualElement safeAreaRoot;
        private Rect lastSafeArea;
        private int lastWidth;
        private int lastHeight;

        public FrontEndFlow Flow => flow;
        public CosmeticCatalog Catalog => cosmeticCatalog;

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (cosmeticCatalog == null)
            {
                cosmeticCatalog = Resources.Load<CosmeticCatalog>("CosmeticCatalog");
            }

            if (cosmeticCatalog != null)
            {
                ProgressionService.Initialize(cosmeticCatalog);
            }

            if (deckSkinLoader == null)
            {
                deckSkinLoader = GetComponent<DeckSkinLoader>();
            }

            if (deckSkinLoader != null && cosmeticCatalog != null)
            {
                deckSkinLoader.Configure(cosmeticCatalog.Decks);
            }

            var root = uiDocument.rootVisualElement;
            deckUnlockPlayer = new DeckUnlockPlayer(root, deckSkinLoader);
            progressionService = ProgressionService.Instance;
            progressionService.MatchRecorded += OnMatchRecorded;
            safeAreaRoot = root.Q<VisualElement>("safe-area");
            flow = new FrontEndFlow(root, cosmeticCatalog, deckSkinLoader);
            ApplySafeArea();
            if (SceneFlow.TryTakeDestination(out var destination))
            {
                flow.NavigateTo(destination);
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                flow?.Back();
            }
            else if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
            {
                flow?.Back();
            }

            if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafeArea)
            {
                ApplySafeArea();
            }
        }

        private void OnDestroy()
        {
            if (progressionService != null)
            {
                progressionService.MatchRecorded -= OnMatchRecorded;
            }

            deckUnlockPlayer?.Cancel();
            flow?.Dispose();
        }

        private void OnMatchRecorded(ProgressionReport report)
        {
            deckUnlockPlayer?.Play(report);
        }

        private void ApplySafeArea()
        {
            if (safeAreaRoot == null)
            {
                return;
            }

            SafeAreaUtility.Apply(safeAreaRoot, Screen.safeArea, Screen.width, Screen.height);
            lastSafeArea = Screen.safeArea;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
        }
    }
}