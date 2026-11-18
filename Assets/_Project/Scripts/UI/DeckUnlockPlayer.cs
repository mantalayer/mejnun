using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Game;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class DeckUnlockPlayer
    {
        private readonly DeckSkinLoader skinLoader;
        private readonly DeckUnlockPresentation unlockPresentation;
        private ProgressionReport lastPlayedReport;
        private int loadGeneration;

        public DeckUnlockPlayer(VisualElement documentRoot, DeckSkinLoader loader)
        {
            skinLoader = loader;
            unlockPresentation = new DeckUnlockPresentation(documentRoot);
        }

        public void Play(ProgressionReport report)
        {
            if (report == null || ReferenceEquals(lastPlayedReport, report))
            {
                return;
            }

            lastPlayedReport = report;
            var generation = ++loadGeneration;
            var entries = ProgressionService.Instance.Catalog?.Decks?.GetEntries();
            if (entries == null)
            {
                return;
            }

            var candidates = new List<DeckSkinInfo>();
            var seenIds = new HashSet<string>();
            for (var unlockIndex = 0; unlockIndex < report.NewlyUnlockedItems.Count; unlockIndex++)
            {
                var itemId = report.NewlyUnlockedItems[unlockIndex];
                if (string.IsNullOrWhiteSpace(itemId) || !seenIds.Add(itemId))
                {
                    continue;
                }

                for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    if (string.Equals(entries[entryIndex].Id, itemId, StringComparison.Ordinal))
                    {
                        candidates.Add(entries[entryIndex]);
                        break;
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return;
            }

            var resolved = new DeckUnlockPresentationItem[candidates.Count];
            var pendingLoads = 0;
            for (var index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].Skin == null && candidates[index].IsAddressable && skinLoader != null)
                {
                    pendingLoads++;
                }
            }

            if (pendingLoads == 0)
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    resolved[index] = CreatePresentationItem(candidates[index], candidates[index].Skin);
                }

                PlayResolvedDecks(resolved);
                return;
            }

            for (var index = 0; index < candidates.Count; index++)
            {
                var entry = candidates[index];
                if (entry.Skin != null)
                {
                    resolved[index] = CreatePresentationItem(entry, entry.Skin);
                    continue;
                }

                if (!entry.IsAddressable || skinLoader == null)
                {
                    continue;
                }

                var candidateIndex = index;
                skinLoader.Load(entry.Id, loaded =>
                {
                    if (generation != loadGeneration)
                    {
                        return;
                    }

                    resolved[candidateIndex] = CreatePresentationItem(entry, loaded);
                    pendingLoads--;
                    if (pendingLoads == 0)
                    {
                        PlayResolvedDecks(resolved);
                    }
                });
            }
        }

        public void Cancel()
        {
            loadGeneration++;
            unlockPresentation.Cancel();
        }

        private static DeckUnlockPresentationItem CreatePresentationItem(DeckSkinInfo entry, DeckSkin skin)
        {
            return skin == null ? null : new DeckUnlockPresentationItem(skin, entry.DisplayName,
                string.IsNullOrWhiteSpace(entry.Artist) ? skin.Artist : entry.Artist);
        }

        private void PlayResolvedDecks(IReadOnlyList<DeckUnlockPresentationItem> resolved)
        {
            var items = new List<DeckUnlockPresentationItem>();
            for (var index = 0; index < resolved.Count; index++)
            {
                if (resolved[index] != null)
                {
                    items.Add(resolved[index]);
                }
            }

            if (items.Count > 0)
            {
                unlockPresentation.Play(items, null);
            }
        }
    }
}