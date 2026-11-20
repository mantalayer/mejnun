using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Core;
using Mejnun.Game;
using Mejnun.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public sealed class GalleryScreen : IDisposable
    {
        private readonly VisualElement root;
        private readonly CosmeticCatalog catalog;
        private readonly ProgressionService progression;
        private readonly CosmeticPreviews previews;
        private readonly Button decksTab;
        private readonly Button feltsTab;
        private readonly Label countLabel;
        private readonly ScrollView itemList;
        private readonly Label selectedTitle;
        private readonly Label selectedRequirement;
        private readonly Label selectedProgressLabel;
        private readonly Label selectedNewBadge;
        private readonly VisualElement selectedPreview;
        private readonly ProgressBar selectedProgress;
        private bool showingFelts;
        private string selectedItemId;

        public GalleryScreen(VisualElement documentRoot, CosmeticCatalog cosmeticCatalog,
            ProgressionService progressionService, DeckSkinLoader loader)
        {
            root = documentRoot.Q<VisualElement>("gallery-screen");
            catalog = cosmeticCatalog;
            progression = progressionService ?? throw new ArgumentNullException(nameof(progressionService));
            previews = new CosmeticPreviews(loader);
            decksTab = root.Q<Button>("gallery-decks-tab");
            feltsTab = root.Q<Button>("gallery-felts-tab");
            countLabel = root.Q<Label>("gallery-count-label");
            itemList = root.Q<ScrollView>("gallery-items");
            selectedTitle = root.Q<Label>("gallery-selected-title");
            selectedRequirement = root.Q<Label>("gallery-requirement");
            selectedProgressLabel = root.Q<Label>("gallery-progress-label");
            selectedNewBadge = root.Q<Label>("gallery-selected-new");
            selectedPreview = root.Q<VisualElement>("gallery-preview");
            selectedProgress = root.Q<ProgressBar>("gallery-progress");

            decksTab.clicked += () => SelectTab(false);
            feltsTab.clicked += () => SelectTab(true);
            progression.Changed += OnProgressionChanged;
            Loc.LanguageChanged += OnLanguageChanged;
        }

        public string SelectedItemId => selectedItemId;

        public void Show(string itemId = null)
        {
            root.style.display = DisplayStyle.Flex;
            showingFelts = false;
            selectedItemId = null;
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                if (FindFelt(itemId) != null)
                {
                    showingFelts = true;
                }

                selectedItemId = itemId;
            }

            Refresh();
            if (selectedItemId != null)
            {
                SelectItem(selectedItemId);
            }
        }

        public void SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void MarkSeen()
        {
            var evaluator = progression.CreateUnlockEvaluator();
            progression.MarkSeen(evaluator.NewItems());
        }

        public void Dispose()
        {
            progression.Changed -= OnProgressionChanged;
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        private void SelectTab(bool felts)
        {
            showingFelts = felts;
            selectedItemId = null;
            Refresh();
        }

        private void Refresh()
        {
            decksTab.EnableInClassList("selected-tab", !showingFelts);
            feltsTab.EnableInClassList("selected-tab", showingFelts);
            var evaluator = progression.CreateUnlockEvaluator();
            itemList.contentContainer.Clear();
            if (showingFelts)
            {
                var felts = catalog?.Felts?.Felts;
                var owned = evaluator.OwnedFeltIds().Length;
                countLabel.text = Loc.Format("gallery.count.felts", owned, felts == null ? 0 : felts.Count);
                if (felts != null)
                {
                    for (var index = 0; index < felts.Count; index++)
                    {
                        var felt = felts[index];
                        if (felt != null)
                        {
                            AddFeltTile(felt, evaluator);
                        }
                    }
                }
            }
            else
            {
                var skins = catalog?.Decks?.GetEntries();
                var owned = evaluator.OwnedFaceIds().Length;
                countLabel.text = Loc.Format("gallery.count.decks", owned, skins == null ? 0 : skins.Count);
                if (skins != null)
                {
                    for (var index = 0; index < skins.Count; index++)
                    {
                        AddSkinTile(skins[index], evaluator);
                    }
                }
            }

            RenderSelected(evaluator);
        }

        private void AddSkinTile(DeckSkinInfo skin, UnlockEvaluator evaluator)
        {
            var owned = evaluator.IsOwned(skin.Id, skin.Unlock);
            var progress = skin.Unlock.Evaluate(progression.Profile);
            var tile = CreateTile(skin.Id, skin.DisplayName, owned, progress, evaluator, skin.Artist);
            var preview = new VisualElement();
            preview.AddToClassList("gallery-tile-preview");
            previews.AddDeckFan(preview, skin, OnPreviewLoaded);
            tile.Insert(0, preview);
            tile.clicked += () => SelectItem(skin.Id);
            itemList.Add(tile);
        }

        private void AddFeltTile(TableFelt felt, UnlockEvaluator evaluator)
        {
            var owned = evaluator.IsOwned(felt.Id, felt.Unlock);
            var progress = felt.Unlock.Evaluate(progression.Profile);
            var tile = CreateTile(felt.Id, felt.DisplayName, owned, progress, evaluator);
            var preview = CosmeticPreviews.CreateFeltSwatch(felt);
            preview.AddToClassList("gallery-tile-preview");
            tile.Insert(0, preview);
            tile.clicked += () => SelectItem(felt.Id);
            itemList.Add(tile);
        }

        private Button CreateTile(string id, string displayName, bool owned, UnlockProgress progress,
            UnlockEvaluator evaluator, string artist = null)
        {
            var tile = new Button();
            tile.AddToClassList("gallery-item-tile");
            if (!owned)
            {
                tile.AddToClassList("locked-choice");
            }

            if (string.Equals(selectedItemId, id, StringComparison.Ordinal))
            {
                tile.AddToClassList("selected-choice");
            }

            AddNamedLabel(tile, "gallery-item-name", displayName);
            if (!string.IsNullOrWhiteSpace(artist))
            {
                AddNamedLabel(tile, "gallery-item-artist", DeckSkin.FormatArtistCredit(artist));
            }

            if (!owned)
            {
                AddNamedLabel(tile, "gallery-item-lock",
                    Loc.Format("gallery.tile.locked_requirement", evaluator.GetRequirement(id).Describe(progress)));
                if (progress.IsCountable && progress.Required > 0)
                {
                    var progressBar = new ProgressBar
                    {
                        name = "gallery-item-progress",
                        lowValue = 0,
                        highValue = progress.Required,
                        value = Mathf.Clamp(progress.Current, 0, progress.Required)
                    };
                    tile.Add(progressBar);
                }
            }

            if (IsNew(id, evaluator))
            {
                AddNamedLabel(tile, "gallery-item-new", Loc.Get("gallery.tile.new"));
            }

            return tile;
        }

        private static void AddNamedLabel(VisualElement parent, string elementName, string text)
        {
            var label = new Label(text);
            label.name = elementName;
            parent.Add(label);
        }

        private void SelectItem(string itemId)
        {
            selectedItemId = itemId;
            Refresh();
        }

        private void RenderSelected(UnlockEvaluator evaluator)
        {
            selectedPreview.Clear();
            selectedProgress.style.display = DisplayStyle.None;
            selectedProgressLabel.style.display = DisplayStyle.None;
            selectedNewBadge.style.display = DisplayStyle.None;
            if (string.IsNullOrWhiteSpace(selectedItemId))
            {
                selectedTitle.text = Loc.Get("gallery.details.empty_title");
                selectedRequirement.text = Loc.Get("gallery.details.empty_body");
                return;
            }

            var skin = FindSkin(selectedItemId);
            if (skin.HasValue)
            {
                selectedTitle.text = skin.Value.DisplayName;
                var owned = evaluator.IsOwned(selectedItemId, skin.Value.Unlock);
                if (owned && skin.Value.Skin != null)
                {
                    selectedPreview.Add(CosmeticPreviews.BuildDeckGrid(skin.Value.Skin));
                }
                else
                {
                    previews.AddDeckFan(selectedPreview, skin.Value, OnPreviewLoaded);
                }

                RenderRequirement(selectedItemId, skin.Value.Unlock, evaluator);
                return;
            }

            var felt = FindFelt(selectedItemId);
            if (felt != null)
            {
                selectedTitle.text = felt.DisplayName;
                selectedPreview.Add(CosmeticPreviews.CreateFeltSwatch(felt));
                RenderRequirement(selectedItemId, felt.Unlock, evaluator);
                return;
            }

            selectedTitle.text = selectedItemId;
            selectedRequirement.text = Loc.Get("gallery.details.missing_item");
        }

        private void RenderRequirement(string id, UnlockRequirement requirement, UnlockEvaluator evaluator)
        {
            var owned = evaluator.IsOwned(id, requirement);
            var progress = requirement.Evaluate(progression.Profile);
            selectedRequirement.text = owned ? Loc.Get("gallery.details.owned") : requirement.Describe(progress);
            if (!owned && progress.IsCountable && progress.Required > 0)
            {
                selectedProgress.lowValue = 0;
                selectedProgress.highValue = progress.Required;
                selectedProgress.value = Mathf.Clamp(progress.Current, 0, progress.Required);
                selectedProgress.title = Loc.Format("gallery.details.progress", progress.Current, progress.Required);
                selectedProgress.style.display = DisplayStyle.Flex;
                selectedProgressLabel.text = Loc.Format("gallery.details.progress", progress.Current,
                    progress.Required);
                selectedProgressLabel.style.display = DisplayStyle.Flex;
            }

            if (IsNew(id, evaluator))
            {
                selectedNewBadge.text = Loc.Get("gallery.details.new_badge");
                selectedNewBadge.style.display = DisplayStyle.Flex;
            }
        }

        private void OnProgressionChanged()
        {
            Refresh();
        }

        private void OnLanguageChanged()
        {
            Refresh();
        }

        private void OnPreviewLoaded()
        {
            if (root.panel != null)
            {
                Refresh();
            }
        }

        private bool IsNew(string id, UnlockEvaluator evaluator)
        {
            var newItems = evaluator.NewItems();
            for (var index = 0; index < newItems.Count; index++)
            {
                if (string.Equals(newItems[index], id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private DeckSkinInfo? FindSkin(string id)
        {
            var entries = catalog?.Decks?.GetEntries();
            if (entries == null)
            {
                return null;
            }

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

    }
}