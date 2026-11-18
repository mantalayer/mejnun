using System.Collections.Generic;
using System.Text;
using Mejnun.Cosmetics;
using Mejnun.Core;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class ResultsScreen
    {
        private readonly VisualElement root;
        private readonly MatchFlow matchFlow;
        private readonly Label title;
        private readonly Label message;
        private readonly Label counts;
        private readonly Label mejnunBanner;
        private readonly Label xpGained;
        private readonly Label levelUp;
        private readonly Label levelLabel;
        private readonly ProgressBar levelProgress;
        private readonly VisualElement newAchievements;
        private readonly VisualElement newGalleryItems;
        private readonly VisualElement lockedItems;
        private readonly VisualElement rematchConfirm;
        private readonly Toggle rematchDontAsk;

        public ResultsScreen(VisualElement documentRoot, MatchFlow flow)
        {
            root = documentRoot.Q<VisualElement>("results-screen");
            matchFlow = flow;
            title = root.Q<Label>("result-title");
            message = root.Q<Label>("result-message");
            counts = root.Q<Label>("result-counts");
            mejnunBanner = root.Q<Label>("mejnun-banner");
            xpGained = root.Q<Label>("result-xp-gained");
            levelUp = root.Q<Label>("result-level-up");
            levelLabel = root.Q<Label>("result-level-label");
            levelProgress = root.Q<ProgressBar>("result-level-progress");
            newAchievements = root.Q<VisualElement>("result-new-achievements");
            newGalleryItems = root.Q<VisualElement>("result-new-gallery-items");
            lockedItems = root.Q<VisualElement>("result-locked-items");
            rematchConfirm = root.Q<VisualElement>("rematch-confirm");
            rematchDontAsk = root.Q<Toggle>("rematch-dont-ask");
            root.Q<Button>("rematch-button").clicked += RequestRematch;
            root.Q<Button>("rematch-cancel-button").clicked += () => SetRematchConfirmation(false);
            root.Q<Button>("rematch-accept-button").clicked += ConfirmRematch;
            root.Q<Button>("results-menu-button").clicked += () => matchFlow.ReturnToMenu();
        }

        public void SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Refresh()
        {
            title.text = matchFlow.ResultTitle;
            message.text = matchFlow.ResultMessage;
            mejnunBanner.style.display = matchFlow.LastResult != null &&
                                         matchFlow.LastResult.Kind == GameEndKind.EmptyHand
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            var result = new StringBuilder("Cards remaining\n");
            if (matchFlow.Game != null)
            {
                for (var seat = 0; seat < matchFlow.Game.State.Hands.Count; seat++)
                {
                    result.Append(matchFlow.GetPlayerName(seat)).Append(": ")
                        .Append(matchFlow.Game.State.Hands[seat].Count);
                    if (seat + 1 < matchFlow.Game.State.Hands.Count) result.Append('\n');
                }
            }

            counts.text = result.ToString();
            RefreshProgression();
            SetRematchConfirmation(false);
        }

        private void RefreshProgression()
        {
            var report = matchFlow.LastProgression;
            xpGained.text = report == null ? string.Empty : "+" + report.XpGained + " XP";
            levelUp.style.display = report != null && report.LevelAfter > report.LevelBefore
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            levelUp.text = report == null || report.LevelAfter <= report.LevelBefore
                ? string.Empty
                : "Level up! Level " + report.LevelAfter;

            var profile = ProgressionService.Instance.Profile;
            var progress = ProgressionRules.LevelProgress(profile.Xp);
            levelLabel.text = "Level " + profile.Level + " · " + progress.into + " / " + progress.needed + " XP";
            levelProgress.lowValue = 0;
            levelProgress.highValue = progress.needed;
            levelProgress.value = progress.into;
            levelProgress.title = levelLabel.text;

            newAchievements.Clear();
            newGalleryItems.Clear();
            lockedItems.Clear();
            if (report != null)
            {
                for (var index = 0; index < report.NewAchievements.Count; index++)
                {
                    newAchievements.Add(new Label(AchievementIds.DisplayName(report.NewAchievements[index])));
                }

                for (var index = 0; index < report.NewlyUnlockedItems.Count; index++)
                {
                    AddGalleryChip(newGalleryItems, report.NewlyUnlockedItems[index], "New: ");
                }
            }

            AddLockedGalleryItems();
        }

        private void AddLockedGalleryItems()
        {
            if (matchFlow.Game == null)
            {
                return;
            }

            var evaluator = ProgressionService.Instance.CreateUnlockEvaluator();
            var added = new HashSet<string>();
            var faceIds = matchFlow.Game.State.DeckFaces;
            if (faceIds != null)
            {
                for (var index = 0; index < faceIds.Length; index++)
                {
                    AddLockedGalleryItem(evaluator, added, DeckFaceId.GetSkinId(faceIds[index]));
                }
            }

            AddLockedGalleryItem(evaluator, added, matchFlow.Game.State.TableFeltId);
        }

        private void AddLockedGalleryItem(UnlockEvaluator evaluator, HashSet<string> added, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !added.Add(itemId))
            {
                return;
            }

            var requirement = evaluator.GetRequirement(itemId);
            if (requirement == null || evaluator.IsOwned(itemId, requirement))
            {
                return;
            }

            AddGalleryChip(lockedItems, itemId, "Not unlocked yet: ");
        }

        private void AddGalleryChip(VisualElement container, string itemId, string prefix)
        {
            var button = new Button { text = prefix + DisplayName(itemId) };
            button.AddToClassList("result-chip");
            button.clicked += () => matchFlow.ReturnToMenu(MenuDestination.Gallery(itemId));
            container.Add(button);
        }

        private static string DisplayName(string itemId)
        {
            var catalog = ProgressionService.Instance.Catalog;
            if (catalog?.Decks != null)
            {
                var skins = catalog.Decks.GetEntries();
                for (var index = 0; index < skins.Count; index++)
                {
                    if (skins[index].Id == itemId)
                    {
                        return skins[index].DisplayName;
                    }
                }
            }

            var felt = catalog?.Felts?.Resolve(itemId);
            return felt == null ? itemId.Replace('_', ' ') : felt.DisplayName;
        }

        private void SetRematchConfirmation(bool visible)
        {
            rematchDontAsk.SetValueWithoutNotify(false);
            rematchConfirm.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RequestRematch()
        {
            if (GameSettings.ConfirmLeave)
            {
                SetRematchConfirmation(true);
            }
            else
            {
                ConfirmRematch();
            }
        }

        private void ConfirmRematch()
        {
            if (rematchDontAsk.value)
            {
                GameSettings.ConfirmLeave = false;
            }

            SetRematchConfirmation(false);
            matchFlow.Rematch();
        }
    }
}