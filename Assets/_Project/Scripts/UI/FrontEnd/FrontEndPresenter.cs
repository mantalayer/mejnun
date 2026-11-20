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
        private UiDirection uiDirection;
        private Rect lastSafeArea;
        private int lastWidth;
        private int lastHeight;
        private bool destinationTransitionPending;
        private readonly SoftKeyboardBackGuard keyboardBackGuard = new SoftKeyboardBackGuard();

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
            uiDirection = UiDirection.Attach(root);
            UiScaleUtility.Apply(uiDocument);
            deckUnlockPlayer = new DeckUnlockPlayer(root, deckSkinLoader);
            progressionService = ProgressionService.Instance;
            progressionService.MatchRecorded += OnMatchRecorded;
            safeAreaRoot = root.Q<VisualElement>("safe-area");
            flow = new FrontEndFlow(root, cosmeticCatalog, deckSkinLoader);
            ApplySafeArea();
            if (SceneFlow.TryTakeDestination(out var destination))
            {
                flow.NavigateTo(destination, false);
                destinationTransitionPending = true;
                root.RegisterCallback<GeometryChangedEvent>(OnInitialDestinationLayout);
                if (root.worldBound.width > 0f && root.worldBound.height > 0f)
                {
                    root.schedule.Execute(EnterDestinationScreen).StartingIn(0);
                }
            }
        }

        private void OnInitialDestinationLayout(GeometryChangedEvent evt)
        {
            EnterDestinationScreen();
        }

        private void EnterDestinationScreen()
        {
            if (!destinationTransitionPending)
            {
                return;
            }

            var root = uiDocument.rootVisualElement;
            if (root.worldBound.width <= 0f || root.worldBound.height <= 0f)
            {
                return;
            }

            destinationTransitionPending = false;
            root.UnregisterCallback<GeometryChangedEvent>(OnInitialDestinationLayout);
            flow?.EnterCurrentScreen();
        }

        private void Update()
        {
            UiScaleUtility.Apply(uiDocument);
            var focusedElement = uiDocument == null || uiDocument.rootVisualElement.panel == null
                ? null
                : uiDocument.rootVisualElement.panel.focusController.focusedElement as VisualElement;
            keyboardBackGuard.Track(focusedElement, TouchScreenKeyboard.visible, Time.unscaledTime);

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame ||
                gamepad != null && gamepad.buttonEast.wasPressedThisFrame)
            {
                if (keyboardBackGuard.ShouldSuppressBack(Time.unscaledTime))
                {
                    focusedElement?.Blur();
                }
                else
                {
                    flow?.Back();
                }
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
            uiDirection?.Dispose();
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