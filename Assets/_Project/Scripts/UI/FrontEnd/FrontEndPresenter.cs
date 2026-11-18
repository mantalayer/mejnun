using Mejnun.Cosmetics;
using Mejnun.Game;
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
            flow?.Dispose();
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