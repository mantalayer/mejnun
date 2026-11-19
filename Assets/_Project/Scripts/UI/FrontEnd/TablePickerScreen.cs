using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Core;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public sealed class TablePickerScreen
    {
        private readonly VisualElement root;
        private readonly VisualElement popup;
        private readonly VisualElement popupHost;
        private readonly VisualElement popupTail;
        private readonly CosmeticCatalog catalog;
        private readonly ProgressionService progression;
        private readonly CosmeticPreviews previews;
        private readonly Button[] deckPreviewButtons = new Button[2];
        private readonly string[] deckPicks = new string[2];
        private readonly Button feltPreviewButton;
        private readonly Label popupTitle;
        private readonly Label lockedNotice;
        private readonly ScrollView deckGrid;
        private readonly ScrollView feltGrid;
        private Button popupAnchor;
        private int popupDeckSlot;
        private bool popupShowsFelt;
        private bool popupOpen;
        private MatchRequest setupRequest;
        private string feltPick;

        private enum PopupTailEdge
        {
            Left,
            Right,
            Top,
            Bottom
        }

        public TablePickerScreen(VisualElement documentRoot, CosmeticCatalog cosmeticCatalog,
            ProgressionService progressionService, Action<string> showGallery, DeckSkinLoader loader = null,
            string rootName = "setup-screen", string namePrefix = "")
        {
            root = documentRoot.Q<VisualElement>(rootName);
            popup = documentRoot.Q<VisualElement>("cosmetic-popup");
            popupHost = popup.parent ?? documentRoot;
            popupTail = popup.Q<VisualElement>("cosmetic-popup-tail");
            catalog = cosmeticCatalog;
            progression = progressionService ?? throw new ArgumentNullException(nameof(progressionService));
            previews = new CosmeticPreviews(loader);
            deckPreviewButtons[0] = root.Q<Button>(namePrefix + "deck-preview-0");
            deckPreviewButtons[1] = root.Q<Button>(namePrefix + "deck-preview-1");
            feltPreviewButton = root.Q<Button>(namePrefix + "felt-preview");
            popupTitle = popup.Q<Label>("cosmetic-popup-title");
            lockedNotice = popup.Q<Label>("cosmetic-locked-notice");
            deckGrid = popup.Q<ScrollView>("deck-selector-grid");
            feltGrid = popup.Q<ScrollView>("felt-selector-grid");
            ConfigureGrid(deckGrid);
            ConfigureGrid(feltGrid);
            deckPreviewButtons[0].clicked += () => OpenDeckPopup(0);
            deckPreviewButtons[1].clicked += () => OpenDeckPopup(1);
            feltPreviewButton.clicked += OpenFeltPopup;
            popup.Q<Button>("cosmetic-popup-close").clicked += ClosePopup;
            popupHost.RegisterCallback<PointerDownEvent>(OnPopupHostPointerDown);
            popup.style.display = DisplayStyle.None;
            RestoreLastPicks();
            Refresh();
        }

        public string GetDeckPick(int slot)
        {
            ValidateSlot(slot);
            return deckPicks[slot];
        }

        public string FeltPick => feltPick;
        public bool CanStart => HasDistinctPicks();
        public bool IsPopupOpen => popupOpen;
        public event Action<string> FeltPickChanged;

        public void ReloadPicks()
        {
            RestoreLastPicks();
            Refresh();
        }

        public void SetSetupRequest(MatchRequest request)
        {
            setupRequest = request ?? throw new ArgumentNullException(nameof(request));
            RestoreLastPicks();
            Refresh();
        }

        public void Refresh()
        {
            for (var slot = 0; slot < deckPreviewButtons.Length; slot++)
            {
                RenderDeckPreview(slot);
            }

            RenderFeltPreview();
        }

        private void RestoreLastPicks()
        {
            var evaluator = progression.CreateUnlockEvaluator();
            var remembered = progression.Profile.LastDeckPicks;
            for (var slot = 0; slot < deckPicks.Length; slot++)
            {
                var id = remembered[slot];
                var skin = FindSkin(id);
                deckPicks[slot] = skin.HasValue && evaluator.IsOwned(id, skin.Value.Unlock) ? id : null;
            }

            if (deckPicks[0] != null && deckPicks[1] != null &&
                string.Equals(deckPicks[0], deckPicks[1], StringComparison.Ordinal))
            {
                deckPicks[1] = null;
            }

            var rememberedFelt = progression.Profile.LastFeltPick;
            var felt = FindFelt(rememberedFelt);
            feltPick = felt != null && evaluator.IsOwned(felt.Id, felt.Unlock) ? felt.Id : null;
            RememberPicks();
        }

        private void RenderDeckPreview(int slot)
        {
            var button = deckPreviewButtons[slot];
            button.Clear();
            button.Add(new Label("Deck " + (slot + 1)));
            var selected = FindSkin(deckPicks[slot]);
            if (selected.HasValue)
            {
                previews.AddDeckFan(button, selected.Value, OnPreviewLoaded);
                button.Add(new Label(selected.Value.DisplayName));
            }
            else
            {
                button.Add(BuildRandomDeckPreview());
                button.Add(new Label("Random deck"));
            }
        }

        private void RenderFeltPreview()
        {
            feltPreviewButton.Clear();
            feltPreviewButton.Add(new Label("Table felt"));
            var felt = FindFelt(feltPick);
            feltPreviewButton.Add(CosmeticPreviews.CreateFeltSwatch(felt));
            feltPreviewButton.Add(new Label(felt == null ? "Random felt" : felt.DisplayName));
        }

        public void OpenDeckPopup(int slot)
        {
            ValidateSlot(slot);
            popupDeckSlot = slot;
            popupShowsFelt = false;
            popupTitle.text = "Choose deck " + (slot + 1);
            deckGrid.style.display = DisplayStyle.Flex;
            feltGrid.style.display = DisplayStyle.None;
            OpenPopup(deckPreviewButtons[slot]);
            PopulateDeckChoices();
        }

        public void OpenFeltPopup()
        {
            popupShowsFelt = true;
            popupTitle.text = "Choose table felt";
            deckGrid.style.display = DisplayStyle.None;
            feltGrid.style.display = DisplayStyle.Flex;
            OpenPopup(feltPreviewButton);
            PopulateFeltChoices();
        }

        private void OpenPopup(Button anchor)
        {
            popupAnchor = anchor;
            lockedNotice.style.display = DisplayStyle.None;
            popup.style.display = DisplayStyle.Flex;
            popupOpen = true;
            PositionPopup();
            if (popup.panel != null)
            {
                popup.schedule.Execute(PositionPopup).StartingIn(0);
            }
        }

        public void ClosePopup()
        {
            popup.style.display = DisplayStyle.None;
            popupOpen = false;
            popupAnchor = null;
            lockedNotice.style.display = DisplayStyle.None;
        }

        private void OnPopupHostPointerDown(PointerDownEvent evt)
        {
            if (!IsPopupOpen)
            {
                return;
            }

            var target = evt.target as VisualElement;
            if (target != null && (popup.Contains(target) || popupAnchor != null && popupAnchor.Contains(target)))
            {
                return;
            }

            ClosePopup();
        }

        private void PositionPopup()
        {
            if (popupAnchor == null || popup.panel == null || popupHost.panel == null)
            {
                return;
            }

            var hostWidth = popupHost.resolvedStyle.width;
            var hostHeight = popupHost.resolvedStyle.height;
            var anchorMin = popupHost.WorldToLocal(popupAnchor.worldBound.min);
            var anchorMax = popupHost.WorldToLocal(popupAnchor.worldBound.max);
            var width = popup.resolvedStyle.width;
            var height = popup.resolvedStyle.height;
            if (float.IsNaN(width) || width <= 0f)
            {
                width = 470f;
            }

            if (float.IsNaN(height) || height <= 0f)
            {
                height = 360f;
            }

            const float edgeInset = 8f;
            const float gap = 12f;
            const float tailSize = 18f;
            var anchorCenterX = (anchorMin.x + anchorMax.x) * 0.5f;
            var anchorCenterY = (anchorMin.y + anchorMax.y) * 0.5f;
            var maxLeft = Mathf.Max(edgeInset, hostWidth - width - edgeInset);
            var maxTop = Mathf.Max(edgeInset, hostHeight - height - edgeInset);
            var canFitHorizontally = !IsPortraitLayout(hostWidth, hostHeight) &&
                width <= hostWidth - edgeInset * 2f;
            float left;
            float top;
            PopupTailEdge tailEdge;

            var leftOfAnchor = anchorMin.x - width - gap;
            var rightOfAnchor = anchorMax.x + gap;
            if (canFitHorizontally && rightOfAnchor + width <= hostWidth - edgeInset)
            {
                left = rightOfAnchor;
                top = Mathf.Clamp(anchorCenterY - height * 0.5f, edgeInset, maxTop);
                tailEdge = PopupTailEdge.Left;
            }
            else if (canFitHorizontally && leftOfAnchor >= edgeInset)
            {
                left = leftOfAnchor;
                top = Mathf.Clamp(anchorCenterY - height * 0.5f, edgeInset, maxTop);
                tailEdge = PopupTailEdge.Right;
            }
            else
            {
                left = Mathf.Clamp(anchorCenterX - width * 0.5f, edgeInset, maxLeft);
                var aboveAnchor = anchorMin.y - height - gap;
                var belowAnchor = anchorMax.y + gap;
                if (belowAnchor <= maxTop)
                {
                    top = belowAnchor;
                    tailEdge = PopupTailEdge.Top;
                }
                else if (aboveAnchor >= edgeInset)
                {
                    top = aboveAnchor;
                    tailEdge = PopupTailEdge.Bottom;
                }
                else if (hostHeight - edgeInset - anchorMax.y >= anchorMin.y - edgeInset)
                {
                    top = Mathf.Clamp(belowAnchor, edgeInset, maxTop);
                    tailEdge = PopupTailEdge.Top;
                }
                else
                {
                    top = Mathf.Clamp(aboveAnchor, edgeInset, maxTop);
                    tailEdge = PopupTailEdge.Bottom;
                }
            }

            popup.style.left = left;
            popup.style.top = top;
            if (popupTail != null)
            {
                PositionPopupTail(left, top, width, height, anchorCenterX, anchorCenterY, tailSize, tailEdge);
            }
        }

        private bool IsPortraitLayout(float hostWidth, float hostHeight)
        {
            for (var element = popupHost; element != null; element = element.parent)
            {
                if (element.ClassListContains("portrait-layout"))
                {
                    return true;
                }

                if (element.ClassListContains("landscape-layout"))
                {
                    return false;
                }
            }

            return hostHeight > hostWidth;
        }

        private void PositionPopupTail(float left, float top, float width, float height, float anchorCenterX,
            float anchorCenterY, float tailSize, PopupTailEdge edge)
        {
            popupTail.style.left = StyleKeyword.Auto;
            popupTail.style.right = StyleKeyword.Auto;
            popupTail.style.top = StyleKeyword.Auto;
            popupTail.style.bottom = StyleKeyword.Auto;

            if (edge == PopupTailEdge.Left || edge == PopupTailEdge.Right)
            {
                var tailTop = Mathf.Clamp(anchorCenterY - top - tailSize * 0.5f, tailSize,
                    Mathf.Max(tailSize, height - tailSize * 2f));
                popupTail.style.top = tailTop;
                if (edge == PopupTailEdge.Left)
                {
                    popupTail.style.left = -tailSize * 0.5f;
                }
                else
                {
                    popupTail.style.right = -tailSize * 0.5f;
                }

                return;
            }

            var tailLeft = Mathf.Clamp(anchorCenterX - left - tailSize * 0.5f, tailSize,
                Mathf.Max(tailSize, width - tailSize * 2f));
            popupTail.style.left = tailLeft;
            if (edge == PopupTailEdge.Top)
            {
                popupTail.style.top = -tailSize * 0.5f;
            }
            else
            {
                popupTail.style.bottom = -tailSize * 0.5f;
            }
        }

        private void PopulateDeckChoices()
        {
            var choices = deckGrid.contentContainer;
            choices.Clear();
            var evaluator = progression.CreateUnlockEvaluator();
            AddDeckChoice(choices, popupDeckSlot, null, evaluator);
            var entries = catalog?.Decks?.GetEntries();
            if (entries == null)
            {
                return;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (evaluator.IsOwned(entry.Id, entry.Unlock))
                {
                    AddDeckChoice(choices, popupDeckSlot, entry, evaluator);
                }
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (!evaluator.IsOwned(entry.Id, entry.Unlock))
                {
                    AddDeckChoice(choices, popupDeckSlot, entry, evaluator);
                }
            }
        }

        private void AddDeckChoice(VisualElement container, int slot, DeckSkinInfo? entry,
            UnlockEvaluator evaluator)
        {
            var random = !entry.HasValue;
            var id = random ? null : entry.Value.Id;
            var owned = random || evaluator.IsOwned(id, entry.Value.Unlock);
            var button = new Button();
            button.AddToClassList("cosmetic-choice-tile");
            button.userData = id;
            if (string.Equals(deckPicks[slot], id, StringComparison.Ordinal))
            {
                button.AddToClassList("selected-choice");
            }

            var preview = new VisualElement();
            preview.AddToClassList("picker-choice-preview");
            button.Add(preview);
            if (random)
            {
                preview.Add(BuildRandomDeckPreview());
            }
            else
            {
                previews.AddDeckFan(preview, entry.Value, OnPreviewLoaded);
            }

            AddTileText(button, random ? "Random deck" : entry.Value.DisplayName, owned
                ? null
                : entry.Value.Unlock.Describe(entry.Value.Unlock.Evaluate(progression.Profile)));
            if (!owned)
            {
                button.AddToClassList("locked-choice");
            }

            if (owned && !random && deckPicks[1 - slot] != null &&
                string.Equals(DeckFaceId.GetSkinId(deckPicks[1 - slot]), DeckFaceId.GetSkinId(id),
                    StringComparison.Ordinal))
            {
                button.SetEnabled(false);
            }
            else
            {
                button.clicked += () => SelectDeck(slot, id);
            }

            container.Add(button);
        }

        private static void ConfigureGrid(ScrollView grid)
        {
            grid.mode = ScrollViewMode.Vertical;
            grid.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            grid.contentContainer.AddToClassList("cosmetic-selector-grid-content");
        }

        private static void AddTileText(VisualElement tile, string name, string lockedRequirement)
        {
            var nameLabel = new Label(name);
            nameLabel.AddToClassList("cosmetic-choice-name");
            tile.Add(nameLabel);
            if (lockedRequirement == null)
            {
                return;
            }

            var lockedLabel = new Label("Locked");
            lockedLabel.AddToClassList("cosmetic-choice-lock");
            tile.Add(lockedLabel);
            if (!string.IsNullOrWhiteSpace(lockedRequirement))
            {
                var requirementLabel = new Label(lockedRequirement);
                requirementLabel.AddToClassList("cosmetic-choice-requirement");
                tile.Add(requirementLabel);
            }
        }

        private void PopulateFeltChoices()
        {
            var choices = feltGrid.contentContainer;
            choices.Clear();
            var evaluator = progression.CreateUnlockEvaluator();
            AddFeltChoice(choices, null, evaluator);
            var felts = catalog?.Felts?.Felts;
            if (felts == null)
            {
                return;
            }

            for (var index = 0; index < felts.Count; index++)
            {
                var felt = felts[index];
                if (felt != null && evaluator.IsOwned(felt.Id, felt.Unlock))
                {
                    AddFeltChoice(choices, felt, evaluator);
                }
            }

            for (var index = 0; index < felts.Count; index++)
            {
                var felt = felts[index];
                if (felt != null && !evaluator.IsOwned(felt.Id, felt.Unlock))
                {
                    AddFeltChoice(choices, felt, evaluator);
                }
            }
        }

        private void AddFeltChoice(VisualElement container, TableFelt felt, UnlockEvaluator evaluator)
        {
            var random = felt == null;
            var id = random ? null : felt.Id;
            var owned = random || evaluator.IsOwned(id, felt.Unlock);
            var button = new Button();
            button.AddToClassList("cosmetic-choice-tile");
            button.userData = id;
            if (string.Equals(feltPick, id, StringComparison.Ordinal))
            {
                button.AddToClassList("selected-choice");
            }

            button.Add(CosmeticPreviews.CreateFeltSwatch(felt));
            AddTileText(button, random ? "Random felt" : felt.DisplayName, owned
                ? null
                : felt.Unlock.Describe(felt.Unlock.Evaluate(progression.Profile)));
            if (!owned)
            {
                button.AddToClassList("locked-choice");
            }

            button.clicked += () => SelectFelt(id);
            container.Add(button);
        }

        public bool SelectDeck(int slot, string id)
        {
            ValidateSlot(slot);
            if (id != null)
            {
                var skin = FindSkin(id);
                if (!skin.HasValue || !progression.CreateUnlockEvaluator().IsOwned(id, skin.Value.Unlock))
                {
                    ShowLockedNotice(id, skin.HasValue
                        ? skin.Value.Unlock.Describe(skin.Value.Unlock.Evaluate(progression.Profile))
                        : null);
                    return false;
                }

                if (deckPicks[1 - slot] != null &&
                    string.Equals(DeckFaceId.GetSkinId(deckPicks[1 - slot]), DeckFaceId.GetSkinId(id),
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            deckPicks[slot] = id;
            RememberPicks();
            ClosePopup();
            Refresh();
            return true;
        }

        public bool SelectFelt(string id)
        {
            if (id != null)
            {
                var felt = FindFelt(id);
                if (felt == null || !progression.CreateUnlockEvaluator().IsOwned(id, felt.Unlock))
                {
                    ShowLockedNotice(id, felt == null
                        ? null
                        : felt.Unlock.Describe(felt.Unlock.Evaluate(progression.Profile)));
                    return false;
                }
            }

            feltPick = id;
            RememberPicks();
            ClosePopup();
            Refresh();
            FeltPickChanged?.Invoke(id);
            return true;
        }

        private void ShowLockedNotice(string id, string requirement)
        {
            lockedNotice.text = string.IsNullOrWhiteSpace(requirement)
                ? "Not unlocked yet."
                : "Not unlocked yet — " + requirement;
            lockedNotice.style.display = DisplayStyle.Flex;
            ShakeLockedChoice(deckGrid, id);
            ShakeLockedChoice(feltGrid, id);
        }

        private void ShakeLockedChoice(VisualElement choices, string id)
        {
            var target = FindLockedChoice(choices, id);
            if (target == null)
            {
                return;
            }

            var offsets = new[] { -8f, 8f, -6f, 6f, -3f, 3f, 0f };
            var startedAt = Time.unscaledTime;
            IVisualElementScheduledItem scheduled = null;
            scheduled = target.schedule.Execute(() =>
            {
                var frame = Mathf.FloorToInt((Time.unscaledTime - startedAt) / 0.035f);
                if (frame >= offsets.Length || target.panel == null)
                {
                    target.style.translate = new Translate(0f, 0f);
                    scheduled.Pause();
                    return;
                }

                target.style.translate = new Translate(new Length(offsets[frame], LengthUnit.Pixel),
                    new Length(0f, LengthUnit.Pixel));
            }).Every(0);
        }

        private static VisualElement FindLockedChoice(VisualElement parent, string id)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent.ClassListContains("locked-choice") &&
                parent.userData is string choiceId && string.Equals(choiceId, id, StringComparison.Ordinal))
            {
                return parent;
            }

            for (var index = 0; index < parent.childCount; index++)
            {
                var result = FindLockedChoice(parent[index], id);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void RememberPicks()
        {
            progression.SetLastPicks(deckPicks, feltPick);
        }

        private bool HasDistinctPicks()
        {
            return HasDistinctPicks(deckPicks);
        }

        private static bool HasDistinctPicks(IReadOnlyList<string> picks)
        {
            return picks != null && picks.Count == 2 &&
                   (picks[0] == null || picks[1] == null ||
                    !string.Equals(DeckFaceId.GetSkinId(picks[0]), DeckFaceId.GetSkinId(picks[1]),
                        StringComparison.Ordinal));
        }

        private void OnPreviewLoaded()
        {
            if (root.panel == null)
            {
                return;
            }

            root.schedule.Execute(() =>
            {
                Refresh();
                if (IsPopupOpen)
                {
                    if (popupShowsFelt)
                    {
                        PopulateFeltChoices();
                    }
                    else
                    {
                        PopulateDeckChoices();
                    }
                }
            });
        }

        private VisualElement BuildRandomDeckPreview()
        {
            var preview = new VisualElement();
            preview.AddToClassList("random-deck-logo");
            return preview;
        }

        public MatchRequest CreateMatchRequest()
        {
            if (setupRequest == null)
            {
                throw new InvalidOperationException("Two different deck selections are required to start a match.");
            }

            return CreateMatchRequest(setupRequest, deckPicks, feltPick);
        }

        public MatchRequest CreateMatchRequest(MatchRequest request, IReadOnlyList<string> selectedDeckPicks,
            string selectedFeltPick)
        {
            if (request == null || request.Resume || !HasDistinctPicks(selectedDeckPicks))
            {
                throw new InvalidOperationException("Two different deck selections are required to start a match.");
            }

            var evaluator = progression.CreateUnlockEvaluator();
            var randomFacePool = new List<string>(evaluator.OwnedFaceIds());
            var seed = request.Seed ?? CreateSeed();

            string[] feltPool;
            if (selectedFeltPick != null)
            {
                feltPool = new[] { selectedFeltPick };
            }
            else
            {
                var ownedFelts = evaluator.OwnedFeltIds();
                if (ownedFelts.Length == 0)
                {
                    ownedFelts = new[] { TableFeltAssigner.GreenFelt };
                }

                var feltRng = new Pcg32(seed ^ 0xA0761D6478BD642FUL);
                feltPool = new[] { ownedFelts[feltRng.NextInt(ownedFelts.Length)] };
            }

            return new MatchRequest(request.Seats, request.TurnSeconds, request.Mode,
                deckPicks: new[] { selectedDeckPicks[0], selectedDeckPicks[1] }, randomFacePool: randomFacePool, feltPool: feltPool,
                seed: seed);
        }

        public static string EncodeCosmeticsToken(IReadOnlyList<string> selectedDeckPicks, string selectedFeltPick)
        {
            if (selectedDeckPicks == null || selectedDeckPicks.Count != 2)
            {
                throw new ArgumentException("Two deck selections are required.", nameof(selectedDeckPicks));
            }

            return (selectedDeckPicks[0] ?? string.Empty) + "|" + (selectedDeckPicks[1] ?? string.Empty) + "|" +
                   (selectedFeltPick ?? string.Empty);
        }

        public static bool TryDecodeCosmeticsToken(string token, out string[] selectedDeckPicks,
            out string selectedFeltPick)
        {
            selectedDeckPicks = null;
            selectedFeltPick = null;
            if (token == null)
            {
                return false;
            }

            var parts = token.Split('|');
            if (parts.Length != 3)
            {
                return false;
            }

            selectedDeckPicks = new[]
            {
                string.IsNullOrEmpty(parts[0]) ? null : parts[0],
                string.IsNullOrEmpty(parts[1]) ? null : parts[1]
            };
            selectedFeltPick = string.IsNullOrEmpty(parts[2]) ? null : parts[2];
            return true;
        }

        private DeckSkinInfo? FindSkin(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || catalog?.Decks == null)
            {
                return null;
            }

            var entries = catalog.Decks.GetEntries();
            for (var index = 0; index < entries.Count; index++)
            {
                if (string.Equals(entries[index].Id, id, StringComparison.Ordinal))
                {
                    return entries[index];
                }
            }

            return null;
        }

        private TableFelt FindFelt(string id)
        {
            return catalog?.Felts == null ? null : catalog.Felts.Resolve(id);
        }

        private static void ValidateSlot(int slot)
        {
            if (slot < 0 || slot >= 2)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        private static ulong CreateSeed()
        {
            return BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
        }
    }
}